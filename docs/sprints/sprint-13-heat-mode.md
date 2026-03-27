# Sprint 13: Heat Mode

**Priority:** Medium-High — adds a completely new game mode with a unique AP interaction pattern
**Complexity:** High
**New AP Item IDs:** 37800-37849 (Heat Reduction items, Charge Speed items)
**Depends On:** Sprint 1 (DLC support)
**Mutually Exclusive With:** Progressive Stages / Non-Progressive Stages (this is a third game mode)

## Goal

Add a third game mode inspired by Hades' Heat system. All stages are available from the start using vanilla RoR2 routing — but the game starts at crushing difficulty. As your multiworld partners send you checks, the difficulty peels back layer by layer until you're playing a normal run. The AP dependency is inverted compared to Progressive mode: instead of "I can't go there yet," it's "I *can* go there, but I'll get destroyed."

This creates a fundamentally different pacing feel — early game is a desperate survival struggle, and the arc of the run is the world getting easier rather than harder. Checks feel like relief instead of unlocks.

## Game Mode Comparison

| Mode | Stage Access | Difficulty | AP Interaction |
|------|-------------|------------|----------------|
| **Progressive** | Locked until stage unlock items received | Normal | Checks unlock stages |
| **Non-Progressive** | Unlocked tier by tier as you loop | Normal | Checks are items/rewards |
| **Heat** | All stages available (vanilla routing) | Starts brutal, softens over time | Checks make the game survivable |

## Core Design

### Heat Level

Heat is a numeric value (0-25) that maps to stacked gameplay debuffs. At heat 25, the game is nearly impossible. Each **Progressive Heat Reduction** AP item lowers heat by 1, peeling off one debuff layer.

The debuffs are grouped into categories, with multiple layers per category. This means heat reduction feels gradual — you don't suddenly go from "impossible" to "easy," you feel each layer come off.

### Stage Timer

Each stage has a time limit. When it expires, bad things happen (configurable — see Timer Expiry Modes below). Completing location checks extends the timer, creating urgency to explore while rewarding AP participation.

## Heat Debuff Layers (25 Layers)

Layers peel off from **bottom to top** — the harshest debuffs are removed first, so early heat reductions have the most impact.

### Monster Credits (+enemy spawns) — 5 layers

| Heat | Layer | Effect |
|------|-------|--------|
| 25 | Monster Credits 5 | +100% monster credits (double spawns) |
| 20 | Monster Credits 4 | +80% monster credits |
| 15 | Monster Credits 3 | +60% monster credits |
| 10 | Monster Credits 2 | +40% monster credits |
| 5 | Monster Credits 1 | +20% monster credits |

### Difficulty Scaling (time coefficient) — 5 layers

| Heat | Layer | Effect |
|------|-------|--------|
| 24 | Time Scaling 5 | Difficulty coefficient multiplier 3.0x |
| 19 | Time Scaling 4 | Difficulty coefficient multiplier 2.5x |
| 14 | Time Scaling 3 | Difficulty coefficient multiplier 2.0x |
| 9 | Time Scaling 2 | Difficulty coefficient multiplier 1.5x |
| 4 | Time Scaling 1 | Difficulty coefficient multiplier 1.25x |

### Healing Reduction — 5 layers

| Heat | Layer | Effect |
|------|-------|--------|
| 23 | Healing 5 | -50% healing received |
| 18 | Healing 4 | -40% healing received |
| 13 | Healing 3 | -30% healing received |
| 8 | Healing 2 | -20% healing received |
| 3 | Healing 1 | -10% healing received |

### Elite Promotion — 5 layers

| Heat | Layer | Effect |
|------|-------|--------|
| 22 | Elites 5 | T2 elites (Malachite/Celestine) can spawn from stage 1 |
| 17 | Elites 4 | +40% chance for enemies to spawn as elite |
| 12 | Elites 3 | +30% chance for enemies to spawn as elite |
| 7 | Elites 2 | +20% chance for enemies to spawn as elite |
| 2 | Elites 1 | +10% chance for enemies to spawn as elite |

### Unique Penalties — 5 layers

