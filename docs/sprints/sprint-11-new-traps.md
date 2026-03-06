# Sprint 11: New Traps

**Priority:** Medium — adds variety and excitement to the trap pool
**Complexity:** Medium
**New AP Item IDs:** 37405-37410 (New trap items)
**Depends On:** None

## Goal

Expand the trap pool with new, creative traps that go beyond simple stat penalties. The current traps (Mountain, Time Warp, Combat, Teleport) are solid but limited. New traps should create memorable moments — items flying out of your hands, surprise boss fights, and chaotic encounters that make receiving traps feel dramatic rather than just punishing.

## Current Trap Pool

| Trap | ID | Weight | Effect |
|------|----|--------|--------|
| Mountain Trap | 37401 | 5 | Adds a Mountain Shrine stack to the teleporter |
| Time Warp Trap | 37402 | 20 | Advances run timer by 180 seconds |
| Combat Trap | 37403 | 20 | Spawns a wave of enemies at the player |
| Teleport Trap | 37404 | 10 | Teleports all players to a random map location |

## New Trap 1: Butterfingers Trap

**ID:** 37405
**Weight:** 15
**Effect:** The player drops 1-3 random items from their inventory. Items scatter across the map as pickup droplets that anyone can reclaim.

### Design

- Pick 1-3 items from the player's inventory at random
- Remove them from the inventory via `Inventory.RemoveItem()`
- Spawn them as pickup droplets using `PickupDropletController.CreatePickupDroplet()` launched outward from the player with random velocity vectors
- Items land somewhere on the map as ground pickups — recoverable but you have to go find them
- In multiplayer, scattered items are free-for-all (teammates can grab your stuff)

### Implementation

```csharp
private void ButterfingerssTrap()
{
    foreach (var player in PlayerCharacterMasterController.instances)
    {
        if (!player.master.hasBody) continue;
        var inventory = player.master.inventory;
        var body = player.master.GetBody();

        // Collect all non-empty item stacks
        var heldItems = new List<ItemIndex>();
        foreach (ItemIndex idx in ItemCatalog.allItems)
        {
            int count = inventory.GetItemCount(idx);
            if (count > 0) heldItems.Add(idx);
        }

        if (heldItems.Count == 0) return;

        int dropCount = UnityEngine.Random.Range(1, Math.Min(4, heldItems.Count + 1));
        for (int i = 0; i < dropCount; i++)
        {
            int roll = UnityEngine.Random.Range(0, heldItems.Count);
            ItemIndex toDrop = heldItems[roll];
            heldItems.RemoveAt(roll);

            inventory.RemoveItem(toDrop, 1);

            // Launch item in a random direction
            PickupIndex pickupIdx = PickupCatalog.FindPickupIndex(toDrop);
            Vector3 velocity = UnityEngine.Random.insideUnitSphere * 30f;
            velocity.y = Mathf.Abs(velocity.y) + 10f;  // always launch upward

            PickupDropletController.CreatePickupDroplet(pickupIdx, body.corePosition, velocity);
        }
    }
}
```

### Tuning Considerations

- **Which items can be dropped?** Options:
  - All items including Lunar/Void/Boss (harsher)
  - Only White and Green (gentler — recommended default)
  - Configurable via item tier filter
- **Drop count:** 1-3 feels impactful without being devastating. Could scale with total item count (e.g., drop ~5% of total items, min 1 max 5)
- **Recovery radius:** Items launch 20-40 units away — far enough to be inconvenient, close enough to be recoverable
- **Multiplayer drama:** Your Red item just landed near your teammate. Do they give it back? Emergent social gameplay

## New Trap 2: Alloy Worship Unit Boss Fight

