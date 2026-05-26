using HarmonyLib;

namespace TheFloorIsLava;

[HarmonyPatch(typeof(ENT_Player), "Start")]
internal static class PlayerStartPatch
{
    static void Postfix(ENT_Player __instance)
    {
        var plugin = Plugin.Instance;
        if (plugin == null)
            return;

        if (ModState.RunActive)
        {
            Plugin.LogInfo("New player spawned while old run was still active — force-stopping previous run.");
            plugin.StopRun();
        }

        plugin.TryStartRun(__instance);
    }
}

[HarmonyPatch(typeof(M_Gamemode), nameof(M_Gamemode.Initialize))]
internal static class GamemodeInitPatch
{
    static void Postfix() => ModdedRunGuard.DisableOfficialScoring();
}

[HarmonyPatch(typeof(CL_GameManager), nameof(CL_GameManager.SetGamemode))]
internal static class GamemodeSetPatch
{
    static void Postfix() => ModdedRunGuard.DisableOfficialScoring();
}

[HarmonyPatch(typeof(CL_GameManager), nameof(CL_GameManager.RestartScene))]
internal static class GamemodeRestartPatch
{
    static void Postfix() => ModdedRunGuard.DisableOfficialScoring();
}

[HarmonyPatch(typeof(CL_GameManager), nameof(CL_GameManager.ChangeState))]
internal static class GameStateChangePatch
{
    static void Postfix(string s)
    {
        if (s == "die" || s == "win")
            Plugin.Instance?.StopRun();
    }
}

[HarmonyPatch(typeof(HandholdManager), nameof(HandholdManager.LoadHandholds))]
internal static class RoomLoadedPatch
{
    static void Postfix()
    {
        if (ModState.RunActive)
        {
            Plugin.Instance?.NotifyRoomLoaded();
            return;
        }

        Plugin.Instance?.TryStartRun();
    }
}

[HarmonyPatch(typeof(ENT_Player), "DamageGripStrength")]
internal static class PlayerGripDamagePatch
{
    static bool Prefix() => !ModState.OnLavaZone;
}

[HarmonyPatch(typeof(ENT_Player), "HandStamina")]
internal static class HandStaminaPatch
{
    static bool Prefix() => !ModState.OnLavaZone;
}

[HarmonyPatch(typeof(ENT_Player.Hand), "DamageGripStrength")]
internal static class HandGripDamagePatch
{
    static bool Prefix() => !ModState.OnLavaZone;
}

[HarmonyPatch(typeof(ENT_Player.Hand), "MinGripStrength")]
internal static class HandMinGripPatch
{
    static bool Prefix() => !ModState.OnLavaZone;
}

internal static class HarmonySetup
{
    public static void Apply(Harmony h) => h.PatchAll(typeof(Plugin).Assembly);
}

internal static class ModdedRunGuard
{
    internal static void DisableOfficialScoring()
    {
        if (CL_GameManager.gamemode.allowLeaderboardScoring)
            CL_GameManager.gamemode.allowLeaderboardScoring = false;

        var gm = CL_GameManager.gMan;
        if (gm != null && gm.allowScores)
            gm.allowScores = false;
    }
}
