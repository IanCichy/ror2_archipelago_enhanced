# Sprint 1: Full DLC Support

**Priority:** Must Do First — all other sprints depend on this
**Complexity:** Medium
**New AP Item IDs:** None (uses existing environment range 37700+)

## Goal

Complete DLC support for all three expansions: Survivors of the Void (SOTV), Seekers of the Storm (SOTS), and Alloyed Collective (AC/DLC3). The Python AP world already defines environments for all DLCs, but the C# client is missing AC stages entirely, has incomplete SOTS handling, and is missing win conditions for False Son (case 4) and Solus Heart (case 5).

## Prerequisites

- `feature/connection-lifecycle` branch merged to main (or work on top of it)
- RoR2 game with all DLCs installed for testing
- Runtime verification of actual `SceneCatalog` indices for AC stages

## Phase 1: Scene Index Reconciliation (Critical)

The Python world uses sequential IDs (48+ for SOTS, 56+ for AC) defined in `worlds/ror2/ror2environments.py`. The C# code uses actual RoR2 `SceneCatalog.FindSceneIndex()` values in `LocationHandler.cs` and `StageBlockerHandler.cs`. **These do NOT match.**

The C# code already has a workaround: `HandleReceivedEnvironmentQueueItem()` in `ArchipelagoItemLogicController.cs` swaps Void Locus (45) and Planetarium (46) because the Python and C# IDs are reversed. This ad-hoc pattern must be replaced with a proper mapping.

### Tasks
1. Launch RoR2 with all DLCs, log all `SceneCatalog.FindSceneIndex()` values for every scene
2. Document actual C# scene indices vs Python sequential indices in a reference table
3. Add a `PythonIdToCSharpIndex` mapping dictionary in `LocationNames.cs`
4. Replace the Void Locus/Planetarium swap hack with the new dictionary lookup

### Key Files
- `Archipelago.RiskOfRain2/Lookup/LocationNames.cs` — add mapping dictionary
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — replace swap hack in `HandleReceivedEnvironmentQueueItem()`

## Phase 2: Add AC Stages to C# Client

These AC stages exist in Python but have NO C# counterpart:

| Stage | Python ID | Scene Name | Stage Tier | Special |
|-------|-----------|------------|------------|---------|
| Pretender's Precipice | 56 | `nest` | Stage 2 | Normal |
| Iron Alluvium | 57 | `ironalluvium` | Stage 3 | Pre-loop |
| Iron Auroras | 58 | `ironalluvium2` | Stage 3 | Loop variant |
| Repurposed Crater | 59 | `repurposedcrater` | Stage 4 | Normal |
| Conduit Canyon | 60 | `conduitcanyon` | Stage 4 | **No Newt Altar** |
| Solutional Haunt | 61 | `solutionalhaunt` | Stage 5 (boss) | **No standard checks** |
| Neural Sanctum | 62 | `solusweb` | Final | Victory stage only |

### Tasks
1. Add AC scene constants to `LocationHandler.cs` and `StageBlockerHandler.cs`
2. Add entries to `LocationNames.locationsNames` and `LocationNames.cachedLocationsNames`
3. Add AC stages to `LocationHandler.InitialSetupLocationDict()` — **exclude** Solutional Haunt and Neural Sanctum (no standard checks)
4. Add AC stages to `StageBlockerHandler.stageLookup` and `StageBlockerHandler.locationNames`
5. Handle Conduit Canyon: exclude Newt Altar checks for this environment
6. Handle Solutional Haunt: add to StageBlockerHandler for blocking/unblocking only, not LocationHandler

### Key Files
- `Archipelago.RiskOfRain2/Lookup/LocationNames.cs`
- `Archipelago.RiskOfRain2/Handlers/LocationHandler.cs` — `InitialSetupLocationDict()`
- `Archipelago.RiskOfRain2/Handlers/StageBlockerHandler.cs` — `stageLookup`, `locationNames`

## Phase 3: Verify Python Stage Tier Assignments

The Python `regions.py` places Pretender's Precipice in `OrderedStage_1`, but game data indicates it's a Stage 2 environment. Need to verify all AC stage tiers against the actual game.

### Tasks
1. Cross-reference each AC stage's `stageOrder` field in RoR2's `SceneDef`
2. Fix any tier mismatches in both Python (`ror2environments.py`, `regions.py`) and C# (`StageBlockerHandler.stageLookup`)

### Key Files
- `worlds/ror2/ror2environments.py` — environment table assignments
- `worlds/ror2/regions.py` — region connections per stage tier

## Phase 4: Complete Win Conditions

### False Son (Case 4) — Partial Fix
Currently missing `acceptableLosses` for final-stage-death support:
```csharp
case "4":
    acceptableEndings = new[] { DLC2Content.GameEndings.RebirthEndingDef };
    acceptableLosses = new[] { "meridian" };  // ADD THIS
    victoryCondition = "Rebirth";
    break;
```

### Solus Heart (Case 5) — New Implementation
Completely missing, falls through to default:
```csharp
case "5":
    acceptableEndings = new[] { /* DLC3Content.GameEndings.??? */ };
    acceptableLosses = new[] { "solusweb" };
    victoryCondition = "Solus Wing";
    break;
```

**Note:** The actual `GameEndingDef` field name for AC needs runtime verification. Check `DLC3Content.GameEndings` or iterate `GameEndingCatalog` at startup.

