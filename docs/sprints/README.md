# RoR2 Archipelago Enhanced - Feature Sprints

## Recommended Sprint Order

```
Sprint 1: Full DLC Support               [MUST DO FIRST - all others depend on it]
    |
    +---> Sprint 8: UI Overhaul           [Early, no dependencies, high usability impact]
    +---> Sprint 10: Money Bank           [Early, standalone QoL]
    +---> Sprint 2: Stage Check Priority  [Simple, high value, quick win]
    +---> Sprint 9: Guaranteed Interactables [Simple, standalone]
    +---> Sprint 5: Drone Randomizer      [Simple, standalone]
    +---> Sprint 3: Portal/Hidden Realms  [Medium, enables Obliteration victory]
    +---> Sprint 4: Item Pool Limiting    [Medium-high, major gameplay feature]
    |         |
    |         +---> Sprint 6: Stage Randomizer  [High complexity]
    |
    +---> Sprint 7: Skill Swap Randomizer [Highest complexity, standalone]
    +---> Sprint 11: New Traps           [Medium, standalone, fun factor]
```

**Recommended order:** 1 → 8 → 10 → 2 → 9 → 5 → 3 → 11 → 4 → 6 → 7

## Sprint Summary

| Sprint | Feature | Complexity | New Files | New AP Item IDs |
|--------|---------|------------|-----------|-----------------|
| 1 | [Full DLC Support](sprint-1-dlc-support.md) | Medium | 0 | None (existing ranges) |
| 2 | [Stage Check Prioritization](sprint-2-stage-check-prioritization.md) | Low | 0 | None |
| 3 | [Portal/Hidden Realm Control](sprint-3-portal-control.md) | Medium | 0 | 37600-37609 |
| 4 | [Item Pool Limiting](sprint-4-item-pool-limiting.md) | Medium-High | 1-2 | 37100-37109 |
| 5 | [Drone Randomizer](sprint-5-drone-randomizer.md) | Low | 1 | 37200-37215 |
| 6 | [Stage Randomizer](sprint-6-stage-randomizer.md) | High | 1 | None |
| 7 | [Skill Swap Randomizer](sprint-7-skill-randomizer.md) | Very High | 2 | 38500-38699 |
| 8 | [UI Overhaul](sprint-8-ui-overhaul.md) | Medium | 0 | None |
| 9 | [Guaranteed Interactables](sprint-9-guaranteed-interactables.md) | Low-Medium | 1 | 37150-37169 |
| 10 | [Money Bank](sprint-10-money-bank.md) | Medium | 0 | None (or 37304) |
| 11 | [New Traps](sprint-11-new-traps.md) | Medium | 0 | 37405-37410 |

## AP Item ID Range Allocation

### Existing Ranges (Do Not Modify)
```
37001-37014  Gameplay items (Dio's, Common/Uncommon/Legendary/Boss/Lunar/Void/Equipment, Scraps, Beads, Scanner)
37300-37303  Filler (Money, Lunar Coin, XP)
37400-37404  Traps (Mountain, Time Warp, Combat, Teleport)
37405-37410  New Traps (Butterfingers, Alloy Worship, Aurelionite)  (Sprint 11)
37500-37505  Stage unlocks (Stage 1-4, Progressive Stage)
37700-37762  Environment unlocks (scene index offset from 37700)
```

### New Ranges (Reserved by Sprints)
```
37100-37109  Item Pool Expansion items      (Sprint 4)
37110-37149  Reserved for future pool types
37150-37169  Guaranteed Interactable items   (Sprint 9)
37170-37199  Reserved
37200-37215  Drone Unlock items             (Sprint 5)
37216-37299  Reserved for future drones
37304        Bank Deposit (optional)         (Sprint 10)
37600-37609  Portal Unlock items            (Sprint 3)
37610-37699  Reserved for future portals
38500-38699  Skill Unlock items             (Sprint 7)
```

### Location ID Ranges (Existing)
```
38000-38249  Classic mode item pickups
38250+       Explore mode per-environment (sceneIndex * 44 + 38250)
             Per env: 20 chests + 20 shrines + 1 scavenger + 1 scanner + 2 altars = 44
```

## RoR2 Game Reference Data

### Items by Rarity
| Rarity | Count | DLC |
|--------|-------|-----|
| White / Common | 36 | Base + DLC |
| Green / Uncommon | 42 | Base + DLC |
| Red / Legendary | 36 | Base + DLC |
| Yellow / Boss | 22 | Base + DLC |
| Blue / Lunar | 20 | Base |
| Void | 14 | Survivors of the Void |
| Meal | 5 | Seekers of the Storm |
| Equipment (Regular) | 30 | Base + DLC |
| Equipment (Lunar) | 4 | Base |
| **Total** | **~209** | |

