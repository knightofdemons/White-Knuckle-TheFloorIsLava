using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TheFloorIsLava.Lava;
using UnityEngine;

namespace TheFloorIsLava;

[BepInPlugin(Id, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = "com.thefloorislava.whiteknuckle";
    public const string Name = "The Floor Is Lava";
    public const string Version = "1.12.1";

    public static Plugin? Instance { get; private set; }
    private static ManualLogSource Log => Instance!.Logger;

    private Config? _cfg;
    private HealthPool? _hp;
    private HealthBar? _ui;
    private LavaField? _lava;
    private Grip? _grip;
    private EasterEgg? _egg;
    private FanningSystem? _fan;
    private Harmony? _harmony;

    private float _offLavaTimer;
    private float _statusTimer;
    private float _damageTickTimer;
    private bool _onLava;
    private bool _deathTriggered;

    private void Awake()
    {
        Instance = this;
        _cfg = new Config(Config);
        _hp = new HealthPool();
        _ui = new HealthBar();
        _lava = new LavaField(_cfg);
        _grip = new Grip();
        _egg = new EasterEgg(_cfg);
        _fan = new FanningSystem(_cfg);
        _harmony = new Harmony(Id);
        HarmonySetup.Apply(_harmony);
        LogInfo($"{Name} v{Version}");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        _ui?.Destroy();
        Instance = null;
    }

    public void TryStartRun(ENT_Player? player = null)
    {
        player ??= GameRefs.Player();
        if (player == null || !GameRefs.InRun(player))
            return;
        StartRun(player);
    }

    public void NotifyRoomLoaded()
    {
        if (!ModState.RunActive)
            return;
        var p = GameRefs.Player();
        if (p == null)
            return;
        LogInfo("Room loaded — forcing lava scan.");
        _lava!.ForceScan(p);
    }

    private void Update()
    {
        if (ModState.RunActive && ShouldStopRun())
            StopRun();

        if (!ModState.RunActive)
        {
            _ui?.Hide();
            ModState.OnLavaZone = false;
            ModState.LavaHeatMultiplier = 1f;
            var idlePlayer = GameRefs.Player();
            if (idlePlayer != null && GameRefs.InRun(idlePlayer))
                TryStartRun(idlePlayer);
            return;
        }

        var player = GameRefs.Player();
        if (player == null)
        {
            _ui?.Hide();
            return;
        }

        if (_cfg!.EasterEggEnabled.Value && _egg != null && !_egg.Done)
            _egg.Tick(player, Time.deltaTime, _ui!);

        _lava!.TickWorldScan(player, Time.deltaTime);

        if (_egg != null && _egg.Triggered)
        {
            ModState.OnLavaZone = false;
            if (_onLava)
            {
                _grip!.Exit(player);
                _onLava = false;
            }
            return;
        }

        if (_hp!.Dead)
        {
            _ui!.Show(0f, _hp.Max);
            if (!_deathTriggered)
            {
                _deathTriggered = true;
                var killed = GameRefs.KillPlayer();
                var ended = GameRefs.ForceEndRun();
                LogInfo($"Mod HP depleted — killing player (damage={killed}) and ending run (flag={ended}).");
            }
            return;
        }

        _ui!.Show(_hp.Current, _hp.Max);

        var onLava = _lava.PlayerOnLava(player, out var nearestHoriz, out var candidates);
        ModState.OnLavaZone = onLava;

        var lavaHeat = 1f;
        if (onLava && _cfg!.FanningEnabled.Value && (_egg == null || !_egg.Triggered))
            lavaHeat = _fan!.Tick(player, Time.deltaTime, true);
        else if (_fan != null)
            _fan.Tick(player, Time.deltaTime, false);

        ModState.LavaHeatMultiplier = lavaHeat;

        GustEffect.Tick(Time.deltaTime);

        if (onLava && !_onLava)
            _grip!.Enter(player);
        else if (!onLava && _onLava)
            _grip!.Exit(player);

        _onLava = onLava;

        if (onLava)
        {
            var dmg = _cfg!.LavaDps.Value * lavaHeat * Time.deltaTime;
            if (dmg > 0.0001f)
            {
                _hp.Damage(dmg);
                _offLavaTimer = 0f;
            }
            else
            {
                _offLavaTimer += Time.deltaTime;
                if (_offLavaTimer >= _cfg.HealthRegenDelay.Value)
                    _hp.Heal(_cfg.HealthRegen.Value * Time.deltaTime);
            }

            _damageTickTimer += Time.deltaTime;
            if (_damageTickTimer >= 1f)
            {
                _damageTickTimer = 0f;
                LogInfo(
                    $"LAVA DAMAGE applied: hp={_hp.Current:F1}/{_hp.Max:F0} " +
                    $"(dps={_cfg.LavaDps.Value * lavaHeat:F1}, heat={lavaHeat:F2}, " +
                    $"attached={_fan?.AttachedHeat:F2}, spots={_fan?.SpotCount ?? 0})");
            }
        }
        else
        {
            _damageTickTimer = 0f;
            _offLavaTimer += Time.deltaTime;
            if (_offLavaTimer >= _cfg!.HealthRegenDelay.Value)
                _hp.Heal(_cfg.HealthRegen.Value * Time.deltaTime);
        }

        if (_cfg!.DebugLog.Value)
        {
            _statusTimer += Time.deltaTime;
            if (_statusTimer >= 1f)
            {
                _statusTimer = 0f;
                var statusFeet = GameRefs.FeetPosition(player);
                LogInfo(
                    $"status: onLava={onLava} hp={_hp.Current:F0} heat={lavaHeat:F2} " +
                    $"fanWarmup={_fan?.WarmupCycles ?? 0} coolSpots={_fan?.SpotCount ?? 0} " +
                    $"zones={_lava.Count} candidates={candidates} " +
                    $"nearestH={(nearestHoriz < 0 ? "n/a" : nearestHoriz.ToString("F2") + "m")} " +
                    $"feet=({statusFeet.x:F1},{statusFeet.y:F1},{statusFeet.z:F1})");
            }
        }
    }

    private void FixedUpdate()
    {
        if (!ModState.RunActive || !ModState.OnLavaZone)
            return;
        var p = GameRefs.Player();
        if (p != null)
            _grip!.Regen(p, Time.fixedDeltaTime, _cfg!.GripRegen.Value);
    }

    private void StartRun(ENT_Player player)
    {
        if (ModState.RunActive)
            return;

        ModState.RunActive = true;
        ModState.OnLavaZone = false;
        ModState.LavaHeatMultiplier = 1f;
        _onLava = false;
        _offLavaTimer = 0f;
        _statusTimer = 0f;
        _damageTickTimer = 0f;
        _deathTriggered = false;
        _hp!.Reset(_cfg!.MaxHealth.Value);
        _lava!.Clear();
        _lava.ExcludeStartingFloor(player);
        _lava.ForceScan(player);
        _egg?.Reset();
        _fan?.Reset();
        _ui!.Show(_hp.Current, _hp.Max);
        LogInfo($"Run started ({player.gameObject.scene.name}) hp={_hp.Current:F0}/{_hp.Max:F0}");
    }

    public void StopRun()
    {
        if (!ModState.RunActive)
            return;

        var p = GameRefs.Player();
        if (p != null && _onLava)
            _grip!.Exit(p);

        ModState.RunActive = false;
        ModState.OnLavaZone = false;
        ModState.LavaHeatMultiplier = 1f;
        _onLava = false;
        _deathTriggered = false;
        _offLavaTimer = 0f;
        _statusTimer = 0f;
        _damageTickTimer = 0f;
        _hp?.Reset(_cfg!.MaxHealth.Value);
        _lava?.Clear();
        _egg?.Reset();
        _fan?.Reset();
        _ui?.Hide();
        LogInfo("Run stopped — UI hidden, lava cleared, HP reset.");
    }

    private static bool ShouldStopRun()
    {
        if (GameRefs.RunEndedCheck())
            return true;

        var player = GameRefs.Player();
        return player != null && GameRefs.IsMenu(player.gameObject.scene.name);
    }

    public static void LogInfo(string msg) => Log?.LogInfo(msg);
}
