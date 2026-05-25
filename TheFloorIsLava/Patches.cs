using HarmonyLib;

namespace TheFloorIsLava;

[HarmonyPatch(typeof(ENT_Player), "Start")]
internal static class PlayerStartPatch
{
    static void Postfix() => Plugin.Instance?.NotifyPlayerSpawned();
}

/// <summary>Best-effort hook: trigger an extra scan when a room loads. Not required for
/// generation to keep working — the continuous world scan will find new rooms anyway.</summary>
[HarmonyPatch(typeof(HandholdManager), nameof(HandholdManager.LoadHandholds))]
internal static class RoomLoadedPatch
{
    static void Postfix()
    {
        if (!ModState.RunActive)
            return;
        Plugin.Instance?.NotifyRoomLoaded();
    }
}

[HarmonyPatch(typeof(ENT_Player), "DamageGripStrength")]
internal static class PlayerGripDamagePatch
{
    static bool Prefix() => !ModState.OnLavaZone;
}

internal static class HarmonySetup
{
    private static readonly System.Type HandType = AccessTools.Inner(typeof(ENT_Player), "Hand")!;

    public static void Apply(Harmony h)
    {
        h.PatchAll(typeof(Plugin).Assembly);

        h.Patch(
            AccessTools.Method(typeof(ENT_Player), "HandStamina")!,
            prefix: new HarmonyMethod(typeof(HandStaminaPatch), nameof(HandStaminaPatch.Prefix)));

        h.Patch(
            AccessTools.Method(HandType, "DamageGripStrength")!,
            prefix: new HarmonyMethod(typeof(HandGripPatch), nameof(HandGripPatch.Prefix)));

        h.Patch(
            AccessTools.Method(HandType, "MinGripStrength")!,
            prefix: new HarmonyMethod(typeof(HandGripPatch), nameof(HandGripPatch.Prefix)));
    }
}

internal static class HandStaminaPatch
{
    public static bool Prefix() => !ModState.OnLavaZone;
}

internal static class HandGripPatch
{
    public static bool Prefix() => !ModState.OnLavaZone;
}
