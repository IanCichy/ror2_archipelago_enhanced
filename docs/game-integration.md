# Game Integration

This mod hooks into Risk of Rain 2 using BepInEx's IL hooking system (`On.*` delegates) to intercept and modify game behavior at runtime.

## Hook System

BepInEx + MMHOOK generates `On.*` delegates for every method in RoR2's assemblies. The mod subscribes to these delegates to run custom code before/after the original method:

```csharp
// Subscribe
On.RoR2.Run.BeginGameOver += Run_BeginGameOver;

// Handler receives the original method + all original parameters
void Run_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef def)
{
    // Custom logic before
    if (IsEndingAcceptable(def)) { /* send victory to AP */ }
    // Call original
    orig(self, def);
}
```

All hooks follow the Hook/UnHook pattern. Per-run hooks are managed by `HookGame()` / `UnhookGame()` in `ArchipelagoClient`.

## Hook Points by Category

### Item Drop Interception (`ArchipelagoItemLogicController`)

| Hook | Purpose |
|------|---------|
| `On.RoR2.PickupDropletController.CreatePickupDroplet` | Intercepts all item drops to convert them into AP items. Replaces the dropped item with whatever AP sends back. |
| `On.RoR2.ChestBehavior.ItemDrop` | Tracks chest opens for location check counting |
| `On.RoR2.SceneDirector.Start` | Detects stage load, triggers `isInGame = true` |
| `On.RoR2.SurvivorPodController.OnPassengerExit` | Detects player spawn (pod exit) |
| `On.RoR2.CombatDirector.Awake` | Captures combat director reference for trap spawning |

### Chat & Communication (`ArchipelagoClient`)

| Hook | Purpose |
|------|---------|
| `On.RoR2.UI.ChatBox.SubmitChat` | Intercepts chat messages and forwards to AP server via `SayPacket` |

### Game State (`ArchipelagoClient`)

| Hook | Purpose |
|------|---------|
| `On.RoR2.Run.BeginGameOver` | Detects victory conditions, sends `ClientGoal` status to AP |
| `Run.onRunDestroyGlobal` | Detects run end, triggers `CleanupRun()` |
| `On.RoR2.UI.GameEndReportPanelController.Awake` | Injects Release/Collect buttons into end screen |
| `On.RoR2.SceneObjectToggleGroup.Awake` | Modifies Newt Statue spawn rules (ensures altars spawn) |
| `On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter` | Displays victory condition info at portal dialer |

### Death Link (`DeathLinkHandler`)

| Hook | Purpose |
|------|---------|
| `On.RoR2.CharacterMaster.OnBodyDeath` | Detects player death, sends deathlink to AP |
| `On.RoR2.SceneInfo.Awake` | Subscribes deathlink on scene load |
| `SceneExitController.Begin` | Unsubscribes deathlink on scene exit |

### Location Detection — Explore Mode (`LocationHandler`)

| Hook | Purpose |
|------|---------|
| Various chest/shrine hooks | Tracks per-stage location checks |
| Scene change hooks | Loads location data for current environment |
| Interactable hooks | Detects shrine uses, scanner activations, newt altar interactions |

### Stage Blocking — Explore Mode (`StageBlockerHandler`)

| Hook | Purpose |
|------|---------|
| Stage transition hooks | Blocks access to stages the player hasn't unlocked |
| Seer portal hooks | Spawns portals showing unlocked destinations |

## Item Drop Processing Pipeline

When an item drops in-game (Classic mode):

```
Item physically drops in RoR2
         │
         ▼
PickupDropletController.CreatePickupDroplet intercepted
         │
         ▼
ArchipelagoItemLogicController:
  1. Increment PickedUpItemCount
  2. Check if PickedUpItemCount % ItemPickupStep == 0
         │
         ├── No → Item passes through normally
         │
         ▼ Yes
  3. Send location check to AP server
     session.Locations.CompleteLocationChecksAsync(locationId)
  4. Fire OnItemDropProcessed event
  5. Update progress bar via SyncLocationCheckProgress
         │
         ▼
AP server processes check, may send items back
         │
         ▼
Items received via session.Items callback:
  1. Queued in itemReceivedQueue
  2. Processed on next Update():
     - Parse item ID to determine category
     - 37700-37999: Environment unlock → StageBlockerHandler
     - 37300-37399: Filler (money, exp, lunar coins)
     - 37400-37499: Trap (mountain, time warp, combat, teleport)
     - 37500-37599: Stage progression item
     - Other: Standard RoR2 item → spawn as pickup
```

## Location Check Detection (Explore Mode)

In Explore mode, `LocationHandler` tracks specific interactable types per stage:

```
Player enters a stage
         │
         ▼
LocationHandler.CatchUpSceneLocations(sceneName)
  - Loads LocationInformationTemplate for this environment
  - Sets available check counts (chests, shrines, etc.)
         │
         ▼
Player interacts with chest/shrine/scanner/altar/scavenger
         │
         ▼
LocationHandler detects interaction via hooked events
  - Decrements remaining count for that location type
  - Sends location check to AP: session.Locations.CompleteLocationChecksAsync(locationId)
  - Updates per-environment progress UI
         │
         ▼
All locations in stage complete?
  ├── No → Continue
  └── Yes → Send AllChecksCompleteInStage message
```

## Stage Blocking System (Explore Mode)

`StageBlockerHandler` maintains a dictionary of unlocked stages:

```csharp
static Dictionary<string, bool> stageUnlocks = {
    "Stage 1": false,  // Unlocked by receiving Stage 1 item (or start)
    "Stage 2": false,  // Unlocked by receiving Stage 2 item
    "Stage 3": false,
    "Stage 4": false
};
```

When an environment unlock item is received:
1. The environment is marked as accessible
2. If the environment belongs to a new ordered stage tier, `stageUnlocks` is updated
3. Stage transition hooks check `stageUnlocks` to allow/block access

Progressive Stages: When enabled, each "Progressive Stage" item unlocks the next tier sequentially instead of a specific stage.

## Newt Statue Modification

The mod hooks `SceneObjectToggleGroup.Awake` to ensure Newt Statues (which lead to the Bazaar Between Time) always spawn when they can. It sets `minEnabled = 1, maxEnabled = 2` for the Newt Statue toggle group, guaranteeing at least one altar appears in each stage.

## Victory Detection

`Run_BeginGameOver` checks `IsEndingAcceptable()`:

```csharp
bool IsEndingAcceptable(GameEndingDef def)
{
    return acceptableEndings.Contains(def)
        || (finalStageDeath && def == StandardLoss && acceptableLosses.Contains(currentScene))
        || (finalStageDeath && def == ObliterationEnding && acceptableLosses.Contains(currentScene));
}
```

On acceptable ending:
1. Sends `StatusUpdatePacket` with `ClientGoal` status to AP
2. Broadcasts `ArchipelagoEndMessage` to all clients
3. End screen shows Release/Collect buttons based on room permissions

## Release / Collect

After victory, if the AP room permits:
- **Release**: Sends `!release` to AP — gives all your unchecked locations' items to their owners
- **Collect**: Sends `!collect` to AP — gives you all items from locations you haven't checked yet

These appear as UI buttons injected into RoR2's `GameEndReportPanelController`.
