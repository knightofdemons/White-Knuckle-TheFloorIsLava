using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TheFloorIsLava;

internal static class GameRefs
{
    private static readonly FieldInfo IsGrounded = AccessTools.Field(typeof(ENT_Player), "isGrounded")!;
    private static readonly FieldInfo InfiniteStamina = AccessTools.Field(typeof(ENT_Player), "infiniteStamina")!;
    private static readonly FieldInfo Hands = AccessTools.Field(typeof(ENT_Player), "hands")!;
    private static readonly Type HandType = AccessTools.Inner(typeof(ENT_Player), "Hand")!;
    private static readonly FieldInfo Grip = AccessTools.Field(HandType, "gripStrength")!;
    private static readonly MethodInfo AddGrip = AccessTools.Method(HandType, "AddGripStrength")!;
    private static readonly MethodInfo? IsHoldingM = AccessTools.Method(HandType, "IsHolding");
    private static readonly FieldInfo? FireButtonF = AccessTools.Field(HandType, "fireButton");
    private static readonly FieldInfo? HandModelF = AccessTools.Field(HandType, "handModel");
    private static readonly MethodInfo? GetGripMaxM = AccessTools.Method(HandType, "GetGripStrengthMax");

    public static CL_GameManager? GameManager => CL_GameManager.gMan;

    public static ENT_Player? Player()
    {
        var gm = GameManager;
        var local = gm?.localPlayer;
        if (local != null && local.gameObject.activeInHierarchy)
            return local;

        foreach (var candidate in UnityEngine.Object.FindObjectsOfType<ENT_Player>())
        {
            if (candidate != null && candidate.gameObject.activeInHierarchy && InRun(candidate))
                return candidate;
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

    public static bool RunEndedCheck() => CL_GameManager.runHasEnded;

    public static bool InRun(ENT_Player player)
    {
        if (player == null)
            return false;
        if (CL_GameManager.IsLoading())
            return false;
        if (RunEndedCheck())
            return false;
        if (IsMenu(player.gameObject.scene.name))
            return false;

        var gm = GameManager;
        if (gm?.localPlayer != null && gm.localPlayer != player)
            return false;

        return true;
    }

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

    public static bool KillPlayer(string reason = "Lava")
    {
        var p = Player();
        if (p == null)
        {
            Plugin.LogInfo("KillPlayer: no player found.");
            return false;
        }

        try
        {
            p.Kill(reason);
            return true;
        }
        catch (Exception e)
        {
            Plugin.LogInfo($"KillPlayer: failed — {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    public static bool IsCrouching(ENT_Player player) => player.IsCrouching();

    public static Vector3 LookForward(ENT_Player player)
    {
        var cam = Camera.main;
        if (cam != null)
            return cam.transform.forward;

        return player.GetCameraTargetRotation() * Vector3.forward;
    }

    public static bool BothHandsGrabbing(ENT_Player player)
    {
        try
        {
            if (Input.GetMouseButton(0) && Input.GetMouseButton(1))
                return true;
        }
        catch { /* ignore */ }

        if (Hands.GetValue(player) is not Array arr || arr.Length < 2)
            return false;
        var h0 = arr.GetValue(0);
        var h1 = arr.GetValue(1);
        if (h0 == null || h1 == null)
            return false;

        if (IsHandFireButtonPressed(h0) && IsHandFireButtonPressed(h1))
            return true;

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
        if (FireButtonF == null)
            return false;
        try
        {
            if (FireButtonF.GetValue(hand) is not string btn || string.IsNullOrEmpty(btn))
                return false;
            try { return Input.GetButton(btn); }
            catch { return false; }
        }
        catch { return false; }
    }

    public static bool TryGetHand(ENT_Player player, int index, out object? hand)
    {
        hand = null;
        if (Hands.GetValue(player) is not Array arr || index < 0 || index >= arr.Length)
            return false;
        hand = arr.GetValue(index);
        return hand != null;
    }

    public static Transform? GetHandModel(ENT_Player player, int index)
    {
        if (!TryGetHand(player, index, out var hand) || hand == null || HandModelF == null)
            return null;
        try { return HandModelF.GetValue(hand) as Transform; }
        catch { return null; }
    }

    public static float GetGripStrength(object hand)
    {
        try { return (float)Grip.GetValue(hand)!; }
        catch { return 0f; }
    }

    public static float GetGripStrengthMax(object hand)
    {
        if (GetGripMaxM == null)
            return 100f;
        try { return (float)GetGripMaxM.Invoke(hand, null)!; }
        catch { return 100f; }
    }

    public static bool DamageGripOnHand(object hand, float amount)
    {
        if (amount <= 0f)
            return true;
        try
        {
            var cur = (float)Grip.GetValue(hand)!;
            if (cur < amount)
                return false;
            Grip.SetValue(hand, cur - amount);
            return true;
        }
        catch { return false; }
    }

    public static void AddGripToHand(object hand, float amount)
    {
        if (amount <= 0f)
            return;
        try
        {
            var cur = (float)Grip.GetValue(hand)!;
            var max = GetGripStrengthMax(hand);
            Grip.SetValue(hand, Mathf.Min(max, cur + amount));
        }
        catch { /* ignore */ }
    }

    public static bool IsHandGrabHeld(ENT_Player player, int index)
    {
        if (!TryGetHand(player, index, out var hand) || hand == null)
            return false;
        return IsHandGrabHeld(hand);
    }

    public static bool IsHandGrabHeld(object hand) => IsHandFireButtonPressed(hand);

    public static bool IsHandGrabHeld(object hand, int index)
    {
        if (IsHandFireButtonPressed(hand))
            return true;
        try
        {
            if (index == 0 && Input.GetMouseButton(0))
                return true;
            if (index == 1 && Input.GetMouseButton(1))
                return true;
        }
        catch { /* ignore */ }
        return false;
    }

    public static bool ForceEndRun()
    {
        CL_GameManager.runHasEnded = true;
        return true;
    }
}
