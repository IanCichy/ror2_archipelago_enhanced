# Sprint 8: UI Overhaul

**Priority:** High — should be an early sprint, improves daily usability
**Complexity:** Medium
**New AP Item IDs:** None
**Depends On:** None (can be done in parallel with Sprint 1)

## Goal

Improve the in-game HUD for Archipelago check tracking. The current per-stage display shows `"SceneName: 3/2/1/0/1"` — five slash-separated numbers for chests/shrines/scavengers/scanners/altars. This is unreadable at a glance. Replace it with a clear, icon-labeled format. Also clean up the leftover progress bars on the bottom of the screen that no longer serve a useful purpose.

## Current UI Inventory

| Element | File | Location on Screen | Purpose | Status |
|---------|------|-------------------|---------|--------|
| Per-stage checks | `ArchipelagoLocationsInEnvironmentController.cs` | Objectives panel (top-right) | Shows `SceneName: 3/2/1/0/1` | **Needs rework** |
| Total checks | `ArchipelagoTotalChecksObjectiveController.cs` | Objectives panel (top-right) | Shows `Complete location checks: 45/120` | Keep |
| Item check bar | `ArchipelagoLocationCheckProgressBarUI.cs` | Bottom-center (cloned from XP bar) | Purple bar showing progress to next chest check | **Review — may remove** |
| Shrine check bar | (same class, second instance) | Above item check bar | Purple bar showing progress to next shrine check | **Review — may remove** |
| Connect button | `ArchipelagoConnectButtonController.cs` | Character select lobby | Connect/Disconnect with URL/slot/password fields | Keep |

## Phase 1: Rework Per-Stage Check Display

### Current Implementation
`ArchipelagoLocationsInEnvironmentController.cs` line 17-19:
```csharp
public override string GenerateString()
{
    return $"{CurrentScene}: {CurrentChests}/{CurrentShrines}/{CurrentScavangers}/{CurrentScanners}/{CurrentNewts}";
}
```

This produces: `Titanic Plains: 3/2/1/0/1` — five numbers with no labels.

### Proposed New Format
Replace with a multi-line, icon-labeled display:

```
Titanic Plains
  Chests: 3 remaining
  Shrines: 2 remaining
  Scavenger: 1 remaining
  Newt Altar: 1 remaining
```

Or a compact single-line with Unicode symbols:
```
Titanic Plains — Chests: 3 | Shrines: 2 | Scav: 1 | Altar: 1
```

Only show categories that have remaining checks (hide zeros).

### Implementation
```csharp
public override string GenerateString()
{
    var parts = new List<string>();
    if (CurrentChests > 0)     parts.Add($"Chests: {CurrentChests}");
    if (CurrentShrines > 0)    parts.Add($"Shrines: {CurrentShrines}");
    if (CurrentScavangers > 0) parts.Add($"Scav: {CurrentScavangers}");
    if (CurrentScanners > 0)   parts.Add($"Scanner: {CurrentScanners}");
    if (CurrentNewts > 0)      parts.Add($"Altar: {CurrentNewts}");

    if (parts.Count == 0)
        return $"{CurrentScene}: All checks complete!";

    return $"{CurrentScene}: {string.Join(" | ", parts)}";
}
```

### Network Sync
The values are synced via `SyncCurrentEnvironmentCheckProgress` net message — no changes needed to the network layer, only the display format.

### Key Files
- `Archipelago.RiskOfRain2/UI/ArchipelagoLocationsInEnvironmentController.cs` — rewrite `GenerateString()`

## Phase 2: Clean Up Progress Bars

### Current Bars
Two purple progress bars are created in `ArchipelagoClient.SetupRun()`:

**Explore mode** (lines 328-329):
```csharp
itemCheckBar = new ArchipelagoLocationCheckProgressBarUI(new Vector2(-40, 0), Vector2.zero, "Item Check Progress:");
shrineCheckBar = new ArchipelagoLocationCheckProgressBarUI(new Vector2(0, 170), new Vector2(50, -50), "Shrine Check Progress:");
```

**Classic mode** (line 349):
```csharp
itemCheckBar = new ArchipelagoLocationCheckProgressBarUI(Vector2.zero, Vector2.zero);
```

These bars clone the XP bar and show progress toward the next location check (e.g., "3 out of 5 chests until next AP check"). They take up screen real estate and duplicate info that could be shown more compactly in the objectives panel.

### Options

**Option A: Remove bars entirely.** The objectives panel already shows remaining checks per stage. The bar adds marginal value — it shows progress *within* the current step counter (e.g., 3/5 toward next check), but this is a minor detail.

**Option B: Keep bars but restyle.** Make them smaller, move to a less intrusive position, or integrate into the objectives panel as text (e.g., "Next chest check in: 2 pickups").

**Option C: Replace with objectives text.** Add a line to the objectives panel: "Next check: 2 more chests" that counts down to the next location check trigger.

