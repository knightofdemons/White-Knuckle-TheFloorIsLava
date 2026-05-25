# 🔥 The Floor Is Lava

> *A BepInEx mod for [**White Knuckle**](https://store.steampowered.com/app/3195790/White_Knuckle/) — the floors remember. The heat does not forgive.*

**Author:** [knighToFdemonS](https://github.com/knighToFdemonS)  
**Development support:** [Cursor AI](https://cursor.com)  
**Latest release:** [`release/`](release/) · **Source:** [`source/`](source/) · **Dev project:** [`TheFloorIsLava/`](TheFloorIsLava/)

---

## 🏗️ What this mod does

The facility’s upward-facing surfaces — floors, landings, maintenance grates — slowly **harden into molten lava**. You climb with your hands, but the ground wants you back.

| System | What you get |
|--------|----------------|
| 🌋 **Lava fields** | Procedural lava on level geometry (not props or handholds) |
| ❤️ **Mod HP bar** | Separate health pool + top-center UI — lava drains *this*, not vanilla grip |
| 🤲 **Grip isolation** | Hand stamina is blocked from passive lava drain; regen while standing in heat |
| 💨 **Fanning** | Press/release grab on lava to cool fixed spots on the floor |
| 🎭 **Hidden lore** | Something stirs if you read the signs the way the building intended *(see below)* |

---

## 📦 Requirements

- **White Knuckle** (Steam)
- **[BepInEx 5](https://docs.bepinex.dev/)** installed in the game folder

---

## ⚙️ Installation

1. Download **`TheFloorIsLava.dll`** from [`release/TheFloorIsLava.dll`](release/TheFloorIsLava.dll).
2. Copy it to:
   ```
   <White Knuckle>/BepInEx/plugins/TheFloorIsLava.dll
   ```
3. Launch the game **once** — BepInEx creates the config at:
   ```
   BepInEx/config/com.thefloorislava.whiteknuckle.cfg
   ```
4. Delete any legacy **`SpikeBalanceMod.dll`** from `BepInEx/plugins/` if you still have it from an old install.

Optional: use [`release/com.thefloorislava.whiteknuckle.cfg`](release/com.thefloorislava.whiteknuckle.cfg) as a reference template (your existing config is kept on upgrade).

---

## 🎮 How to play

### Starting a run

The mod activates when a **campaign run** begins. You’ll see a **health bar at the top center** of the screen. It fills at run start and tracks **mod HP only**.

### Lava damage

- Glowing **lava patches** mark dangerous floor. Stand on them → **mod HP** ticks down.
- **Vanilla grip / hand stamina** is *not* drained by lava contact (Harmony isolation).
- Climb handholds and beams as usual — only **horizontal surfaces facing up** carry lava.

### Surviving the heat

- **Grip regen:** While on lava, your hands slowly recover stamina (`GripRegenPerSecond`).
- **HP regen:** Stop taking lava damage for a few seconds → mod HP refills (`HealthRegenDelay`, then `HealthRegenPerSecond`). This also works while standing on a **fully cooled** fan patch.
- **Cooled spots:** On lava, repeatedly **press and release grab** (LMB/RMB) to **fan** the floor:
  1. First cycles **warm up** the technique (`ActivationCycles`, default **2**).
  2. Then each cycle costs grip and cools a **fixed grey patch** where you fan.
  3. Stand on a cooled patch → **reduced or zero** lava damage.
  4. Stop fanning → the patch **reheats** and lava returns.

Fan with **both hands** in sync for faster cooling (higher stamina cost spread across both).

### Death

When mod HP hits **0**, the run ends through the game’s death pipeline — same as a bad fall, but slower and glowing.

---

## 🔧 Config guide

Edit **`BepInEx/config/com.thefloorislava.whiteknuckle.cfg`** while the game is **closed**, then restart.

### ⚖️ Balance — `[Balance]`

| Key | What it does | Example tweak |
|-----|----------------|---------------|
| `MaxHealth` | Mod HP pool size | `150` for easier runs |
| `LavaDamagePerSecond` | DPS on uncooled lava | `7.5` default · `15` is harsh |
| `HealthRegenPerSecond` | HP healed per second after delay | Raise for forgiving runs |
| `HealthRegenDelay` | Seconds without lava damage before regen | `2` default |
| `GripRegenPerSecond` | Hand stamina regen while on lava | `0.4` default |

### 🌋 Lava look & behaviour — `[Lava]`

| Key | What it does | Example tweak |
|-----|----------------|---------------|
| `LavaDrips` | Hanging drips under thin beams/ledges | **`LavaDrips = false`** to remove drips |
| `ShowVisuals` | Toggle lava meshes entirely | `false` for invisible damage zones |
| `VisualScale` | Size of lava patches | Lower = subtler |
| `DensityMin` | Patch coverage (0 = solid, 1 = bare) | `0.32` = more gaps between patches |
| `LavaEmissionStrength` | Glow brightness | Lower if too bright |
| `MaxTiltDegrees` | Max floor slope that gets lava | `20` default |
| `TouchDistance` | Horizontal “on lava” leniency | Smaller = must stand closer |
| `VerticalAbove` / `VerticalBelow` | Vertical damage hitbox height | Lower = must be closer to the surface |

### 💨 Fanning — `[Fanning]`

| Key | What it does |
|-----|----------------|
| `Enabled` | Master toggle for fanning |
| `ActivationCycles` | Warmup grab cycles before cooling starts |
| `FullCoolStaminaFraction` | Total grip cost to fully cool a spot (fraction of one hand’s max grip) |
| `CoolPerCycleSingleHand` / `CoolPerCycleBothHands` | How fast spots cool per fan cycle |
| `HeatRecoveryDelay` | Pause before a **fully** cooled spot begins reheating |
| `HeatRecoveryTime` | Seconds to go from cold → full lava again |

### 🚀 Performance — `[Performance]`

Lower-end PCs: start here.

| Key | What it does | Performance tip |
|-----|----------------|-----------------|
| `RenderDistance` | Disable lava **renderers** beyond this range (damage still works) | Try **`25`** or **`20`** |
| `SurfaceGridStep` | Mesh density — smaller = prettier, heavier | Try **`0.6`** or **`0.75`** |
| `LODUpdateInterval` | How often culling runs | **`1.0`** = less CPU |
| `MaxZones` | Cap on lava zone count | Lower if memory spikes |
| `WorldScanInterval` | How often new rooms are scanned | **`2`** = less scan overhead |

Also in `[Lava]`: reduce `WorldScanHorizRange` / `WorldScanVertRange` if you rarely need far-ahead lava generation.

### 🐛 Debug — `[Debug]`

| Key | What it does |
|-----|----------------|
| `LogStatus` | Writes lava/fanning state to `BepInEx/LogOutput.log` every second |

---

## 📜 Field note *(classified)*

Maintenance logs mention climbers who **could not bear the warning light** above the molten deck.

> *Bow to the glow. Grip the rail with both hands as if your life depended on it — then release what the facility hung around your neck. Some say the heat accepts the offering. Some say the bar never hits the floor.*

The `[EasterEgg]` section only toggles whether that old superstition can still occur. It does not explain the ritual.

---

## 🛠️ Building from source

```powershell
cd TheFloorIsLava
dotnet build TheFloorIsLava.csproj -c Release
```

Output: `bin/Release/TheFloorIsLava.dll`

Set `GameRoot` in the `.csproj` if your White Knuckle install path differs from the default.

---

## ⚠️ Disclaimer

This project is provided **free of charge** as open source.

- You may **use, copy, modify, and redistribute** this code and compiled releases.
- **No warranty** is offered; use at your own risk.
- The authors are **not liable** for any damage, data loss, bans, or other issues arising from use of this mod.
- **White Knuckle** and related assets are property of their respective owners; this is an **unofficial fan mod**, not affiliated with or endorsed by the developers.
- By using or distributing this software, you accept these terms.

---

## 📄 License

Redistribution and modification are permitted under the disclaimer above. If you fork or republish, retain credit to the original author and note any substantial changes.

---

<p align="center"><sub>🔥 Stay off the floor. Keep climbing. 🔥</sub></p>
