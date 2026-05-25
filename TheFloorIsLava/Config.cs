using BepInEx.Configuration;

namespace TheFloorIsLava;

internal sealed class Config
{
    // ---- Player resource pool ----
    public ConfigEntry<float> MaxHealth { get; }
    public ConfigEntry<float> LavaDps { get; }
    public ConfigEntry<float> HealthRegen { get; }
    public ConfigEntry<float> HealthRegenDelay { get; }
    public ConfigEntry<float> GripRegen { get; }

    // ---- Lava placement ----
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

    // ---- Easter egg ----
    public ConfigEntry<bool> EasterEggEnabled { get; }

    // ---- Lava fanning / cooling ----
    public ConfigEntry<bool> FanningEnabled { get; }
    public ConfigEntry<int> FanningActivationCycles { get; }
    public ConfigEntry<float> FanningCostPerCycleSingle { get; }
    public ConfigEntry<float> FanningCostPerCycleBoth { get; }
    public ConfigEntry<float> FanningCoolPerCycleSingle { get; }
    public ConfigEntry<float> FanningCoolPerCycleBoth { get; }
    public ConfigEntry<float> FanningFullCoolStaminaFraction { get; }
    public ConfigEntry<float> FanningSpotRadius { get; }
    public ConfigEntry<float> FanningSpotMergeRadius { get; }
    public ConfigEntry<int> FanningSpotMaxCount { get; }
    public ConfigEntry<float> FanningHeatRecoveryDelay { get; }
    public ConfigEntry<float> FanningHeatRecoveryTime { get; }
    public ConfigEntry<float> FanningBothHandsSyncWindow { get; }

    // ---- Misc ----
    public ConfigEntry<bool> DebugLog { get; }

