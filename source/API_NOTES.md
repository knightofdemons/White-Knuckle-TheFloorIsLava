# White Knuckle — mod API notes

- **Level floors:** static non-trigger colliders, not under `CL_Handhold`
- **Handholds:** climb only; trigger `LoadHandholds` for room changes
- **Grip:** `Hand.gripStrength`, `AddGripStrength`, block `HandStamina` / `DamageGripStrength` on lava
- **Damage:** mod `HealthPool` only — never `ENT_Player.Damage`
