# Sprint 5: Drone Randomizer

**Priority:** Medium — simple standalone feature, adds variety
**Complexity:** Low
**New AP Item IDs:** 37200-37215 (Drone Unlock items)
**Depends On:** Sprint 1

## Goal

Restrict which drone types can spawn as broken interactables in stages. AP checks unlock more drone types, adding another progression layer. Players start with a limited selection of drones and gradually expand their options.

## RoR2 Drone Types

### Interactable Drones (~15 types)

| # | Drone Type | Spawn Card (verify at runtime) | Cost | DLC |
|---|-----------|-------------------------------|------|-----|
| 1 | Gunner Drone | `iscBrokenDrone1` | $40 | Base |
| 2 | Healing Drone | `iscBrokenDrone2` | $40 | Base |
| 3 | Gunner Turret | `iscBrokenTurret1` | $35 | Base |
| 4 | Missile Drone | `iscBrokenMissileDrone` | $60 | Base |
| 5 | Emergency Drone | `iscBrokenEmergencyDrone` | $50 | Base |
| 6 | Equipment Drone | `iscBrokenEquipmentDrone` | 1 Equipment | Base |
| 7 | Incinerator Drone | `iscBrokenFlameDrone` | $100 | Base |
| 8 | TC-280 Prototype | `iscBrokenMegaDrone` | $350 | Base |
| 9 | Cleanup Drone | verify | $60 | DLC |
| 10 | Barrier Drone | verify | $100 | DLC |
| 11 | Jailer Drone | verify | $100 | DLC |
| 12 | Bombardment Drone | verify | $200 | DLC |
| 13 | Freeze Drone | verify | $350 | DLC |
| 14 | Transport Drone | verify | varies | DLC |
| 15 | Junk Drone | verify | $40 | DLC |

**Note:** Exact spawn card names for DLC drones need runtime verification. Launch RoR2, iterate `DirectorCardCategorySelection` categories for "Drones", and log all spawn card names.

## Phase 1: Drone Identification

### Tasks
1. Add a debug log to dump all drone spawn cards at runtime
2. Map each spawn card name to a friendly display name
3. Identify which drones are DLC-gated
4. Build a reference table of all drone types with their spawn card identifiers

### Runtime Discovery Code
```csharp
// Temporary debug code for Phase 1
SceneDirector.onGenerateInteractableCardSelection += (director, selection) =>
{
    foreach (var category in selection.categories)
    {
        Log.LogDebug($"Category: {category.name}");
        foreach (var card in category.cards)
        {
            Log.LogDebug($"  Card: {card.spawnCard.name} Cost: {card.spawnCard.directorCreditCost}");
        }
    }
};
```

## Phase 2: Core Handler

### Tasks
1. Create `Handlers/DronePoolHandler.cs`
2. Maintain a `HashSet<string>` of allowed drone spawn card names
3. Hook `SceneDirector.onGenerateInteractableCardSelection` to filter the "Drones" category

### Implementation
```csharp
public class DronePoolHandler
{
    private HashSet<string> allowedDrones = new();
    private List<string> shuffledDrones;  // deterministic order from seed

    public void Hook()
    {
        SceneDirector.onGenerateInteractableCardSelection += FilterDrones;
    }

    public void UnHook()
    {
        SceneDirector.onGenerateInteractableCardSelection -= FilterDrones;
    }

    private void FilterDrones(SceneDirector director, DirectorCardCategorySelection selection)
    {
        if (!dronePoolEnabled) return;

        for (int catIdx = 0; catIdx < selection.categories.Length; catIdx++)
        {
            var category = selection.categories[catIdx];
            if (category.name != "Drones") continue;

            for (int i = 0; i < category.cards.Length; i++)
            {
                if (!allowedDrones.Contains(category.cards[i].spawnCard.name))
                {
                    // Set cost prohibitively high so director never picks it
                    category.cards[i].spawnCard.directorCreditCost = int.MaxValue;
                }
            }
        }
    }
}
```

### Alternative Approach
Instead of setting cost to `int.MaxValue`, remove the card entirely from the array. This is cleaner but requires rebuilding the array:
```csharp
category.cards = category.cards
    .Where(c => allowedDrones.Contains(c.spawnCard.name))
    .ToArray();
```

### Key Files
- New: `Archipelago.RiskOfRain2/Handlers/DronePoolHandler.cs`

## Phase 3: AP Items

### New AP Items

| AP Item | Item ID | Unlocks |
|---------|---------|---------|
| "Drone: Gunner" | 37201 | Gunner Drone |
| "Drone: Healing" | 37202 | Healing Drone |
| "Drone: Turret" | 37203 | Gunner Turret |
| "Drone: Missile" | 37204 | Missile Drone |
| "Drone: Emergency" | 37205 | Emergency Drone |
| "Drone: Equipment" | 37206 | Equipment Drone |
| "Drone: Incinerator" | 37207 | Incinerator Drone |
| "Drone: TC-280" | 37208 | TC-280 Prototype |
| "Drone: Cleanup" | 37209 | Cleanup Drone |
| "Drone: Barrier" | 37210 | Barrier Drone |
| "Drone: Jailer" | 37211 | Jailer Drone |
| "Drone: Bombardment" | 37212 | Bombardment Drone |
| "Drone: Freeze" | 37213 | Freeze Drone |
| "Drone: Transport" | 37214 | Transport Drone |
| "Drone: Junk" | 37215 | Junk Drone |

### Item ID Range
```csharp
private const long droneRangeLower = 37200;
private const long droneRangeUpper = 37299;
```

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — add drone range to `EnqueueItem()`, add `droneReceivedQueue`