| Heat | Layer | Effect |
|------|-------|--------|
| 21 | Teleporter Drain | Teleporter charge speed -50% |
| 16 | Lethal Fall Damage | Fall damage is lethal (Eclipse 3 modifier) |
| 11 | Ally Starting HP | Start each stage at 50% HP |
| 6 | Speed Penalty | -15% movement speed |
| 1 | Fog | Void fog appears in the edges of each stage |

### Layer Removal Order

When heat drops from 25 → 0, the layers peel off in this order (removing the highest heat number first):

```
Heat 25 → 24: Remove Monster Credits 5 (+100%)
Heat 24 → 23: Remove Time Scaling 5 (3.0x)
Heat 23 → 22: Remove Healing 5 (-50%)
Heat 22 → 21: Remove Elites 5 (T2 from stage 1)
Heat 21 → 20: Remove Teleporter Drain (-50% charge speed)
Heat 20 → 19: Remove Monster Credits 4 (+80%)
Heat 19 → 18: Remove Time Scaling 4 (2.5x)
Heat 18 → 17: Remove Healing 4 (-40%)
Heat 17 → 16: Remove Elites 4 (+40% elite chance)
Heat 16 → 15: Remove Lethal Fall Damage
Heat 15 → 14: Remove Monster Credits 3 (+60%)
Heat 14 → 13: Remove Time Scaling 3 (2.0x)
Heat 13 → 12: Remove Healing 3 (-30%)
Heat 12 → 11: Remove Elites 3 (+30% elite chance)
Heat 11 → 10: Remove Ally Starting HP (50%)
Heat 10 →  9: Remove Monster Credits 2 (+40%)
Heat  9 →  8: Remove Time Scaling 2 (1.5x)
Heat  8 →  7: Remove Healing 2 (-20%)
Heat  7 →  6: Remove Elites 2 (+20% elite chance)
Heat  6 →  5: Remove Speed Penalty (-15%)
Heat  5 →  4: Remove Monster Credits 1 (+20%)
Heat  4 →  3: Remove Time Scaling 1 (1.25x)
Heat  3 →  2: Remove Healing 1 (-10%)
Heat  2 →  1: Remove Elites 1 (+10% elite chance)
Heat  1 →  0: Remove Fog
Heat  0:      Normal RoR2
```

This interleaving means every heat reduction touches a different system, so each one feels distinct.

## Stage Timer

### Base Timer
Each stage has a configurable base time limit (default: 300 seconds / 5 minutes). The timer starts when you enter a stage.

### Timer Extension
Each location check completed on the stage extends the timer (default: +30 seconds per check). This rewards exploration and AP participation.

### Timer Expiry Modes (YAML configurable)

| Mode | Effect | Feel |
|------|--------|------|
| **Surge** (default) | Difficulty coefficient spikes massively — equivalent to a permanent Shrine of the Mountain proc every 30s | Punishing but survivable |
| **Lockout** | Teleporter begins charging automatically — you leave with what you got | Urgency-focused, no death spiral |
| **Burn** | Uncollected location checks in the stage become permanently inaccessible | High stakes, forces check prioritization |

### Timer Interaction with Heat
At high heat, the timer is effectively shorter because everything takes longer (slower charge speed, harder enemies, reduced healing means more time recovering). As heat drops, the same base timer becomes much more comfortable.

## New AP Items

### Progressive Heat Reduction
**IDs:** 37800-37824 (25 items)
**Classification:** `useful`
**Effect:** Each one reduces current heat by 1, permanently removing a debuff layer.

In the AP item pool, these replace the stage unlock items that Progressive mode uses. Same concept (progressive unlocks that gate difficulty), different mechanism.

### Teleporter Charge Speed Boost
**ID:** 37830
**Classification:** `useful`
**Effect:** Permanently increases holdout zone (teleporter) charge rate by +15% per item received. Stacks additively.
**Pool Count:** Configurable (default: 5 in pool = +75% max charge speed)

This is a strong standalone item worth adding even outside heat mode — useful in any game mode as a filler/useful item.

### Coolant Cell
**ID:** 37831
**Classification:** `filler`
**Effect:** Temporarily reduces heat by 2 for the current stage only. Heat returns next stage.
**Use case:** Provides breathing room without permanent progression. Good filler item.

### Timer Extension
**ID:** 37832
**Classification:** `filler`
**Effect:** Adds +60 seconds to the current stage timer when received.
**Use case:** Emergency relief when timer is running low. Only relevant in heat mode.

