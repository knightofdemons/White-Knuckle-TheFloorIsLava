using UnityEngine;

namespace TheFloorIsLava;

internal sealed class EasterEgg
{
    private readonly Config _cfg;

    private float _conditionTime;
    private bool _triggered;
    private float _animTime;
    private bool _propSpawned;
    private GameObject? _droppedProp;

    public EasterEgg(Config cfg) => _cfg = cfg;

    public bool Triggered => _triggered;
    public bool Done => _propSpawned;

    public void Reset()
    {
        _conditionTime = 0f;
        _triggered = false;
        _animTime = 0f;
        _propSpawned = false;
        if (_droppedProp != null)
        {
            Object.Destroy(_droppedProp);
            _droppedProp = null;
        }
    }

    public void Tick(ENT_Player player, float dt, HealthBar ui)
    {
        if (_propSpawned) return;
        if (player == null) { _conditionTime = 0f; return; }

        if (_triggered)
        {
            _animTime += dt;
            UpdateRipAnimation(_animTime, ui);
            if (_animTime >= _cfg.EasterEggAnimDuration.Value)
                SpawnDroppedHpBar(player, ui);
            return;
        }

        var look = GameRefs.LookForward(player);
        var lookDown = look.y < _cfg.EasterEggLookDownThreshold.Value;
        var crouch = GameRefs.IsCrouching(player);
        var grab = GameRefs.BothHandsGrabbing(player);
        var all = lookDown && crouch && grab;

        if (all)
        {
            _conditionTime += dt;
            if (_conditionTime >= _cfg.EasterEggHoldDuration.Value && !_triggered)
            {
                _triggered = true;
                _animTime = 0f;
                Plugin.LogInfo("EasterEgg: gesture complete");
            }
        }
        else
            _conditionTime = 0f;
    }

    // Where the bar travels to during the rip — roughly between the visible
    // hands in the lower-third of a 1920x1080 reference canvas, i.e. where the
    // tablet finally ends up in 3D.
    private const float HandsAnchorY = 320f;

    /// <summary>
    /// 1.0-second three-phase rip animation driven from <see cref="HealthBar"/>'s
    /// container RectTransform + CanvasGroup. The bar travels UP from its
    /// resting position at the bottom of the screen into the player's hands
    /// before fading and being replaced by the 3D prop.
    /// </summary>
    private static void UpdateRipAnimation(float t, HealthBar ui)
    {
        var rect = ui.Container;
        var grp = ui.Group;
        if (rect == null || grp == null) return;

        // Phase 1 (0.00 - 0.30s): both "hands grip" — bar shakes in place.
        if (t < 0.30f)
        {
            var shake = Mathf.Sin(t * 70f) * 4f;
            var bob = Mathf.Sin(t * 90f + 1.3f) * 1.6f;
            rect.anchoredPosition = new Vector2(shake, 16f + bob);
            rect.localScale = new Vector3(1f + Mathf.Sin(t * 50f) * 0.04f, 1f + Mathf.Sin(t * 50f + 0.6f) * 0.06f, 1f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 55f) * 1.2f);
            grp.alpha = 1f;
            return;
        }

