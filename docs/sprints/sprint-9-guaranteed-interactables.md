# Sprint 9: Guaranteed Interactable Unlocks

**Priority:** Medium — adds meaningful progression, builds on existing spawn patterns
**Complexity:** Low-Medium
**New AP Item IDs:** 37150-37169 (Interactable Unlock items)
**Depends On:** Sprint 1

## Goal

Add AP items that guarantee specific interactable types spawn on every stage. In vanilla RoR2, Scrappers, 3D Printers, Cauldrons, Cleansing Pools, etc. are random — they may or may not appear. Receiving an AP "Guaranteed Scrapper" item means a Scrapper will always spawn, giving the player reliable access to crafting stations. This adds meaningful progression without being overpowered.

## RoR2 Interactable Types

### Crafting / Economy Interactables

| Interactable | SpawnCard (verify) | Function | Value as AP Item |
|-------------|-------------------|----------|-----------------|
| Scrapper | `iscScrapper` | Convert items to scrap (same tier) | **High** — enables item economy |
| 3D Printer (White) | `iscDuplicator` | Trade white scrap for specific white item | **High** — targeted item acquisition |
| 3D Printer (Green) | `iscDuplicatorLarge` | Trade green scrap for specific green item | **High** |
| 3D Printer (Red) | `iscDuplicatorMilitary` | Trade red scrap for specific red item | **Medium** — rare normally |
| 3D Printer (Boss/Yellow) | `iscDuplicatorWild` | Trade yellow scrap for specific boss item | **Low** — very rare |
| Cleansing Pool | `iscShrineCleanse` | Convert lunar items to pearls | **Medium** |
| Lunar Cauldron (W→G) | `iscLunarCauldron, White` | Trade 3 white for 1 green | **Medium** |
| Lunar Cauldron (G→R) | `iscLunarCauldron, Green` | Trade 5 green for 1 red | **Medium** |
| Lunar Cauldron (R→W×3) | `iscLunarCauldron, Red` | Trade 1 red for 3 white | **Low** |
| Void Cradle | `iscVoidChest` | Void item source | **Medium** (SOTV) |

### Other Spawnable Interactables

| Interactable | Function | Value as AP Item |
|-------------|----------|-----------------|
| Rusty Lockbox | Opens with Rusted Key item | **Low** |
| Shrine of Order | Randomizes inventory | **Trap potential** |
| Multi-Shop Terminal | Buy from 3 choices | **Medium** |
| Equipment Barrel | Equipment drop | **Medium** |

## Phase 1: Core Guaranteed Spawn System

### Existing Pattern
`LocationHandler.cs` already guarantees Radio Tower spawns using `DirectorCore.instance.TrySpawnObject()` after `SceneDirector.PopulateScene`:

```csharp
private void SceneDirector_PopulateScene(orig, self)
{
    orig(self);  // vanilla spawning first
    if (0 < checkAvailable(LocationTypes.radio_scanner))
    {
        DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(
            LegacyResourcesAPI.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscRadarTower"),
            new DirectorPlacementRule { placementMode = DirectorPlacementRule.PlacementMode.Random },
            new Xoroshiro128Plus(self.rng.nextUlong)));
    }
}
```

This exact pattern works for Scrappers, Printers, etc.

### Tasks
1. Create `Handlers/InteractableGuaranteeHandler.cs`
2. Maintain a `HashSet<string>` of guaranteed interactable spawn card names
3. Hook `SceneDirector.PopulateScene` — after `orig()`, spawn one of each guaranteed type
4. Use `DirectorCore.instance.TrySpawnObject()` with `PlacementMode.Random`