**Recommendation:** Option C — remove the bars, add a compact countdown to the objectives panel. This is cleaner and doesn't overlap with other HUD elements.

### Implementation for Option C
Add a new `ObjectiveTracker` in `ArchipelagoLocationsInEnvironmentController` or a new controller:
```csharp
public override string GenerateString()
{
    if (ItemsUntilNextCheck > 0)
        return $"Next check in {ItemsUntilNextCheck} pickup(s)";
    if (ShrinesUntilNextCheck > 0)
        return $"Next shrine check in {ShrinesUntilNextCheck} use(s)";
    return "";
}
```

### Key Files
- `Archipelago.RiskOfRain2/UI/ArchipelagoLocationCheckProgressBarUI.cs` — remove or refactor
- `Archipelago.RiskOfRain2/UI/ArchipelagoLocationCheckProgressBarController.cs` — remove or refactor
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — remove bar creation in `SetupRun()`, update `CleanupRun()` disposal
- `Archipelago.RiskOfRain2/Handlers/LocationHandler.cs` — replace `updateBar()` calls with objective updates
- `Archipelago.RiskOfRain2/Net/SyncLocationCheckProgress.cs` — update to sync objective text instead of bar values
- `Archipelago.RiskOfRain2/Net/SyncShrineCheckProgress.cs` — same

## Phase 3: Improve Chat Notifications

### Current State
AP item/location messages are sent via `ChatMessage.Send()` or `ChatMessage.SendColored()`. The AP server log messages use per-part coloring. Item grants use `Chat.PlayerPickupChatMessage` with `pickupToken`.

### Improvements
1. **Item grant notifications**: Show the actual RoR2 item icon in chat when possible. Use `<sprite>` tags if the chat supports TextMeshPro sprite assets, or use colored item tier brackets:
   ```
   [AP] Received: [Soldier's Syringe] (White) from PlayerX
   ```

2. **Location check notifications**: When a check is completed, show what was sent:
   ```
   [AP] Sent: Stage 3 to PlayerX's World
   ```

3. **Stage unlock notifications**: Already colored. Enhance with stage tier info:
   ```
   [AP] Stage unlocked: Titanic Plains (Stage 1)
   ```

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — `Session_OnMessageReceived_Thread()`
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — item grant chat messages

## Phase 4: Connect Panel Polish (Optional)

### Current Issues
- Input fields are populated from BepInEx config but may not reflect current values after manual edit
- No visual feedback during connection attempt (no "connecting..." state)
- Panel layout comes from an AssetBundle (`connectbundle`) which is harder to iterate on

### Improvements
1. Add a "Connecting..." state to the button (yellow color, disabled)
2. Show connection status below the button: "Connected to archipelago.gg:38281 as SlotName"
3. Show session info: "Explore Mode | Victory: Mithrix | Checks: 45/120"

### Key Files
- `Archipelago.RiskOfRain2/UI/ArchipelagoConnectButtonController.cs`
- `connectbundle` asset (may need Unity editor to modify)

## Configuration Options

No slot data needed. These are client-side display changes.

Optional BepInEx config entries:
| Config Key | Type | Default | Notes |
|------------|------|---------|-------|
| `showProgressBars` | bool | false | Legacy bar display (off by default after rework) |
| `compactObjectives` | bool | false | Single-line vs multi-line check display |

## Testing Criteria

1. **Per-stage display**: Enter a stage in explore mode. Verify the objectives panel shows labeled check counts (not slash-separated numbers)
2. **Zero hiding**: Complete all chest checks on a stage. Verify "Chests" line disappears from display
3. **All complete**: Complete all checks on a stage. Verify "All checks complete!" message
4. **Bar removal**: Verify no purple progress bars appear on the HUD (unless legacy config is on)
5. **Countdown**: If Option C is implemented, verify "Next check in X pickups" counts down correctly
6. **Classic mode**: Verify classic mode also gets the improved display
7. **Multiplayer**: Verify non-host clients see the same improved display (synced via net messages)
8. **Chat messages**: Verify AP notifications are readable and properly colored

## Risks and Open Questions

- **Objectives panel space**: Adding multi-line per-stage info may crowd the objectives panel, especially with other mods that add objectives. The compact single-line format mitigates this.
- **Bar removal impact**: Some players may prefer the visual progress bar. Making it configurable (off by default) is safest.
- **Chat sprite support**: RoR2's chat may or may not support TextMeshPro sprite tags for item icons. Need to test. Fallback is colored text with tier brackets.
- **AssetBundle modification**: The connect panel is built from `connectbundle`. Changing its layout requires Unity Editor access. Text-only changes can be done in code, but layout changes need the bundle rebuilt.
- **Network message compatibility**: If we change what `SyncCurrentEnvironmentCheckProgress` sends (e.g., add new fields), need to handle backward compatibility with older clients in multiplayer.
