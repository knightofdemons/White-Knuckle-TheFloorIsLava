using System.Collections.Generic;
using UnityEngine;

namespace TheFloorIsLava;

internal sealed class FanningSystem
{
    private readonly Config _cfg;
    private readonly List<CoolingSpot> _spots = new();
    private readonly HandCycleTracker _hand0 = new();
    private readonly HandCycleTracker _hand1 = new();

    private int _warmupCycles;
    private float _hand0LastCycleTime = -999f;
    private float _hand1LastCycleTime = -999f;
    private bool _wasOnLava;
    private CoolingSpot? _attachedSpot;

    private static Material? _patchMaterial;

    public int WarmupCycles => _warmupCycles;
    public int SpotCount => _spots.Count;
    public float AttachedHeat => _attachedSpot?.Heat ?? 1f;

    public FanningSystem(Config cfg) => _cfg = cfg;

    public void Reset()
    {
        _warmupCycles = 0;
        _hand0LastCycleTime = -999f;
        _hand1LastCycleTime = -999f;
        _wasOnLava = false;
        _attachedSpot = null;
        ModState.LavaHeatMultiplier = 1f;
        _hand0.Reset();
        _hand1.Reset();
        ClearSpots();
    }

    public float Tick(ENT_Player player, float dt, bool onLava)
    {
        if (!_cfg.FanningEnabled.Value)
        {
            _attachedSpot = null;
            return 1f;
        }

        if (!onLava)
        {
            if (_wasOnLava)
            {
                _warmupCycles = 0;
                _hand0.Reset();
                _hand1.Reset();
            }
            _wasOnLava = false;
            _attachedSpot = null;
            UpdateSpots(dt);
            return 1f;
        }

        _wasOnLava = true;

        GameRefs.TryGetHand(player, 0, out var h0);
        GameRefs.TryGetHand(player, 1, out var h1);

        var c0 = h0 != null && _hand0.Tick(h0, 0);
        var c1 = h1 != null && _hand1.Tick(h1, 1);

        if (c0 && c1)
            OnFanCycle(player, 0, h0!, h1, bothHands: true);
        else if (c0)
            OnFanCycle(player, 0, h0!, h1, bothHands: IsOtherHandSynced(1));
        else if (c1)
            OnFanCycle(player, 1, h1!, h0, bothHands: IsOtherHandSynced(0));

        UpdateSpots(dt);
        RefreshAttachedSpot(player);
        return GetDamageHeatMultiplier(GameRefs.FeetPosition(player), player.transform.position);
    }

    public float GetDamageHeatMultiplier(Vector3 feet, Vector3 body)
    {
        if (_spots.Count == 0)
            return 1f;

        var lookupSq = SpotLookupRadiusSq();
        var heat = 1f;
        var hit = false;
        foreach (var spot in _spots)
        {
            if (!spot.OverlapsXZ(feet, body, lookupSq))
                continue;
            hit = true;
            heat = Mathf.Min(heat, spot.Heat);
        }

        return hit ? Mathf.Clamp01(heat) : 1f;
    }

    private float SpotLookupRadiusSq()
    {
        var spotR = _cfg.FanningSpotRadius.Value;
        var pad = _cfg.FanningSpotLookupPadding.Value;
        var lookupR = Mathf.Max(spotR + pad, _cfg.FanningSpotMergeRadius.Value * _cfg.FanningSpotLookupMergeFactor.Value);
        return lookupR * lookupR;
    }

    private void RefreshAttachedSpot(ENT_Player player)
    {
        var feet = GameRefs.FeetPosition(player);
        var body = player.transform.position;
        var lookupSq = SpotLookupRadiusSq();

        _attachedSpot = null;
        var bestDist = float.MaxValue;
        foreach (var spot in _spots)
        {
            if (!spot.OverlapsXZ(feet, body, lookupSq))
                continue;
            var d = spot.DistanceSqXZ(feet);
            if (d < bestDist)
            {
                bestDist = d;
                _attachedSpot = spot;
            }
        }
    }

    private bool IsOtherHandSynced(int otherHandIndex)
    {
        var sync = _cfg.FanningBothHandsSyncWindow.Value;
        var otherTime = otherHandIndex == 0 ? _hand0LastCycleTime : _hand1LastCycleTime;
        return Mathf.Abs(Time.time - otherTime) <= sync;
    }

    private float GetGripCostPerCycle(object hand, bool bothHands)
    {
        var maxGrip = GameRefs.GetGripStrengthMax(hand);
        var coolPerCycle = bothHands
            ? _cfg.FanningCoolPerCycleBoth.Value
            : _cfg.FanningCoolPerCycleSingle.Value;
        var cyclesToFullCool = Mathf.Max(1, Mathf.CeilToInt(1f / coolPerCycle));
        var totalBudget = maxGrip * _cfg.FanningFullCoolStaminaFraction.Value;
        return bothHands
            ? totalBudget / (cyclesToFullCool * 2f)
            : totalBudget / cyclesToFullCool;
    }