    public Config(ConfigFile file)
    {
        MaxHealth = file.Bind("Balance", "MaxHealth", 100f,
            "Maximum mod HP. The bar fills to this value at run start and after healing.");
        LavaDps = file.Bind("Balance", "LavaDamagePerSecond", 7.5f,
            "Mod HP lost per second while standing on lava.");
        HealthRegen = file.Bind("Balance", "HealthRegenPerSecond", 10f,
            "Mod HP regenerated per second once the regen delay has elapsed.");
        HealthRegenDelay = file.Bind("Balance", "HealthRegenDelay", 2f,
            "Seconds without taking lava damage before HP starts regenerating.");
        GripRegen = file.Bind("Balance", "GripRegenPerSecond", 0.4f,
            "Hand grip strength regenerated per second while standing on lava.");

        MaxTiltDegrees = file.Bind("Lava", "MaxTiltDegrees", 20f,
            "Max surface tilt from horizontal (degrees) that still counts as 'upward-facing'.");
        LavaSpacing = file.Bind("Lava", "Spacing", 1.6f,
            "Distance between lava zone centers in the surface grid.");
        MaxZones = file.Bind("Lava", "MaxZones", 12000,
            "Maximum number of lava zones present at once.");
        WorldScanInterval = file.Bind("Lava", "WorldScanInterval", 1.0f,
            "Seconds between continuous world scans for newly loaded geometry.");
        WorldScanHorizRange = file.Bind("Lava", "WorldScanHorizRange", 80f,
            "Max horizontal distance from the player to scan for new floors.");
        WorldScanVertRange = file.Bind("Lava", "WorldScanVertRange", 60f,
            "Max vertical distance from the player to scan (up & down).");
        TouchDistance = file.Bind("Lava", "TouchDistance", 1.4f,
            "Extra horizontal distance to a zone center for it to count as 'on lava'.");
        VerticalAbove = file.Bind("Lava", "VerticalAbove", 0.45f,
            "Max feet height above a zone to still count as standing on it.");
        VerticalBelow = file.Bind("Lava", "VerticalBelow", 0.25f,
            "Max feet height below a zone to still count as touching it.");
        ShowVisuals = file.Bind("Lava", "ShowVisuals", true,
            "Show glowing lava patches on every floor zone.");
        VisualScale = file.Bind("Lava", "VisualScale", 1.0f,
            "Master scale multiplier for the lava patches.");
        DensityMin = file.Bind("Lava", "DensityMin", 0.32f,
            "Perlin-noise density floor [0..1]. Surface cells with noise below this stay bare, creating patches with no lava. 0 = solid coverage, 1 = no lava.");
        NoiseScale = file.Bind("Lava", "NoiseScale", 0.18f,
            "Spatial frequency of the density noise. Smaller = larger patches; larger = finer mottling.");
        LavaHeightVariance = file.Bind("Lava", "LavaHeightVariance", 0.05f,
            "Maximum dome height of a single lava patch (meters). Keep small for flat hardened-lava look.");
        LavaShapeJitter = file.Bind("Lava", "LavaShapeJitter", 0.4f,
            "How irregular the patch outlines are [0..1]. Wobble is inward-only so patches never overhang ledges.");
        LavaScaleJitter = file.Bind("Lava", "LavaScaleJitter", 0.25f,
            "Random per-patch size variation as a fraction of the base size.");
        LavaEmissionStrength = file.Bind("Lava", "LavaEmissionStrength", 0.6f,
            "HDR brightness multiplier on the emissive channel. Most of the patch is dark crust so this only boosts the cracks.");
        LavaDrips = file.Bind("Lava", "LavaDrips", true,
            "If true, lava on thin floors (beams, ledges) hangs down a small drip through the underside.");
        ThinFloorThreshold = file.Bind("Lava", "ThinFloorThreshold", 0.6f,
            "A supporting collider is considered 'thin' (drips-eligible) if its vertical extent is below this many meters.");
        RenderDistance = file.Bind("Performance", "RenderDistance", 35f,
            "Lava surfaces farther than this from the player have their renderers disabled. Damage still applies.");
        SurfaceGridStep = file.Bind("Performance", "SurfaceGridStep", 0.45f,
            "Grid cell size of the merged lava surface mesh (meters). Smaller = smoother edges but more triangles.");
        SurfaceInfluenceRadius = file.Bind("Lava", "SurfaceInfluenceRadius", 1.1f,
            "How far each lava zone spreads its coverage across the merged surface mesh. Larger = more merging between neighbours.");
        TextureWorldScale = file.Bind("Lava", "TextureWorldScale", 0.45f,
            "World-space texture density. 0.45 = the lava texture repeats roughly every 2.2 meters. Lower = bigger pattern.");
        LODUpdateInterval = file.Bind("Performance", "LODUpdateInterval", 0.5f,
            "Seconds between far-surface culling passes. Higher = less per-frame overhead, slower pop-in.");

        EasterEggEnabled = file.Bind("EasterEgg", "Enabled", true,
            "Enable optional hidden content.");

        FanningEnabled = file.Bind("Fanning", "Enabled", true,
            "Allow fanning lava by repeatedly opening/closing grab buttons while standing on lava.");
        FanningActivationCycles = file.Bind("Fanning", "ActivationCycles", 2,
            "Completed grab press-release cycles required before fanning starts cooling (first cycles only warm up).");
        FanningCostPerCycleSingle = file.Bind("Fanning", "GripCostPerCycleSingleHand", 1.25f,
            "Grip stamina drained per completed fan cycle when only one hand is fanning.");
        FanningCostPerCycleBoth = file.Bind("Fanning", "GripCostPerCycleBothHands", 0.75f,
            "Grip stamina drained per hand per cycle when both hands fan within the sync window.");
        FanningCoolPerCycleSingle = file.Bind("Fanning", "CoolPerCycleSingleHand", 0.18f,
            "Heat removed from the lava spot per single-hand fan cycle [0..1].");
        FanningCoolPerCycleBoth = file.Bind("Fanning", "CoolPerCycleBothHands", 0.30f,
            "Heat removed from the lava spot per both-hands fan cycle [0..1].");
        FanningFullCoolStaminaFraction = file.Bind("Fanning", "FullCoolStaminaFraction", 0.8f,
            "Fraction of one hand's max grip stamina spent in total to fully cool a spot (heat 1→0). Per-cycle cost is derived from this and CoolPerCycle*.");
        FanningSpotRadius = file.Bind("Fanning", "SpotRadius", 0.5f,
            "Radius of each cooled lava spot in meters (~1 m diameter).");
        FanningSpotMergeRadius = file.Bind("Fanning", "SpotMergeRadius", 1.2f,
            "If a new cool action lands within this distance of an existing spot, merge into it.");
        FanningSpotMaxCount = file.Bind("Fanning", "SpotMaxCount", 40,
            "Maximum cooled spots tracked at once. Oldest fully-healed spots are removed first.");
        FanningHeatRecoveryDelay = file.Bind("Fanning", "HeatRecoveryDelay", 3f,
            "Seconds after fully cooling a spot (heat=0) before it begins heating back up.");
        FanningHeatRecoveryTime = file.Bind("Fanning", "HeatRecoveryTime", 4f,
            "Seconds for a spot to heat from 0 back to full damage once recovery has started.");
        FanningBothHandsSyncWindow = file.Bind("Fanning", "BothHandsSyncWindow", 0.35f,
            "If both hands complete a grab cycle within this many seconds, count as a both-hands fan.");

        DebugLog = file.Bind("Debug", "LogStatus", true,
            "Log lava state and damage diagnostics every second.");
    }
}