**ID:** 37406
**Weight:** 5
**Effect:** Spawns an Alloy Worship Unit (the flying boss from Siren's Call) at the player's location for a surprise mid-stage boss encounter.

### Design

- Spawn an Alloy Worship Unit using `DirectorCore.TrySpawnObject()` or `MasterSummon`
- The boss spawns at the player's current position
- No additional rewards — this is purely a punishment
- Low weight (5) because this is a significant threat

### Implementation

```csharp
private void AlloyWorshipUnitTrap()
{
    var playerBody = GetLocalPlayerBody();
    if (playerBody == null) return;

    var spawnCard = Resources.Load<CharacterSpawnCard>("SpawnCards/CharacterSpawnCards/cscSuperRoboBallBoss");
    // Alloy Worship Unit = SuperRoboBallBoss

    var spawnRequest = new DirectorSpawnRequest(spawnCard, new DirectorPlacementRule
    {
        placementMode = DirectorPlacementRule.PlacementMode.NearestNode,
        position = playerBody.corePosition
    }, RoR2Application.rng);

    spawnRequest.teamIndexOverride = TeamIndex.Monster;

    DirectorCore.instance.TrySpawnObject(spawnRequest);
    ChatMessage.SendColored("Something stirs above...", "#FF4444");
}
```

### Notes

- Spawn card name needs runtime verification — may be `cscSuperRoboBallBoss` or similar
- The boss scales with current difficulty coefficient automatically
- Consider playing a warning sound effect before spawn (1-2 second delay) so the player can react

## New Trap 3: Aurelionite Fight

**ID:** 37407
**Weight:** 5
**Effect:** Spawns Aurelionite (the Gilded Coast boss, a massive gold golem) at the player's location.

### Design

- Spawn Aurelionite using its spawn card (`cscTitanGold` or similar)
- The boss appears at the player's position
- Aurelionite is tankier than Alloy Worship Unit — this is a serious threat
- Very low weight because Aurelionite is a major boss encounter
- Killing it does NOT drop the Halcyon Seed (no reward, purely punishment)

### Implementation

```csharp
private void AurelioniteTrap()
{
    var playerBody = GetLocalPlayerBody();
    if (playerBody == null) return;

    var spawnCard = Resources.Load<CharacterSpawnCard>("SpawnCards/CharacterSpawnCards/cscTitanGold");

    var spawnRequest = new DirectorSpawnRequest(spawnCard, new DirectorPlacementRule
    {
        placementMode = DirectorPlacementRule.PlacementMode.NearestNode,
        position = playerBody.corePosition
    }, RoR2Application.rng);

    spawnRequest.teamIndexOverride = TeamIndex.Monster;

    DirectorCore.instance.TrySpawnObject(spawnRequest);
    ChatMessage.SendColored("The gold stirs to life...", "#FFD700");
}
```

### Notes

- Spawn card name needs runtime verification
- Consider gating behind a minimum stage or difficulty to avoid one-shotting early-game players with zero counterplay
- If Aurelionite proves too brutal, consider making it a "mini" version with reduced HP via `DeathRewards` or `HealthComponent` override

## Additional Trap Ideas (Future)

These could be added in a later pass or as community picks:

### Meteor Trap
**Effect:** Triggers a Meteor Storm (same as the Glowing Meteorite equipment). Meteors rain down on the map for 20 seconds, damaging everything (including the player).
**Spawn card:** Can be triggered via `MeteorStormController` — the game already has this system built in for the equipment item.

### Fog Trap
**Effect:** Triggers the Void Fog effect for 30 seconds, creating a shrinking safe zone that damages players outside it. Creates urgency and chaos.

### Doppelganger Trap
**Effect:** Spawns a shadow clone of the player (like the Artifact of Vengeance umbra) that has the player's items and attacks them. A mirror match.

### Economy Trap
**Effect:** Drains 50-75% of the player's current gold. Simple but effective, especially on stages with expensive chests.

## Python AP World Changes

### items.py
```python
trap_table: Dict[str, RiskOfRainItemData] = {
    "Lunar Item":           RiskOfRainItemData("Trap", 6 + offset, ItemClassification.trap, 16),
    "Mountain Trap":        RiskOfRainItemData("Trap", 1 + trap_offset, ItemClassification.trap, 5),
    "Time Warp Trap":       RiskOfRainItemData("Trap", 2 + trap_offset, ItemClassification.trap, 20),
    "Combat Trap":          RiskOfRainItemData("Trap", 3 + trap_offset, ItemClassification.trap, 20),
    "Teleport Trap":        RiskOfRainItemData("Trap", 4 + trap_offset, ItemClassification.trap, 10),
    # New traps
    "Butterfingers Trap":   RiskOfRainItemData("Trap", 5 + trap_offset, ItemClassification.trap, 15),
    "Alloy Worship Trap":   RiskOfRainItemData("Trap", 6 + trap_offset, ItemClassification.trap, 5),
    "Aurelionite Trap":     RiskOfRainItemData("Trap", 7 + trap_offset, ItemClassification.trap, 5),
}
```

### Trap ID conflict note
The existing "Lunar Item" trap uses ID `37006` (6 + offset), NOT in the 37400 trap range. The new traps use 37405-37407 which are clean.

## C# Changes

### ArchipelagoItemLogicController.cs
Add new trap handlers to `HandleReceivedTrapQueueItem()`:
```csharp
case 37405:
    ButterfingersTrap();
    break;
case 37406:
    AlloyWorshipUnitTrap();
    break;
case 37407:
    AurelioniteTrap();
    break;
```

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — new trap handler methods + dispatch
- `worlds/ror2/items.py` — new trap item definitions

## Configuration Options

| Slot Data Key | Type | Range | Default |
|---------------|------|-------|---------|
| `butterfingersDropCount` | Range | 1-5 | 3 |
| `butterfingersAllTiers` | Toggle | - | off (White+Green only) |

## Testing Criteria

1. **Butterfingers**: Receive trap. Verify 1-3 items removed from inventory and appear as pickups on the map
2. **Butterfingers recovery**: Pick up the dropped items. Verify they return to inventory correctly
3. **Butterfingers empty inventory**: Receive trap with 0 items. Verify no crash, trap is silently consumed
4. **Butterfingers multiplayer**: Verify items drop from all local players. Verify any player can pick up dropped items
5. **Alloy Worship Unit**: Receive trap. Verify boss spawns near the player and is hostile
6. **Aurelionite**: Receive trap. Verify boss spawns near the player and is hostile
7. **Boss scaling**: Verify spawned bosses scale with current difficulty coefficient
8. **Boss no reward**: Verify killing trap-spawned bosses does not grant special drops (no Halcyon Seed, etc.)
9. **Trap weights**: Over many items, verify new traps appear at roughly their configured weight ratios
10. **Cooldowns**: Verify boss traps have cooldown protection to prevent immediate back-to-back spawns

## Risks and Open Questions

- **Spawn card names**: All boss spawn card names need runtime verification. The names used above (`cscSuperRoboBallBoss`, `cscTitanGold`) are educated guesses based on RoR2 naming conventions
- **Butterfingers item tier filtering**: Dropping a player's only Red item feels very punishing. Defaulting to White+Green only is safer. Consider making tier filtering configurable
- **Boss traps on early stages**: An Aurelionite on Stage 1 with no items is almost certain death. Consider a minimum difficulty threshold (e.g., only spawn after stage 3 or difficulty coefficient > 2.0). Alternatively, queue the trap and delay it until the threshold is met
- **Addressable assets**: Modern RoR2 (post-SOTV) moved many assets to Addressables. `Resources.Load` may not work — may need `Addressables.LoadAssetAsync<CharacterSpawnCard>()` instead. Verify at runtime
- **Network sync**: Boss spawns should be server-authoritative. In multiplayer, only the host should execute the spawn. `PickupDropletController.CreatePickupDroplet()` is already networked
- **Butterfingers + Artifact of Command**: With Command active, dropped items become command essences. Players could re-pick, turning the trap into a free item reroll. This is arguably a fun interaction and not a bug
