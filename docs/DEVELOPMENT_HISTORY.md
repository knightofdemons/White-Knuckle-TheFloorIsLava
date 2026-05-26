# The Floor Is Lava — development history (AI-assisted)

This document describes how **The Floor Is Lava** was built for **White Knuckle** with heavy assistance from **Cursor AI** (Composer agent in the IDE). It is written from the actual project conversation and code evolution, not from marketing copy.

**Author:** knighToFdemonS  
**Repository:** https://github.com/knightofdemons/White-Knuckle-TheFloorIsLava  
**Current version:** 1.12.1  
**Plugin GUID:** `com.thefloorislava.whiteknuckle`

---

## Table of contents

1. [What this mod is](#what-this-mod-is)
2. [How AI was used](#how-ai-was-used)
3. [Original starting idea (prompt #1)](#original-starting-idea-prompt-1)
4. [Development phases](#development-phases)
5. [Architecture (current)](#architecture-current)
6. [Major bugs fixed during development](#major-bugs-fixed-during-development)
7. [Balance and design decisions](#balance-and-design-decisions)
8. [Release and packaging](#release-and-packaging)
9. [Conversation index — all 71 user prompts](#conversation-index--all-71-user-prompts)
10. [Conversation highlights](#conversation-highlights)
11. [Attached full transcript](#attached-full-transcript)
12. [File map](#file-map)

---

## What this mod is

A BepInEx 5 mod that turns upward-facing level geometry into **lava**. The player gets:

- An **extra health bar** (bottom center UI) — lava damage hits this pool, not vanilla health.
- **Grip isolation on lava** — standing on lava does not drain hand stamina through vanilla paths; grip can regen on lava instead.
- **Fanning** — press/release grab on lava to cool a **fixed floor spot**; damage scales with spot heat.
- **Easter egg** — look down + crouch + both grabs → rip the health bar off the UI into a grabbable 3D prop; lava damage stops for the rest of the run.

The mod was renamed from **Spike Balance Mod** → **The Floor Is Lava** once the design shifted from cone spikes to lava patches (prompt #23).

---

## How AI was used

| Area | AI role | Human role |
|------|---------|------------|
| **C# / Harmony / Unity** | Wrote and rewrote most plugin code, patches, lava mesh logic, fanning, easter egg | Playtesting, bug reports, balance requests |
| **Debugging** | Read logs, traced game APIs via reflection, iterated on fixes | Reported in-game behavior (“still no damage”, “grip still drops”) |
| **Visuals** | Shader/material attempts, merged surface meshes, gust VFX, icon PNG | Screenshots, references (lava photo URL), aesthetic feedback |
| **Docs** | README, Thunderstore manifest, this history file | Wording corrections, tone, disclosure requirements |
| **Project structure** | `release/`, `source/`, GitHub layout, zip packaging | Chose final GitHub path, upload to Thunderstore |

**Tool:** Cursor IDE agent (Composer), conversation stored as JSONL.  
**Not used for:** game assets owned by White Knuckle; the mod references game assemblies locally at build time only.

The author is **not** a professional C# / Unity developer. The workflow was: describe intent → AI implements → test in game → report failures → repeat. Several full rewrites were requested explicitly (prompts #8, #12–13, #22).

---

## Original starting idea (prompt #1)

> Create a White Knuckle mod with spikes on every upward-facing surface that damage the player; show a separate health bar; on spikes, damage health but recover hand stamina; off ground, recover health after a delay.

That single prompt led to the implementation plan and the first **Spike Balance Mod** codebase. Almost everything after that was iterative correction and feature expansion driven by playtest feedback.

---

## Development phases

### Phase 1 — Spike Balance Mod bootstrap (prompts #1–#8)

- BepInEx plugin scaffold, config, Harmony patches.
- **Problems:** mod loaded but no UI/spikes; UI visible in menu; multiple failed placement attempts.
- **Outcome:** first working health bar + spike damage, after several restarts from scratch.

### Phase 2 — Separate health vs grip (prompts #9–#13)

- Core design: lava/spike damage must **not** reduce hand stamina.
- **Problems:** grip still dropped while touching hazards only.
- **Outcome:** Harmony blocks on `DamageGripStrength`, `HandStamina`, hand-level grip methods while `ModState.OnLavaZone`; dedicated `HealthPool` for mod HP.

### Phase 3 — Placement and damage detection (prompts #14–#26)

- Spikes must sit on **level geometry**, not props/boxes/handholds; upward surfaces up to ~20° tilt.
- **Problems:** invisible markers, no damage, only first room, wall clipping while on handholds, scan not continuous.
- **Outcome:** world scan around player, terrain/level collider filtering, continuous rescan on room load; renamed toward “floor is lava”.

### Phase 4 — Spikes → lava visuals (prompts #27–#37)

- Cone spikes → textured cones → **glowing lava patches** with industrial look, drips on thin floors, merged surfaces, performance LOD.
- **Problems:** black/invisible meshes, wrong scale (“lavaclouds”), URP transparent shader failures, FPS cost.
- **Outcome:** opaque alpha-tested URP Unlit materials with emission; `LavaField` surface merging; render distance culling.

### Phase 5 — UI, run lifecycle, easter egg (prompts #38–#45)

- HP bar moved to **bottom** of screen; slim red fill; run reset on restart; menu leak fixes.
- Easter egg: look down + crouch + both grabs → rip animation → 3D grabbable HP bar prop (`CL_Prop` clone).
- **Problems:** gesture not firing (look/crouch/grab detection); black 3D bar; prop not visible; not grabbable.
- **Outcome:** `EasterEgg.cs` + `GameRefs` diagnostics; rip animation in `HealthBar`; prop uses game’s grab system.

### Phase 6 — Fanning / cooling (prompts #46–#58)

- Fan grab cycles on lava to cool fixed radial spots; heat 0–1; gust VFX; stamina cost; HP regen on cooled spots.
- **Problems:** spot followed player; damage heat ignored; config overrides ignored; transparent shaders; stamina too high; regen only off lava entirely.
- **Outcome:** `FanningSystem.cs`, `FullCoolStaminaFraction = 0.8`, vertical hitbox halved twice, heat applied via `ModState.LavaHeatMultiplier`, spots anchored in world space.

### Phase 7 — Rename, release, Thunderstore (prompts #59–#67)

- Spike → lava naming in code; `release/` + `source/` folders; README; icon; manifest; Thunderstore zip.
- Config template shipped in package; AI disclosure category required on upload.

### Phase 8 — Documentation polish (prompts #68–#71)

- README rewritten for GitHub users (AI disclosure, plain language, extra health bar terminology).
- Easter egg **Field note** hint restored in README.
- Fanning warmup off-by-one fixed: stamina only after real cooling cycles.
- This development history file + full transcript archive.

---

## Architecture (current)

```
TheFloorIsLava/
  Plugin.cs           — run lifecycle, damage tick, integrates subsystems
  Config.cs           — BepInEx config bindings
  Patches.cs          — player spawn, room load, grip damage blocks
  GameRefs.cs         — reflection helpers (player, hands, grip, kill run)
  HealthPool.cs       — mod extra health
  HealthBar.cs        — bottom-center UI + rip animation hooks
  Grip.cs             — grip regen while on lava
  ModState.cs         — run/lava zone/heat multiplier flags for patches
  FanningSystem.cs    — fan cycles, cooling spots, gust effect
  EasterEgg.cs        — gesture + 3D dropped bar
  Lava/
    LavaField.cs      — zone grid, world scan, player-on-lava test
    LavaZone.cs       — single zone data
    LevelGeom.cs      — geometry classification helpers
```

**Build:** .NET Standard 2.1, references `BepInEx`, `0Harmony`, game `Assembly-CSharp` + Unity modules from local White Knuckle install.

---

## Major bugs fixed during development

| Symptom | Root cause (typical) | Fix direction |
|---------|----------------------|---------------|
| Mod loads, nothing in run | Run detection too early / wrong hooks | `NotifyPlayerSpawned`, `InRun` gating |
| Grip drops on hazard only | Vanilla grip damage not fully blocked | Harmony prefixes on player + hand methods |
| Lava damage not applied | Heat multiplier not wired to DPS | `ModState.LavaHeatMultiplier` from fanning tick |
| Fanning config ignored | Old `.cfg` values + hardcoded fallbacks | Clamp/derive costs from `FullCoolStaminaFraction` |
| Cooled spot follows player | Anchor updated each frame | Removed follow; fixed world position at fan time |
| HP regen never on cooled lava | Regen gated on “not on lava” | Regen when `lavaHeat == 0` (no damage tick) |
| Black / invisible lava | URP transparent materials | Opaque alpha-tested Unlit + emission |
| HP bar stuck after run | Run end not clearing state | `StopRun()` on player swap / menu / death |
| Easter egg no trigger | Look/crouch/grab thresholds | `GameRefs` gesture helpers + logging |
| Fanning stamina during warmup | Off-by-one on activation cycles | Separate `_warmupCycles`; grip drain only after cooling |

---

## Balance and design decisions

| Setting | Default (code) | Notes |
|---------|----------------|-------|
| `MaxHealth` | 100 | Extra health pool |
| `LavaDamagePerSecond` | 7.5 | Shipped cfg template may differ (author tuning) |
| `FanningActivationCycles` | 2 | Warmup grab cycles **without** stamina cost |
| `FullCoolStaminaFraction` | 0.8 | Total grip budget to fully cool one spot |
| `VerticalAbove` / `VerticalBelow` | 0.45 / 0.25 | Halved twice from earlier values per playtest |
| Easter egg | Enabled by default | Disables lava damage after successful rip |

---

## Release and packaging

| Path | Purpose |
|------|---------|
| `release/TheFloorIsLava.dll` | Built plugin (when present) |
| `release/manifest.json` | Thunderstore metadata |
| `release/icon.png` | 256×256 mod icon |
| `release/README.md` | Package readme (mirrors GitHub) |
| `release/config/*.cfg` | Author-tuned default config |
| `release/knightofdemons-The_Floor_Is_Lava-1.12.1.zip` | Upload bundle |
| `source/` | Mirror of core `.cs` sources |
| `docs/conversation-transcript.jsonl` | **Full AI conversation** (713 lines) |
| `docs/transcript-user-queries.txt` | All user prompts, numbered |

---

## Conversation index — all 71 user prompts

Full text (minimal truncation) is in [`transcript-user-queries.txt`](transcript-user-queries.txt).

| # | Topic summary |
|---|----------------|
| 1 | Initial idea: spikes on upward surfaces, separate HP, stamina regen on ground, HP regen off ground — **create plan** |
| 2–3 | Implement attached Spike Balance Mod plan |
| 4 | Mod loads but no spikes / health UI in campaign |
| 5 | UI too early; spikes not appearing (timing) |
| 6 | Fix UI top-center + numeric display; fix spike placement |
| 7 | Still no spikes or UI |
| 8 | **Restart mod from scratch** |
| 9 | Spikes work; **decouple spike damage from hand stamina** |
| 10–11 | Grip still drops on spike contact |
| 12–13 | **Full recreate** — grip isolation still broken |
| 14 | Spikes on props/boxes — want **level geometry only** |
| 15 | No spike damage areas at all |
| 16 | Damage while on handhold — analyze level objects |
| 17 | Decouple from handholds; upward surfaces ≤20° |
| 18 | Markers in logs but no visible damage in rooms 1–2 |
| 19 | Markers room 1 only; no walking damage |
| 20 | Room 2 missing markers; health UI gone |
| 21 | Discontinuous generation; still no damage |
| 22 | **Full recreate** — remove old leftovers |
| 23 | Damage works but only first floor — rename to **TheFloorIsLava** |
| 24 | Good placement but **no damage to separate health bar** |
| 25–26 | Still no damage + generation stops after floor 2 |
| 27 | Cone metal spikes; death at 0 HP; exempt first floor; HP text visibility; regen delay config |
| 28 | Bigger spikes + level texture; half damage; kill player at 0 HP |
| 29 | 0 HP removes bar/spikes but no kill; spikes still black |
| 30 | Spikes invisible; death works |
| 31 | Spikes black again |
| 32 | Level color match; ground support check; noise; merged surface areas |
| 33 | Replace spikes with **glowing lava patches** + drips + shader |
| 34–35 | Lava too large / cloud-like — reference lava photo URL |
| 36 | More detail; fix neighbor clipping; **performance** / unload old rooms |
| 37 | Sharp edges; solid base color not clouds |
| 38 | Run end HP bar leak; move bar **bottom**; slim UI; **easter egg gesture** spec |
| 39 | Remove “/100”; taller bar; fix easter egg trigger description |
| 40 | Easter egg logs crouch only — not firing |
| 41–42 | Rip animation + 3D grabbable bar like GUI |
| 43 | Move bar up into hands; fix black texture |
| 44 | Animation OK but no world object |
| 45 | Match GUI bar — white border, red fill, 3× length |
| 46 | Make 3D bar grabbable; **plan fanning/cooling** (detailed spec) |
| 47 | Review plan then implement fanning |
| 48 | Spot square/black; no damage negate; stamina too high |
| 49 | Fanning broken entirely |
| 50 | Spot vanishes; damage not negated; no gust |
| 51 | Still no negate; stamina still too high (¼ suggestion) |
| 52–53 | No change after rebuild |
| 54 | Gust too big — particles toward ground; **HP regen on cooled spot** |
| 55 | Cooled spot attached to player — should stay fixed |
| 56 | Full cool = **80% one hand max grip** |
| 57–58 | Halve lava vertical hitbox (twice if needed); config not applied |
| 59 | Rename spike→lava; release/source folders; GitHub README + disclaimer |
| 60 | SpikeBalanceMod filenames still present |
| 61 | Richer README; config guide; cryptic easter egg hint; colorful |
| 62 | Mod icon 256×256; manifest; GitHub path |
| 63–64 | Thunderstore zip package |
| 65 | DLL only or include configs? |
| 66 | Include author cfg in package |
| 67 | Thunderstore **AI Generated** category requirement |
| 68 | **Full README rewrite** — Cursor disclosure, plain language |
| 69 | README fixes: “download”, extra health bar, no Thunderstore in readme, build cmd |
| 70 | Re-add easter egg hint |
| 71 | Remove EasterEgg config line; fanning stamina fix; **this history doc** |

---

## Conversation highlights

### Highlight A — Core concept in one message (prompt #1)

The entire project scope was defined in the first user message: hazard on upward floors, separate health resource, stamina regen while on the hazard, delayed HP regen when off it. Every later feature (lava visuals, fanning, easter egg) was additive.

### Highlight B — Grip isolation struggle (prompts #9–#13)

The longest recurring pain point. The game routes several grip/stamina drains outside the obvious damage API. Final approach: block **all** vanilla grip damage paths while on lava, use direct field writes only for **intentional** fanning costs.

### Highlight C — “Make the floor lava” pivot (prompt #23)

Explicit rename and design alignment with the childhood game: not isolated spike markers, but **continuous upward-surface coverage** with scanning as the player climbs.

### Highlight D — Fanning spec in full (prompt #46)

User wrote a complete mechanical spec in one prompt: 2-cycle activation, per-hand vs both-hands stamina, heat 0–1, radial buffer ~1 m, grey cooling patch visual, gust FX, partial vs full reheat timing. Implementation tracked this closely in `FanningSystem.cs`.

### Highlight E — Easter egg as facility lore (prompts #38–#45, #70)

Gesture: maximum look-down + crouch + dual grab. Animation rips UI bar into world. README **Field note** gives cryptic hint without documenting the exact inputs in config.

### Highlight F — AI disclosure / distribution (prompts #67–#69)

Thunderstore rejected upload without **AI Generated** category. README now leads with Cursor AI disclosure for GitHub downloaders.

### Highlight G — Fanning stamina fix (prompt #71)

Warmup used an off-by-one (`_completedCycles++` before `< ActivationCycles`), so the **first** “cooling” cycle happened one grab too early. Fixed with `_warmupCycles` and grip drain **after** affordability checks, only when cooling is applied.

---

## Code cleanup (2026-05-26)

- Removed unused helpers (`PruneFar`, `BothHandsHolding`, dead fanning cost config entries, etc.).
- Every `Config.cs` entry is now referenced in runtime code.
- `VisualScale`, `LavaHeightVariance`, `LavaShapeJitter`, and `LavaScaleJitter` drive lava mesh/zone generation.
- Fanning warmup fixed: grip is spent only after warmup cycles and only when cooling applies.
- Easter egg timing (`HoldDuration`, `LookDownThreshold`, `AnimDuration`) moved to config.
- Full conversation archive: [`docs/conversation-transcript.jsonl`](conversation-transcript.jsonl)

---

The complete Cursor agent conversation (user + assistant messages, tool results omitted from human-readable flow but present in raw JSONL) is archived at:

**[`docs/conversation-transcript.jsonl`](conversation-transcript.jsonl)**

| Property | Value |
|----------|-------|
| Lines | 713 |
| Size | ~1.57 MB |
| Session ID | `82ea67d1-2cb9-4ccf-9c79-c47852b1188e` |
| Format | JSONL — one JSON object per line, `"role":"user"` or `"role":"assistant"` |

To search locally:

```powershell
Select-String -Path docs\conversation-transcript.jsonl -Pattern "FanningSystem"
```

User-only extract:

**[`docs/transcript-user-queries.txt`](transcript-user-queries.txt)** — all 71 prompts, numbered.

---

## File map

| Version | Milestone |
|---------|-----------|
| v1.0.x | Spike Balance Mod — basic spikes + HP |
| v1.1.x | Lava visuals, world scan, merged meshes |
| v1.11.x | Fanning system, gust VFX, balance passes |
| v1.12.x | Rename cleanup, release folders, Thunderstore, README/docs |

Current assembly version string: **`1.12.1`** in `Plugin.cs`.

---

*This document was assembled from the original Cursor development session and repository state. For gameplay instructions see [README.md](../README.md).*
