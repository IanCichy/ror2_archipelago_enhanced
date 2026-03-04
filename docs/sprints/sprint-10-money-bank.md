# Sprint 10: Money Bank / Persistent Gold

**Priority:** Medium — improves AP filler item value, quality-of-life
**Complexity:** Medium
**New AP Item IDs:** 37304 (Bank Deposit filler item, replaces or supplements 37301)
**Depends On:** None

## Goal

AP "Money" filler items currently grant gold that vanishes at the teleporter like normal gold. This makes them nearly worthless — you get money you can't spend fast enough. Introduce a "bank" system: a persistent gold reserve that carries across stages and runs within the same AP session. Bank gold is distributed to the player at the start of each stage, giving AP money items lasting value.

## Current Money System

**File:** `ArchipelagoItemLogicController.cs`, `GiveMoneyToPlayers()` (lines 738-756)

```csharp
private void GiveMoneyToPlayers()
{
    foreach (var player in PlayerCharacterMasterController.instances)
    {
        var coefficient = Run.instance.difficultyCoefficient;
        uint money = (uint)(100 * coefficient);
        player.master.money += money;   // directly adds to CharacterMaster.money
        // ... chat notification
    }
}
```

- **Item ID:** 37301 (filler)
- **Amount:** `100 * difficultyCoefficient` (scales with time/stage)
- **Persistence:** None — vanilla RoR2 resets `CharacterMaster.money` at stage transitions
- **API:** `player.master.money += money` (direct field set, not `GiveMoney()`)

## Phase 1: Bank Data Structure

### Tasks
1. Add a `bankBalance` field to `ArchipelagoClient` session state:
   ```csharp
   private uint bankBalance = 0;
   ```
2. Bank persists across stages (not reset at teleporter)
3. Bank persists across runs within the same AP session (cached in session state)

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — bank state

## Phase 2: Bank Deposits

When an AP "Money" filler item is received, instead of (or in addition to) giving gold directly, deposit into the bank:

### Option A: All Money Goes to Bank
```csharp
private void GiveMoneyToPlayers()
{
    var coefficient = Run.instance.difficultyCoefficient;
    uint money = (uint)(100 * coefficient);
    bankBalance += money;
    ChatMessage.SendColored($"${money} deposited to AP Bank (Balance: ${bankBalance})", Color.green);
}
```

### Option B: Split — Immediate + Bank
```csharp
private void GiveMoneyToPlayers()
{
    var coefficient = Run.instance.difficultyCoefficient;
    uint money = (uint)(100 * coefficient);
    uint immediate = money / 2;
    uint banked = money - immediate;

    foreach (var player in PlayerCharacterMasterController.instances)
        player.master.money += immediate;

    bankBalance += banked;
    ChatMessage.SendColored($"${immediate} received, ${banked} banked (Balance: ${bankBalance})", Color.green);
}
```

### Option C: New AP Item Type — "Bank Deposit"
Keep item 37301 as immediate gold. Add item 37304 as banked gold:
- 37301 "Money" → instant gold (current behavior)
- 37304 "Bank Deposit" → goes to bank

**Recommendation:** Option A is simplest and makes money items most valuable. But offer a config toggle between immediate/banked so players can choose.

## Phase 3: Bank Withdrawals (Per-Stage Payout)

At the start of each stage, grant a portion of the bank balance to the player.

### Withdrawal Strategy

| Strategy | Behavior | Pros | Cons |
|----------|----------|------|------|
| Full payout | All bank gold given at stage start | Simple | Gold still vanishes if not spent |
| Percentage payout | X% of bank per stage | Sustainable across stages | Complex |
| Fixed payout | Min(bankBalance, fixedAmount) per stage | Predictable | May be too slow |
| On-demand | Player withdraws via a command/interactable | Player control | Requires UI |

**Recommendation:** Percentage payout — give 50% of bank balance at stage start (configurable). This means the bank slowly drains but always provides some gold. The remaining balance carries to the next stage.

### Implementation
Hook `Run.BeginStage` or `SceneDirector.Start` to distribute bank gold:
```csharp
private void Run_onRunStartGlobal(Run obj)
{
    // Give starting bank payout
    DistributeBankPayout();
}

// Also hook stage transitions
On.RoR2.Run.BeginStage += (orig, self) =>
{
    orig(self);
    DistributeBankPayout();
};

private void DistributeBankPayout()
{
    if (bankBalance == 0) return;

    uint payout = (uint)(bankBalance * bankPayoutPercent);
    payout = Math.Max(payout, 1);  // always give at least $1 if bank has money
    bankBalance -= payout;

    foreach (var player in PlayerCharacterMasterController.instances)
    {
        player.master.money += payout;
    }

    ChatMessage.SendColored($"Bank payout: ${payout} (Remaining: ${bankBalance})", Color.green);
}
```

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — stage start hook, payout logic
- `Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs` — modify `GiveMoneyToPlayers()`

## Phase 4: Persistence Across Runs

Bank balance must survive across runs within the same AP session.