### Implementation
```csharp
public class InteractableGuaranteeHandler
{
    private HashSet<string> guaranteedSpawnCards = new();

    // SpawnCard resource paths for each interactable
    private static readonly Dictionary<string, string> spawnCardPaths = new()
    {
        { "scrapper",       "SpawnCards/InteractableSpawnCard/iscScrapper" },
        { "printer_white",  "SpawnCards/InteractableSpawnCard/iscDuplicator" },
        { "printer_green",  "SpawnCards/InteractableSpawnCard/iscDuplicatorLarge" },
        { "printer_red",    "SpawnCards/InteractableSpawnCard/iscDuplicatorMilitary" },
        { "cleansing_pool", "SpawnCards/InteractableSpawnCard/iscShrineCleanse" },
        // ... etc
    };

    public void Hook()
    {
        On.RoR2.SceneDirector.PopulateScene += SceneDirector_PopulateScene;
    }

    private void SceneDirector_PopulateScene(orig, self)
    {
        orig(self);
        foreach (var cardKey in guaranteedSpawnCards)
        {
            if (spawnCardPaths.TryGetValue(cardKey, out var path))
            {
                var spawnCard = LegacyResourcesAPI.Load<SpawnCard>(path);
                if (spawnCard != null)
                {
                    DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(
                        spawnCard,
                        new DirectorPlacementRule { placementMode = DirectorPlacementRule.PlacementMode.Random },
                        new Xoroshiro128Plus(self.rng.nextUlong)));
                }
            }
        }
    }

    public void UnlockInteractable(string key)
    {
        guaranteedSpawnCards.Add(key);
        Log.LogDebug($"Guaranteed interactable unlocked: {key}");
    }
}
```

### Key Files
- New: `Archipelago.RiskOfRain2/Handlers/InteractableGuaranteeHandler.cs`

## Phase 2: AP Items

### New AP Items

| AP Item | Item ID | Guarantees |
|---------|---------|-----------|
| "Guaranteed: Scrapper" | 37150 | 1 Scrapper per stage |
| "Guaranteed: White Printer" | 37151 | 1 White 3D Printer per stage |
| "Guaranteed: Green Printer" | 37152 | 1 Green 3D Printer per stage |
| "Guaranteed: Red Printer" | 37153 | 1 Red 3D Printer per stage |
| "Guaranteed: Cleansing Pool" | 37154 | 1 Cleansing Pool per stage |
| "Guaranteed: Cauldron" | 37155 | 1 Lunar Cauldron per stage |
| "Guaranteed: Multi-Shop" | 37156 | 1 Multi-Shop Terminal per stage |
| "Guaranteed: Equipment Barrel" | 37157 | 1 Equipment Barrel per stage |
| "Guaranteed: Void Cradle" | 37158 | 1 Void Cradle per stage (SOTV) |

### Item ID Range
```csharp
private const long interactableRangeLower = 37150;
private const long interactableRangeUpper = 37199;
```

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — add interactable range, queue, processing

## Phase 3: Persistence

Guaranteed interactable state persists across runs within the same AP session.

### Tasks
1. Store unlocked set in cached session state
2. Restore on `SetupRun()`
3. Replay via `ProcessAllReceivedItems()`

## Phase 4: Boss Stage Exclusions

Some stages should NOT get guaranteed interactables:
- **Commencement**: No standard interactable spawning
- **Bazaar Between Time**: Fixed layout
- **Void Fields**: Special cell-based encounters
- **Gilded Coast**: Fixed layout
- **Solutional Haunt**: Boss only
- **A Moment, Fractured/Whole**: Special areas

### Implementation
```csharp
private static readonly HashSet<string> excludedScenes = new()
{
    "moon", "moon2", "bazaar", "arena", "goldshores",
    "mysteryspace", "limbo", "artifactworld",
    "voidraid", "meridian", "solusweb", "solutionalhaunt"
};

private void SceneDirector_PopulateScene(orig, self)
{
    orig(self);
    string currentScene = SceneManager.GetActiveScene().name;
    if (excludedScenes.Contains(currentScene)) return;
    // ... spawn guaranteed interactables
}
```

## Python AP World Changes

