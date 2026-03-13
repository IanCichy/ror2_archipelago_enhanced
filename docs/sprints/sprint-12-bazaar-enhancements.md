# Sprint 12: Bazaar Enhancements

**Priority:** Medium — high fun factor, adds multiworld integration depth and QoL
**Complexity:** Medium
**New AP Location IDs:** 5 bazaar shop slots (within existing allocation or new range)
**New AP Item IDs:** None
**Depends On:** Sprint 1

## Goal

Transform the Bazaar Between Time from a passive lunar shop into a proper multiworld hub. Two enhancements:

1. **AP Shop Checks** — Replace the 5 lunar shop terminals with AP location checks. Purchasing a terminal sends a location check to the AP server, delivering an item to another player (or yourself). This mirrors how shops work in other AP games like Hollow Knight's Iselda shop.

2. **Utility Stations** — Spawn guaranteed utility interactables in the Bazaar: a Scrapper, a Cleansing Pool, and a Drone Combiner. These give players reliable access to item management tools whenever they visit the Bazaar, making it feel like a true hub world.

## Part A: AP Shop Checks

### Current Bazaar Shop Behavior

The Bazaar has 5 `ShopTerminalBehavior` terminals that sell Lunar items for Lunar Coins. Each terminal shows a Lunar item and costs 2 Lunar Coins. The Bazaar also has 3 Seer stations and a Newt NPC.

### Design

- On Bazaar scene load, intercept the 5 shop terminals
- Replace each terminal's displayed item with an AP location check name
- Replace the cost with a configurable amount (default: 1 Lunar Coin per check, or free)
- On purchase, send the AP location check instead of granting a Lunar item
- Display the recipient player name and item name on the terminal tooltip
- Each visit to the Bazaar refreshes with new unchecked locations (if any remain)

### C# Implementation

#### Hook Points

```csharp
// In a new BazaarShopHandler or within LocationHandler
On.RoR2.ShopTerminalBehavior.GenerateNewPickupServer += ShopTerminalBehavior_GenerateNewPickupServer;
On.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;
```

#### Terminal Replacement Flow

```csharp
private void SceneCatalog_OnActiveSceneChanged(orig, self, oldScene, newScene)
{
    // ... existing logic ...
    if (newScene.name == "bazaar")
    {
        SetupBazaarShopChecks();
    }
}

private void SetupBazaarShopChecks()
{
    // Find all ShopTerminalBehavior instances in the Bazaar
    var terminals = GameObject.FindObjectsOfType<ShopTerminalBehavior>();

    // Get unchecked locations from the AP session
    var unchecked = session.Locations.AllMissingLocations
        .Where(id => IsBazaarShopLocation(id))
        .Take(terminals.Length)
        .ToList();

    for (int i = 0; i < terminals.Length && i < unchecked.Count; i++)
    {
        // Override terminal display and behavior
        var terminal = terminals[i];
        var purchase = terminal.GetComponent<PurchaseInteraction>();

        // Scout the location to show what item it contains
        // Display: "[PlayerName]'s [ItemName]"
        purchase.cost = bazaarCheckCost; // configurable
        purchase.contextToken = $"Send check to {playerName}";
    }
}
```

#### Key Challenges

- **Item display**: Need to scout locations via `session.Locations.ScoutLocationsAsync()` to show what each terminal will send and to whom
- **Terminal visuals**: May need to change the hologram display or hide it and rely on the context text
- **Multiplayer sync**: Terminal state needs to sync across clients; use existing `SyncLocationCheckProgress` pattern

### Python AP World Changes

#### locations.py — New Bazaar Location IDs

```python
bazaar_shop_offset = 38700  # or within existing allocation

bazaar_shop_table: Dict[str, int] = {
    "Bazaar Shop Item 1": bazaar_shop_offset + 0,
    "Bazaar Shop Item 2": bazaar_shop_offset + 1,
    "Bazaar Shop Item 3": bazaar_shop_offset + 2,
    "Bazaar Shop Item 4": bazaar_shop_offset + 3,
    "Bazaar Shop Item 5": bazaar_shop_offset + 4,
}
```

#### regions.py — Bazaar Region Access

The Bazaar region already exists and is accessible from OrderedStage_1. Bazaar shop locations should be added to the Bazaar region.

```python
# In create_regions():
bazaar_region.locations.extend(bazaar_shop_table.keys())
```

#### options.py

```python
class BazaarShopChecks(Toggle):
    """Replace Bazaar lunar shop terminals with AP location checks."""
    display_name = "Bazaar Shop Checks"
    default = False
```

### Open Questions — Part A

- **Cost**: Should bazaar checks cost Lunar Coins (1-2 each), regular gold, or be free? Lunar coins add strategic cost; free makes it purely about reaching the Bazaar.
- **Refreshing**: Do the 5 shop slots refresh each time you visit the Bazaar, or are they one-time checks? Recommendation: one-time (5 total checks across the game), consistent with how AP locations work.
- **Location count**: 5 checks matches the terminal count. Could also be configurable (e.g., `bazaar_shop_count` option in YAML).
- **Terminal visuals**: Can we change the hologram to show an AP icon or the recipient's item? Or just use text?

---

## Part B: Utility Stations

### Design

Spawn guaranteed utility interactables at fixed positions in the Bazaar scene. These are always present — no AP unlock required.

| Interactable | Prefab/SpawnCard | Purpose |
|-------------|-----------------|---------|
| Scrapper | `iscScrapper` | Convert items to scrap |
| Cleansing Pool | `iscShrineCleanse` | Convert lunar items to pearls |
| Drone Combiner | Addressables path TBD | Combine drones |

### C# Implementation

