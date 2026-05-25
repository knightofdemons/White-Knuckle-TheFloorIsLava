using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TheFloorIsLava;

internal static class GameRefs
{
    private static readonly FieldInfo RunEnded = AccessTools.Field(typeof(CL_GameManager), "runHasEnded")!;
    private static readonly FieldInfo LocalPlayer = AccessTools.Field(typeof(CL_GameManager), "localPlayer")!;
    private static readonly FieldInfo IsGrounded = AccessTools.Field(typeof(ENT_Player), "isGrounded")!;
    private static readonly FieldInfo Hands = AccessTools.Field(typeof(ENT_Player), "hands")!;
    private static readonly Type HandType = AccessTools.Inner(typeof(ENT_Player), "Hand")!;
    private static readonly FieldInfo Grip = AccessTools.Field(HandType, "gripStrength")!;
    private static readonly FieldInfo InfiniteStamina = AccessTools.Field(typeof(ENT_Player), "infiniteStamina")!;
    private static readonly MethodInfo AddGrip = AccessTools.Method(HandType, "AddGripStrength")!;
    private static readonly MethodInfo? KillString =
        AccessTools.Method(typeof(ENT_Player), "Kill", new[] { typeof(string) });
    private static readonly MethodInfo? IsCrouchingM = AccessTools.Method(typeof(ENT_Player), "IsCrouching");
    private static readonly FieldInfo? MainCamTarget = AccessTools.Field(typeof(ENT_Player), "mainCamTarget");
    private static readonly MethodInfo? GetCameraTargetRotationM = AccessTools.Method(typeof(ENT_Player), "GetCameraTargetRotation");
    private static readonly MethodInfo? IsHoldingM = AccessTools.Method(HandType, "IsHolding");
    private static readonly MethodInfo? IsHangingM = AccessTools.Method(HandType, "IsHanging");
    private static readonly FieldInfo? FireButtonF = AccessTools.Field(HandType, "fireButton");
    private static readonly FieldInfo? HandModelF = AccessTools.Field(HandType, "handModel");
    private static readonly MethodInfo? GetGripMaxM = AccessTools.Method(HandType, "GetGripStrengthMax");

    public static ENT_Player? Player()
    {
        var gm = UnityEngine.Object.FindObjectOfType<CL_GameManager>();
        if (gm != null)
        {
            var p = LocalPlayer.GetValue(gm) as ENT_Player;
            if (p != null && p.gameObject.activeInHierarchy)
                return p;
        }

        foreach (var p in UnityEngine.Object.FindObjectsOfType<ENT_Player>())
        {
            if (p != null && p.gameObject.activeInHierarchy && !IsMenu(p.gameObject.scene.name))
                return p;
        }

        return null;
    }

    public static bool IsMenu(string scene)
    {
        if (string.IsNullOrEmpty(scene))
            return true;
        var n = scene.ToLowerInvariant();
        return n.Contains("menu") || n.Contains("title") || n.Contains("bootstrap");
    }

    public static bool RunEndedCheck()
    {
        var gm = UnityEngine.Object.FindObjectOfType<CL_GameManager>();
        return gm != null && (bool)RunEnded.GetValue(gm)!;
    }

    public static bool InRun(ENT_Player player) =>
        !IsMenu(player.gameObject.scene.name) && !RunEndedCheck();

    public static Vector3 BodyPosition(ENT_Player player) => player.transform.position;