### Default "Any" Case
Add AC endings to the default/any case:
- Add AC `GameEndingDef` to `acceptableEndings`
- Add `"solusweb"` to `acceptableLosses`

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — `victory` switch statement (~line 199)
- `worlds/ror2/options.py` — `Victory.option_solus_wing = 5` (already exists)

## Phase 5: Handle Special Stages

### Solutional Haunt (Boss-Only Stage)
- No chests, no shrines, no Newt Altar — boss fight only
- Must NOT appear in `LocationHandler.InitialSetupLocationDict()`
- MUST appear in `StageBlockerHandler` for environment unlock/blocking
- **Python bug:** Currently in `environment_ac_orderedstage_5_table` which generates location checks. Must be moved to `environment_ac_special_table` (like Commencement/Planetarium)

### Neural Sanctum (Victory Stage)
- Like Commencement — no standard checks, victory stage only
- Block/unblock control only via StageBlockerHandler

### Conduit Canyon (No Newt Altar)
- Regular stage with chests and shrines, but no Newt Altar spawns
- Need per-environment altar count override in `LocationHandler.buildTemplateFromSlotData()` or exclude this scene name from altar checks

### Computational Exchange (Bazaar-like, if applicable)
- If this is a shop stage like the Bazaar, it needs the same treatment: no standard checks, time/difficulty paused

### Key Files
- `worlds/ror2/ror2environments.py` — move Solutional Haunt to special table
- `worlds/ror2/locations.py` — exclude special stages from location generation
- `Archipelago.RiskOfRain2/Handlers/LocationHandler.cs` — per-environment altar exclusion

## Phase 6: Portal Blocking for AC Paths

The AC DLC introduces new portal types for reaching Neural Sanctum (Mainline Portal, etc.). Need to add blocking hooks similar to existing Colossus portal handling for SOTS.

### Tasks
1. Identify AC portal hooks (likely similar to `SceneExitController_Begin` for Colossus)
2. Add blocking logic that checks if Neural Sanctum is unlocked before allowing portal entry
3. Add `"solusweb"` and related AC scenes to StageBlockerHandler's portal blocking

### Key Files
- `Archipelago.RiskOfRain2/Handlers/StageBlockerHandler.cs` — portal blocking hooks

## C# Implementation Summary

| File | Changes |
|------|---------|
| `LocationNames.cs` | Add AC scene entries, add `PythonIdToCSharpIndex` mapping |
| `LocationHandler.cs` | Add AC stages to location dict, handle Conduit Canyon altar exclusion |
| `StageBlockerHandler.cs` | Add AC stages to lookups, add AC portal blocking hooks |
| `ArchipelagoClient.cs` | Fix case 4 `acceptableLosses`, add case 5, update default case |
| `ArchipelagoItemLogicController.cs` | Replace ID swap hack with mapping lookup |

## Python Changes Summary

| File | Changes |
|------|---------|
| `ror2environments.py` | Move Solutional Haunt to special table, verify stage tiers |
| `regions.py` | Verify AC stage tier connections |
| `locations.py` | Exclude special stages from location generation |
| `options.py` | Verify `option_solus_wing = 5` exists (already present) |

## Configuration Options

| Option | Type | Default | Notes |
|--------|------|---------|-------|
| `dlc_sotv` | Toggle | off | Already exists |
| `dlc_sots` | Toggle | off | Already exists |
| `dlc_ac` | Toggle | off | Already exists |

No new config options needed — DLC toggles already control environment inclusion.

## Testing Criteria

1. **Scene Index Verification**: Launch with all DLCs, log every scene's `SceneCatalog.FindSceneIndex()`, verify against mapping dictionary
2. **AC Environment Unlocks**: Generate an explore seed with `dlc_ac=true`, verify all AC environments can be unlocked and traveled to
3. **Conduit Canyon**: Verify no Newt Altar check is generated/expected
4. **Solutional Haunt**: Verify no standard location checks on this stage
5. **False Son Victory**: Complete a run defeating False Son with `victory=4`, verify `ClientGoal` status sent
6. **Solus Heart Victory**: Complete a run defeating Solus Heart with `victory=5`, verify `ClientGoal` status sent
7. **Final Stage Death**: Verify `finalStageDeath` works for `"meridian"` and `"solusweb"`
8. **Backward Compatibility**: Generate a seed with no DLCs enabled, verify existing behavior unchanged
9. **Python Tests**: Run `test_false_son_goal` and `test_solus_wing_goal` if test suite exists

## Risks and Open Questions

- **Scene index instability**: RoR2 scene indices can change with game updates. Consider runtime lookup by scene name via `SceneCatalog.FindSceneIndex(sceneName)` instead of hardcoded integers.
- **DLC3 GameEndingDef**: Need to find the actual class/field for AC/Solus Wing ending. May be `DLC3Content.GameEndings.*` — needs runtime verification.
- **Pretender's Precipice tier**: Python has it as `orderedstage_1` but game data may indicate Stage 2. Needs in-game verification.
- **False Son behavior**: False Son doesn't end the game like Mithrix — you can beat him and continue the run. Need to verify how `RebirthEndingDef` is triggered (is it automatic on kill, or does the player choose to leave?).
- **Solutional Haunt in Python**: Currently generates location checks — this is a bug that must be fixed before any AP world using AC DLC is generated.
