using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheFloorIsLava.Lava;

/// <summary>
/// Lava zone manager. Continuously scans level geometry around the player and
/// detects player contact with pure arithmetic (no physics overlap dependency).
///
/// v2 visual: one merged lava surface mesh per ground collider. Zones placed on
/// the same collider all contribute coverage to that single mesh, so neighbours
/// merge into one continuous flowing lava surface. World-space UVs keep the
/// texture continuous across patches and across colliders that share a Y-level.
/// </summary>
internal sealed class LavaField
{
    private readonly Config _cfg;
    private readonly List<LavaZone> _zones = new();
    private readonly HashSet<long> _cells = new();
    private readonly HashSet<int> _excludedColliders = new();
    private readonly Dictionary<int, LavaSurface> _surfaces = new();
    private readonly HashSet<int> _dirtySurfaces = new();
    private float _worldScanTimer;
    private float _lodTimer;
    private Material? _lavaBaseMaterial;
    private Material? _lavaGlowMaterial;
    private Material? _lavaDripMaterial;

    public int Count => _zones.Count;
    public int SurfaceCount => _surfaces.Count;

    public LavaField(Config cfg) => _cfg = cfg;

    public void Clear()
    {
        foreach (var z in _zones)
        {
            if (z != null)
                Object.Destroy(z.gameObject);
        }
        _zones.Clear();
        _cells.Clear();
        _excludedColliders.Clear();

        foreach (var s in _surfaces.Values)
            s.Dispose();
        _surfaces.Clear();
        _dirtySurfaces.Clear();

        _worldScanTimer = 0f;
        _lodTimer = 0f;
    }

    /// <summary>Exempt the floor directly beneath the player from lava placement.</summary>
    public void ExcludeStartingFloor(ENT_Player player)
    {
        var origin = player.transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(origin, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            Plugin.LogInfo("Starting floor exemption: no floor found beneath player.");
            return;
        }
        var col = hit.collider;
        if (col == null)
            return;
        _excludedColliders.Add(col.GetInstanceID());
        Plugin.LogInfo($"Starting floor exempted: '{col.name}' (id={col.GetInstanceID()}) at y={hit.point.y:F1}");
    }

    /// <summary>
    /// Always-running scan around the player. Walks every scene root, finds upward-facing
    /// static floor colliders within range, fills them with lava zones. Self-healing.
    /// </summary>
    public void TickWorldScan(ENT_Player player, float dt)
    {
        _worldScanTimer += dt;
        _lodTimer += dt;

        // LOD pass (cheap; toggles renderers on far surfaces).
        if (_lodTimer >= _cfg.LODUpdateInterval.Value)
        {
            _lodTimer = 0f;
            UpdateLOD(player.transform.position);
        }

        if (_worldScanTimer < _cfg.WorldScanInterval.Value)
            return;
        _worldScanTimer = 0f;
        ScanScene(player);
    }

    public void ForceScan(ENT_Player player) => ScanScene(player);

    private void ScanScene(ENT_Player player)
    {
        var scene = player.gameObject.scene;
        if (!scene.IsValid())
            return;

        if (_lavaBaseMaterial == null)
            BuildLavaMaterials();

        var minUp = LevelGeom.MinUpDot(_cfg.MaxTiltDegrees.Value);
        var spacing = _cfg.LavaSpacing.Value;
        var cap = _cfg.MaxZones.Value;
        var before = _zones.Count;
        var playerPos = player.transform.position;
        var horizMax = _cfg.WorldScanHorizRange.Value;
        var vertMax = _cfg.WorldScanVertRange.Value;
        var horizMaxSq = horizMax * horizMax;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (_zones.Count >= cap)
                break;
            ScanRoot(root, minUp, spacing, scene, cap, playerPos, horizMaxSq, vertMax);
        }

        RebuildDirtySurfaces();

