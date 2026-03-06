# Sprint 2: Stage Check Prioritization

**Priority:** High — simple QoL improvement, quick win after Sprint 1
**Complexity:** Low
**New AP Item IDs:** None
**Depends On:** Sprint 1 (all DLC stages registered with correct IDs)

## Goal

Strengthen the existing stage-weighting system so that when traversing the teleporter, stages with uncompleted AP checks are strongly preferred over — or exclusively selected before — stages with no remaining checks. This prevents players from being RNG-trapped on completed stages while locations remain elsewhere.

## Current Implementation

`LocationHandler.cs` already has a `SceneCollection.AddToWeightedSelection` hook (lines 619-648) that adds +5 weight per remaining location in each candidate environment. This creates a soft bias but doesn't guarantee check-bearing stages are selected.

The user wants a stronger version: stages with checks left should **always** be selected before stages without, even if that means stages appear out of their normal tier order.

## Phase 1: Enhance Existing Weight Hook

### Tasks
1. After the existing `orig(self, dest, canAdd)` call and the current +5 weight loop, add a second pass
2. In the second pass: check if ANY stage in the weighted selection has remaining checks
3. If yes AND priority mode is "hard": set weight to near-zero (0.001f) for all stages with ZERO remaining checks
4. If no stages have remaining checks: leave weights as-is (normal play resumes once all checks done)

### Pseudocode
```csharp
private void SceneCollection_AddToWeightedSelection(orig, self, dest, canAdd)
{
    orig(self, dest, canAdd);
    if (dest == null || priorityMode == PriorityMode.Off) return;

    bool anyHasChecks = false;

    // First pass: add soft weight bonus and detect if any stage has checks
    for (int i = 0; i < dest.Count; i++)
    {
        string stageName = dest.choices[i].value.cachedName;
        int envIndex = GetSceneIndex(stageName);
        CatchUpSceneLocations(stageName);
        if (currentlocations.TryGetValue(envIndex, out var locs))
        {
            int remaining = locs.total();
            if (remaining > 0) anyHasChecks = true;
            if (priorityMode >= PriorityMode.Soft)
                dest.ModifyChoiceWeight(i, dest.choices[i].weight + remaining * 5);
        }
    }

    // Second pass (hard mode): zero out stages with no remaining checks
    if (anyHasChecks && priorityMode >= PriorityMode.Hard)
    {
        for (int i = 0; i < dest.Count; i++)
        {
            string stageName = dest.choices[i].value.cachedName;
            int envIndex = GetSceneIndex(stageName);
            if (!currentlocations.TryGetValue(envIndex, out var locs) || locs.total() == 0)
            {
                dest.ModifyChoiceWeight(i, 0.001f);
            }
        }
    }
}
```

### Key Files
- `Archipelago.RiskOfRain2/Handlers/LocationHandler.cs` — modify `SceneCollection_AddToWeightedSelection`

## Phase 2: Configuration Option

Add a slot data option to control prioritization strength:

| Value | Name | Behavior |
|-------|------|----------|
| 0 | Off | No weight adjustment — vanilla stage selection |
| 1 | Soft | Current behavior: +5 weight per remaining location (additive) |
| 2 | Hard | Stages with no checks get near-zero weight when stages with checks exist |

### Tasks
1. Parse `stageCheckPriority` from slot data in `ArchipelagoClient.cs`
2. Pass value to `LocationHandler` (static field or constructor parameter)
3. Use the value in the enhanced weight hook

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — parse slot data
- `Archipelago.RiskOfRain2/Handlers/LocationHandler.cs` — consume setting

## Phase 3: Edge Cases

### Tasks
1. **All checks done at current tier**: When all stages at the current tier have completed checks, leave weights as-is (normal selection resumes)
2. **Loop behavior**: After a full loop (Stage 5 → Stage 1 again), all first-pass checks are already done. Verify the hook handles this gracefully — stages should still be selectable
3. **StageBlockerHandler interaction**: Both `StageBlockerHandler.Run_CanPickStage` and this hook manipulate stage selection. Verify they don't conflict (StageBlockerHandler blocks stages, this hook weights among non-blocked stages)
4. **Performance**: `CatchUpSceneLocations` queries the AP session. Cache results per scene per teleporter event to avoid repeated lookups

## Python AP World Changes

Add to `worlds/ror2/options.py`:
```python
class StageCheckPriority(Choice):
    """Controls how strongly the game prioritizes stages with uncompleted checks.
    Off: vanilla stage selection.
    Soft: stages with checks get bonus weight.
    Hard: stages without checks are nearly excluded when stages with checks exist."""
    display_name = "Stage Check Priority"
    option_off = 0
    option_soft = 1
    option_hard = 2
    default = 1
```

Add to `fill_slot_data()` in `__init__.py`:
```python
slot_data["stageCheckPriority"] = self.options.stage_check_priority.value
```

Add to `ROR2Options` dataclass and explore mode option group.

## Configuration Options

| Slot Data Key | Type | Values | Default |
|---------------|------|--------|---------|
| `stageCheckPriority` | Choice | 0=off, 1=soft, 2=hard | 1 (soft) |

## UI/UX

- When hard mode zeroes out a stage, log a debug message: "Stage [name] skipped (no remaining checks)"
- Consider a chat message at teleporter completion: "Prioritizing stages with remaining checks" (only once per session)
- Seer portals in the Bazaar should reflect the prioritization visually (available seers point to check-bearing stages)

## Testing Criteria

1. **Soft mode**: Play several teleporter transitions with `stageCheckPriority=1`. Verify stages with more checks appear more frequently
2. **Hard mode**: Complete all checks in Stage 1 environments. With `stageCheckPriority=2`, verify Stage 1 environments with no checks are never selected when other Stage 1 environments have checks remaining
3. **All complete**: Complete all checks everywhere. Verify normal stage selection resumes with no errors
4. **Loop behavior**: Complete a full loop. Verify stages are still selectable on the second pass
5. **Off mode**: Set `stageCheckPriority=0`. Verify no weight manipulation occurs and vanilla RNG applies
6. **Interaction with stage blocking**: Block a stage that has checks. Verify it stays blocked (StageBlockerHandler takes precedence)

## Risks and Open Questions

- **Performance**: `CatchUpSceneLocations` makes session queries. If called frequently (e.g., the hook fires multiple times per scene transition), cache the results
- **StageBlockerHandler conflict**: Both hooks manipulate `WeightedSelection`. The order they run matters. Since `AddToWeightedSelection` is a MonoMod On hook, the last-registered hook wraps the first. Verify the execution order is correct (LocationHandler should run after StageBlockerHandler removes blocked stages)
- **Cross-tier prioritization**: The user mentioned wanting stages with checks "even if that means stages would be out of order." This is harder — it would require overriding the tier-based scene collection entirely. For now, prioritization only works within the same tier. Cross-tier routing would be Sprint 6 (Stage Randomizer) territory
