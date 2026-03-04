# Sprint 3: Better Portal / Hidden Realm Control

**Priority:** Medium — enables Obliteration victory, improves Bazaar access
**Complexity:** Medium
**New AP Item IDs:** 37600-37609 (Portal Unlock items)
**Depends On:** Sprint 1 (all stages/scenes registered)

## Goal

Decouple portal access from the environment unlock system. Currently, portals are blocked as a side-effect of stage blocking in `StageBlockerHandler` — if the Bazaar Between Time isn't unlocked as an environment, the Blue Portal is blocked. This sprint adds independent AP items for portal unlocks, makes Bazaar access more reliable, and ensures the Obliteration victory path (A Moment, Whole) works properly.

## Current Portal Blocking

Portal blocking happens in `StageBlockerHandler.cs`:
- `TeleporterInteraction_AttemptToSpawnAllEligiblePortals1` (lines 571-609): blocks Blue/Gold portals based on environment block status
- `Interactor_PerformInteraction` (lines 381-444): blocks Void Fields, Void Locus, Gilded Coast portal interactions
- `FrogController_Pet` (lines 446-467): blocks Planetarium access via frog
- `PortalDialerController_PerformActionServer` (lines 469-491): blocks Bulwark's Ambry
- `EntityStates.LunarTeleporter.Active.OnEnter` (lines 502-518): blocks Commencement via lunar teleporter
- `MSObelisk.TransitionToNextStage.FixedUpdate` (lines 497-533): blocks/redirects A Moment, Whole

All of these currently check `CheckBlocked(sceneName)` which uses the environment unlock list. The portal itself has no independent unlock status.

## Phase 1: Independent Portal Unlock Items

### New AP Items

| Portal Type | AP Item Name | Item ID | Classification | DLC |
|-------------|-------------|---------|----------------|-----|
| Blue Portal → Bazaar | "Portal: Bazaar" | 37601 | progression | Base |
| Gold Portal → Gilded Coast | "Portal: Gilded Coast" | 37602 | useful | Base |
| Celestial Portal → A Moment, Fractured | "Portal: Celestial" | 37603 | progression | Base |
| Null Portal → Void Fields | "Portal: Void Fields" | 37604 | useful | Base |
| Deep Void Portal → Planetarium | "Portal: Planetarium" | 37605 | progression | SOTV |
| Colossus Portal → Prime Meridian | "Portal: Prime Meridian" | 37606 | progression | SOTS |
| Mainline Portal → Neural Sanctum | "Portal: Neural Sanctum" | 37607 | progression | AC |

### Tasks
1. Add `portalUnlocks` dictionary to `StageBlockerHandler`:
   ```csharp
   public static Dictionary<string, bool> portalUnlocks = new()
   {
       { "bazaar", false },
       { "goldshores", false },
       { "mysteryspace", false },  // A Moment, Fractured
       { "arena", false },          // Void Fields
       { "voidraid", false },       // Planetarium
       { "meridian", false },       // Prime Meridian
       { "solusweb", false },       // Neural Sanctum
   };
   ```
2. Add `UnlockPortal(int portalIndex)` method
3. Add portal range constants to `ArchipelagoItemLogicController.cs`:
   ```csharp
   private const long portalRangeLower = 37600;
   private const long portalRangeUpper = 37699;
   ```
4. Add `portalReceivedQueue` and handle in `EnqueueItem()` and `RoR2Application_Update()`
5. Modify all portal blocking hooks to check `portalUnlocks[sceneName]` when portal mode is active

### Key Files
- `Archipelago.RiskOfRain2/Handlers/StageBlockerHandler.cs` — add `portalUnlocks`, modify all portal hooks
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — add portal range, queue, processing

## Phase 2: Bazaar Reliability

The Bazaar is the primary way players navigate to hidden realms and choose their next stage (via Lunar Seers). Making it more accessible is key to the AP experience.

### Tasks
1. **Guaranteed Newt Altar**: Already partially implemented — `SceneObjectToggleGroup_Awake` forces min/max to 1/2. Verify this works with portal mode
2. **Guaranteed Blue Portal**: When "Portal: Bazaar" is unlocked, hook `TeleporterInteraction` to guarantee the shop portal spawns:
   ```csharp
   // After teleporter event, if Bazaar is unlocked, ensure Blue Portal spawns
   On.RoR2.TeleporterInteraction.AttemptToSpawnShopPortal += (orig, self) =>
   {
       if (portalMode == PortalMode.Portals && portalUnlocks["bazaar"])
       {
           // Force spawn by setting shouldAttemptToSpawnShopPortal = true
           self.shouldAttemptToSpawnShopPortal = true;
       }
       orig(self);
   };
   ```
3. **Lunar Coin refund**: When Newt Altar is used but the portal was already going to spawn, consider refunding the coin

### Key Files
- `Archipelago.RiskOfRain2/Handlers/StageBlockerHandler.cs`

## Phase 3: Obliteration Victory Path

Verify the full A Moment, Whole → Obliteration → Victory pipeline:

### Tasks
1. Verify case 3 (Limbo) victory condition works end-to-end in `ArchipelagoClient.cs`
2. Consider adding `RoR2Content.GameEndings.ObliterationEnding` as acceptable for Limbo victory (currently commented out at line 229 in ArchipelagoClient.cs)
3. With portal mode: Celestial Portal unlock should be independent of environment unlock
4. Verify Beads of Fealty redirect (Obliteration → A Moment, Whole) works when portal is unlocked