### items.py
```python
interactable_offset: int = offset + 150  # 37150

interactable_table: Dict[str, RiskOfRainItemData] = {
    "Guaranteed: Scrapper":        RiskOfRainItemData("Interactable", 0 + interactable_offset, ItemClassification.useful),
    "Guaranteed: White Printer":   RiskOfRainItemData("Interactable", 1 + interactable_offset, ItemClassification.useful),
    "Guaranteed: Green Printer":   RiskOfRainItemData("Interactable", 2 + interactable_offset, ItemClassification.useful),
    "Guaranteed: Red Printer":     RiskOfRainItemData("Interactable", 3 + interactable_offset, ItemClassification.progression),
    "Guaranteed: Cleansing Pool":  RiskOfRainItemData("Interactable", 4 + interactable_offset, ItemClassification.filler),
    "Guaranteed: Cauldron":        RiskOfRainItemData("Interactable", 5 + interactable_offset, ItemClassification.useful),
    "Guaranteed: Multi-Shop":      RiskOfRainItemData("Interactable", 6 + interactable_offset, ItemClassification.useful),
    "Guaranteed: Equipment Barrel":RiskOfRainItemData("Interactable", 7 + interactable_offset, ItemClassification.filler),
    "Guaranteed: Void Cradle":     RiskOfRainItemData("Interactable", 8 + interactable_offset, ItemClassification.useful),
}
```

### options.py
```python
class GuaranteedInteractables(Toggle):
    """Add AP items that guarantee crafting stations spawn every stage."""
    display_name = "Guaranteed Interactables"
    default = False
```

### __init__.py
When enabled, add all interactable items to the pool (gate Void Cradle by `dlc_sotv`).

No logic rules needed — these are quality-of-life items, not progression gates.

## Configuration Options

| Slot Data Key | Type | Default |
|---------------|------|---------|
| `guaranteedInteractables` | Toggle | off |

## UI/UX

- Chat message on unlock: "Guaranteed spawn unlocked: **Scrapper** will now appear every stage!" in cyan
- Console command `ap_interactables` to list guaranteed spawns

## Testing Criteria

1. **Scrapper guarantee**: Receive "Guaranteed: Scrapper". Verify a Scrapper spawns on every subsequent stage
2. **Multiple guarantees**: Receive Scrapper + White Printer. Verify both spawn
3. **Boss stage exclusion**: Verify no guaranteed spawns on Commencement, Bazaar, etc.
4. **Persistence**: Die, start new run. Verify guarantees persist
5. **Stacking with natural spawns**: If a Scrapper was going to spawn anyway, verify you get 2 (the natural one + the guaranteed one), not just 1
6. **Disabled**: Set `guaranteedInteractables=false`. Verify no interactable items generated
7. **Placement**: Verify `TrySpawnObject` with `PlacementMode.Random` places interactables in valid, reachable locations

## Risks and Open Questions

- **SpawnCard paths**: The exact resource paths for each interactable need runtime verification. The Radio Tower path is confirmed (`iscRadarTower`), but Scrapper/Printer paths are educated guesses. Launch game, iterate `DirectorCardCategorySelection` categories to get exact names.
- **Placement failures**: `TrySpawnObject` can fail if no valid node graph position is found. This is unlikely but possible on cramped stages. Add a retry with different RNG or log a warning.
- **Balance**: Guaranteed Scrapper + Printer every stage is extremely powerful for item optimization. This is intentional — it's an AP reward — but may trivialize the item economy for experienced players. The slot data toggle controls whether these items exist in the pool.
- **Interaction with Artifact of Command**: When Command is active, Printers don't spawn (vanilla behavior). Should the guarantee override this? **Recommendation:** No — respect vanilla artifact rules. If Command is active, printer guarantees are ignored.
- **Multiple of same type**: If the player receives "Guaranteed: White Printer" but the stage already has one, they get 2 White Printers. This is fine — more options.
- **Lunar Cauldron type**: There are 3 types of Lunar Cauldron (White→Green, Green→Red, Red→3White). Should the guarantee pick one randomly, or guarantee all three? **Recommendation:** One random type per stage. A future enhancement could add separate items for each type.
