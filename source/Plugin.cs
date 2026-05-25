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

    private bool _run;
    private float _offLavaTimer;
    private float _statusTimer;
    private float _damageTickTimer;
    private bool _onLava;
    private bool _deathTriggered;
    private int _lastPlayerId;

    private void Awake()
    {
        Instance = this;
        _cfg = new Config(Config);
        _hp = new HealthPool();
        _ui = new HealthBar();
        _lava = new LavaField(_cfg);
        _grip = new Grip();
        _egg = new EasterEgg();
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

    public void NotifyPlayerSpawned()
    {
        // A new ENT_Player Start means a new run is beginning. If we still
        // think a previous run is active, force-stop it so HP / UI / lava
        // state is wiped before the fresh run starts. This is what fixes the
        // "HP didn't reset after restart" bug.
        if (_run)
        {
            LogInfo("New player spawned while old run was still active — force-stopping previous run.");
            StopRun();
        }
        TryStartRun();
    }

    public void NotifyRoomLoaded()
    {
        if (!_run)
            return;
        var p = GameRefs.Player();
        if (p == null)
            return;
        LogInfo("Room loaded — forcing lava scan.");
        _lava!.ForceScan(p);
    }

    private void Update()
    {
        var player = GameRefs.Player();

        // Detect a player swap during a run (e.g. the game spawned a new
        // ENT_Player after death without us getting a clean run-end signal).
        if (_run && player != null)
        {
            var id = player.GetInstanceID();
            if (_lastPlayerId != 0 && id != _lastPlayerId)
            {
                LogInfo("Player instance changed mid-run — treating as a restart.");
                StopRun();
            }
            else
            {
                _lastPlayerId = id;
            }
        }

        // Stop the run when the game flags it ended, when we're back in the
        // menu, or when the player has vanished entirely.
        if (_run && ShouldStopRun(player))
        {
            StopRun();
        }

        if (!_run)
        {
            // Defensive: in menus / between runs the UI must NOT be visible
            // and we must NOT be holding any lava / HP state from a prior run.
            _ui?.Hide();
            ModState.RunActive = false;
            ModState.OnLavaZone = false;
            ModState.LavaHeatMultiplier = 1f;
            if (player != null && GameRefs.InRun(player))
                StartRun(player);
            return;
        }

        ModState.RunActive = true;

        if (player == null)
        {
            // No player but run flag still true — could be a brief scene gap.
            // Hide UI so we don't leak into the next menu frame.
            _ui?.Hide();
            return;
        }

        // Easter-egg gesture detection runs even before damage processing so
        // the player can trigger it at any time during the run. Once the
        // gesture fires the egg keeps ticking through its rip animation until
        // Done — at that point the UI has faded out and a 3D prop is spawned.
        if (_cfg!.EasterEggEnabled.Value && _egg != null && !_egg.Done)
            _egg.Tick(player, Time.deltaTime, _ui!);

        // Continuous world scan — adds new lava as the player climbs into new rooms.
        _lava!.TickWorldScan(player, Time.deltaTime);

        // After the easter egg fires the bar is gone and lava is harmless.
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

        var feet = GameRefs.FeetPosition(player);
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
                // Cooled spot — no lava damage; regen after the usual delay.
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
                    $"fanCycles={_fan?.CompletedCycles ?? 0} coolSpots={_fan?.SpotCount ?? 0} " +
                    $"zones={_lava.Count} candidates={candidates} " +
                    $"nearestH={(nearestHoriz < 0 ? "n/a" : nearestHoriz.ToString("F2") + "m")} " +
                    $"feet=({statusFeet.x:F1},{statusFeet.y:F1},{statusFeet.z:F1})");
            }
        }
    }

    private void FixedUpdate()
    {
        if (!_run || !ModState.OnLavaZone)
            return;
        var p = GameRefs.Player();
        if (p != null)
            _grip!.Regen(p, Time.fixedDeltaTime, _cfg!.GripRegen.Value);
    }

    private void TryStartRun()
    {
        var p = GameRefs.Player();
        if (p != null && GameRefs.InRun(p))
            StartRun(p);
    }

    private void StartRun(ENT_Player player)
    {
        if (_run)
            return;

        _run = true;
        _lastPlayerId = player.GetInstanceID();
        ModState.RunActive = true;
        ModState.OnLavaZone = false;
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

    private bool ShouldStopRun(ENT_Player? player)
    {
        if (!_run) return false;
        if (GameRefs.RunEndedCheck()) return true;
        if (player != null && GameRefs.IsMenu(player.gameObject.scene.name)) return true;
        return false;
    }

    private void StopRun()
    {
        if (!_run) return;

        var p = GameRefs.Player();
        if (p != null && _onLava)
            _grip!.Exit(p);

        _run = false;
        _lastPlayerId = 0;
        _deathTriggered = false;
        _offLavaTimer = 0f;
        _statusTimer = 0f;
        _damageTickTimer = 0f;
        ModState.RunActive = false;
        ModState.OnLavaZone = false;
        ModState.LavaHeatMultiplier = 1f;
        _onLava = false;
        _hp?.Reset(_cfg?.MaxHealth.Value ?? 100f);
        _lava?.Clear();
        _egg?.Reset();
        _fan?.Reset();
        _ui?.Hide();
        LogInfo("Run stopped — UI hidden, lava cleared, HP reset.");
    }

    public static void LogInfo(string msg) => Log?.LogInfo(msg);
}