## Itemipelago Synergy

Heat mode pairs naturally with the existing item pool limiting system (Sprint 4). When both are enabled:

- You start with a limited item pool AND crushing difficulty
- AP checks expand your item pool (Sprint 4) AND reduce your heat (Sprint 13)
- Early game: few items, brutal enemies → you're working with whites and greens against elite hordes
- Late game: full item pool, normal difficulty → standard powerful RoR2 run

This is the "maximum AP integration" configuration for experienced players who want everything gated through the multiworld.

## YAML Options

### Game Mode Selection
```yaml
game_mode:
  # "progressive" | "non_progressive" | "heat"
  # Mutually exclusive — replaces the current stage_unlock toggle
  progressive:
    display_name: "Progressive Stages"
    description: "Stages unlock as you receive AP items"
  non_progressive:
    display_name: "Non-Progressive Stages"
    description: "Stages unlock naturally as you loop"
  heat:
    display_name: "Heat Mode"
    description: "All stages available, difficulty starts extreme and reduces with AP checks"
```

### Heat Mode Options
```yaml
heat_starting_level:
  display_name: "Starting Heat Level"
  range: 5-25
  default: 25

heat_layers:
  display_name: "Total Heat Layers"
  range: 10-25
  default: 25
  description: "How many Progressive Heat Reduction items are placed in the pool"

stage_time_limit:
  display_name: "Stage Time Limit (seconds)"
  range: 120-600
  default: 300
  description: "Base time limit per stage. 0 = no timer."

timer_extension_per_check:
  display_name: "Timer Extension Per Check (seconds)"
  range: 10-60
  default: 30

timer_expiry_mode:
  display_name: "Timer Expiry Mode"
  options: ["surge", "lockout", "burn"]
  default: "surge"

charge_speed_items_in_pool:
  display_name: "Charge Speed Items in Pool"
  range: 0-10
  default: 5

coolant_cells_in_pool:
  display_name: "Coolant Cells in Pool"
  range: 0-15
  default: 5
```

## Python AP World Changes

### items.py — New Item Definitions
```python
heat_offset = 37800

heat_table: Dict[str, RiskOfRainItemData] = {
    "Progressive Heat Reduction": RiskOfRainItemData(
        "Heat", 0 + heat_offset, ItemClassification.useful, 1),
    "Charge Speed Boost":         RiskOfRainItemData(
        "Heat", 30 + heat_offset, ItemClassification.useful, 1),
    "Coolant Cell":               RiskOfRainItemData(
        "Heat", 31 + heat_offset, ItemClassification.filler, 1),
    "Timer Extension":            RiskOfRainItemData(
        "Heat", 32 + heat_offset, ItemClassification.filler, 1),
}
```

### options.py — Game Mode Option
Replace the current `require_stage_unlocks` toggle with a three-way `game_mode` choice that selects between progressive, non-progressive, and heat.

### regions.py
When game mode is heat, all stage regions are accessible from the start (vanilla routing). No stage unlock access rules.

## C# Changes

### New File: Services/HeatService.cs
Core heat system — tracks current heat level, manages debuff application, handles timer.

```csharp
public class HeatService : IDisposable
{
    private int currentHeat;
    private int maxHeat;
    private float stageTimer;
    private float baseTimeLimit;
    private float extensionPerCheck;
    private string expiryMode;

    // Debuff categories
    private float monsterCreditMultiplier;
    private float difficultyMultiplier;
    private float healingMultiplier;
    private float eliteChanceBonus;
    private float chargeSpeedMultiplier;

    public void ReduceHeat(int amount) { ... }
    public void ExtendTimer(float seconds) { ... }
    private void RecalculateDebuffs() { ... }
    private void OnStageStart() { ... }
    private void OnTimerExpired() { ... }
}
```

### Key Hooks
- `CombatDirector.Awake` — modify monster credits based on heat
- `Run.RecalculateDifficultyCoefficent` — apply difficulty multiplier
- `HealthComponent.Heal` — reduce healing
- `CombatDirector.AttemptSpawnOnTarget` — force elite spawns
- `HoldoutZoneController` — modify charge rate
- `Run.FixedUpdate` or `Stage.FixedUpdate` — stage timer tick