## Phase 4: Starting Drones

At session start, use the AP seed to deterministically select starting drones.

### Algorithm
1. Build list of all drone types (filtered by enabled DLCs)
2. Shuffle with seeded RNG
3. Select first N as starting pool (N from slot data)
4. Remaining drones are unlocked in shuffle order by AP items

### Special Consideration: Essential Drones
Healing Drone is nearly essential for survival. Consider:
- Option A: Always include Healing Drone in starting pool (recommended)
- Option B: Make it configurable with a "guaranteed drones" list
- Option C: No guarantees, purely random

**Recommendation:** Always include Healing Drone and Gunner Drone in the starting pool. The `startingDroneCount` config applies to additional random drones beyond these.

## Phase 5: Persistence

Drone pool persists across runs within the same AP session, like stage unlocks.

### Tasks
1. Store unlocked drone set in `ArchipelagoClient` cached state
2. On `CleanupRun()`: save drone unlock state
3. On `SetupRun()`: restore drone pool from cached state
4. On `ProcessAllReceivedItems()`: replay drone unlock items

## Python AP World Changes

### items.py
```python
drone_offset: int = offset + 200  # 37200

drone_table: Dict[str, RiskOfRainItemData] = {
    "Drone: Gunner":       RiskOfRainItemData("Drone", 1 + drone_offset, ItemClassification.filler),
    "Drone: Healing":      RiskOfRainItemData("Drone", 2 + drone_offset, ItemClassification.useful),
    "Drone: Turret":       RiskOfRainItemData("Drone", 3 + drone_offset, ItemClassification.filler),
    "Drone: Missile":      RiskOfRainItemData("Drone", 4 + drone_offset, ItemClassification.useful),
    "Drone: Emergency":    RiskOfRainItemData("Drone", 5 + drone_offset, ItemClassification.useful),
    "Drone: Equipment":    RiskOfRainItemData("Drone", 6 + drone_offset, ItemClassification.filler),
    "Drone: Incinerator":  RiskOfRainItemData("Drone", 7 + drone_offset, ItemClassification.filler),
    "Drone: TC-280":       RiskOfRainItemData("Drone", 8 + drone_offset, ItemClassification.useful),
    # ... remaining DLC drones
}
```

### options.py
```python
class DroneRandomizer(Toggle):
    """Restrict which drone types can spawn. AP checks unlock more drones."""
    display_name = "Drone Randomizer"
    default = False

class StartingDroneCount(Range):
    """Number of drone types available at start (in addition to Healing and Gunner)."""
    display_name = "Starting Drone Count"
    range_start = 0
    range_end = 13
    default = 2
```

### __init__.py
When `droneRandomizer` is enabled:
```python
if self.options.drone_randomizer:
    # Healing + Gunner are guaranteed, rest are shuffled
    extra_drones = [d for d in drone_table if d not in ("Drone: Healing", "Drone: Gunner")]
    random.shuffle(extra_drones)  # use self.random for determinism
    starting = self.options.starting_drone_count.value
    for drone_name in extra_drones[starting:]:
        item_pool.append(drone_name)
```

No logic rules needed — drones are quality-of-life, not progression-gating.

## Configuration Options

| Slot Data Key | Type | Range | Default |
|---------------|------|-------|---------|
| `droneRandomizer` | Toggle | - | off |
| `startingDroneCount` | Range | 0-13 | 2 |

## UI/UX

- Chat message on drone unlock: "Drone type unlocked: **Missile Drone**!" in teal/cyan color
- Console command `ap_drones` to list currently available and locked drone types
- Optional: show locked drone repair stations as visually different (grayed out, sparking) — may require custom shader work, likely out of scope

## Testing Criteria

1. **Restricted spawns**: Enable with 0 starting drones (only Healing + Gunner). Verify only those 2 types appear as broken drones in stages
2. **Unlock**: Receive "Drone: Missile". Verify Missile Drone starts spawning in subsequent stages
3. **All unlocked**: Receive all drone items. Verify all drone types spawn normally
4. **Disabled**: Set `droneRandomizer=false`. Verify all drones spawn as normal
5. **Persistence**: Die, start new run in same AP session. Verify drone pool persists
6. **DLC gating**: Disable SOTV. Verify SOTV-specific drones are excluded from the pool and no unlock items are generated for them
7. **Determinism**: Same seed produces same starting drone selection
8. **Director behavior**: Verify that setting `directorCreditCost = int.MaxValue` reliably prevents spawning (or verify the array-removal approach works)

## Risks and Open Questions

- **Spawn card identification**: DLC drone spawn card names need runtime verification. The names listed above are educated guesses for base game drones; DLC drone names may differ
- **Equipment Drone**: This drone consumes an Equipment item from the player. If the player has no equipment, it can't be purchased. Should it still spawn when unlocked? **Recommendation:** Yes, it spawns normally; the player just can't buy it without equipment
- **Healing Drone essentiality**: Without Healing Drone, survival is significantly harder. Guaranteeing it in the starting pool prevents frustrating early deaths. If the user prefers full randomization, make the guarantee configurable
- **TC-280 rarity**: TC-280 is very expensive ($350) and rarely spawns. Excluding it from the starting pool has minimal gameplay impact. It's fine as a late unlock
- **Director credit cost hack**: Setting cost to `int.MaxValue` might cause integer overflow issues in the director's budget calculation. The array-removal approach is safer but more complex. Test both approaches
- **Multiplayer**: Drone spawning is server-side. Only the host needs the drone pool state. Clients see whatever drones the server spawns — no sync needed