        // Phase 2 (0.30 - 0.78s): the hands pull — bar travels UP toward the
        // player's hand area and stretches as it's torn out of the UI.
        if (t < 0.78f)
        {
            var p = (t - 0.30f) / 0.48f;
            var eased = Mathf.SmoothStep(0f, 1f, p);
            rect.localScale = new Vector3(1f + eased * 0.40f, 1f + eased * 0.70f, 1f);
            rect.anchoredPosition = new Vector2(
                Mathf.Sin(p * 25f) * 3f,
                Mathf.Lerp(16f, HandsAnchorY, eased));
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(p * 18f) * 2.5f);
            grp.alpha = Mathf.Lerp(1f, 0.9f, eased);
            return;
        }

        // Phase 3 (0.78 - 1.00s): rip complete — bar stays at hand level, snaps
        // shut, and fades out as the 3D prop materializes in its place.
        {
            var p = (t - 0.78f) / 0.22f;
            var eased = Mathf.SmoothStep(0f, 1f, p);
            rect.localScale = new Vector3(1.40f * (1f - eased), 1.70f * (1f - eased), 1f);
            rect.anchoredPosition = new Vector2(0f, HandsAnchorY + eased * 6f);
            grp.alpha = Mathf.Lerp(0.9f, 0f, eased);
        }
    }

    private void SpawnDroppedHpBar(ENT_Player player, HealthBar ui)
    {
        _propSpawned = true;
        ui.Hide();
        ui.ResetTransform();

        // Spawn at the camera's forward vector. Camera + forward is reliably
        // "where the player is looking" and guaranteed to be visible.
        var cam = Camera.main;
        Vector3 camPos, camFwd;
        if (cam != null) { camPos = cam.transform.position; camFwd = cam.transform.forward; }
        else { camPos = player.transform.position + Vector3.up * 1.6f; camFwd = player.transform.forward; }

        var horiz = camFwd; horiz.y = 0f;
        if (horiz.sqrMagnitude < 0.01f) horiz = Vector3.forward; else horiz.Normalize();
        var pos = camPos + horiz * 0.7f + Vector3.up * -0.15f;
        var rot = Quaternion.LookRotation(horiz, Vector3.up);

        Plugin.LogInfo($"EasterEgg: spawning HP-bar prop. spawnPos={pos}");

        // Preferred path: clone an existing CL_Prop so the hand-grab pipeline
        // picks it up like a box. Falls back to a free physics object if the
        // clone path fails for any reason.
        _droppedProp = TryCloneCLProp(pos, rot, player) ?? CreateFreeProp(pos, rot, player);
        if (_droppedProp == null)
        {
            Plugin.LogInfo("EasterEgg: !! prop spawn returned NULL — nothing visible !!");
            return;
        }

        var rends = _droppedProp.GetComponentsInChildren<MeshRenderer>(true);
        var firstMr = rends.Length > 0 ? rends[0] : null;
        var hasProp = _droppedProp.GetComponent<CL_Prop>() != null;
        Plugin.LogInfo(
            $"EasterEgg: HP-bar tablet spawned as '{_droppedProp.name}' " +
            $"pos={_droppedProp.transform.position} active={_droppedProp.activeInHierarchy} " +
            $"grabbable={hasProp} renderers={rends.Length} " +
            $"firstMatShader='{firstMr?.sharedMaterial?.shader?.name}'. Lava damage is now disabled.");
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Clone an existing <see cref="CL_Prop"/> so the HP-bar tablet inherits
    /// the game's hand-grab/throw pipeline (Rigidbody, colliders, CL_Prop
    /// component, audio, save/load, etc).
    ///
    /// The trick: <see cref="GameEntity.Start"/> runs
    /// <c>CL_GameManager.replacementScopes[i].ShouldReplace(this, true)</c>
    /// against every entity that starts up. An earlier naive clone got
    /// matched and immediately replaced/destroyed by that system, which is
    /// why the tablet vanished. To avoid that, we instantiate the prop into
    /// a deactivated parent (so its <c>Start()</c> doesn't run yet), give it
    /// a unique <c>entityPrefabID</c> that no replacement scope is going to
    /// match, set <c>canInteract=true</c>, swap the visual children, then
    /// re-parent and activate. From that point on it lives like any other
    /// grabbable prop.
    /// </summary>
    private static GameObject? TryCloneCLProp(Vector3 pos, Quaternion rot, ENT_Player player)
    {
        CL_Prop? template = null;
        foreach (var p in Object.FindObjectsOfType<CL_Prop>())
        {
            if (p != null && p.gameObject != null && p.gameObject.activeInHierarchy)
            {
                template = p;
                break;
            }
        }
        if (template == null)
        {
            Plugin.LogInfo("EasterEgg: no CL_Prop in scene to clone — falling back to free prop.");
            return null;
        }

        GameObject? inactiveParent = null;
        GameObject? clone = null;

        try
        {
            // Deactivated parent so the clone is born inactive — Start() will
            // wait for activation.
            inactiveParent = new GameObject("_HpBarPropInit");
            inactiveParent.SetActive(false);

            clone = Object.Instantiate(template.gameObject, inactiveParent.transform, false);
            clone.name = "TheFloorIsLava_HpBarProp";

            // Override CL_Prop / GameEntity fields BEFORE Start() runs.
            var cp = clone.GetComponent<CL_Prop>();
            if (cp != null)
            {
                cp.entityPrefabID = "TheFloorIsLava_HpBarProp_v1"; // unique → no replacement match
                cp.canInteract = true;                              // must be true or hands won't grab
                cp.stuck = false;
                cp.hideOnKill = true;
                cp.canSave = false;                                 // don't persist into save data
                cp.holdDistance = 0.55f;
                cp.holdOffset = Vector3.zero;
                cp.buoyancy = 0.5f;
                cp.addForceMult = 1f;
                cp.minDamageHitImpulse = 9999f; // never breaks
                cp.health = 99999f;
                cp.maxHealth = 99999f;
                cp.objectType = "TheFloorIsLava_HpBar";
            }

            // Disable cloned visuals (we replace with our own HP-bar look).
            foreach (var mr in clone.GetComponentsInChildren<MeshRenderer>(true))
                mr.enabled = false;
            foreach (var sr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sr.enabled = false;

            // Resize the prop's colliders to the HP-bar tablet shape so the
            // hand raycast hits something the right size. Disable any mesh
            // collider whose geometry we can't easily resize.
            var size = new Vector3(TabletLength, 0.10f, 0.15f);
            var hasBox = false;
            foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger) continue; // leave triggers alone
                if (col is BoxCollider box)
                {
                    box.size = size;
                    box.center = Vector3.zero;
                    box.enabled = true;
                    hasBox = true;
                }
                else if (col is MeshCollider mc)
                {
                    mc.enabled = false;
                }
            }
            if (!hasBox)
            {
                var box = clone.AddComponent<BoxCollider>();
                box.size = size;
            }

            // Attach our visible HP-bar tablet as a child.
            var visual = BuildHpBarVisual();
            visual.transform.SetParent(clone.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Re-parent out, position, then activate — Start() runs here.
            clone.transform.SetParent(null, false);
            clone.transform.position = pos;
            clone.transform.rotation = rot;
            clone.transform.localScale = Vector3.one;
            clone.SetActive(true);

            // Reset and throw the rigidbody (which Initialize() also points to).
            var rb = clone.GetComponent<Rigidbody>() ?? clone.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.mass = Mathf.Max(0.4f, rb.mass);
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                ApplyThrowImpulse(rb, player);
            }

            Object.Destroy(inactiveParent);
            Plugin.LogInfo($"EasterEgg: cloned CL_Prop template '{template.name}' as grabbable HP-bar tablet.");
            return clone;
        }
        catch (System.Exception e)
        {
            Plugin.LogInfo($"EasterEgg: CL_Prop clone path failed ({e.GetType().Name}: {e.Message}). " +
                           "Falling back to free physics prop.");
            if (clone != null) Object.Destroy(clone);
            if (inactiveParent != null) Object.Destroy(inactiveParent);
            return null;
        }
    }

    /// <summary>Spawn a fresh physics object with the HP-bar visual. Sized so
    /// it can't tunnel through floors, with continuous collision detection.
    /// Throws gently forward + up so it lands in front of the player.</summary>
    private static GameObject CreateFreeProp(Vector3 pos, Quaternion rot, ENT_Player player)
    {
        var go = BuildHpBarVisual();
        go.name = "TheFloorIsLava_HpBarProp";
        go.transform.position = pos;
        go.transform.rotation = rot;

        // Collider sized to the visible tablet's world dimensions. Slightly
        // oversized in Y/Z to give a stable collider that won't tunnel during
        // the throw and lets the player grab/kick it like a solid object.
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(TabletLength, 0.10f, 0.15f);
        col.center = Vector3.zero;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.6f;
        rb.drag = 0.4f;
        rb.angularDrag = 0.6f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        ApplyThrowImpulse(rb, player);
        return go;
    }

    private static void ApplyThrowImpulse(Rigidbody rb, ENT_Player player)
    {
        var fwd = player.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        // Gentle toss: ~1.3 m/s forward, ~0.7 m/s up at our mass. Not enough
        // velocity to tunnel through a wall, but the player will see it pop
        // forward out of the screen and tumble naturally.
        rb.AddForce(fwd * 0.5f + Vector3.up * 0.25f, ForceMode.Impulse);
        rb.AddTorque(new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f),
            Random.Range(-0.2f, 0.2f)), ForceMode.Impulse);
    }

    // Tablet dimensions — 3x longer than the original 0.45 m, with a slightly
    // taller "border" so the bar reads as a clearly framed health bar instead
    // of a flat black tile when viewed from any angle.
    private const float TabletLength = 1.50f;
    private const float TabletThickness = 0.05f;
    private const float TabletDepth = 0.12f;

    /// <summary>Builds the HP-bar tablet as two solid-colored emissive cubes:
    /// a white outer "border" slab and a slightly-taller red inner "fill"
    /// that sticks out top and bottom. From above/below (the natural view
    /// once the tablet lands on the floor) it reads as a red bar inside a
    /// white frame. The red fill peeks past the white slab in Y so the bar's
    /// red shows on the long faces too. Uses the same alpha-tested URP/Unlit
    /// material recipe that the lava patches use (proven to render on this
    /// build) plus emission so the bar is visible in any scene lighting.</summary>
    private static GameObject BuildHpBarVisual()
    {
        var root = new GameObject("HpBarTablet");

        // Outer white "border" slab.
        var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(border.GetComponent<Collider>());
        border.name = "Border";
        border.transform.SetParent(root.transform, false);
        border.transform.localScale = new Vector3(TabletLength, TabletThickness, TabletDepth);
        border.transform.localPosition = Vector3.zero;
        ConfigureRenderer(border.GetComponent<MeshRenderer>(),
            BuildEmissiveSolid(new Color(0.96f, 0.96f, 0.96f, 1f), 1.0f, "Border"));

        // Red "fill" — slightly larger in Y (so it pokes above/below the
        // white slab, showing red on the long faces), slightly smaller in
        // X+Z (so the white border is visible around it from top/bottom).
        var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(fill.GetComponent<Collider>());
        fill.name = "Fill";
        fill.transform.SetParent(root.transform, false);
        fill.transform.localScale = new Vector3(
            TabletLength - 0.06f,
            TabletThickness + 0.02f,
            TabletDepth - 0.03f);
        fill.transform.localPosition = Vector3.zero;
        ConfigureRenderer(fill.GetComponent<MeshRenderer>(),
            BuildEmissiveSolid(new Color(0.95f, 0.13f, 0.10f, 1f), 2.0f, "Fill"));

        return root;
    }

    private static void ConfigureRenderer(MeshRenderer mr, Material mat)
    {
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        mr.sharedMaterial = mat;
    }

    /// <summary>Solid-color emissive material that uses the same alpha-tested
    /// URP/Unlit recipe as the lava base material (the proven-working variant
    /// in this URP build). No texture — the colour is baked into _BaseColor
    /// and into _EmissionColor so the cube is self-illuminated and visible
    /// regardless of how dark the scene is.</summary>
    private static Material BuildEmissiveSolid(Color color, float emissiveStrength, string nameSuffix)
    {
        var shaderName = "";
        Shader? shader = null;
        foreach (var n in new[]
                 {
                     "Universal Render Pipeline/Unlit",
                     "Universal Render Pipeline/Lit",
                     "Standard",
                 })
        {
            shader = Shader.Find(n);
            if (shader != null) { shaderName = n; break; }
        }
        if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

        var mat = new Material(shader!) { name = $"TheFloorIsLava_HpBar{nameSuffix}" };
        mat.mainTexture = null;

        // Force-set every colour slot the shader might expose so nothing falls
        // through to a default black value.
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        // Self-illumination so the bar is visible in any lighting.
        var emission = new Color(color.r * emissiveStrength, color.g * emissiveStrength,
            color.b * emissiveStrength, 1f);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        // Match the lava base material EXACTLY: alpha-tested URP/Unlit opaque.
        // Even though we have no texture, this is the shader variant that this
        // build actually compiles/renders — the plain "Opaque" variant
        // (_AlphaClip=0, no _ALPHATEST_ON keyword) seems to be stripped and
        // falls back to black. Since _BaseColor.a is 1, every fragment passes
        // the alpha cutoff, so the cube renders as a solid emissive colour.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 2f);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.05f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

        Plugin.LogInfo($"EasterEgg: {nameSuffix} material — shader='{shaderName}' " +
                       $"color={color} emission={emission}");

        return mat;
    }
}
