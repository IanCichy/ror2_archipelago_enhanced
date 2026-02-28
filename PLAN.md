# Plan: Skill Randomization, Loot Pool Limiting & Survivor Locking for Archipelago RoR2

## Context

The Archipelago Risk of Rain 2 mod currently supports item grants and location checks. We want to add three new features inspired by the SkillSwap and LootPoolLimiter mods:

1. **Skill Randomization** - Randomly assign skills from any survivor to each character's 4 slots. Primary starts unlocked; secondary, utility, and special start locked. AP items unlock them. Any skill can go in any slot (cross-slot chaos).
2. **Loot Pool Limiting** - Start with a small item drop pool per tier and expand it as AP items are received.
3. **Survivor Locking** - Start with 1 survivor unlocked; unlock more survivors as AP items are received. Since passives are untouched by skill randomization, each survivor still feels unique.

Both the mod (C# client) AND the Archipelago world definition (Python server) need changes to define new item types.

**Reference mods studied:**
- [SkillSwap](https://thunderstore.io/package/pseudopulse/SkillSwap/) - decompiled source in `Reference DLLs/`
- [LootPoolLimiter](https://thunderstore.io/package/0n_x/LootPoolLimiter/) - decompiled source in `Reference DLLs/`

---

## Feature 1: Skill Randomization

### New File: `ArchipelagoSkillRandomizer.cs`

**Responsibilities:**
- On run start, collect all `SkillDef`s from all survivors (via `SurvivorCatalog.allSurvivorDefs` -> body prefab -> `SkillLocator` -> `GenericSkill.skillFamily.variants[]`)
- Pool ALL skills into one flat list (cross-slot: any skill, any slot)
- Using a deterministic seed (from slot data), randomly assign 1 skill per slot per survivor
- Primary slot starts **unlocked**; secondary/utility/special start **locked**
- Use RoR2's `GenericSkill.SetSkillOverride()` / `UnsetSkillOverride()` to apply locked/unlocked state on `CharacterBody.Start` (handles spawns, respawns, stage transitions)
- Create a "locked" `SkillDef` placeholder (`ScriptableObject.CreateInstance<SkillDef>()` with 0 stock, infinite cooldown)
- When AP item `"Skill Unlock"` received: increment unlock count, unlock next slot (secondary -> utility -> special), re-apply to all living player bodies

**Key RoR2 APIs:**
- `SurvivorCatalog.allSurvivorDefs` - iterate survivors
- `SkillLocator.primary/secondary/utility/special` - `GenericSkill` components
- `GenericSkill.skillFamily.variants[].skillDef` - collect skills
- `GenericSkill.SetSkillOverride(source, skillDef, SkillOverridePriority.Replacement)` - lock/assign
- `GenericSkill.UnsetSkillOverride(source, skillDef, SkillOverridePriority.Replacement)` - unlock
- `PlayerCharacterMasterController.instances` - iterate players for re-application

**Unlock Order:** Primary (starts unlocked) -> Secondary (1st unlock) -> Utility (2nd) -> Special (3rd)

**Deterministic Seeding:** All players use the same seed from slot data (`skillRandomSeed`) with `new System.Random(seed)` to ensure identical assignments in multiplayer.

### New Net Messages:
- `SyncSkillConfig.cs` - Sent at session start: `(enabled, seed, totalUnlocks, currentUnlockCount)`
- `SyncSkillUnlock.cs` - Sent on each unlock: `(newUnlockCount)`

---

## Feature 2: Loot Pool Limiting

### New File: `ArchipelagoLootPoolController.cs`

**Responsibilities:**
- On run start, snapshot `Run.instance.availableTierXDropList` for each tier
- Using a deterministic seed (from slot data), shuffle each tier's items and select the first N as the starting whitelist
- Remaining items form a pre-computed expansion queue per tier
- Hook `On.RoR2.BasicPickupDropTable.GenerateWeightedSelection` to filter: after `orig()` runs, iterate `self.selector` and call `ModifyChoiceWeight(index, 0f)` for non-whitelisted items
- When AP item `"Item Pool Expansion (White/Green/Red/Boss/Lunar/Equipment)"` received: dequeue next item from that tier's expansion queue, add to whitelist, call `PickupDropTable.RegenerateAll()`

**Key RoR2 APIs:**
- `Run.instance.availableTier1DropList` / `Tier2` / `Tier3` / `Boss` / `LunarCombined` / `Equipment` - item pools
- `On.RoR2.BasicPickupDropTable.GenerateWeightedSelection` - MMHOOK for filtering
- `WeightedSelection<PickupIndex>.GetChoice(index)` / `.ModifyChoiceWeight(index, weight)` / `.Count` - manipulate weights
- `PickupDropTable.RegenerateAll()` - refresh all tables after changes

**Starting Pool Sizes** (configurable via slot data):
- White: 2, Green: 1, Red: 1, Boss: 1, Lunar: 1, Equipment: 1

**Deterministic Seeding:** Same pattern as skills - seed from slot data ensures all players see same whitelist/expansion order.

### New Net Messages:
- `SyncLootPoolConfig.cs` - Sent at session start: `(enabled, startingCounts[6], seed, currentExpansions[6])`
- `SyncLootPoolExpansion.cs` - Sent on expansion: `(tierName, newExpansionCount)`

---

## Feature 3: Survivor Locking (TODO - needs further research)

### New File: `ArchipelagoSurvivorController.cs`

**Concept:**
- Start with 1 survivor unlocked (randomly chosen or configured via slot data)
- All other survivors are locked on the character select screen
- AP item `"Survivor Unlock"` unlocks the next survivor
- Since skill randomization doesn't touch passives, each survivor still feels unique even with randomized active skills

**Likely RoR2 APIs to investigate:**
- `SurvivorDef.unlockableDef` - each survivor has an unlock condition
- `SurvivorCatalog` - manages available survivors
- `CharacterSelectController` - controls the lobby character select UI
- `UserProfile` / `NetworkUser` - per-player unlock state
- Possible approach: hook `SurvivorCatalog` or `CharacterSelectController` to filter available survivors based on AP unlock state

**Open Questions:**
- How exactly does the character select screen query available survivors?
- Can we override unlock state without touching the persistent user profile?
- Should we hook `CharacterSelectController.BuildSkillStripDisplayData` or filter at the `SurvivorCatalog` level?

---

## Changes to Existing Files

### `ArchipelagoClient.cs`
- Add fields for new slot data (skill/loot pool/survivor settings)
- Read new slot data keys in `Connect()` after existing reads (~line 86)
- Create `ArchipelagoSkillRandomizer`, `ArchipelagoLootPoolController`, and `ArchipelagoSurvivorController` instances
- Wire events: `ItemLogic.OnSkillUnlockReceived += SkillRandomizer.HandleSkillUnlock`
- Wire events: `ItemLogic.OnLootPoolExpansionReceived += LootPoolController.ExpandPool`
- Wire events: `ItemLogic.OnSurvivorUnlockReceived += SurvivorController.HandleSurvivorUnlock`
- Dispose new controllers in `Dispose()`

### `ArchipelagoItemLogicController.cs`
- Add events: `public event Action OnSkillUnlockReceived`, `public event Action<string> OnLootPoolExpansionReceived`, `public event Action OnSurvivorUnlockReceived`
- Add cases to `HandleReceivedItemQueueItem()` switch (after line 201):
  - `"Skill Unlock"` -> raise `OnSkillUnlockReceived`
  - `"Item Pool Expansion (White)"` -> raise `OnLootPoolExpansionReceived("White")`
  - Same for Green, Red, Boss, Lunar, Equipment
  - `"Survivor Unlock"` -> raise `OnSurvivorUnlockReceived`

### `ArchipelagoPlugin.cs`
- Register new `INetMessage` types in `Awake()` (~line 66):
  - `SyncSkillConfig`, `SyncSkillUnlock`, `SyncLootPoolConfig`, `SyncLootPoolExpansion`, `SyncSurvivorConfig`, `SyncSurvivorUnlock`

---

## New AP Item Types (for the Python AP world)

| AP Item Name | Purpose |
|---|---|
| `Skill Unlock` | Unlocks the next skill slot (secondary -> utility -> special) |
| `Survivor Unlock` | Unlocks the next survivor on the character select screen |
| `Item Pool Expansion (White)` | Adds 1 white item to the drop pool |
| `Item Pool Expansion (Green)` | Adds 1 green item to the drop pool |
| `Item Pool Expansion (Red)` | Adds 1 red item to the drop pool |
| `Item Pool Expansion (Boss)` | Adds 1 boss item to the drop pool |
| `Item Pool Expansion (Lunar)` | Adds 1 lunar item to the drop pool |
| `Item Pool Expansion (Equipment)` | Adds 1 equipment to the drop pool |

## New Slot Data Fields

| Key | Type | Purpose |
|---|---|---|
| `skillRandomization` | bool | Enable skill randomization |
| `skillRandomSeed` | long | Deterministic seed for skill assignments |
| `totalSkillUnlocks` | int | Total "Skill Unlock" items (default 3) |
| `lootPoolLimiting` | bool | Enable loot pool limiting |
| `lootPoolSeed` | long | Deterministic seed for pool selection |
| `startingWhiteItems` | int | Starting white items in pool (default 2) |
| `startingGreenItems` | int | Starting green items (default 1) |
| `startingRedItems` | int | Starting red items (default 1) |
| `startingBossItems` | int | Starting boss items (default 1) |
| `startingLunarItems` | int | Starting lunar items (default 1) |
| `startingEquipment` | int | Starting equipment (default 1) |
| `survivorLocking` | bool | Enable survivor locking |
| `survivorSeed` | long | Deterministic seed for survivor unlock order |
| `totalSurvivorUnlocks` | int | Total "Survivor Unlock" items in the pool |

---

## New Files Summary

| File | Purpose |
|---|---|
| `Archipelago.RiskOfRain2/ArchipelagoSkillRandomizer.cs` | Skill randomization controller |
| `Archipelago.RiskOfRain2/ArchipelagoLootPoolController.cs` | Loot pool limiting controller |
| `Archipelago.RiskOfRain2/ArchipelagoSurvivorController.cs` | Survivor locking controller |
| `Archipelago.RiskOfRain2/Net/SyncSkillConfig.cs` | Net message: skill config at session start |
| `Archipelago.RiskOfRain2/Net/SyncSkillUnlock.cs` | Net message: skill unlock progress |
| `Archipelago.RiskOfRain2/Net/SyncLootPoolConfig.cs` | Net message: loot pool config at session start |
| `Archipelago.RiskOfRain2/Net/SyncLootPoolExpansion.cs` | Net message: pool expansion progress |
| `Archipelago.RiskOfRain2/Net/SyncSurvivorConfig.cs` | Net message: survivor config at session start |
| `Archipelago.RiskOfRain2/Net/SyncSurvivorUnlock.cs` | Net message: survivor unlock progress |

---

## Implementation Order

**Phase 1: Loot Pool Limiting** (simplest, fewer RoR2 APIs)
1. Create `ArchipelagoLootPoolController.cs`
2. Create `SyncLootPoolConfig.cs` and `SyncLootPoolExpansion.cs`
3. Add slot data reading to `ArchipelagoClient.cs`
4. Add pool expansion cases to `ArchipelagoItemLogicController.cs`
5. Register net messages in `ArchipelagoPlugin.cs`

**Phase 2: Skill Randomization** (more complex)
1. Create `ArchipelagoSkillRandomizer.cs` with locked SkillDef and override logic
2. Create `SyncSkillConfig.cs` and `SyncSkillUnlock.cs`
3. Add slot data reading to `ArchipelagoClient.cs`
4. Add skill unlock case to `ArchipelagoItemLogicController.cs`
5. Register net messages in `ArchipelagoPlugin.cs`

**Phase 3: Survivor Locking** (needs API research first)
1. Research RoR2 survivor availability APIs
2. Create `ArchipelagoSurvivorController.cs`
3. Create `SyncSurvivorConfig.cs` and `SyncSurvivorUnlock.cs`
4. Add slot data reading and wiring to `ArchipelagoClient.cs`
5. Add survivor unlock case to `ArchipelagoItemLogicController.cs`
6. Register net messages in `ArchipelagoPlugin.cs`

**Phase 4: Polish**
1. Chat notifications for unlocks ("Skill unlocked: Secondary!", "White item pool expanded!", "New survivor unlocked: Huntress!")
2. Test stage transitions, respawns, multiplayer sync

---

## Verification

- Build the project with `dotnet build` and confirm no compile errors
- Test in single-player: start a run with skill randomization on, verify primary works and other slots are locked
- Simulate receiving "Skill Unlock" items and verify slots unlock in order
- Test loot pool: verify limited drops at run start, verify expansion when AP items arrive
- Test survivor locking: verify only 1 survivor available at character select, verify unlocks add more
- Test multiplayer: verify all clients see same skill assignments, loot pools, and survivor availability (deterministic seeding)
- Test stage transitions: verify skills and loot pool state persist across stages

---

## Architecture Notes

### Existing Patterns to Follow
- All controllers implement `IDisposable` (see `ArchipelagoItemLogicController`)
- Network messages implement `INetMessage` with parameterless + data constructors, `Serialize`/`Deserialize`, `OnReceived` (see `SyncTotalCheckProgress.cs`)
- Hooks use MMHOOK `On.RoR2.*` prefix, installed/removed in matched pairs
- Random selection uses `IEnumerableExtensions.Choice()` (but new features need seeded `System.Random` for determinism)
- Events used for loose coupling between controllers (e.g., `OnItemDropProcessed`)

### Key Design Decisions
- **Deterministic seeding** from slot data ensures multiplayer consistency without sending full state
- **Event-driven** architecture: `ArchipelagoItemLogicController` raises events, controllers subscribe
- **Skill overrides** use RoR2's stack-based `SetSkillOverride` system (safe, works with Heretic transformations, etc.)
- **Loot pool filtering** uses weight zeroing on `BasicPickupDropTable.GenerateWeightedSelection` (same approach as LootPoolLimiter reference mod)