    private void OnFanCycle(ENT_Player player, int handIndex, object primaryHand, object? otherHand, bool bothHands)
    {
        var now = Time.time;

        if (handIndex == 0) _hand0LastCycleTime = now;
        else _hand1LastCycleTime = now;
        if (bothHands)
        {
            _hand0LastCycleTime = now;
            _hand1LastCycleTime = now;
        }

        if (_warmupCycles < _cfg.FanningActivationCycles.Value)
        {
            _warmupCycles++;
            Plugin.LogInfo($"Fanning: warmup {_warmupCycles}/{_cfg.FanningActivationCycles.Value}");
            return;
        }

        var coolAmount = bothHands
            ? _cfg.FanningCoolPerCycleBoth.Value
            : _cfg.FanningCoolPerCycleSingle.Value;
        var gripCost = GetGripCostPerCycle(primaryHand, bothHands);

        if (GameRefs.GetGripStrength(primaryHand) < gripCost)
            return;

        if (bothHands && otherHand != null && GameRefs.GetGripStrength(otherHand) < gripCost)
            return;

        if (!GameRefs.DamageGripOnHand(primaryHand, gripCost))
            return;

        if (bothHands && otherHand != null && !GameRefs.DamageGripOnHand(otherHand, gripCost))
        {
            GameRefs.AddGripToHand(primaryHand, gripCost);
            return;
        }

        var feet = GameRefs.FeetPosition(player);
        var surface = SampleSurface(feet);
        var spot = GetOrCreateSpot(feet, surface);
        spot.Heat = Mathf.Clamp01(spot.Heat - coolAmount);
        spot.LastFanTime = now;
        spot.UpdateVisual(_cfg.FanningSpotRadius.Value);
        _attachedSpot = spot;
        GustEffect.Play(player, handIndex, bothHands);
    }

    private CoolingSpot GetOrCreateSpot(Vector3 anchor, (Vector3 point, Vector3 normal) surface)
    {
        var merge = _cfg.FanningSpotMergeRadius.Value;
        var mergeSq = merge * merge;
        CoolingSpot? best = null;
        var bestDistSq = float.MaxValue;

        foreach (var spot in _spots)
        {
            var distSq = spot.DistanceSqXZ(anchor);
            if (distSq <= mergeSq && distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = spot;
            }
        }

        if (best != null)
            return best;

        var created = new CoolingSpot(anchor, surface.point, surface.normal, BuildPatchMaterial());
        _spots.Add(created);
        EnforceSpotCap();
        created.UpdateVisual(_cfg.FanningSpotRadius.Value);
        return created;
    }

    private static (Vector3 point, Vector3 normal) SampleSurface(Vector3 near)
    {
        var origin = near + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            return (hit.point, hit.normal);
        return (near, Vector3.up);
    }

    private void UpdateSpots(float dt)
    {
        if (_spots.Count == 0)
            return;

        var delay = _cfg.FanningHeatRecoveryDelay.Value;
        var recoveryRate = _cfg.FanningHeatRecoveryTime.Value > 0.01f
            ? 1f / _cfg.FanningHeatRecoveryTime.Value
            : 0.25f;
        var radius = _cfg.FanningSpotRadius.Value;
        var cooledThreshold = _cfg.FanningFullyCooledThreshold.Value;
        var recentWindow = _cfg.FanningRecentCoolWindow.Value;

        for (var i = _spots.Count - 1; i >= 0; i--)
        {
            var spot = _spots[i];
            var cooledRecently = Time.time - spot.LastFanTime < recentWindow;
            var fullyCooled = spot.Heat <= cooledThreshold;
            var canRecover = !cooledRecently
                && (!fullyCooled || Time.time - spot.LastFanTime >= delay);
            if (canRecover)
                spot.Heat = Mathf.Min(1f, spot.Heat + recoveryRate * dt);

            if (spot.Heat >= 1f)
            {
                if (ReferenceEquals(spot, _attachedSpot))
                    _attachedSpot = null;
                spot.Dispose();
                _spots.RemoveAt(i);
                continue;
            }

            spot.UpdateVisual(radius);
        }
    }

    private void EnforceSpotCap()
    {
        while (_spots.Count > _cfg.FanningSpotMaxCount.Value)
        {
            _spots[0].Dispose();
            _spots.RemoveAt(0);
        }
    }

