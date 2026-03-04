# Sprint 7: Skill Swap Randomizer

**Priority:** Last — highest complexity, standalone feature
**Complexity:** Very High
**New AP Item IDs:** 38500-38699 (Skill Unlock items)
**Depends On:** Sprint 1
**Reference:** [SkillSwap mod](https://thunderstore.io/package/pseudopulse/SkillSwap/)

## Goal

Each survivor starts with a randomized single skill per slot. AP checks unlock additional skill options for specific survivor/slot combinations. Phase 1 is per-survivor skill locking (your skills, but restricted to 1 per slot). Phase 2 (future) is cross-survivor skill swapping (Commando gets Mercenary's sword, etc.).

## RoR2 Skill System

### Architecture
- `SkillLocator` component on each survivor body: references `GenericSkill` components for each slot
- Each `GenericSkill` has a `SkillFamily` containing `SkillFamily.Variant[]`
- Each `Variant` has a `SkillDef` and an `UnlockableDef` (vanilla unlock requirement)
- Slots: Passive, Primary (M1), Secondary (M2), Utility (Shift), Special (R)

### Survivors and Skill Counts (18 survivors)

| Survivor | Primary | Secondary | Utility | Special | Notes |
|----------|---------|-----------|---------|---------|-------|
| Commando | 2 | 2 | 2 | 2 | |
| Huntress | 2 | 2 | 2 | 2 | |
| Bandit | 2 | 2 | 2 | 2 | |
| MUL-T | 2+2 | 2 | 2 | 2 | 2 primary slots (swap mechanic) |
| Engineer | 2 | 2 | 2 | 2 | Turrets are special |
| Artificer | 2 | 2 | 2 | 2 | |
| Mercenary | 2 | 2 | 2 | 2 | Melee hitboxes |
| REX | 2 | 2 | 2 | 2 | |
| Loader | 2 | 2 | 2 | 2 | Grapple mechanics |
| Acrid | 2 | 2 | 2 | 2 | |
| Captain | 2 | 2 | 2 | 2 | |
| Railgunner | 2 | 2 | 2 | 2 | SOTV |
| Void Fiend | 2 | 2 | 2 | 2 | SOTV, dual form |
| Seeker | 2 | 2 | 2 | 2 | SOTS |
| False Son | 2 | 2 | 2 | 2 | SOTS |
| CHEF | 2 | 2 | 2 | 2 | SOTS |
| Operator | 2 | 2 | 2 | 2 | AC |
| Drifter | 2 | 2 | 2 | 2 | AC |

**Note:** Exact skill variant counts need runtime verification. Most survivors have 2 variants per slot (default + alternate). Some may have 3+.

**Approximate total:** 18 survivors x 4 slots x ~1 unlockable variant = ~72 skill unlock items (each survivor starts with 1 skill per slot; the rest are unlockable).

## Phase 1: Skill System Research

### Tasks
1. Enumerate all survivors via `SurvivorCatalog`
2. For each survivor, enumerate all `GenericSkill` components via `SkillLocator`
3. For each `GenericSkill`, enumerate the `SkillFamily.Variant[]` array
4. Build a reference table: Survivor → Slot → List of SkillDefs
5. Identify which skills require DLC
6. Count total unlockable skills (total variants - 1 starting per slot per survivor)

### Runtime Discovery Code
```csharp
foreach (var survivorDef in SurvivorCatalog.allSurvivorDefs)
{
    var body = survivorDef.bodyPrefab;
    var skillLocator = body.GetComponent<SkillLocator>();
    if (skillLocator == null) continue;

    Log.LogDebug($"Survivor: {survivorDef.cachedName}");
    LogSkillSlot("Primary", skillLocator.primary);
    LogSkillSlot("Secondary", skillLocator.secondary);
    LogSkillSlot("Utility", skillLocator.utility);
    LogSkillSlot("Special", skillLocator.special);
}

void LogSkillSlot(string slotName, GenericSkill skill)
{
    if (skill?.skillFamily == null) return;
    Log.LogDebug($"  {slotName}: {skill.skillFamily.variants.Length} variants");
    foreach (var variant in skill.skillFamily.variants)
    {
        Log.LogDebug($"    - {variant.skillDef.skillName} (unlock: {variant.unlockableDef?.cachedName ?? "none"})");
    }
}
```

### Key Files
- New: `Archipelago.RiskOfRain2/Lookup/SkillData.cs` — enumeration and reference data

## Phase 2: Skill Restriction System (Per-Survivor)

### Tasks
1. Create `Handlers/SkillPoolHandler.cs`
2. At session start, for each survivor, use AP seed to deterministically assign ONE starting skill per slot
3. Maintain `Dictionary<string, Dictionary<string, HashSet<string>>>` mapping `survivorName → slotName → allowedSkillNames`
4. Hook into the loadout/skill system to enforce restrictions

### Restriction Approach — Option A: Loadout Override
Hook `Loadout.BodyLoadoutManager.BodyLoadout.ToXml` or `CharacterSelectController` to filter available skills in character select:
```csharp
// At character select, only show allowed skills
On.RoR2.Loadout.BodyLoadoutManager.BodyLoadout.SetSkillVariant += (orig, self, slotIndex, variant) =>
{
    if (!IsSkillVariantAllowed(self.bodyIndex, slotIndex, variant))
        return; // Block selection of locked variants
    orig(self, slotIndex, variant);
};
```

### Restriction Approach — Option B: Runtime Override
Hook `CharacterBody.Start` to force skill assignment after spawn:
```csharp
private void CharacterBody_Start(orig, self)
{
    orig(self);
    if (!NetworkServer.active) return;

    var skillLocator = self.GetComponent<SkillLocator>();
    var survivorDef = SurvivorCatalog.FindSurvivorDefFromBody(self.gameObject);
    if (survivorDef == null || skillLocator == null) return;

    RestrictSlot(skillLocator.primary, survivorDef, "Primary");
    RestrictSlot(skillLocator.secondary, survivorDef, "Secondary");
    RestrictSlot(skillLocator.utility, survivorDef, "Utility");
    RestrictSlot(skillLocator.special, survivorDef, "Special");
}

private void RestrictSlot(GenericSkill skill, SurvivorDef survivor, string slotName)
{
    if (skill?.skillFamily == null) return;
    var allowed = GetAllowedSkills(survivor.cachedName, slotName);
    // If current skill not allowed, swap to first allowed one
    if (!allowed.Contains(skill.skillDef.skillName))
    {
        foreach (var variant in skill.skillFamily.variants)
        {
            if (allowed.Contains(variant.skillDef.skillName))
            {
                skill.SetBaseSkill(variant.skillDef);
                break;
            }
        }
    }
}
```

**Recommendation:** Use both approaches — Option A prevents selection in lobby, Option B enforces at spawn as a safety net.

### Key Files
- New: `Archipelago.RiskOfRain2/Handlers/SkillPoolHandler.cs`

## Phase 3: AP Skill Unlock Items

### Item ID Scheme
Use ID range 38500-38699. Since item IDs and location IDs are separate namespaces in Archipelago, no conflict.

### Item Naming Pattern
```
"Skill: [Survivor] [Slot]"
```
Examples: "Skill: Commando Primary", "Skill: Huntress Utility", "Skill: MUL-T Primary"

Each item unlocks the NEXT skill variant in the deterministic order for that survivor/slot. If Commando has 2 Primary skills and starts with skill A, receiving "Skill: Commando Primary" unlocks skill B.

### ID Assignment
```python
skill_offset = 38500
# Assign IDs sequentially per survivor per slot
id_counter = 0
for survivor in sorted_survivors:
    for slot in ["Primary", "Secondary", "Utility", "Special"]:
        variants = get_variants(survivor, slot)
        for variant_idx in range(1, len(variants)):  # skip starting variant
            skill_table[f"Skill: {survivor} {slot}"] = RiskOfRainItemData(
                "Skill", skill_offset + id_counter, ItemClassification.useful
            )
            id_counter += 1
```

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — add skill range, queue
- `worlds/ror2/items.py` — skill item table

## Phase 4: Persistence

Skill unlock state persists across runs within the same AP session.

### Tasks
1. Store unlocked skill set in cached session state
2. On `CleanupRun()`: save skill state
3. On `SetupRun()`: restore from cached state
4. On `ProcessAllReceivedItems()`: replay skill unlock items

## Phase 5: Cross-Survivor Skill Swap (Future / Advanced)

**This phase is significantly more complex and should be a separate sprint.**

Cross-survivor skill swapping means giving Commando a Mercenary sword swing, or Huntress a Loader grapple. This requires:

### Technical Challenges
1. **Melee hitboxes**: Many melee skills reference survivor-specific hitbox groups. Giving Commando a melee skill requires adding a hitbox to his model
2. **Projectile origins**: Skills that fire from specific bones (e.g., Engineer's turret placement from hands) need origin point mapping
3. **Passive ability dependencies**: Some skills assume passive abilities (e.g., Void Fiend's corruption mechanic affects all skills)
4. **Animation states**: Skills trigger specific animations. Cross-survivor skills may not have matching animations
5. **Scale differences**: Melee ranges assume specific model scales

### Reference: SkillSwap Mod
The SkillSwap mod solves these problems with:
- `HitboxRefit` system: Adds/modifies hitbox groups per survivor body
- Passive patches: Strips passive-dependent behavior when skills are moved
- Animation fallbacks: Uses generic animation states when survivor-specific ones don't exist

### Recommendation
Ship Phase 1 (per-survivor) first. Evaluate complexity of cross-survivor swapping as a separate sprint after the skill system infrastructure is in place.

## Python AP World Changes

### items.py
```python
skill_offset: int = 38500

# Generated dynamically based on skill data
# This requires a static skill data file or runtime generation
skill_table: Dict[str, RiskOfRainItemData] = {
    "Skill: Commando Primary":   RiskOfRainItemData("Skill", skill_offset + 0, ItemClassification.useful),
    "Skill: Commando Secondary": RiskOfRainItemData("Skill", skill_offset + 1, ItemClassification.useful),
    "Skill: Commando Utility":   RiskOfRainItemData("Skill", skill_offset + 2, ItemClassification.useful),
    "Skill: Commando Special":   RiskOfRainItemData("Skill", skill_offset + 3, ItemClassification.useful),
    "Skill: Huntress Primary":   RiskOfRainItemData("Skill", skill_offset + 4, ItemClassification.useful),
    # ... ~72 total items
}
```

### options.py
```python
class SkillRandomizer(Toggle):
    """Restrict skill variants per survivor. AP checks unlock more skill options."""
    display_name = "Skill Randomizer"
    default = False
```

### __init__.py
```python
if self.options.skill_randomizer:
    for skill_name, skill_data in skill_table.items():
        # Filter by enabled DLCs
        if is_dlc_skill(skill_name) and not dlc_enabled(skill_name):
            continue
        item_pool.append(skill_name)
```

### Slot Data
```python
slot_data["skillRandomizer"] = self.options.skill_randomizer.value
# Starting skill assignments communicated via seed (deterministic)
```

## Configuration Options

| Slot Data Key | Type | Default | Notes |
|---------------|------|---------|-------|
| `skillRandomizer` | Toggle | off | |
| `excludePassiveSlot` | Toggle | on | Don't randomize passive slot |

## UI/UX

- Chat message on skill unlock: "Skill unlocked: **Commando** - Phase Round (Primary)!" in skill-themed color
- Character select screen: locked skills shown grayed out with lock icon
- Loadout screen: only available (unlocked) skills can be selected
- Console command `ap_skills` to list unlocked skills per survivor

## Testing Criteria

1. **Restriction**: Enable skill randomizer. Verify each survivor has exactly 1 skill per active slot
2. **Unlock**: Receive "Skill: Commando Primary". Verify the alternate primary becomes selectable
3. **All unlocked**: Receive all skill items for a survivor. Verify full skill access
4. **MUL-T**: Verify MUL-T's dual primary slots are both restricted (or decide to leave one unrestricted)
5. **Loadout persistence**: Select a skill, start a run, die, return to lobby. Verify selection persists
6. **Character select**: Verify locked skills cannot be selected in the character select loadout
7. **Multiplayer**: Verify skill restrictions apply per-player (each player has their own AP session)
8. **Disabled**: Set `skillRandomizer=false`. Verify full skill access for all survivors
9. **DLC survivors**: Disable SOTV. Verify Railgunner/Void Fiend skill items not generated

## Risks and Open Questions

- **This is the highest-complexity feature.** Per-survivor skill locking alone requires hooks into the loadout system, character body initialization, and skill family manipulation. Budget extra time for debugging.
- **Loadout system persistence**: RoR2's loadout system persists between runs and even between game sessions (saved to disk). Need to hook the loadout save/load to apply AP restrictions. If a player's saved loadout includes a locked skill, it must be overridden.
- **Heresy items**: Lunar items that replace skills (Visions of Heresy, Hooks of Heresy, Strides of Heresy, Essence of Heresy). Do these bypass skill restrictions? **Recommendation:** Yes — Heresy items always work, since they replace the skill entirely. The player made a conscious choice to pick them up.
- **Passive slot**: Not all survivors have meaningful passive skill variations. Many have exactly 1 passive. **Recommendation:** Exclude passive slot from randomization by default (configurable).
- **MUL-T special case**: Has 2 primary skill slots and a "Retool" swap mechanic. Options: (a) restrict both primary slots independently, (b) treat both as one "Primary" unlock, (c) leave MUL-T's primaries unrestricted. **Recommendation:** (b) one unlock covers both.
- **Void Fiend special case**: Has a corruption mechanic that changes skill behavior at high corruption. The corrupted forms are NOT separate SkillDefs — they're runtime state changes. This should work fine with per-survivor skill locking.
- **Skill prerequisites**: Some alternate skills require completing vanilla challenges (e.g., "Kill 15 enemies in a single stage as Huntress"). AP skill unlock should BYPASS these vanilla unlock requirements. Need to set `variant.unlockableDef = null` for AP-unlocked variants or check AP state in addition to vanilla state.
- **Network authority**: Skill selection is replicated via loadout system. The host validates loadouts. Need to ensure AP skill restrictions are checked on the host, not just the client. In single-player/host context, this is automatic.
- **Skill count verification**: The assumed ~72 items is based on 18 survivors x 4 slots x 1 extra variant. Actual counts may differ (some survivors may have 3+ variants per slot with DLC). Phase 1 research must produce exact numbers.