### Tasks
1. On `CleanupRun()`: cache `bankBalance` in session state
2. On `SetupRun()`: restore `bankBalance` from cached state
3. On `ProcessAllReceivedItems()`: replay money items into bank

### Key Files
- `Archipelago.RiskOfRain2/ArchipelagoClient.cs` — cache/restore in `CleanupRun()`/`SetupRun()`

## Phase 5: UI

### HUD Display
Show bank balance somewhere visible. Options:

**Option A: Objectives panel line**
```
AP Bank: $1,250
```
Added as another `ObjectiveTracker` in the objectives panel.

**Option B: Near the gold counter**
RoR2 shows current gold in the top-right HUD. Add a second line below it showing bank balance. This requires hooking into the `MoneyText` HUD component.

**Option C: Chat only**
No persistent HUD element — show balance in chat on deposit/payout.

**Recommendation:** Option A (objectives panel) is simplest and consistent with other AP HUD elements. Add bank balance as an objective line when bank has money.

### Console Command
Add `ap_bank` command:
```
AP Bank Balance: $1,250
Payout rate: 50% per stage
Next payout: ~$625
```

## Phase 6: Difficulty Scaling Consideration

Gold's value decreases as difficulty scales — a $100 chest at minute 5 costs $500 at minute 20. The bank payout should optionally scale with difficulty coefficient to remain useful in later stages.

### Scaled Payout
```csharp
uint rawPayout = (uint)(bankBalance * bankPayoutPercent);
uint scaledPayout = (uint)(rawPayout * Run.instance.difficultyCoefficient);
```

This means $100 banked early is worth more gold when paid out later. Makes early-game AP money items more valuable throughout the run.

**Recommendation:** Make scaling optional via config. Default: scaled.

## Python AP World Changes

### options.py
```python
class MoneyBank(Toggle):
    """AP money items deposit into a persistent bank instead of granting gold immediately.
    Bank gold is paid out at the start of each stage."""
    display_name = "Money Bank"
    default = True

class BankPayoutPercent(Range):
    """Percentage of bank balance paid out at the start of each stage."""
    display_name = "Bank Payout Percent"
    range_start = 10
    range_end = 100
    default = 50

class BankScaleWithDifficulty(Toggle):
    """Scale bank payouts with difficulty coefficient (early deposits worth more later)."""
    display_name = "Bank Scales With Difficulty"
    default = True
```

### fill_slot_data()
```python
slot_data["moneyBank"] = self.options.money_bank.value
slot_data["bankPayoutPercent"] = self.options.bank_payout_percent.value
slot_data["bankScaleWithDifficulty"] = self.options.bank_scale_with_difficulty.value
```

No new item IDs needed if using Option A (redirect existing money items to bank). If using Option C (new "Bank Deposit" item), add ID 37304 to items.py.

## Configuration Options

| Slot Data Key | Type | Range | Default |
|---------------|------|-------|---------|
| `moneyBank` | Toggle | - | on |
| `bankPayoutPercent` | Range | 10-100 | 50 |
| `bankScaleWithDifficulty` | Toggle | - | on |

## Testing Criteria

1. **Deposit**: Receive AP money item. Verify gold goes to bank (not directly to player) when bank is enabled
2. **Stage payout**: Teleport to next stage. Verify bank payout appears as gold at stage start
3. **Percentage**: With 50% payout and $1000 bank, verify ~$500 received and ~$500 remains
4. **Run persistence**: Die, start new run. Verify bank balance carries over
5. **Scaling**: With difficulty coefficient 3.0 and $100 bank payout, verify ~$300 actually received (if scaling enabled)
6. **Bank disabled**: Set `moneyBank=false`. Verify money items give gold immediately as before
7. **Multiplayer**: Verify bank payout distributed to all connected players
8. **100% payout**: Set payout to 100%. Verify entire bank is distributed each stage (effectively immediate but delayed to stage start)
9. **Empty bank**: Verify no chat spam when bank is empty

## Risks and Open Questions

- **Gold cap**: RoR2 uses `uint` for money. Very large bank balances could theoretically overflow, but this is unlikely in practice (uint max is ~4.2 billion).
- **Multiplayer fairness**: Should each player have their own bank, or one shared bank? Currently money items give to ALL players. **Recommendation:** One shared bank — payout goes to all players equally, same as current money behavior.
- **Gold-losing mechanics**: Some items/shrines cost gold (Shrine of Blood grants gold, Shrine of the Mountain). Bank payouts interact with the existing gold economy. No special handling needed — banked gold becomes regular gold on payout.
- **Timing of payout**: `Run.BeginStage` fires before the player exits their pod. This means gold is available immediately when the player starts moving. This is fine — prevents the awkward "I have money but the pod hasn't opened yet" state.
- **XP filler**: The same bank concept could apply to XP (item 37303 "1000 Exp"). Currently XP is granted immediately and has limited value late-game. A banked XP system would be a natural extension but is lower priority.
- **Balance concern**: A large bank balance could let players buy everything on a stage instantly. The percentage payout mitigates this — you get a fraction per stage, not everything at once. 50% default means the bank halves each stage.