    private void ClearSpots()
    {
        foreach (var s in _spots)
            s.Dispose();
        _spots.Clear();
    }

    private static Material BuildPatchMaterial()
    {
        if (_patchMaterial != null) return _patchMaterial;

        Shader? shader = null;
        foreach (var n in new[] { "Universal Render Pipeline/Unlit", "Standard", "Unlit/Texture" })
        {
            shader = Shader.Find(n);
            if (shader != null) break;
        }
        if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

        _patchMaterial = new Material(shader!) { name = "TheFloorIsLava_CoolPatch" };
        var white = Color.white;
        _patchMaterial.color = white;
        if (_patchMaterial.HasProperty("_BaseColor")) _patchMaterial.SetColor("_BaseColor", white);
        if (_patchMaterial.HasProperty("_Color")) _patchMaterial.SetColor("_Color", white);
        if (_patchMaterial.HasProperty("_Surface")) _patchMaterial.SetFloat("_Surface", 0f);
        if (_patchMaterial.HasProperty("_Blend")) _patchMaterial.SetFloat("_Blend", 0f);
        if (_patchMaterial.HasProperty("_SrcBlend")) _patchMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (_patchMaterial.HasProperty("_DstBlend")) _patchMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        if (_patchMaterial.HasProperty("_ZWrite")) _patchMaterial.SetFloat("_ZWrite", 1f);
        if (_patchMaterial.HasProperty("_Cull")) _patchMaterial.SetFloat("_Cull", 0f);
        if (_patchMaterial.HasProperty("_AlphaClip")) _patchMaterial.SetFloat("_AlphaClip", 1f);
        if (_patchMaterial.HasProperty("_Cutoff")) _patchMaterial.SetFloat("_Cutoff", 0.05f);
        _patchMaterial.EnableKeyword("_ALPHATEST_ON");
        _patchMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _patchMaterial.DisableKeyword("_ALPHABLEND_ON");
        _patchMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest + 2;
        return _patchMaterial;
    }

    private sealed class HandCycleTracker
    {
        private bool _wasDown;

        public void Reset() => _wasDown = false;

        public bool Tick(object hand, int index)
        {
            var down = GameRefs.IsHandGrabHeld(hand, index);
            var completed = _wasDown && !down;
            _wasDown = down;
            return completed;
        }
    }

    private sealed class CoolingSpot
    {
        public Vector3 Center;
        public float Heat = 1f;
        public float LastFanTime;
        private readonly GameObject _visual;
        private readonly MeshRenderer _renderer;
        private readonly Material _mat;

        public CoolingSpot(Vector3 anchor, Vector3 surfacePoint, Vector3 normal, Material sharedMat)
        {
            LastFanTime = Time.time;
            Center = new Vector3(anchor.x, surfacePoint.y, anchor.z);

            _visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _visual.name = "TheFloorIsLava_CoolSpot";
            Object.Destroy(_visual.GetComponent<Collider>());

            _renderer = _visual.GetComponent<MeshRenderer>();
            _mat = new Material(sharedMat) { name = "TheFloorIsLava_CoolSpotMat" };
            _renderer.sharedMaterial = _mat;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            ApplyTransform(surfacePoint, normal);
        }

        public float DistanceSqXZ(Vector3 pos)
        {
            var dx = pos.x - Center.x;
            var dz = pos.z - Center.z;
            return dx * dx + dz * dz;
        }

        public bool ContainsXZ(Vector3 pos, float radiusSq) => DistanceSqXZ(pos) <= radiusSq;

        public bool OverlapsXZ(Vector3 feet, Vector3 body, float radiusSq) =>
            ContainsXZ(feet, radiusSq) || ContainsXZ(body, radiusSq);

        private void ApplyTransform(Vector3 surfacePoint, Vector3 normal)
        {
            _visual.transform.position = surfacePoint + normal * 0.04f;
            _visual.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        }

        public void UpdateVisual(float radius)
        {
            var coolStrength = 1f - Heat;
            var diameter = radius * 2f;
            var fade = Mathf.SmoothStep(0f, 1f, coolStrength);
            var fadeOut = Heat > 0.9f ? Mathf.InverseLerp(1f, 0.9f, Heat) : 1f;
            var scale = diameter * Mathf.Lerp(0.25f, 1f, fade) * fadeOut;
            _visual.transform.localScale = new Vector3(scale, 0.008f, scale);
            _renderer.enabled = fadeOut > 0.02f;

            var hotGrey = new Color(0.62f, 0.62f, 0.65f);
            var coldGrey = new Color(0.22f, 0.22f, 0.26f);
            var grey = Color.Lerp(hotGrey, coldGrey, fade * fadeOut);
            _mat.color = grey;
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", grey);
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", grey);
            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.SetColor("_EmissionColor", grey * Mathf.Lerp(0.05f, 0.45f, fade));
                _mat.EnableKeyword("_EMISSION");
            }
        }