### Flow
```
Stage 5 Teleporter → Celestial Portal appears (if unlocked)
  → A Moment, Fractured → Obelisk
  → Without Beads: Obliterate (ObliterationEnding)
  → With Beads: A Moment, Whole → Limbo (LimboEnding)
```

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — victory conditions, `IsEndingAcceptable()`

## Phase 4: Portal Mode Configuration

### Portal Modes

| Value | Name | Behavior |
|-------|------|----------|
| 0 | Environments | Current behavior — portals gated by environment unlock (backward compatible) |
| 1 | Portals | Portals gated by independent portal unlock items |
| 2 | Open | All portals always available from the start |

### Tasks
1. Parse `portalMode` from slot data in `ArchipelagoClient.cs`
2. Pass to `StageBlockerHandler`
3. In each portal blocking hook, branch on portal mode:
   - Mode 0: check `CheckBlocked(sceneName)` (current behavior)
   - Mode 1: check `portalUnlocks[sceneName]`
   - Mode 2: always allow

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — parse slot data
- `Archipelago.RiskOfRain2/Handlers/StageBlockerHandler.cs` — branch on mode

## Python AP World Changes

### items.py
```python
portal_offset: int = offset + 600  # 37600

portal_table: Dict[str, RiskOfRainItemData] = {
    "Portal: Bazaar":           RiskOfRainItemData("Portal", 1 + portal_offset, ItemClassification.progression),
    "Portal: Gilded Coast":     RiskOfRainItemData("Portal", 2 + portal_offset, ItemClassification.useful),
    "Portal: Celestial":        RiskOfRainItemData("Portal", 3 + portal_offset, ItemClassification.progression),
    "Portal: Void Fields":      RiskOfRainItemData("Portal", 4 + portal_offset, ItemClassification.useful),
    "Portal: Planetarium":      RiskOfRainItemData("Portal", 5 + portal_offset, ItemClassification.progression),
    "Portal: Prime Meridian":   RiskOfRainItemData("Portal", 6 + portal_offset, ItemClassification.progression),
    "Portal: Neural Sanctum":   RiskOfRainItemData("Portal", 7 + portal_offset, ItemClassification.progression),
}
```

### options.py
```python
class PortalMode(Choice):
    """Controls how portal access is gated.
    Environments: portals are gated by environment unlocks (legacy behavior).
    Portals: portals have their own independent unlock items.
    Open: all portals are always available."""
    display_name = "Portal Mode"
    option_environments = 0
    option_portals = 1
    option_open = 2
    default = 0
```

### regions.py / rules.py
- When `portalMode=portals`: add access rules requiring portal items for hidden realm regions
- Example: "Bazaar Between Time" region requires `state.has("Portal: Bazaar", player)`
- When `portalMode=environments`: keep current environment-based rules
- When `portalMode=open`: no portal-related access rules

### __init__.py
- In `create_items()`: when `portalMode=portals`, add portal items to the pool
- Gate DLC portals by DLC toggles (Planetarium requires `dlc_sotv`, etc.)

## Configuration Options

| Slot Data Key | Type | Values | Default |
|---------------|------|--------|---------|
| `portalMode` | Choice | 0=environments, 1=portals, 2=open | 0 |

## UI/UX

- Chat message when portal unlocked: "Portal to **[Destination]** is now accessible!" in a themed color (blue for Bazaar, gold for Gilded Coast, purple for Void, etc.)
- Seer stations in the Bazaar should respect portal unlocks (seers for locked portals should be disabled)
- Consider an objective panel entry: "Portals unlocked: 3/7"

## Testing Criteria

1. **Portal mode (portals)**: Generate seed with `portalMode=1`. Verify all portals blocked until portal items received
2. **Bazaar access**: Receive "Portal: Bazaar". Verify Newt Altar spawns, Blue Portal spawns after teleporter
3. **Obliteration path**: Receive "Portal: Celestial" + "Beads of Fealty". Reach A Moment, Whole. Verify victory=3 (Limbo) sends goal
4. **Environment mode**: Set `portalMode=0`. Verify backward-compatible behavior
5. **Open mode**: Set `portalMode=2`. Verify all portals accessible from Stage 1
6. **DLC portals**: Enable SOTV, verify Planetarium portal only available after receiving "Portal: Planetarium"
7. **Multiplayer**: Verify portal unlock state syncs to non-host players (via network messages or host-authoritative)
8. **Gold portal**: Verify Altar of Gold purchase still required even when "Portal: Gilded Coast" is unlocked (the portal item unlocks the portal type, not the trigger condition)

## Risks and Open Questions

- **Portal unlock vs environment unlock**: Do portal unlocks REPLACE environment unlocks for hidden realms, or are BOTH required? **Recommendation:** In portal mode, portal items control portal access. Environment unlocks only control ordered-stage (Stage 1-5) selection. Hidden realms are portal-only.
- **Celestial Portal timing**: Celestial Portal only appears every 3rd stage after completing a loop. Should the portal unlock also guarantee it appears, or just allow it when it naturally spawns? **Recommendation:** Just allow — don't override the loop timing.
- **Frog interaction**: The frog on Commencement leads to Planetarium. Does "Portal: Planetarium" unlock the frog path? **Recommendation:** Yes.
- **Void Fields access**: Currently reached via Null Portal under the Bazaar. Requires both Bazaar access AND Void Fields portal access. Both portal items needed.
- **Bazaar Seer destinations**: Seers show specific stages. Should seers for portals show portal destinations, or only ordered stages? Currently seers only show ordered stages.
