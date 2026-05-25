namespace TheFloorIsLava;

internal static class ModState
{
    public static bool RunActive;
    public static bool OnLavaZone;
    /// <summary>Lava damage multiplier [0 = no damage, 1 = full DPS]. Set by FanningSystem.</summary>
    public static float LavaHeatMultiplier = 1f;
}