        public void Dispose()
        {
            if (_visual != null) Object.Destroy(_visual);
            if (_mat != null) Object.Destroy(_mat);
        }
    }
}

internal static class GustEffect
{
    private static readonly List<GustPuff> _active = new();
    private static Material? _mat;

    public static void Play(ENT_Player player, int handIndex, bool bothHands)
    {
        SpawnBurst(player, handIndex);
        if (bothHands)
            SpawnBurst(player, handIndex == 0 ? 1 : 0);
    }

    public static void Tick(float dt)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            if (!_active[i].Tick(dt))
                _active.RemoveAt(i);
        }
    }

    private static void SpawnBurst(ENT_Player player, int handIndex)
    {
        var origin = GetHandWorldPos(player, handIndex);
        var cam = Camera.main;
        var right = cam != null ? cam.transform.right : player.transform.right;
        var mat = BuildMaterial();

        for (var i = 0; i < 5; i++)
        {
            var spread = (i - 2) * 0.12f;
            var dir = (Vector3.down + right * spread * 0.22f).normalized;
            var offset = right * spread * 0.04f + Vector3.up * Random.Range(-0.01f, 0.02f);
            _active.Add(new GustPuff(origin + offset, dir, mat));
        }
    }

    private static Vector3 GetHandWorldPos(ENT_Player player, int handIndex)
    {
        var model = GameRefs.GetHandModel(player, handIndex);
        if (model != null)
            return model.position;

        var cam = Camera.main;
        if (cam != null)
        {
            var side = handIndex == 0 ? -0.22f : 0.22f;
            return cam.transform.position
                   + cam.transform.forward * 0.35f
                   + cam.transform.right * side
                   + cam.transform.up * -0.18f;
        }

        var bodySide = handIndex == 0 ? -0.3f : 0.3f;
        return player.transform.position
               + player.transform.forward * 0.4f
               + player.transform.right * bodySide
               + Vector3.up * 1.1f;
    }

    private static Material BuildMaterial()
    {
        if (_mat != null) return _mat;

        Shader? shader = null;
        foreach (var n in new[] { "Universal Render Pipeline/Unlit", "Standard" })
        {
            shader = Shader.Find(n);
            if (shader != null) break;
        }
        if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

        _mat = new Material(shader!) { name = "TheFloorIsLava_Gust" };
        var tint = new Color(0.85f, 0.92f, 1f, 1f);
        _mat.color = tint;
        if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", tint);
        if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", tint);
        if (_mat.HasProperty("_EmissionColor")) _mat.SetColor("_EmissionColor", tint * 2.5f);
        if (_mat.HasProperty("_Surface")) _mat.SetFloat("_Surface", 0f);
        if (_mat.HasProperty("_AlphaClip")) _mat.SetFloat("_AlphaClip", 1f);
        if (_mat.HasProperty("_Cutoff")) _mat.SetFloat("_Cutoff", 0.05f);
        _mat.EnableKeyword("_ALPHATEST_ON");
        _mat.EnableKeyword("_EMISSION");
        _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest + 3;
        return _mat;
    }

    private sealed class GustPuff
    {
        private readonly GameObject _go;
        private readonly Material _inst;
        private readonly Vector3 _velocity;
        private readonly float _startScale;
        private float _life = 0.35f;

        public GustPuff(Vector3 pos, Vector3 dir, Material shared)
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(_go.GetComponent<Collider>());
            _go.name = "TheFloorIsLava_Gust";
            _go.transform.position = pos;
            _startScale = Random.Range(0.04f, 0.07f);
            _go.transform.localScale = Vector3.one * _startScale;
            _inst = new Material(shared);
            var mr = _go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _inst;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _velocity = dir * Random.Range(1.2f, 2.2f);
        }

        public bool Tick(float dt)
        {
            _life -= dt;
            var t = Mathf.Clamp01(_life / 0.35f);
            _go.transform.position += _velocity * dt;
            _go.transform.localScale = Vector3.one * _startScale * (1f + (1f - t) * 1.5f);

            var fade = t * t;
            var c = new Color(0.85f, 0.92f, 1f, 1f);
            _inst.color = c;
            if (_inst.HasProperty("_BaseColor")) _inst.SetColor("_BaseColor", c);
            if (_inst.HasProperty("_EmissionColor")) _inst.SetColor("_EmissionColor", c * (2.5f * fade));

            if (_life <= 0f)
            {
                Object.Destroy(_go);
                Object.Destroy(_inst);
                return false;
            }
            return true;
        }
    }
}
