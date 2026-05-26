# 🔥 The Floor Is Lava

> **⚠️ AI disclosure:** A **significant part** of this mod was written with help from **[Cursor AI](https://cursor.com)** (code, debugging, docs). **Download** and use it with that in mind.

<sub>**Personal note:** I'm not a C# or Unity developer. I used AI because I couldn't realistically build and debug this on my own in the time I had. If you want to make your own "floor is lava" mod for White Knuckle — different code, different balance, whatever — that's fine by me.</sub>

**Author:** [knighToFdemonS](https://github.com/knighToFdemonS)  
**Game:** [White Knuckle](https://store.steampowered.com/app/3195790/White_Knuckle/) (Steam) · **Version:** 1.12.1  
**Repo:** https://github.com/knightofdemons/White-Knuckle-TheFloorIsLava

BepInEx mod. Upward-facing floors get lava patches. Lava drains an **extra health bar**. You can **fan** lava to cool small floor spots.

---

## Features

| | |
|---|---|
| 🌋 **Lava** | Lava on upward floors, landings, and similar surfaces — not on handholds or props |
| ❤️ **Extra health** | Separate health pool + bar at the **bottom center** of the screen |
| 🤲 **Grip** | Lava does **not** drain vanilla hand stamina; grip **regenerates** while you stand on lava |
| 💨 **Fanning** | Press/release grab on lava to cool a fixed spot; stand on cooled ground to avoid damage |
| ⚙️ **Config** | Most balance, visuals, fanning, and performance settings in one `.cfg` file |

---

## Requirements

- **White Knuckle** (Steam)
- **[BepInEx 5](https://docs.bepinex.dev/)** in your game folder

---

## Installation

1. Copy **`plugins/TheFloorIsLava.dll`** to:
   ```
   <White Knuckle>/BepInEx/plugins/TheFloorIsLava.dll
   ```
2. Optional: copy **`config/com.thefloorislava.whiteknuckle.cfg`** to:
   ```
   BepInEx/config/com.thefloorislava.whiteknuckle.cfg
   ```
   If you skip this, BepInEx creates a config on first run.
3. Start the game once.
4. Remove old **`SpikeBalanceMod.dll`** from `BepInEx/plugins/` if it is still there.

---

## How to play

### Run start

- The mod starts when a **campaign run** begins.
- An **extra health bar** appears at the **bottom center** of the screen.
- It tracks **extra health only**, not vanilla health or grip.

### Lava damage

- Glowing patches mark lava on **horizontal / upward** surfaces.
- Stand on lava → **extra health** goes down over time.
- **Hand stamina is not drained** by lava contact.
- Walls, handholds, and beams you climb are unchanged unless they count as upward floor.

### Stay alive

- **Grip regen:** On lava, hand stamina slowly refills.
- **HP regen:** After a short time **without lava damage**, extra health refills. Works on cooled fan spots too (no damage = regen can start).
- **Death:** Extra health at **0** ends the run through the normal death flow.

### Fanning (cool spots)

While standing on lava:

1. **Press and release grab** (LMB / RMB) in a fan motion.
2. The first **`ActivationCycles`** (default **2**) only warm up — no cooling yet.
3. After that, each cycle **costs grip** and **cools a fixed spot** where you fan.
4. On a **cooled** spot, lava damage is reduced or stops.
5. If you stop fanning, the spot **heats up again** after a delay.

**Both hands:** Fan with **left and right grab within the sync window**. Each hand pays **less grip per cycle** than one-hand fanning, and the spot cools **faster**.

---

## Config

Edit **`BepInEx/config/com.thefloorislava.whiteknuckle.cfg`** with the game **closed**, then restart.

### `[Balance]`

| Key | What it does |
|-----|----------------|
| `MaxHealth` | Extra health maximum |
| `LavaDamagePerSecond` | Extra health lost per second on hot lava |
| `HealthRegenPerSecond` | Extra health healed per second after regen starts |
| `HealthRegenDelay` | Seconds without lava damage before HP regen |
| `GripRegenPerSecond` | Hand stamina regen per second while on lava |

### `[Lava]`

| Key | What it does |
|-----|----------------|
| `ShowVisuals` | Show or hide lava meshes |
| `VisualScale` | Patch size multiplier |
| `DensityMin` | How much floor is bare vs covered (0 = full cover, 1 = almost none) |
| `LavaEmissionStrength` | Glow brightness |
| `LavaDrips` | Drips under thin floors (`false` = off) |
| `MaxTiltDegrees` | Max floor slope that still gets lava |
| `TouchDistance` | Horizontal leniency for “standing on lava” |
| `VerticalAbove` / `VerticalBelow` | Vertical hitbox above/below your feet |
| `Spacing` | Distance between lava zone centers |
| `MaxZones` | Max lava zones at once |
| `WorldScanInterval` | Seconds between scans for new geometry |
| `WorldScanHorizRange` / `WorldScanVertRange` | Scan range around the player |

### `[Fanning]`

| Key | What it does |
|-----|----------------|
| `Enabled` | Turn fanning on/off |
| `ActivationCycles` | Warmup cycles before cooling starts |
| `FullCoolStaminaFraction` | Total grip budget to fully cool one spot (fraction of one hand’s max grip) |
| `CoolPerCycleSingleHand` / `CoolPerCycleBothHands` | Heat removed per fan cycle |
| `BothHandsSyncWindow` | Max seconds between both hands for a dual-hand cycle |
| `SpotRadius` | Radius of a cooled spot |
| `SpotMergeRadius` | Distance to merge new cooling into an old spot |
| `SpotMaxCount` | Max cooled spots tracked |
| `HeatRecoveryDelay` | Wait after full cool before spot reheats |
| `HeatRecoveryTime` | Time for a cold spot to become hot again |

### `[Performance]`

| Key | What it does |
|-----|----------------|
| `RenderDistance` | Hide lava meshes beyond this range (damage still works) |
| `SurfaceGridStep` | Mesh detail (higher = lighter, rougher) |
| `LODUpdateInterval` | How often distance culling runs |

Lower-end PCs: raise `RenderDistance` cutoff (e.g. `25`), `SurfaceGridStep` (e.g. `0.6`), and `LODUpdateInterval` (e.g. `1.0`). Lower `MaxZones` if needed.

### `[Debug]`

| Key | What it does |
|-----|----------------|
| `LogStatus` | Write lava/fan state to `BepInEx/LogOutput.log` every second |

### `[EasterEgg]`

| Key | What it does |
|-----|----------------|
| `Enabled` | Toggle optional hidden content |

---

## Field note

Maintenance logs mention climbers who **could not bear the warning light** above the molten deck.

> *Bow to the glow. Grip the rail with both hands as if your life depended on it — hold steady. Some say the extra health bar tears loose. Some say the heat stops counting.*

`[EasterEgg] Enabled` only turns this on or off. It does not explain the gesture.

---

## Build from source

See the [GitHub repository](https://github.com/knightofdemons/White-Knuckle-TheFloorIsLava) for source and build steps.

---

## Disclaimer

This is an **unofficial fan mod** for White Knuckle. It is **not** affiliated with or endorsed by the game's developers.

Provided free and open source. **No warranty.** Use at your own risk.

You may use, copy, modify, and redistribute this project. **White Knuckle** and related assets belong to their owners.