```csharp
private void SetupBazaarUtilities()
{
    string sceneName = SceneManager.GetActiveScene().name;
    if (sceneName != "bazaar") return;

    // Spawn each utility at a fixed position in the Bazaar
    // Positions need to be determined at runtime — place them near the shop area
    // but not blocking existing interactables
    SpawnUtility("SpawnCards/InteractableSpawnCard/iscScrapper", bazaarScrapperPosition);
    SpawnUtility("SpawnCards/InteractableSpawnCard/iscShrineCleanse", bazaarCleansePosition);
    // Drone Combiner may need Addressables loading like SeerPortal.cs pattern
}

private void SpawnUtility(string spawnCardPath, Vector3 position)
{
    var spawnCard = LegacyResourcesAPI.Load<SpawnCard>(spawnCardPath);
    if (spawnCard == null) return;

    DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(
        spawnCard,
        new DirectorPlacementRule
        {
            placementMode = DirectorPlacementRule.PlacementMode.Direct,
            spawnOnTarget = CreatePositionTarget(position)
        },
        new Xoroshiro128Plus((ulong)Run.instance.stageRng.nextUlong)));
}
```

### Determining Positions

Bazaar positions need to be found at runtime. Approach:
1. Use a debug command to teleport around the Bazaar and log `transform.position`
2. Find clear areas near the existing shop terminals
3. Hardcode the positions (the Bazaar layout is fixed)

### Open Questions — Part B

- **Drone Combiner spawn card path**: Need to verify the exact SpawnCard or Addressables path for the Drone Combiner at runtime. It may be a SOTS/AC-specific interactable.
- **Cleansing Pool utility**: Is this the right "drone shredder"? The Cleansing Pool converts Lunar items to pearls. If the user meant Equipment Drone recycling, that's a different interactable. Clarify intent.
- **Always present vs AP-gated**: These are designed as always-present QoL. Could optionally be gated behind an AP item (Sprint 9 pattern) if desired.
- **Placement collisions**: Using `PlacementMode.Direct` with hardcoded positions avoids random placement issues but requires finding valid positions that don't clip into Bazaar geometry.

---

## Key Files

### New/Modified Files
| File | Changes |
|------|---------|
| `Handlers/LocationHandler.cs` or new `Handlers/BazaarHandler.cs` | Shop terminal hooks, utility spawning |
| `worlds/ror2/locations.py` | Bazaar shop location IDs |
| `worlds/ror2/regions.py` | Add bazaar shop locations to Bazaar region |
| `worlds/ror2/options.py` | `BazaarShopChecks` toggle |
| `worlds/ror2/__init__.py` | Wire up option and locations |

### Existing Patterns to Reuse
| Pattern | Source File | Usage |
|---------|------------|-------|
| Addressables asset loading | `Handlers/SeerPortal.cs:16` | Loading prefabs for utility stations |
| DirectorCore spawning | `Handlers/LocationHandler.cs` (Radio Tower) | Spawning interactables |
| Location scouting | `ArchipelagoClient.cs` (session.Locations) | Scouting bazaar shop items |
| Scene detection | `LocationHandler.cs` (SceneCatalog_OnActiveSceneChanged) | Detecting Bazaar entry |

## Configuration Options

| Slot Data Key | Type | Default | Description |
|---------------|------|---------|-------------|
| `bazaarShopChecks` | Toggle | off | Replace lunar terminals with AP checks |
| `bazaarCheckCost` | Range (0-5) | 1 | Lunar coin cost per bazaar check |
| `bazaarUtilities` | Toggle | on | Spawn Scrapper/Cleansing Pool/Drone Combiner |

## Testing Criteria

1. **Shop replacement**: Enable `bazaarShopChecks`. Visit Bazaar. Verify 5 terminals show AP check info instead of Lunar items
2. **Purchase sends check**: Buy a terminal. Verify the AP location check is sent and the recipient receives their item
3. **Scouted display**: Verify each terminal shows the recipient player name and item name
4. **Cost**: Verify terminals cost the configured Lunar Coin amount
5. **Exhaustion**: Complete all 5 bazaar checks. Visit Bazaar again. Verify terminals are disabled or show "checked"
6. **Utility spawns**: Visit Bazaar. Verify Scrapper, Cleansing Pool, and Drone Combiner are present
7. **Utility function**: Use each utility station. Verify they work normally (scrap items, cleanse, combine drones)
8. **Disabled**: Set `bazaarShopChecks=false`. Verify normal Lunar shop behavior
9. **Multiplayer**: Both players visit Bazaar. Verify terminal state syncs correctly
10. **Persistence**: Die, start new run. Visit Bazaar again. Verify completed checks stay completed

## Risks and Open Questions

- **ShopTerminalBehavior hooks**: The exact hooks needed to replace terminal contents depend on how `ShopTerminalBehavior.GenerateNewPickupServer` works internally. May need IL hooks if the method doesn't have clean extension points.
- **Scouting performance**: `ScoutLocationsAsync` is async — need to handle the case where the Bazaar loads before scouting completes. Could pre-scout on connection or cache results.
- **Terminal visuals**: Replacing the hologram item display with text/custom visuals may require Unity UI work. Fallback: hide the hologram entirely and rely on the interaction context text.
- **Drone Combiner availability**: The Drone Combiner may only exist in SOTS/AC DLCs. Need to gate its spawning behind DLC ownership checks.
- **Balance**: 5 free/cheap AP checks per Bazaar visit could be very strong if the Bazaar is visited frequently. The one-time nature (5 total checks, not refreshing) keeps it balanced.
- **Interaction with Sprint 3 (Portal Control)**: If Bazaar portal is AP-locked, players need the Bazaar unlock before they can access these features. This is intentional — the Bazaar unlock becomes more valuable.