    public static Vector3 FeetPosition(ENT_Player player)
    {
        var body = player.transform.position;
        var up = Vector3.up * 0.3f;
        if (Physics.Raycast(body + up, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.05f;
        return body + Vector3.down * ((bool)IsGrounded.GetValue(player)! ? 1.5f : 0.8f);
    }

    /// <summary>
    /// Y coordinate for lava vertical hitbox checks. Unlike FeetPosition(), this does
    /// not snap to the floor below when the player is airborne over lava.
    /// </summary>
    public static float LavaVerticalCheckY(ENT_Player player)
    {
        var body = player.transform.position;
        if ((bool)IsGrounded.GetValue(player)!)
        {
            if (Physics.Raycast(body + Vector3.up * 0.3f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y + 0.05f;
        }

        return body.y - 0.9f;
    }

    public static void SetInfiniteStamina(ENT_Player player, bool on) =>
        InfiniteStamina.SetValue(player, on);

    public static bool GetInfiniteStamina(ENT_Player player) => (bool)InfiniteStamina.GetValue(player)!;

    public static void AddGripToAllHands(ENT_Player player, float amount)
    {
        if (amount <= 0f || Hands.GetValue(player) is not Array arr)
            return;
        foreach (var hand in arr)
        {
            if (hand != null)
                AddGrip.Invoke(hand, new object[] { amount });
        }
    }

    public static float Grip0(ENT_Player player)
    {
        if (Hands.GetValue(player) is not Array arr || arr.Length == 0 || arr.GetValue(0) == null)
            return -1f;
        return (float)Grip.GetValue(arr.GetValue(0)!)!;
    }

    /// <summary>Kill the player via the game's own death pipeline.</summary>
    public static bool KillPlayer(string reason = "Lava")
    {
        var p = Player();
        if (p == null)
        {
            Plugin.LogInfo("KillPlayer: no player found.");
            return false;
        }
        if (KillString == null)
        {
            Plugin.LogInfo("KillPlayer: ENT_Player.Kill(string) not found.");
            return false;
        }
        try
        {
            KillString.Invoke(p, new object[] { reason });
            return true;
        }
        catch (Exception e)
        {
            Plugin.LogInfo($"KillPlayer: invoke failed — {e.GetType().Name}: {e.Message}\n{e.InnerException?.Message}");
            return false;
        }
    }

    public static bool IsCrouching(ENT_Player player)
    {
        if (IsCrouchingM == null) return false;
        try { return (bool)IsCrouchingM.Invoke(player, null)!; }
        catch { return false; }
    }

    /// <summary>Forward direction the player is actually looking. Tries the
    /// real rendering camera first (most reliable — its transform reflects
    /// head pitch), then ENT_Player.GetCameraTargetRotation(), then the
    /// mainCamTarget transform, then finally the player body.</summary>
    public static Vector3 LookForward(ENT_Player player)
    {
        var cam = Camera.main;
        if (cam != null)
            return cam.transform.forward;

        if (GetCameraTargetRotationM != null)
        {
            try
            {
                var rot = (Quaternion)GetCameraTargetRotationM.Invoke(player, null)!;
                return rot * Vector3.forward;
            }
            catch { /* fall through */ }
        }

        if (MainCamTarget?.GetValue(player) is Transform target)
            return target.forward;
        return player.transform.forward;
    }

    /// <summary>True when both hands are actively gripping a handhold (the
    /// state required to "grab" the easter-egg HP bar).</summary>
    public static bool BothHandsHolding(ENT_Player player)
    {
        if (IsHoldingM == null) return false;
        if (Hands.GetValue(player) is not Array arr || arr.Length < 2) return false;
        var h0 = arr.GetValue(0);
        var h1 = arr.GetValue(1);
        if (h0 == null || h1 == null) return false;
        try
        {
            var holding0 = (bool)IsHoldingM.Invoke(h0, null)!;
            var holding1 = (bool)IsHoldingM.Invoke(h1, null)!;
            return holding0 && holding1;
        }
        catch { return false; }
    }

    /// <summary>True when both grab BUTTONS are pressed (regardless of whether
    /// the hand actually catches a handhold). Accepts:
    ///   - Both mouse buttons (LMB + RMB) simultaneously, OR
    ///   - Both hands' configured Input axes (via Hand.fireButton) firing, OR
    ///   - Both hands actually attached to a handhold (IsHolding true).
    /// Any of these counts so the gesture works on whichever input the player
    /// has bound.</summary>
    public static bool BothHandsGrabbing(ENT_Player player)
    {
        // 1. Default mouse-button setup. This matches the standard binding in
        // White Knuckle and the vast majority of FPS controls.
        try
        {
            if (Input.GetMouseButton(0) && Input.GetMouseButton(1))
                return true;
        }
        catch { /* ignore */ }

        if (Hands.GetValue(player) is not Array arr || arr.Length < 2) return false;
        var h0 = arr.GetValue(0);
        var h1 = arr.GetValue(1);
        if (h0 == null || h1 == null) return false;

        // 2. Hand.fireButton axes.
        if (IsHandFireButtonPressed(h0) && IsHandFireButtonPressed(h1))
            return true;

        // 3. Already-actually-gripping fallback (works whatever the input).
        if (IsHoldingM != null)
        {
            try
            {
                if ((bool)IsHoldingM.Invoke(h0, null)! && (bool)IsHoldingM.Invoke(h1, null)!)
                    return true;
            }
            catch { /* ignore */ }
        }

        return false;
    }

    private static bool IsHandFireButtonPressed(object hand)
    {
        if (FireButtonF == null) return false;
        try
        {
            if (FireButtonF.GetValue(hand) is not string btn || string.IsNullOrEmpty(btn))
                return false;
            try { return Input.GetButton(btn); }
            catch { return false; }
        }
        catch { return false; }
    }

    /// <summary>Return hand object at index (0=left, 1=right in typical setup).</summary>
    public static bool TryGetHand(ENT_Player player, int index, out object? hand)
    {
        hand = null;
        if (Hands.GetValue(player) is not Array arr || index < 0 || index >= arr.Length)
            return false;
        hand = arr.GetValue(index);
        return hand != null;
    }

    /// <summary>World transform for a hand model (used for gust VFX placement).</summary>
    public static Transform? GetHandModel(ENT_Player player, int index)
    {
        if (!TryGetHand(player, index, out var hand) || hand == null || HandModelF == null)
            return null;
        try { return HandModelF.GetValue(hand) as Transform; }
        catch { return null; }
    }

    /// <summary>Read current grip strength on a hand.</summary>
    public static float GetGripStrength(object hand)
    {
        try { return (float)Grip.GetValue(hand)!; }
        catch { return 0f; }
    }

    public static float GetGripStrengthMax(object hand)
    {
        if (GetGripMaxM == null) return 100f;
        try { return (float)GetGripMaxM.Invoke(hand, null)!; }
        catch { return 100f; }
    }

    /// <summary>
    /// Drain grip stamina on a hand. Uses direct field write because Harmony
    /// blocks Hand.DamageGripStrength while standing on lava (by design for
    /// vanilla drain isolation) — fanning is mod-initiated drain.
    /// </summary>
    public static bool DamageGripOnHand(object hand, float amount)
    {
        if (amount <= 0f) return true;
        try
        {
            var cur = (float)Grip.GetValue(hand)!;
            if (cur < amount) return false;
            Grip.SetValue(hand, cur - amount);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Is the hand's grab button held this frame?</summary>
    public static bool IsHandGrabHeld(ENT_Player player, int index)
    {
        if (!TryGetHand(player, index, out var hand) || hand == null)
            return false;
        return IsHandGrabHeld(hand);
    }

    /// <summary>Is this hand object's grab button held?</summary>
    public static bool IsHandGrabHeld(object hand) => IsHandFireButtonPressed(hand);

    /// <summary>Grab held, with mouse-button fallback (LMB/RMB) per hand index.</summary>
    public static bool IsHandGrabHeld(object hand, int index)
    {
        if (IsHandFireButtonPressed(hand))
            return true;
        try
        {
            if (index == 0 && Input.GetMouseButton(0)) return true;
            if (index == 1 && Input.GetMouseButton(1)) return true;
        }
        catch { /* ignore */ }
        return false;
    }

    /// <summary>One-shot diagnostic line dump so we can see what the easter-egg
    /// detectors are actually reading at runtime. Called from EasterEgg the
    /// first time it ticks during a run.</summary>
    public static string DiagnoseEasterEgg(ENT_Player player)
    {
        var lines = new System.Collections.Generic.List<string>();
        var camMain = Camera.main;
        lines.Add($"Camera.main={(camMain == null ? "<null>" : camMain.name)} " +
                  $"fwd={(camMain == null ? Vector3.zero : camMain.transform.forward)}");

        Quaternion? camRot = null;
        if (GetCameraTargetRotationM != null)
        {
            try { camRot = (Quaternion)GetCameraTargetRotationM.Invoke(player, null)!; }
            catch { /* ignore */ }
        }
        lines.Add($"GetCameraTargetRotation={(camRot.HasValue ? (camRot.Value * Vector3.forward).ToString("F2") : "<null>")}");

        if (MainCamTarget?.GetValue(player) is Transform mct)
            lines.Add($"mainCamTarget.fwd={mct.forward:F2}");
        else
            lines.Add("mainCamTarget=<null>");

        if (Hands.GetValue(player) is Array arr)
        {
            for (var i = 0; i < arr.Length; i++)
            {
                var h = arr.GetValue(i);
                if (h == null) { lines.Add($"hand[{i}]=<null>"); continue; }
                string btn = "<missing>";
                try { btn = (FireButtonF?.GetValue(h) as string) ?? "<null>"; } catch { }
                lines.Add($"hand[{i}].fireButton='{btn}'");
            }
        }
        else lines.Add("hands=<no-array>");
        return string.Join(" | ", lines);
    }

    /// <summary>Force the game manager to flag the run as ended (used as a backstop
    /// when KillPlayer doesn't immediately stop the run).</summary>
    public static bool ForceEndRun()
    {
        var gm = UnityEngine.Object.FindObjectOfType<CL_GameManager>();
        if (gm == null)
            return false;
        try
        {
            RunEnded.SetValue(gm, true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
