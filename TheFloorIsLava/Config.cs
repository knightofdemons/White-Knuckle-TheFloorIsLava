using BepInEx.Configuration;

namespace TheFloorIsLava;

internal sealed class Config
{
    public ConfigEntry<float> MaxHealth { get; }
    public ConfigEntry<float> LavaDps { get; }
    public ConfigEntry<float> HealthRegen { get; }
    public ConfigEntry<float> HealthRegenDelay { get; }
    public ConfigEntry<float> GripRegen { get; }

    public ConfigEntry<float> MaxTiltDegrees { get; }
    public ConfigEntry<float> LavaSpacing { get; }
    public ConfigEntry<int> MaxZones { get; }
    public ConfigEntry<float> WorldScanInterval { get; }
    public ConfigEntry<float> WorldScanHorizRange { get; }
    public ConfigEntry<float> WorldScanVertRange { get; }
    public ConfigEntry<float> TouchDistance { get; }
    public ConfigEntry<float> VerticalAbove { get; }
    public ConfigEntry<float> VerticalBelow { get; }
    public ConfigEntry<bool> ShowVisuals { get; }
    public ConfigEntry<float> VisualScale { get; }
    public ConfigEntry<float> DensityMin { get; }
    public ConfigEntry<float> NoiseScale { get; }
    public ConfigEntry<float> LavaHeightVariance { get; }
    public ConfigEntry<float> LavaShapeJitter { get; }
    public ConfigEntry<float> LavaScaleJitter { get; }
    public ConfigEntry<float> LavaEmissionStrength { get; }
    public ConfigEntry<bool> LavaDrips { get; }
    public ConfigEntry<float> ThinFloorThreshold { get; }
    public ConfigEntry<float> RenderDistance { get; }
    public ConfigEntry<float> SurfaceGridStep { get; }
    public ConfigEntry<float> SurfaceInfluenceRadius { get; }
    public ConfigEntry<float> TextureWorldScale { get; }
    public ConfigEntry<float> LODUpdateInterval { get; }

    public ConfigEntry<bool> EasterEggEnabled { get; }
    public ConfigEntry<float> EasterEggHoldDuration { get; }
    public ConfigEntry<float> EasterEggLookDownThreshold { get; }
    public ConfigEntry<float> EasterEggAnimDuration { get; }

    public ConfigEntry<bool> FanningEnabled { get; }
    public ConfigEntry<int> FanningActivationCycles { get; }
    public ConfigEntry<float> FanningCoolPerCycleSingle { get; }
    public ConfigEntry<float> FanningCoolPerCycleBoth { get; }
    public ConfigEntry<float> FanningFullCoolStaminaFraction { get; }
    public ConfigEntry<float> FanningSpotRadius { get; }
    public ConfigEntry<float> FanningSpotMergeRadius { get; }
    public ConfigEntry<int> FanningSpotMaxCount { get; }
    public ConfigEntry<float> FanningHeatRecoveryDelay { get; }
    public ConfigEntry<float> FanningHeatRecoveryTime { get; }
    public ConfigEntry<float> FanningBothHandsSyncWindow { get; }
    public ConfigEntry<float> FanningSpotLookupPadding { get; }
    public ConfigEntry<float> FanningSpotLookupMergeFactor { get; }
    public ConfigEntry<float> FanningFullyCooledThreshold { get; }
    public ConfigEntry<float> FanningRecentCoolWindow { get; }

    public ConfigEntry<bool> DebugLog { get; }

