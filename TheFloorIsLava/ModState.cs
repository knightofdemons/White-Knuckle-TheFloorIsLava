namespace TheFloorIsLava;

/// <summary>Global flags shared by the plugin and Harmony patches.</summary>
internal static class ModState
{
    /// <summary>Single source of truth for whether a modded run is active.</summary>
    public static bool RunActive;
    public static bool OnLavaZone;
    public static float LavaHeatMultiplier = 1f;
}