        var added = _zones.Count - before;
        if (added > 0)
            Plugin.LogInfo($"Lava scan: +{added} zones (total {_zones.Count}, surfaces {_surfaces.Count}) near ({playerPos.x:F0},{playerPos.y:F0},{playerPos.z:F0})");
    }

    private void ScanRoot(GameObject root, float minUp, float spacing, Scene scene, int cap,
                          Vector3 playerPos, float horizMaxSq, float vertMax)
    {
        var colliders = root.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (var col in colliders)
        {
            if (_zones.Count >= cap)
                return;
            if (!LevelGeom.IsFloorCollider(col))
                continue;
            if (_excludedColliders.Contains(col.GetInstanceID()))
                continue;

            var b = col.bounds;
            var dxc = Mathf.Max(0f, Mathf.Abs(b.center.x - playerPos.x) - b.extents.x);
            var dzc = Mathf.Max(0f, Mathf.Abs(b.center.z - playerPos.z) - b.extents.z);
            var dyc = Mathf.Max(0f, Mathf.Abs(b.center.y - playerPos.y) - b.extents.y);
            if (dxc * dxc + dzc * dzc > horizMaxSq)
                continue;
            if (dyc > vertMax)
                continue;

            FillCollider(col, minUp, spacing, scene, cap);
        }
    }

    private void BuildLavaMaterials()
    {
        if (_lavaBaseMaterial != null) return;

        var shader = FindLavaShader(out var shaderName);
        if (shader == null)
        {
            Plugin.LogInfo("No usable shader found for lava patches!");
            return;
        }

        var baseTex = LavaTexture.Base();
        var glowTex = LavaTexture.Glow();
        var dripTex = LavaTexture.DripStrip();

        // Two-layer rendering:
        //   1) Base — opaque alpha-cutout solid mesh-color (dark crust plates).
        //      This gives the lava a hard silhouette that occludes the floor.
        //   2) Glow — additive, drawn on top of the base inside the same sharp
        //      boundary. Only the bright crack pixels contribute, so the crust
        //      stays solid-dark and only the fissures glow.
        _lavaBaseMaterial = BuildBaseMaterial(shader, baseTex, "TheFloorIsLava_Base");
        _lavaGlowMaterial = BuildGlowMaterial(shader, glowTex, "TheFloorIsLava_Glow");
        // Drip is overlay-only — same additive style as the glow.
        _lavaDripMaterial = BuildGlowMaterial(shader, dripTex, "TheFloorIsLava_Drip");

        Plugin.LogInfo(
            $"Lava materials built: shader='{shaderName}' " +
            $"baseTex={baseTex.width}x{baseTex.height} glowTex={glowTex.width}x{glowTex.height} " +
            $"dripTex={dripTex.width}x{dripTex.height}.");
    }

    /// <summary>Opaque, alpha-tested crust layer. Vertex alpha is used as the
    /// cutoff mask so the patch silhouette is hard and the mesh has a real
    /// "base color" against the floor.</summary>
    private Material BuildBaseMaterial(Shader shader, Texture2D tex, string name)
    {
        var mat = new Material(shader) { name = name };
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

        var white = new Color(1f, 1f, 1f, 1f);
        mat.color = white;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", white);

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f); // Opaque
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.45f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return mat;
    }

    /// <summary>Additive highlight layer. Renders ON TOP of the base inside the
    /// exact same alpha-cutout boundary so the glow doesn't leak beyond the
    /// silhouette. Dark pixels (the crust) contribute almost nothing to additive
    /// blending; only the bright crack pixels show up as glow.</summary>
    private Material BuildGlowMaterial(Shader shader, Texture2D tex, string name)
    {
        var mat = new Material(shader) { name = name };
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);

        var intensity = Mathf.Max(0.1f, _cfg.LavaEmissionStrength.Value);
        var tint = new Color(intensity, intensity, intensity, 1f);
        mat.color = tint;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f); // Additive
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.45f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest + 1;

        mat.EnableKeyword("_EMISSION");
        var em = new Color(1f, 0.5f, 0.12f) * intensity;
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", em);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return mat;
    }

    private static Shader? FindLavaShader(out string name)
    {
        string[] candidates =
        {
            "Universal Render Pipeline/Unlit",
            "Unlit/Transparent",
            "Sprites/Default",
            "Legacy Shaders/Particles/Alpha Blended",
            "Standard",
            "Universal Render Pipeline/Lit",
        };
        foreach (var n in candidates)
        {
            var s = Shader.Find(n);
            if (s != null) { name = n; return s; }
        }
        name = "";
        return null;
    }

    private void FillCollider(Collider col, float minUp, float spacing, Scene scene, int cap)
    {
        var bounds = col.bounds;
        if (bounds.size.x < 0.2f && bounds.size.z < 0.2f)
            return;

        var step = Mathf.Max(spacing * 0.45f, 0.4f);
        var probeY = bounds.max.y + 0.3f;
        var minDensity = _cfg.DensityMin.Value;
        var noiseScale = _cfg.NoiseScale.Value;
        var jitter = step * 0.7f;
        var supportRadius = Mathf.Clamp(spacing * 0.45f, 0.25f, 0.6f);

        for (var x = bounds.min.x; x <= bounds.max.x + 0.01f; x += step)
        {
            for (var z = bounds.min.z; z <= bounds.max.z + 0.01f; z += step)
            {
                if (_zones.Count >= cap)
                    return;

                var density = Mathf.PerlinNoise(x * noiseScale, z * noiseScale);
                if (density < minDensity)
                    continue;

                var jx = (Mathf.PerlinNoise(x * 1.7f + 13.1f, z * 1.7f) - 0.5f) * 2f * jitter;
                var jz = (Mathf.PerlinNoise(x * 1.7f, z * 1.7f + 27.9f) - 0.5f) * 2f * jitter;

                var probe = new Vector3(x + jx, probeY, z + jz);
                if (!Physics.Raycast(probe, Vector3.down, out var hit, bounds.size.y + 1.5f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (!IsHitOnCollider(hit, col))
                    continue;
                if (!LevelGeom.IsFloorHit(hit, minUp))
                    continue;
                if (!HasGroundSupport(hit.point, supportRadius, col))
                    continue;

                SpawnZone(col, hit.point, spacing, scene);
            }
        }
    }

    private static readonly Vector3[] SupportOffsets =
    {
        new Vector3(1f, 0f, 0f),
        new Vector3(-1f, 0f, 0f),
        new Vector3(0f, 0f, 1f),
        new Vector3(0f, 0f, -1f),
    };

    private static bool HasGroundSupport(Vector3 surface, float radius, Collider target)
    {
        var supported = 0;
        for (var i = 0; i < SupportOffsets.Length; i++)
        {
            var origin = surface + SupportOffsets[i] * radius + Vector3.up * 0.35f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 0.9f, ~0,
                    QueryTriggerInteraction.Ignore))
                continue;
            if (Mathf.Abs(hit.point.y - surface.y) > 0.35f)
                continue;
            if (!IsHitOnCollider(hit, target))
                continue;
            supported++;
        }
        return supported >= 3;
    }

    private static bool IsHitOnCollider(RaycastHit hit, Collider target)
    {
        if (hit.collider == target)
            return true;
        return hit.collider.transform.IsChildOf(target.transform) ||
               target.transform.IsChildOf(hit.collider.transform);
    }

    /// <summary>
    /// Creates the cheap marker GameObject used for arithmetic damage detection
    /// and adds the zone position to the host collider's merged lava surface.
    /// No mesh/renderer here — the visual is one shared surface per collider.
    /// </summary>
    private void SpawnZone(Collider host, Vector3 surface, float spacing, Scene scene)
    {
        var zoneRadius = Mathf.Clamp(spacing * 0.5f, 0.45f, 0.85f);
        var pos = surface + Vector3.up * 0.01f;
        var cell = CellKey(pos, spacing);
        if (!_cells.Add(cell))
            return;

        var go = new GameObject("LavaZone");
        if (scene.IsValid())
            SceneManager.MoveGameObjectToScene(go, scene);

        go.transform.position = pos;
        go.transform.rotation = Quaternion.identity;

        var zone = go.AddComponent<LavaZone>();
        zone.Setup(zoneRadius);
        _zones.Add(zone);

        if (!_cfg.ShowVisuals.Value)
            return;

        // Group the zone into the merged surface for its host collider.
        var id = host.GetInstanceID();
        if (!_surfaces.TryGetValue(id, out var surface2))
        {
            surface2 = new LavaSurface(host, scene);
            _surfaces[id] = surface2;
        }
        surface2.AddZone(pos);
        _dirtySurfaces.Add(id);
    }

    /// <summary>Rebuild every surface that gained new zones in the last scan.
    /// Cheap because each collider is touched at most once per scan tick.</summary>
    private void RebuildDirtySurfaces()
    {
        if (_dirtySurfaces.Count == 0 || _lavaBaseMaterial == null)
            return;

        var gridStep = _cfg.SurfaceGridStep.Value;
        var influence = _cfg.SurfaceInfluenceRadius.Value;
        var texScale = _cfg.TextureWorldScale.Value;
        var enableDrips = _cfg.LavaDrips.Value;
        var thinThr = _cfg.ThinFloorThreshold.Value;

        foreach (var id in _dirtySurfaces)
        {
            if (!_surfaces.TryGetValue(id, out var s))
                continue;
            s.Rebuild(_lavaBaseMaterial, _lavaGlowMaterial, _lavaDripMaterial,
                gridStep, influence, texScale, enableDrips, thinThr);
        }
        _dirtySurfaces.Clear();
    }

    /// <summary>Renderer-only culling: turn off MeshRenderers on surfaces that
    /// are far from the player so we stop paying transparent-overdraw cost for
    /// rooms behind closed doors. Markers and damage detection are untouched.</summary>
    private void UpdateLOD(Vector3 playerPos)
    {
        var dist = _cfg.RenderDistance.Value;
        var distSq = dist * dist;
        foreach (var kvp in _surfaces)
        {
            var s = kvp.Value;
            if (s == null) continue;
            s.SetVisible((s.Center - playerPos).sqrMagnitude <= distSq);
        }
    }

    /// <summary>
    /// Pure-arithmetic detection: zone center horizontal distance vs. player feet/body,
    /// with a vertical window. No physics queries, no Rigidbody messages.
    /// </summary>
    public bool PlayerOnLava(ENT_Player player, out float nearestHoriz, out int candidates)
    {
        nearestHoriz = float.MaxValue;
        candidates = 0;
        if (_zones.Count == 0)
            return false;

        var touch = _cfg.TouchDistance.Value;
        var vertAbove = _cfg.VerticalAbove.Value;
        var vertBelow = _cfg.VerticalBelow.Value;
        var body = player.transform.position;
        var checkY = GameRefs.LavaVerticalCheckY(player);

        var touched = false;

        foreach (var zone in _zones)
        {
            if (zone == null)
                continue;
            var zp = zone.transform.position;

            var dy = checkY - zp.y;
            if (dy > vertAbove || dy < -vertBelow)
                continue;

            candidates++;

            var dxF = body.x - zp.x;
            var dzF = body.z - zp.z;
            var horizSq = dxF * dxF + dzF * dzF;
            var horiz = Mathf.Sqrt(horizSq);
            if (horiz < nearestHoriz)
                nearestHoriz = horiz;

            if (horiz <= zone.Radius + touch)
                touched = true;
        }

        if (nearestHoriz == float.MaxValue)
            nearestHoriz = -1f;
        return touched;
    }

    public void PruneFar(Vector3 playerPos, float maxDistance)
    {
        if (_zones.Count < _cfg.MaxZones.Value * 0.9f)
            return;

        var maxSq = maxDistance * maxDistance;
        for (var i = _zones.Count - 1; i >= 0; i--)
        {
            var z = _zones[i];
            if (z == null)
            {
                _zones.RemoveAt(i);
                continue;
            }
            if ((z.transform.position - playerPos).sqrMagnitude > maxSq)
            {
                Object.Destroy(z.gameObject);
                _zones.RemoveAt(i);
            }
        }
    }

    private static long CellKey(Vector3 p, float spacing)
    {
        unchecked
        {
            var ix = (long)Mathf.FloorToInt(p.x / spacing);
            var iy = (long)Mathf.FloorToInt(p.y / spacing);
            var iz = (long)Mathf.FloorToInt(p.z / spacing);
            return (ix * 73856093L) ^ (iy * 19349663L) ^ (iz * 83492791L);
        }
    }

    // ----- inner types --------------------------------------------------------

    /// <summary>One merged lava mesh per ground collider. Vertices form a flat
    /// grid sized to the collider's footprint, each carrying a coverage value
    /// (vertex-alpha) that falls off with distance from the registered zones.
    /// Result: zones placed close together visually merge into one continuous
    /// puddle with organic edges, and the texture is world-space-tiled so the
    /// pattern is continuous across the puddle.</summary>
    private sealed class LavaSurface
    {
        public Collider? Collider;
        public readonly List<Vector3> ZonePositions = new();
        public GameObject? Root;
        public Mesh? Mesh;
        public MeshRenderer? Renderer;
        public Vector3 Center { get; private set; }
        public bool IsThinFloor { get; private set; }
        private bool _visible = true;

        public LavaSurface(Collider col, Scene scene)
        {
            Collider = col;
            Root = new GameObject("LavaSurface");
            if (scene.IsValid())
                SceneManager.MoveGameObjectToScene(Root, scene);
            Center = col.bounds.center;
            Root.transform.position = Center;
            Root.transform.rotation = Quaternion.identity;
        }

        public void AddZone(Vector3 pos)
        {
            ZonePositions.Add(pos);
        }

        public void Rebuild(Material? baseMat, Material? glowMat, Material? dripMat,
            float gridStep, float influence, float texScale, bool enableDrips, float thinThreshold)
        {
            if (Collider == null || Root == null || baseMat == null) return;
            if (ZonePositions.Count == 0) return;

            var b = Collider.bounds;
            IsThinFloor = b.size.y < thinThreshold;
            Center = b.center;
            Root.transform.position = Center;

            // Mesh has TWO submeshes pointing to the same triangle list so Unity
            // can render the same geometry twice with two materials (base then
            // glow overlay) in a single MeshRenderer.
            var newMesh = LavaSurfaceMesh.Build(b, ZonePositions, gridStep, influence,
                texScale, Center);
            if (newMesh == null) return;

            if (Mesh != null) Object.Destroy(Mesh);
            Mesh = newMesh;

            var mf = Root.GetComponent<MeshFilter>();
            if (mf == null) mf = Root.AddComponent<MeshFilter>();
            mf.sharedMesh = Mesh;

            Renderer = Root.GetComponent<MeshRenderer>();
            if (Renderer == null) Renderer = Root.AddComponent<MeshRenderer>();
            if (glowMat != null)
                Renderer.sharedMaterials = new[] { baseMat, glowMat };
            else
                Renderer.sharedMaterial = baseMat;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            Renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            if (enableDrips && IsThinFloor && dripMat != null)
            {
                EnsureDrips(dripMat);
            }
            else
            {
                ClearDrips();
            }
        }

        private readonly List<GameObject> _drips = new();

        private void EnsureDrips(Material dripMat)
        {
            ClearDrips();
            if (Root == null) return;
            for (var i = 0; i < ZonePositions.Count; i += 3)
            {
                var zp = ZonePositions[i];
                var seed = unchecked(Mathf.RoundToInt(zp.x * 17.3f) ^ Mathf.RoundToInt(zp.z * 31.7f));
                var len = 0.2f + (Mathf.PerlinNoise(zp.x, zp.z)) * 0.4f;
                var mesh = LavaDripMesh.Build(seed, 0.18f, len);
                var go = new GameObject("LavaDrip");
                go.transform.SetParent(Root.transform, false);
                go.transform.position = zp + new Vector3(0f, -0.02f, 0f);
                go.transform.rotation = Quaternion.identity;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = dripMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _drips.Add(go);
            }
        }

        private void ClearDrips()
        {
            foreach (var d in _drips)
                if (d != null) Object.Destroy(d);
            _drips.Clear();
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            if (Renderer != null) Renderer.enabled = visible;
            foreach (var d in _drips)
            {
                if (d == null) continue;
                var mr = d.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = visible;
            }
        }

        public void Dispose()
        {
            ClearDrips();
            if (Mesh != null) Object.Destroy(Mesh);
            if (Root != null) Object.Destroy(Root);
            Mesh = null;
            Root = null;
            Renderer = null;
        }
    }

    /// <summary>Builder for the merged per-collider lava surface mesh.</summary>
    private static class LavaSurfaceMesh
    {
        public static Mesh? Build(Bounds b, List<Vector3> zones, float gridStep,
            float influence, float texScale, Vector3 localOrigin)
        {
            // Restrict the grid to the rectangle that actually contains zones (plus
            // the influence radius). That keeps the mesh small even on huge floors.
            var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            var max = new Vector3(float.MinValue, 0f, float.MinValue);
            foreach (var z in zones)
            {
                if (z.x < min.x) min.x = z.x;
                if (z.x > max.x) max.x = z.x;
                if (z.z < min.z) min.z = z.z;
                if (z.z > max.z) max.z = z.z;
            }
            min.x = Mathf.Max(min.x - influence, b.min.x);
            min.z = Mathf.Max(min.z - influence, b.min.z);
            max.x = Mathf.Min(max.x + influence, b.max.x);
            max.z = Mathf.Min(max.z + influence, b.max.z);

            var sizeX = max.x - min.x;
            var sizeZ = max.z - min.z;
            if (sizeX <= 0f || sizeZ <= 0f) return null;

            var nx = Mathf.Max(2, Mathf.CeilToInt(sizeX / gridStep) + 1);
            var nz = Mathf.Max(2, Mathf.CeilToInt(sizeZ / gridStep) + 1);
            var vertCount = nx * nz;
            if (vertCount > 16000) return null; // safety: skip huge surfaces

            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var colors = new Color[vertCount];

            // Per-vertex coverage from nearest zone(s). Inverse-distance blend over
            // zones within range, with per-vertex Y interpolated from those zones'
            // surface heights so the mesh follows ramps too.
            var influenceSq = influence * influence;
            var maxBlendSq = (influence * 1.8f) * (influence * 1.8f);

            for (var j = 0; j < nz; j++)
            {
                var wz = (nz <= 1) ? min.z : Mathf.Lerp(min.z, max.z, j / (float)(nz - 1));
                for (var i = 0; i < nx; i++)
                {
                    var idx = j * nx + i;
                    var wx = (nx <= 1) ? min.x : Mathf.Lerp(min.x, max.x, i / (float)(nx - 1));

                    var coverage = 0f;
                    var yWeighted = 0f;
                    var yWeight = 0f;
                    for (var k = 0; k < zones.Count; k++)
                    {
                        var zp = zones[k];
                        var dx = wx - zp.x;
                        var dz = wz - zp.z;
                        var dSq = dx * dx + dz * dz;
                        if (dSq > maxBlendSq) continue;

                        var d = Mathf.Sqrt(dSq);
                        if (dSq <= influenceSq)
                        {
                            var inf = 1f - Mathf.SmoothStep(0f, 1f, d / influence);
                            if (inf > coverage) coverage = inf;
                        }
                        var w = 1f / (d + 0.15f);
                        yWeighted += zp.y * w;
                        yWeight += w;
                    }
                    var vy = (yWeight > 0f) ? (yWeighted / yWeight) : b.max.y;

                    // Organic, jagged-edged coverage: modulate with low-freq Perlin
                    // so the puddle outline ripples instead of being a clean circle.
                    var ripple = Mathf.PerlinNoise(wx * 0.6f + 13f, wz * 0.6f + 7f);
                    var rippleAlt = Mathf.PerlinNoise(wx * 1.7f + 91f, wz * 1.7f + 41f) * 0.4f;
                    var edgeMod = Mathf.Lerp(0.7f, 1.25f, ripple) + (rippleAlt - 0.2f) * 0.4f;
                    coverage = Mathf.Clamp01(coverage * edgeMod);

                    // The base material does alpha-cutout at 0.45, so coverage
                    // here is consumed as a binary mask. Push interior alpha
                    // well above the cutoff so the inside reads as a solid
                    // plate; the outer ring drops sharply through the cutoff
                    // for a clean organic edge with no "cloud" halo.
                    coverage = Mathf.SmoothStep(0f, 1f, coverage);
                    coverage = Mathf.Clamp01((coverage - 0.18f) * 1.7f);
                    if (coverage < 0.05f) coverage = 0f;

                    verts[idx] = new Vector3(wx - localOrigin.x, vy - localOrigin.y + 0.02f, wz - localOrigin.z);
                    uvs[idx] = new Vector2(wx * texScale, wz * texScale);
                    colors[idx] = new Color(1f, 1f, 1f, coverage);
                }
            }

            // Build triangles, skipping quads whose four corners are all transparent.
            var tris = new List<int>(nx * nz * 6);
            for (var j = 0; j < nz - 1; j++)
            {
                for (var i = 0; i < nx - 1; i++)
                {
                    var a = j * nx + i;
                    var b2 = a + 1;
                    var c2 = a + nx;
                    var d2 = c2 + 1;
                    if (colors[a].a + colors[b2].a + colors[c2].a + colors[d2].a < 0.05f)
                        continue;
                    tris.Add(a); tris.Add(c2); tris.Add(b2);
                    tris.Add(b2); tris.Add(c2); tris.Add(d2);
                }
            }
            if (tris.Count == 0) return null;

            var mesh = new Mesh { name = "TheFloorIsLava_Surface" };
            if (vertCount > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            // Two submeshes that share the same triangle list — one consumed by
            // the opaque base material, the other by the additive glow overlay.
            var triArr = tris.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetTriangles(triArr, 0);
            mesh.SetTriangles(triArr, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    /// <summary>Quad-strip drip ribbon that hangs under thin floors.</summary>
    private static class LavaDripMesh
    {
        public static Mesh Build(int seed, float radius, float length)
        {
            var rng = new System.Random(seed);
            const int rungs = 6;
            var verts = new Vector3[rungs * 2];
            var uvs = new Vector2[verts.Length];
            var colors = new Color[verts.Length];
            var tris = new int[(rungs - 1) * 6];

            for (var i = 0; i < rungs; i++)
            {
                var t = i / (float)(rungs - 1);
                var y = -length * t;
                var taper = Mathf.Lerp(1f, 0.15f, t);
                var sway = (float)(rng.NextDouble() - 0.5) * radius * 0.25f * t;
                var width = radius * taper;

                verts[i * 2 + 0] = new Vector3(-width + sway, y, 0f);
                verts[i * 2 + 1] = new Vector3(width + sway, y, 0f);
                uvs[i * 2 + 0] = new Vector2(0f, t);
                uvs[i * 2 + 1] = new Vector2(1f, t);
                var alpha = Mathf.Lerp(0.95f, 0f, t * t);
                colors[i * 2 + 0] = new Color(1f, 1f, 1f, alpha);
                colors[i * 2 + 1] = new Color(1f, 1f, 1f, alpha);
            }

            var k = 0;
            for (var i = 0; i < rungs - 1; i++)
            {
                var a = i * 2;
                var b = a + 1;
                var c = a + 2;
                var d = a + 3;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
            }

            var mesh = new Mesh { name = "TheFloorIsLava_Drip" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Procedural cracked-basalt lava textures. Bakes two aligned 512²
    /// textures sharing the SAME crack pattern: a dark opaque "base" of crust
    /// plates and a black-background "glow" of molten cracks only. Together they
    /// build a layered material — solid mesh-color underneath, bright fissures
    /// added on top. Cracks come from a Voronoi cell field so the plates look
    /// polygonal (real cooled-lava plate structure), not soft ridges.</summary>
    private static class LavaTexture
    {
        private static Texture2D? _base;
        private static Texture2D? _glow;
        private static Texture2D? _drip;

        public static Texture2D Base() { BakeSurface(); return _base!; }
        public static Texture2D Glow() { BakeSurface(); return _glow!; }

        private static void BakeSurface()
        {
            if (_base != null && _glow != null) return;
            const int size = 512;

            _base = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "TheFloorIsLava_BaseTex",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            _glow = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "TheFloorIsLava_GlowTex",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };

            var crustVeryDark = new Color(0.020f, 0.018f, 0.015f);
            var crustDark = new Color(0.055f, 0.048f, 0.042f);
            var crustMid = new Color(0.130f, 0.105f, 0.085f);
            var crustHigh = new Color(0.220f, 0.180f, 0.140f);
            var glowDeep = new Color(0.70f, 0.18f, 0.04f);
            var glowMid = new Color(1.00f, 0.55f, 0.10f);
            var glowHot = new Color(1.00f, 0.92f, 0.40f);

            var basePixels = new Color[size * size];
            var glowPixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // PLATE STRUCTURE: Voronoi cells form irregular polygonal
                    // plates. cellEdge ~ 0 right on a crack between plates, ~1 in
                    // the interior of a plate. Two scales of cells stacked so we
                    // get both large plates and smaller subdivisions.
                    var bigCell = VoronoiEdge(x, y, 0.022f, 0);
                    var smallCell = VoronoiEdge(x, y, 0.060f, 71);
                    var plateInterior = bigCell * 0.7f + smallCell * 0.3f;

                    // Small ridged Perlin fissures add wispy secondary cracks
                    // running inside large plates.
                    var p = Mathf.PerlinNoise(x * 0.034f + 137f, y * 0.034f + 51f);
                    var ridge = 1f - Mathf.Abs(p * 2f - 1f);
                    ridge = Mathf.Pow(ridge, 9f);

                    // Combined "is this a crack?" signal. 1 = right on a crack,
                    // 0 = deep inside a plate.
                    var crackSignal = Mathf.Max(1f - plateInterior, ridge);

                    // Sharp threshold so crust and cracks separate cleanly.
                    var crackMask = Mathf.SmoothStep(0.42f, 0.58f, crackSignal);
                    var thinCrackMask = Mathf.SmoothStep(0.55f, 0.78f, crackSignal);

                    // Plate surface detail (subtle relief inside crust).
                    var dA = Mathf.PerlinNoise(x * 0.07f + 300f, y * 0.07f + 150f);
                    var dB = Mathf.PerlinNoise(x * 0.21f + 500f, y * 0.21f + 700f);
                    var dC = Mathf.PerlinNoise(x * 0.50f + 870f, y * 0.50f + 230f);
                    var crustDetail = dA * 0.55f + dB * 0.30f + dC * 0.15f;

                    // Slight banding/stratification for the layered crust look.
                    var bandPhase = y * 0.05f + Mathf.Sin(x * 0.018f + 1.3f) * 2.6f;
                    var band = Mathf.Sin(bandPhase) * 0.5f + 0.5f;
                    var bandShade = band * 0.45f + 0.55f;

                    // Per-plate colour variation: cells share a hue, so different
                    // plates read as distinct chunks of crust.
                    var plateId = Mathf.PerlinNoise(x * 0.006f + 1000f, y * 0.006f + 2000f);

                    var crustTone = Color.Lerp(crustDark, crustMid, crustDetail);
                    crustTone = Color.Lerp(crustTone, crustHigh, dB * 0.35f);
                    crustTone.r *= bandShade;
                    crustTone.g *= bandShade;
                    crustTone.b *= bandShade;
                    // Subtle warm/cool tint per plate.
                    crustTone.r += (plateId - 0.5f) * 0.04f;
                    crustTone.g += (plateId - 0.5f) * 0.025f;

                    // Cracks in the BASE are even darker — deep crevices between
                    // plates. This is what gives them visual depth even before
                    // the glow layer is applied.
                    var baseColor = Color.Lerp(crustTone, crustVeryDark, crackMask * 0.85f);

                    // Darken the top of every other band slightly (the "darker on
                    // most-top-surfaces with slight layers visible" requirement).
                    if (band > 0.78f)
                    {
                        var topShade = (band - 0.78f) / 0.22f * 0.25f;
                        baseColor.r *= 1f - topShade;
                        baseColor.g *= 1f - topShade;
                        baseColor.b *= 1f - topShade;
                    }

                    // Sparse warm embers only on the cooler crust (not on cracks).
                    var ember = Mathf.PerlinNoise(x * 0.09f + 800f, y * 0.09f + 900f);
                    if (ember > 0.78f && crackMask < 0.15f)
                    {
                        var hot = (ember - 0.78f) / 0.22f;
                        baseColor.r += hot * 0.22f;
                        baseColor.g += hot * 0.07f;
                    }
                    baseColor.a = 1f;

                    // Glow LAYER: black everywhere except along the cracks. The
                    // bright pixels here add to the base on top, producing the
                    // visible inner-glow molten fissures.
                    var glowVar = Mathf.PerlinNoise(x * 0.04f + 1100f, y * 0.04f + 1300f);
                    Color rawGlow;
                    if (glowVar < 0.45f)
                        rawGlow = Color.Lerp(glowDeep, glowMid, glowVar / 0.45f);
                    else
                        rawGlow = Color.Lerp(glowMid, glowHot, (glowVar - 0.45f) / 0.55f);

                    // Heat ramp along the crack: brightest right at the centre of
                    // a crack, dimmer towards the crust edge.
                    var heatCore = Mathf.SmoothStep(0.5f, 0.75f, crackSignal);
                    var glowColor = rawGlow * heatCore;
                    // Thin hottest highlight running along the crack centre.
                    glowColor.r += thinCrackMask * 0.25f;
                    glowColor.g += thinCrackMask * 0.18f;
                    glowColor.b += thinCrackMask * 0.05f;
                    glowColor.r = Mathf.Clamp01(glowColor.r);
                    glowColor.g = Mathf.Clamp01(glowColor.g);
                    glowColor.b = Mathf.Clamp01(glowColor.b);
                    glowColor.a = 1f;

                    var idx = y * size + x;
                    basePixels[idx] = baseColor;
                    glowPixels[idx] = glowColor;
                }
            }

            _base.SetPixels(basePixels);
            _base.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            _glow.SetPixels(glowPixels);
            _glow.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        }

        /// <summary>Worley/Voronoi F2-F1 distance: returns 1 deep inside a cell
        /// and falls to 0 along the boundary between cells. Used to mark the
        /// dark crack network around polygonal crust plates.</summary>
        private static float VoronoiEdge(int px, int py, float scale, int seedOffset)
        {
            var fx = px * scale;
            var fy = py * scale;
            var gx = Mathf.FloorToInt(fx);
            var gy = Mathf.FloorToInt(fy);
            var bestD = float.MaxValue;
            var secondD = float.MaxValue;
            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    var cx = gx + ox;
                    var cy = gy + oy;
                    var nx = Mathf.PerlinNoise(cx * 0.27f + seedOffset, cy * 0.27f + seedOffset * 2);
                    var ny = Mathf.PerlinNoise(cx * 0.31f + seedOffset * 3f, cy * 0.31f + seedOffset * 5f);
                    var dx = (cx + nx) - fx;
                    var dy = (cy + ny) - fy;
                    var d = dx * dx + dy * dy;
                    if (d < bestD) { secondD = bestD; bestD = d; }
                    else if (d < secondD) secondD = d;
                }
            }
            var edge = Mathf.Sqrt(secondD) - Mathf.Sqrt(bestD);
            return Mathf.Clamp01(edge * 3.2f);
        }

        public static Texture2D DripStrip()
        {
            if (_drip != null) return _drip;
            const int w = 32, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
            {
                name = "TheFloorIsLava_DripStrip",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var glowCore = new Color(1.00f, 0.85f, 0.30f);
            var glowFaint = new Color(0.55f, 0.12f, 0.03f);
            for (var y = 0; y < h; y++)
            {
                var t = y / (float)(h - 1);
                var heat = Color.Lerp(glowCore, glowFaint, t);
                for (var x = 0; x < w; x++)
                {
                    var u = x / (float)(w - 1);
                    var edge = 1f - Mathf.Abs(u - 0.5f) * 2f;
                    edge = Mathf.SmoothStep(0f, 1f, edge);
                    var mottle = Mathf.PerlinNoise(x * 0.3f, y * 0.18f) * 0.25f;
                    var c = new Color(
                        Mathf.Clamp01(heat.r - mottle),
                        Mathf.Clamp01(heat.g - mottle * 1.1f),
                        Mathf.Clamp01(heat.b - mottle * 0.9f),
                        edge * Mathf.Lerp(0.95f, 0f, t)
                    );
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            _drip = tex;
            return tex;
        }
    }
}