### ArchipelagoItemLogicController.cs
Route new item IDs:
```csharp
case 37800: // Progressive Heat Reduction (37800-37824)
    heatService.ReduceHeat(1);
    break;
case 37830:
    heatService.BoostChargeSpeed(0.15f);
    break;
case 37831:
    heatService.ApplyCoolant(2);
    break;
case 37832:
    heatService.ExtendTimer(60f);
    break;
```

### Net Messages
- `SyncHeatConfig` — initial heat settings from host to clients
- `SyncHeatLevel` — broadcast heat changes
- `SyncStageTimer` — keep timer in sync for multiplayer

### UI Integration
- Scoreboard page showing current heat level + active debuffs
- Stage timer displayed near the existing objective panel
- Chat messages when heat reduces ("Heat reduced to 18! Removed: +80% monster credits")
- Color-coded heat display (red → orange → yellow → green as heat drops)

## AP Item ID Range

```
37800-37824  Progressive Heat Reduction (25 items)
37825-37829  Reserved
37830        Charge Speed Boost
37831        Coolant Cell
37832        Timer Extension
37833-37849  Reserved for future heat items
```

## Testing Criteria

1. **Game mode selection**: Verify heat mode is selectable in YAML and mutually exclusive with progressive/non-progressive
2. **Starting heat**: Begin a run at heat 25. Verify all debuffs are active simultaneously
3. **Heat reduction**: Receive Progressive Heat Reduction items. Verify debuffs peel off in correct order
4. **Heat 0**: Reduce heat to 0. Verify gameplay matches normal vanilla RoR2
5. **Stage timer**: Verify timer appears and counts down. Verify checks extend it
6. **Timer expiry — Surge**: Let timer expire. Verify difficulty spikes
7. **Timer expiry — Lockout**: Let timer expire. Verify teleporter auto-charges
8. **Timer expiry — Burn**: Let timer expire. Verify remaining checks become inaccessible
9. **Charge speed items**: Receive charge speed boosts. Verify teleporter charges faster
10. **Coolant cell**: Receive coolant. Verify temporary heat reduction for current stage only, returns next stage
11. **Monster credits**: At various heat levels, verify enemy spawn counts scale correctly
12. **Healing reduction**: At various heat levels, verify healing amounts are reduced
13. **Elite spawns**: At high heat, verify increased elite frequency and T2 elites on early stages
14. **Multiplayer sync**: Verify heat level, timer, and debuffs sync across all clients
15. **Itemipelago combo**: Enable both heat mode and item pool limiting. Verify both systems work together
16. **Vanilla routing**: Verify stages follow vanilla RoR2 routing (no AP stage gates)
17. **Heat UI**: Verify scoreboard page shows heat level and active debuffs
18. **Chat notifications**: Verify chat messages appear on heat reduction with debuff name

## Risks and Open Questions

- **Balance**: 25 layers is a lot of knobs. Needs extensive playtesting to ensure heat 25 is "nearly impossible but not literally impossible" and that the reduction curve feels good. May need to adjust individual layer values
- **Difficulty coefficient hooks**: Modifying `Run.RecalculateDifficultyCoefficent` may conflict with other mods or difficulty settings. Need to check if Eclipse modifiers stack with our multiplier
- **Monster credit modification**: `CombatDirector` credit overrides need to be done carefully to avoid breaking boss spawns or teleporter events
- **Healing hook**: Need to verify we catch all healing sources (passive regen, items like Harvester's Scythe, Fungus, etc.) — `HealthComponent.Heal` should cover most but check edge cases
- **Timer UX**: A visible countdown timer could feel stressful in a way that's not fun. Consider making the timer optional or having it only appear in the last 60 seconds
- **Coolant cell design**: Temporary heat reduction that reverts next stage could feel bad ("wait, my healing got worse again?"). Alternative: coolant permanently reduces heat by 1 but is classified as filler instead of useful. Needs playtesting
- **Game mode refactor**: Changing from a toggle to a three-way choice is a breaking YAML change. Need migration path for existing configs
- **Void fog layer**: The fog unique penalty at heat 1 may require spawning fog volumes that don't exist on vanilla stages. Could be complex — consider replacing with a simpler penalty if implementation is too involved
