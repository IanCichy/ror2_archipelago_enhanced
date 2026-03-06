# Sprint 4: Item Pool Limiting

**Priority:** Medium-High — major gameplay feature, creates "item randomizer" layer
**Complexity:** Medium-High
**New AP Item IDs:** 37100-37109 (Pool Expansion items)
**Depends On:** Sprint 1 (DLC item tiers include Void/Meal items)

## Goal

Restrict which RoR2 items can drop from chests, shrines, and other sources to a limited starting pool. AP checks gradually expand the pool by adding items of each rarity tier. This creates an "item randomizer" layer where discovering which game items you can access is gated by AP progression. The pool persists across runs within the same AP session.

**Reference:** [LootPoolLimiter](https://github.com/Tizitendo/LootPoolLimiter) mod for hook patterns.

## Game Item Counts

| Rarity | Count | Notes |
|--------|-------|-------|
| White / Common | 36 | Base + DLC |
| Green / Uncommon | 42 | Base + DLC |
| Red / Legendary | 36 | Base + DLC |
| Yellow / Boss | 22 | Teleporter boss drops |
| Blue / Lunar | 20 | User wants toggle to disable entirely |
| Void | 14 | SOTV DLC only |
| Meal | 5 | SOTS DLC only |
| Equipment (Regular) | 30 | Active items |
| Equipment (Lunar) | 4 | Active lunar items |

## Phase 1: Core Pool System

### Tasks
1. Create `Handlers/ItemPoolHandler.cs` implementing the handler pattern
2. Maintain per-tier allowed item sets:
   ```csharp
   public class ItemPoolHandler
   {
       private HashSet<ItemIndex> allowedWhite = new();
       private HashSet<ItemIndex> allowedGreen = new();
       private HashSet<ItemIndex> allowedRed = new();
       private HashSet<ItemIndex> allowedBoss = new();
       private HashSet<ItemIndex> allowedLunar = new();
       private HashSet<ItemIndex> allowedVoid = new();
       private HashSet<ItemIndex> allowedEquipment = new();

       private int whiteExpansions = 0;
       private int greenExpansions = 0;
       // ... per tier

       public bool IsItemAllowed(ItemIndex item) { ... }
       public void ExpandPool(string tier, int count) { ... }
   }
   ```
3. Hook `BasicPickupDropTable.GenerateWeightedSelection` to zero-weight items not in the allowed set
4. Hook `PickupTransmutationManager.RebuildAvailablePickupGroups` to exclude items from printers/scrappers

### Key Hook — Drop Table Filtering
```csharp
private void FilterDropTable(
    On.RoR2.BasicPickupDropTable.orig_GenerateWeightedSelection orig,
    BasicPickupDropTable self)
{
    orig(self);
    if (!poolEnabled) return;

    for (int i = 0; i < self.selector.Count; i++)
    {
        PickupIndex pickup = self.selector.GetChoice(i).value;
        PickupDef def = PickupCatalog.GetPickupDef(pickup);
        if (def != null && def.itemIndex != ItemIndex.None && !IsItemAllowed(def.itemIndex))
        {
            self.selector.ModifyChoiceWeight(i, 0f);
        }
        if (def != null && def.equipmentIndex != EquipmentIndex.None && !IsEquipmentAllowed(def.equipmentIndex))
        {
            self.selector.ModifyChoiceWeight(i, 0f);
        }
    }
}
```

### Key Files
- New: `Archipelago.RiskOfRain2/Handlers/ItemPoolHandler.cs`

## Phase 2: Pool Initialization

At session start, use the AP session seed to deterministically select starting items per tier.

### Algorithm
1. Enumerate all items per tier from `ItemCatalog.allItemDefs`, filtering by `ItemDef.tier`
2. Sort by `ItemIndex` for deterministic ordering
3. Shuffle using a seeded `System.Random` (seed from AP slot data)
4. Select first N items per tier (N = starting pool size from slot data)
5. Build allowed sets

### Determinism Requirement
Two runs with the same AP seed must produce the same starting pool. The shuffled ordering stays fixed for the session — expansion items always add the "next" items in the deterministic sequence.

```csharp
private List<ItemIndex> shuffledWhite;  // deterministic ordering per session

public void Initialize(string seed, int startingWhite, int startingGreen, ...)
{
    var rng = new System.Random(seed.GetHashCode());
    shuffledWhite = ItemCatalog.allItemDefs
        .Where(d => d.tier == ItemTier.Tier1)
        .OrderBy(d => d.itemIndex)
        .Select(d => d.itemIndex)
        .ToList();
    Shuffle(shuffledWhite, rng);

    for (int i = 0; i < startingWhite && i < shuffledWhite.Count; i++)
        allowedWhite.Add(shuffledWhite[i]);
    // ... repeat for each tier
}
```

### Key Files
- `Archipelago.RiskOfRain2/Handlers/ItemPoolHandler.cs` — initialization
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — create handler in `SetupRun()`, pass seed + slot data

## Phase 3: AP Expansion Items

### New AP Items

| AP Item | Item ID | Effect |
|---------|---------|--------|
| "White Pool Expansion" | 37101 | Adds N white items to pool |
| "Green Pool Expansion" | 37102 | Adds N green items to pool |
| "Red Pool Expansion" | 37103 | Adds N red items to pool |
| "Boss Pool Expansion" | 37104 | Adds N boss items to pool |
| "Lunar Pool Expansion" | 37105 | Adds N lunar items to pool |
| "Void Pool Expansion" | 37106 | Adds N void items to pool (SOTV) |
| "Equipment Pool Expansion" | 37107 | Adds N equipment to pool |

### Processing
```csharp
public void HandlePoolExpansion(long itemId)
{
    int tierIndex = (int)(itemId - 37100);
    int itemsPerExpansion = GetItemsPerExpansion(tierIndex);

    switch (tierIndex)
    {
        case 1: // White
            int start = allowedWhite.Count;
            for (int i = start; i < start + itemsPerExpansion && i < shuffledWhite.Count; i++)
                allowedWhite.Add(shuffledWhite[i]);
            whiteExpansions++;
            break;
        // ... other tiers
    }
}
```

### Item ID Range
```csharp
private const long poolRangeLower = 37100;
private const long poolRangeUpper = 37199;
```

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — add pool range to `EnqueueItem()`, add `poolReceivedQueue`
- `Archipelago.RiskOfRain2/Handlers/ItemPoolHandler.cs` — expansion logic

## Phase 4: Persistence Across Runs

The pool state must persist across runs within the same AP session, like stage unlocks.

### Tasks
1. Store expansion counts in `ArchipelagoClient`'s cached session state
2. On `CleanupRun()`: save current expansion counts
3. On `SetupRun()`: rebuild pool from seed + total expansions received
4. On `ProcessAllReceivedItems()`: replay pool expansion items

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — cache pool state in `CleanupRun()`, restore in `SetupRun()`

## Phase 5: Item Class Toggles

The user wants to disable entire item classes:

| Toggle | Effect |
|--------|--------|
| Lunar Off | No lunar items ever drop; no Lunar Pool Expansion items generated |
| Void Off | No void items (also controlled by `dlc_sotv`) |
| Boss Off | Boss items removed from pool (bosses drop green items instead) |
| Equipment Off | No equipment drops |

### Implementation
- When a class is toggled off: set starting pool to 0, generate 0 expansion items, items of that class never enter the allowed set
- The `enable_lunar` option already exists in `options.py` — extend pattern for other classes

## Phase 6: UI

### Chat Messages
When a pool expansion is received, show newly unlocked items in chat with tier-colored names:
```
[AP] White item pool expanded! Now available: Soldier's Syringe, Tougher Times
[AP] Red item pool expanded! Now available: Brilliant Behemoth
```

Use `ItemCatalog.GetItemDef(itemIndex).nameToken` resolved via `Language.GetString()` for display names.

### Console Command
Add `ap_pool` console command:
```
White:  8/36  [Soldier's Syringe, Tougher Times, Crowbar, ...]
Green:  5/42  [Ukulele, AtG Missile Mk. 1, ...]
Red:    2/36  [Brilliant Behemoth, 57 Leaf Clover]
...
```

### HUD (Optional)
Objective panel entry showing pool sizes: "Item Pool: White 8/36, Green 5/42, Red 2/36"

## Python AP World Changes

### items.py
```python
pool_offset: int = offset + 100  # 37100

pool_table: Dict[str, RiskOfRainItemData] = {
    "White Pool Expansion":     RiskOfRainItemData("Pool", 1 + pool_offset, ItemClassification.useful),
    "Green Pool Expansion":     RiskOfRainItemData("Pool", 2 + pool_offset, ItemClassification.useful),
    "Red Pool Expansion":       RiskOfRainItemData("Pool", 3 + pool_offset, ItemClassification.progression),
    "Boss Pool Expansion":      RiskOfRainItemData("Pool", 4 + pool_offset, ItemClassification.useful),
    "Lunar Pool Expansion":     RiskOfRainItemData("Pool", 5 + pool_offset, ItemClassification.filler),
    "Void Pool Expansion":      RiskOfRainItemData("Pool", 6 + pool_offset, ItemClassification.useful),
    "Equipment Pool Expansion": RiskOfRainItemData("Pool", 7 + pool_offset, ItemClassification.useful),
}
```

### options.py
```python
class ItemPoolLimiting(Toggle):
    """Restrict which items can drop. AP checks expand the available pool."""
    display_name = "Item Pool Limiting"
    default = False

class StartingWhitePool(Range):
    """Number of white items available at run start."""
    display_name = "Starting White Pool"
    range_start = 1
    range_end = 36
    default = 5

class ItemsPerWhiteExpansion(Range):
    """Number of white items added per Pool Expansion check."""
    display_name = "Items Per White Expansion"
    range_start = 1
    range_end = 8
    default = 2

# Similar for Green, Red, Boss, Lunar, Void, Equipment...

class EnableLunarItems(Toggle):
    """Allow lunar items in the item pool."""
    display_name = "Enable Lunar Items"
    default = False

class EnableBossItems(Toggle):
    """Allow boss items in the item pool."""
    display_name = "Enable Boss Items"
    default = True
```

### __init__.py — create_items()
When `itemPoolLimiting` is enabled:
```python
if self.options.item_pool_limiting:
    total_items_in_tier = 36  # white count
    starting = self.options.starting_white_pool.value
    per_expansion = self.options.items_per_white_expansion.value
    num_expansions = math.ceil((total_items_in_tier - starting) / per_expansion)
    for _ in range(num_expansions):
        item_pool.append("White Pool Expansion")
    # Repeat for each enabled tier...
```

### fill_slot_data()
Send all pool config to C# client:
```python
slot_data["itemPoolLimiting"] = self.options.item_pool_limiting.value
slot_data["startingWhitePool"] = self.options.starting_white_pool.value
slot_data["itemsPerWhiteExpansion"] = self.options.items_per_white_expansion.value
# ... per tier
```

## Configuration Options

| Slot Data Key | Type | Range | Default |
|---------------|------|-------|---------|
| `itemPoolLimiting` | Toggle | - | off |
| `startingWhitePool` | Range | 1-36 | 5 |
| `startingGreenPool` | Range | 1-42 | 3 |
| `startingRedPool` | Range | 0-36 | 1 |
| `startingBossPool` | Range | 0-22 | 1 |
| `startingLunarPool` | Range | 0-20 | 0 (disabled) |
| `startingVoidPool` | Range | 0-14 | 0 (requires SOTV) |
| `startingEquipmentPool` | Range | 1-34 | 3 |
| `itemsPerWhiteExpansion` | Range | 1-8 | 2 |
| `itemsPerGreenExpansion` | Range | 1-8 | 2 |
| `itemsPerRedExpansion` | Range | 1-4 | 1 |
| `enableLunarItems` | Toggle | - | off |
| `enableBossItems` | Toggle | - | on |

## Testing Criteria

1. **Pool restriction**: Enable pool limiting. Verify only starting pool items drop from chests and shrines
2. **Expansion**: Receive "White Pool Expansion". Verify new items now appear in chest drops
3. **Printer filtering**: Verify 3D Printers only offer items currently in the pool
4. **Scrapper filtering**: Verify Scrappers only show pool items (or: scrap produces white/green/red scrap regardless)
5. **Persistence**: Die and restart run in same AP session. Verify pool state persists (same items available)
6. **Determinism**: Two runs with same AP seed produce identical starting pools
7. **Lunar disable**: Set `enableLunarItems=false`, verify no lunar items drop from any source
8. **Full expansion**: Receive all expansion items for a tier. Verify all items of that tier become available
9. **Disabled mode**: Set `itemPoolLimiting=false`, verify completely normal drop behavior
10. **Artifact of Command**: Verify Command Essence selection grid only shows pool items
11. **Void item pairing**: If a base item is not in the pool but its void counterpart is, verify behavior is reasonable

## Risks and Open Questions

- **Void item corruption**: Void items corrupt specific base items. If the base item isn't in the pool but the void counterpart is, the corruption can't happen naturally. Options: (a) always pair void items with their base items, (b) allow void items independently, (c) only add void items when their base item is already in the pool
- **Artifact of Command UI**: Need to also filter the Command grid. Hook `PickupPickerController.SetOptionsFromInteractor` or similar to remove non-pool items
- **Boss items**: Boss items drop from specific bosses with a 15% chance. Should pool limiting affect which boss item drops, or just whether a boss item drops at all? **Recommendation:** Filter the boss item selection to pool-only items
- **Multishop terminals**: Terminals show 3 items of the same tier. Need to verify the pool filter applies to terminal item generation
- **Performance**: `GenerateWeightedSelection` is called frequently. `HashSet<ItemIndex>.Contains()` is O(1), so this should be fine
- **Multiplayer sync**: The host runs AP and has pool state. Clients see items from chests — since the host controls what drops, clients should see correct items. But the Command grid is client-side — may need a network message to sync the pool
- **Scavenger backpacks**: Scavenger loot uses a different drop path. Need to verify the hook catches it (LootPoolLimiter hooks `ChestBehavior.BaseItemDrop` for this)
- **Halcyonite Shrine**: Uses `PickupPickerController.GenerateOptionsFromDropTablePlusForcedStorm`. Need additional hook from LootPoolLimiter pattern