### Stages by Tier
| Tier | Base Game | SOTV | SOTS | AC | Total |
|------|-----------|------|------|----|-------|
| Stage 1 | 2 | 1 | 2 (+2 loop) | 0 | 5 (+2) |
| Stage 2 | 2 | 1 | 1 | 1 | 5 |
| Stage 3 | 2 | 1 | 1 (+1 loop) | 1 (+1 loop) | 5 (+2) |
| Stage 4 | 3 | 0 | 0 | 2 | 5 |
| Stage 5 | 1 | 0 | 1 | 0* | 2 |
| Final Boss | 1 | 1 | 1 | 1 | 4 |
| Hidden Realms | 5 | 1 | 0 | 0 | 6 |

*Solutional Haunt is AC Stage 5 tier but is a boss-only stage with no standard checks.

### Win Conditions
| # | Condition | Stage | DLC | C# Status |
|---|-----------|-------|-----|-----------|
| 0 | Any | All | - | Implemented |
| 1 | Mithrix | Commencement | Base | Implemented |
| 2 | Voidling | The Planetarium | SOTV | Implemented |
| 3 | Obliteration/Limbo | A Moment, Whole | Base | Implemented |
| 4 | False Son | Prime Meridian | SOTS | Partial (missing acceptableLosses) |
| 5 | Solus Heart | Neural Sanctum | AC | NOT implemented |

### Survivors (18 total)
**Base:** Commando, Huntress, Bandit, MUL-T, Engineer, Artificer, Mercenary, REX, Loader, Acrid, Captain
**SOTV:** Railgunner, Void Fiend
**SOTS:** Seeker, False Son, CHEF
**AC:** Operator, Drifter

### Interactable Drone Types (~15)
Gunner, Healing, Gunner Turret, Junk, Emergency, Equipment, Missile, Incinerator, TC-280, Cleanup, Barrier, Jailer, Bombardment, Freeze, Transport

### Portals
| Color | Destination | Access Method |
|-------|-------------|---------------|
| Blue | Bazaar Between Time | Newt Altar (1 Lunar Coin) or random |
| Gold | Gilded Coast | Altar of Gold + Teleporter |
| Null | Void Fields | From Bazaar |
| Void | Void Locus | After Void Fields or random post-Stage 7 |
| Deep Void | The Planetarium | After Void Locus or pet frog 10x |
| Celestial | A Moment, Fractured | Every 3rd stage after loop |
| Artifact | Sky Meadow | Fixed location |
| Colossus | Prime Meridian | SOTS path |
| Mainline | Neural Sanctum | AC path |

## Key Source Files
| File | Role | Modified By |
|------|------|-------------|
| `ArchipelagoClient.cs` | Connection lifecycle, slot data, win conditions | All sprints |
| `ArchipelagoItemLogicController.cs` | Item ID routing, queues, item grants | All sprints |
| `Handlers/StageBlockerHandler.cs` | Stage blocking, portal control, scene hooks | 1, 3, 6 |
| `Handlers/LocationHandler.cs` | Location checks, weighted selection | 1, 2, 6 |
| `Lookup/LocationNames.cs` | Scene name/ID mappings | 1 |
| `worlds/ror2/items.py` | Python item definitions | 3, 4, 5, 7 |
| `worlds/ror2/options.py` | Python YAML options | 2, 3, 4, 5, 6, 7 |
| `worlds/ror2/regions.py` | Python region/stage graph | 1, 3, 6 |
| `worlds/ror2/ror2environments.py` | Python environment tables | 1 |

## Wiki Reference
Always use https://riskofrain2.wiki.gg/wiki/ (the other wiki is outdated).

Key pages:
- [Items](https://riskofrain2.wiki.gg/wiki/Items)
- [Environments](https://riskofrain2.wiki.gg/wiki/Environments)
- [Bosses](https://riskofrain2.wiki.gg/wiki/Bosses)
- [Survivors](https://riskofrain2.wiki.gg/wiki/Survivors)
- [Drones](https://riskofrain2.wiki.gg/wiki/Drones)
- [Portals](https://riskofrain2.wiki.gg/wiki/Portals)
- [Artifacts](https://riskofrain2.wiki.gg/wiki/Artifacts)