    public Config(ConfigFile file)
    {
        MaxHealth = file.Bind("Balance", "MaxHealth", 100f, "Extra health maximum.");
        LavaDps = file.Bind("Balance", "LavaDamagePerSecond", 7.5f, "Extra health lost per second on hot lava.");
        HealthRegen = file.Bind("Balance", "HealthRegenPerSecond", 10f, "Extra health healed per second after regen starts.");
        HealthRegenDelay = file.Bind("Balance", "HealthRegenDelay", 2f, "Seconds without lava damage before HP regen.");
        GripRegen = file.Bind("Balance", "GripRegenPerSecond", 0.4f, "Hand stamina regen per second while on lava.");

        MaxTiltDegrees = file.Bind("Lava", "MaxTiltDegrees", 20f, "Max upward floor tilt in degrees.");
        LavaSpacing = file.Bind("Lava", "Spacing", 1.6f, "Distance between lava zone centers.");
        MaxZones = file.Bind("Lava", "MaxZones", 12000, "Maximum lava zones at once.");
        WorldScanInterval = file.Bind("Lava", "WorldScanInterval", 1.0f, "Seconds between world scans.");
        WorldScanHorizRange = file.Bind("Lava", "WorldScanHorizRange", 80f, "Horizontal scan range around player.");
        WorldScanVertRange = file.Bind("Lava", "WorldScanVertRange", 60f, "Vertical scan range around player.");
        TouchDistance = file.Bind("Lava", "TouchDistance", 1.4f, "Horizontal leniency for standing on lava.");
        VerticalAbove = file.Bind("Lava", "VerticalAbove", 0.45f, "Max feet height above a zone.");
        VerticalBelow = file.Bind("Lava", "VerticalBelow", 0.25f, "Max feet height below a zone.");
        ShowVisuals = file.Bind("Lava", "ShowVisuals", true, "Show lava meshes.");
        VisualScale = file.Bind("Lava", "VisualScale", 1.0f, "Lava zone radius multiplier.");
        DensityMin = file.Bind("Lava", "DensityMin", 0.32f, "Noise floor for patch coverage [0..1].");
        NoiseScale = file.Bind("Lava", "NoiseScale", 0.18f, "Density noise frequency.");
        LavaHeightVariance = file.Bind("Lava", "LavaHeightVariance", 0.05f, "Max mesh dome height in meters.");
        LavaShapeJitter = file.Bind("Lava", "LavaShapeJitter", 0.4f, "Patch edge irregularity [0..1].");
        LavaScaleJitter = file.Bind("Lava", "LavaScaleJitter", 0.25f, "Per-zone radius variation fraction.");
        LavaEmissionStrength = file.Bind("Lava", "LavaEmissionStrength", 0.6f, "Glow brightness multiplier.");
        LavaDrips = file.Bind("Lava", "LavaDrips", true, "Drips under thin floors.");
        ThinFloorThreshold = file.Bind("Lava", "ThinFloorThreshold", 0.6f, "Collider height below which drips apply.");
        RenderDistance = file.Bind("Performance", "RenderDistance", 35f, "Hide lava meshes beyond this range.");
        SurfaceGridStep = file.Bind("Performance", "SurfaceGridStep", 0.45f, "Merged surface mesh grid step.");
        SurfaceInfluenceRadius = file.Bind("Lava", "SurfaceInfluenceRadius", 1.1f, "Zone merge radius on surface mesh.");
        TextureWorldScale = file.Bind("Lava", "TextureWorldScale", 0.45f, "World-space lava texture scale.");
        LODUpdateInterval = file.Bind("Performance", "LODUpdateInterval", 0.5f, "Seconds between render culling passes.");

        EasterEggEnabled = file.Bind("EasterEgg", "Enabled", true, "Enable optional hidden content.");
        EasterEggHoldDuration = file.Bind("EasterEgg", "HoldDuration", 1.0f, "Seconds to hold the gesture.");
        EasterEggLookDownThreshold = file.Bind("EasterEgg", "LookDownThreshold", -0.90f, "Camera forward.y must be below this.");
        EasterEggAnimDuration = file.Bind("EasterEgg", "AnimDuration", 1.0f, "Rip animation length in seconds.");

        FanningEnabled = file.Bind("Fanning", "Enabled", true, "Enable lava fanning.");
        FanningActivationCycles = file.Bind("Fanning", "ActivationCycles", 2, "Warmup grab cycles before cooling.");
        FanningCoolPerCycleSingle = file.Bind("Fanning", "CoolPerCycleSingleHand", 0.18f, "Heat removed per single-hand cycle.");
        FanningCoolPerCycleBoth = file.Bind("Fanning", "CoolPerCycleBothHands", 0.30f, "Heat removed per both-hands cycle.");
        FanningFullCoolStaminaFraction = file.Bind("Fanning", "FullCoolStaminaFraction", 0.8f, "Grip budget fraction to fully cool a spot.");
        FanningSpotRadius = file.Bind("Fanning", "SpotRadius", 0.5f, "Cooled spot radius in meters.");
        FanningSpotMergeRadius = file.Bind("Fanning", "SpotMergeRadius", 1.2f, "Distance to merge cooled spots.");
        FanningSpotMaxCount = file.Bind("Fanning", "SpotMaxCount", 40, "Max cooled spots tracked.");
        FanningHeatRecoveryDelay = file.Bind("Fanning", "HeatRecoveryDelay", 3f, "Delay before a fully cooled spot reheats.");
        FanningHeatRecoveryTime = file.Bind("Fanning", "HeatRecoveryTime", 4f, "Seconds to reheat from cold to hot.");
        FanningBothHandsSyncWindow = file.Bind("Fanning", "BothHandsSyncWindow", 0.35f, "Dual-hand sync window in seconds.");
        FanningSpotLookupPadding = file.Bind("Fanning", "SpotLookupPadding", 0.55f, "Extra radius when testing spot overlap.");
        FanningSpotLookupMergeFactor = file.Bind("Fanning", "SpotLookupMergeFactor", 0.85f, "Merge-radius factor for spot lookup.");
        FanningFullyCooledThreshold = file.Bind("Fanning", "FullyCooledThreshold", 0.01f, "Heat at or below this counts as fully cooled.");
        FanningRecentCoolWindow = file.Bind("Fanning", "RecentCoolWindow", 0.35f, "Seconds after cooling before reheat starts.");

        DebugLog = file.Bind("Debug", "LogStatus", true, "Write status lines to BepInEx log every second.");
    }
}
