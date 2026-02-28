using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.RiskOfRain2.Net;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;

namespace Archipelago.RiskOfRain2
{
    public class ArchipelagoLootPoolController : IDisposable
    {
        private bool enabled;

        private int startingWhiteCount;
        private int startingGreenCount;
        private int startingRedCount;
        private int startingBossCount;
        private int startingLunarCount;
        private int startingEquipmentCount;

        private HashSet<PickupIndex> whitelistedTier1;
        private HashSet<PickupIndex> whitelistedTier2;
        private HashSet<PickupIndex> whitelistedTier3;
        private HashSet<PickupIndex> whitelistedBoss;
        private HashSet<PickupIndex> whitelistedLunar;
        private HashSet<PickupIndex> whitelistedEquipment;

        private Queue<PickupIndex> tier1ExpansionOrder;
        private Queue<PickupIndex> tier2ExpansionOrder;
        private Queue<PickupIndex> tier3ExpansionOrder;
        private Queue<PickupIndex> bossExpansionOrder;
        private Queue<PickupIndex> lunarExpansionOrder;
        private Queue<PickupIndex> equipmentExpansionOrder;

        private int whiteExpansions;
        private int greenExpansions;
        private int redExpansions;
        private int bossExpansions;
        private int lunarExpansions;
        private int equipmentExpansions;

        private int itemsPerExpansion;

        private System.Random rng;
        private bool initialized;
        private List<string> pendingExpansions = new List<string>();

        public ArchipelagoLootPoolController(bool enabled, int startWhite, int startGreen,
            int startRed, int startBoss, int startLunar, int startEquipment, long seed,
            int itemsPerExpansion = 1)
        {
            this.enabled = enabled;
            startingWhiteCount = startWhite;
            startingGreenCount = startGreen;
            startingRedCount = startRed;
            startingBossCount = startBoss;
            startingLunarCount = startLunar;
            startingEquipmentCount = startEquipment;
            this.itemsPerExpansion = itemsPerExpansion;
            rng = new System.Random((int)seed);
        }

        public void Initialize()
        {
            if (!enabled) return;

            Run.onRunStartGlobal += OnRunStart;
            On.RoR2.BasicPickupDropTable.GenerateWeightedSelection += BasicPickupDropTable_GenerateWeightedSelection;

            SyncLootPoolConfig.OnLootPoolConfigReceived += OnConfigReceived;
            SyncLootPoolExpansion.OnLootPoolExpansionReceived += OnExpansionReceived;

            Log.LogDebug("ArchipelagoLootPoolController initialized.");
        }

        private void OnRunStart(Run run)
        {
            BuildWhitelists(run);
        }

        private void BuildWhitelists(Run run)
        {
            whitelistedTier1 = BuildWhitelistForTier(run.availableTier1DropList, startingWhiteCount, out tier1ExpansionOrder);
            whitelistedTier2 = BuildWhitelistForTier(run.availableTier2DropList, startingGreenCount, out tier2ExpansionOrder);
            whitelistedTier3 = BuildWhitelistForTier(run.availableTier3DropList, startingRedCount, out tier3ExpansionOrder);
            whitelistedBoss = BuildWhitelistForTier(run.availableBossDropList, startingBossCount, out bossExpansionOrder);
            whitelistedLunar = BuildWhitelistForTier(run.availableLunarCombinedDropList, startingLunarCount, out lunarExpansionOrder);
            whitelistedEquipment = BuildWhitelistForTier(run.availableEquipmentDropList, startingEquipmentCount, out equipmentExpansionOrder);

            whiteExpansions = 0;
            greenExpansions = 0;
            redExpansions = 0;
            bossExpansions = 0;
            lunarExpansions = 0;
            equipmentExpansions = 0;

            initialized = true;

            Log.LogInfo($"Loot pool initialized - White: {whitelistedTier1.Count}/{run.availableTier1DropList.Count}, " +
                        $"Green: {whitelistedTier2.Count}/{run.availableTier2DropList.Count}, " +
                        $"Red: {whitelistedTier3.Count}/{run.availableTier3DropList.Count}");

            // Replay any expansions that were received before the loot pool was initialized
            if (pendingExpansions.Count > 0)
            {
                Log.LogInfo($"Replaying {pendingExpansions.Count} pending loot pool expansions");
                foreach (var tierName in pendingExpansions)
                {
                    ExpandPool(tierName);
                }
                pendingExpansions.Clear();
            }

            PickupDropTable.RegenerateAll(run);
        }

        private HashSet<PickupIndex> BuildWhitelistForTier(List<PickupIndex> availableItems, int startingCount,
            out Queue<PickupIndex> expansionOrder)
        {
            var shuffled = availableItems.OrderBy(x => rng.Next()).ToList();
            var count = Math.Min(startingCount, shuffled.Count);

            var whitelist = new HashSet<PickupIndex>(shuffled.Take(count));
            expansionOrder = new Queue<PickupIndex>(shuffled.Skip(count));

            return whitelist;
        }

        private void BasicPickupDropTable_GenerateWeightedSelection(
            On.RoR2.BasicPickupDropTable.orig_GenerateWeightedSelection orig, BasicPickupDropTable self, Run run)
        {
            orig(self, run);

            if (!enabled || !initialized) return;

            for (int i = 0; i < self.selector.Count; i++)
            {
                var choice = self.selector.GetChoice(i);
                var pickupIndex = choice.value.pickupIndex;

                if (!IsWhitelisted(pickupIndex))
                {
                    self.selector.ModifyChoiceWeight(i, 0f);
                }
            }
        }

        private bool IsWhitelisted(PickupIndex index)
        {
            if (whitelistedTier1.Contains(index)) return true;
            if (whitelistedTier2.Contains(index)) return true;
            if (whitelistedTier3.Contains(index)) return true;
            if (whitelistedBoss.Contains(index)) return true;
            if (whitelistedLunar.Contains(index)) return true;
            if (whitelistedEquipment.Contains(index)) return true;

            // Items not in any managed tier (void items, artifacts, etc.) are allowed through
            var pickupDef = PickupCatalog.GetPickupDef(index);
            if (pickupDef == null) return true;

            var itemIndex = pickupDef.itemIndex;
            if (itemIndex != ItemIndex.None)
            {
                var itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef != null)
                {
                    // Only filter items we're actively managing
                    switch (itemDef.tier)
                    {
                        case ItemTier.Tier1:
                        case ItemTier.Tier2:
                        case ItemTier.Tier3:
                        case ItemTier.Boss:
                        case ItemTier.Lunar:
                            return false; // Not in our whitelists = filtered out
                    }
                }
            }

            var equipIndex = pickupDef.equipmentIndex;
            if (equipIndex != EquipmentIndex.None)
            {
                var equipDef = EquipmentCatalog.GetEquipmentDef(equipIndex);
                if (equipDef != null && !equipDef.isLunar)
                {
                    return false; // Non-lunar equipment not in whitelist
                }
            }

            // Anything else (void items, misc pickups) passes through
            return true;
        }

        public void ExpandPool(string tierName)
        {
            if (!enabled) return;

            if (!initialized)
            {
                Log.LogDebug($"Loot pool not yet initialized, queuing expansion for {tierName}");
                pendingExpansions.Add(tierName);
                return;
            }

            HashSet<PickupIndex> whitelist;
            Queue<PickupIndex> queue;
            switch (tierName)
            {
                case "White": whitelist = whitelistedTier1; queue = tier1ExpansionOrder; break;
                case "Green": whitelist = whitelistedTier2; queue = tier2ExpansionOrder; break;
                case "Red": whitelist = whitelistedTier3; queue = tier3ExpansionOrder; break;
                case "Boss": whitelist = whitelistedBoss; queue = bossExpansionOrder; break;
                case "Lunar": whitelist = whitelistedLunar; queue = lunarExpansionOrder; break;
                case "Equipment": whitelist = whitelistedEquipment; queue = equipmentExpansionOrder; break;
                default: return;
            }

            int unlocked = 0;
            for (int i = 0; i < itemsPerExpansion; i++)
            {
                if (ExpandTier(whitelist, queue))
                    unlocked++;
                else
                    break;
            }

            if (unlocked > 0)
            {
                switch (tierName)
                {
                    case "White": whiteExpansions++; break;
                    case "Green": greenExpansions++; break;
                    case "Red": redExpansions++; break;
                    case "Boss": bossExpansions++; break;
                    case "Lunar": lunarExpansions++; break;
                    case "Equipment": equipmentExpansions++; break;
                }

                string msg = unlocked > 1
                    ? $"Item pool expanded! +{unlocked} {tierName} items"
                    : $"Item pool expanded! ({tierName})";
                ChatMessage.SendColored(msg, UnityEngine.Color.cyan);

                if (Run.instance != null)
                {
                    PickupDropTable.RegenerateAll(Run.instance);
                }

                new SyncLootPoolExpansion(tierName, GetExpansionCount(tierName))
                    .Send(NetworkDestination.Clients);
            }
            else
            {
                ChatMessage.SendColored($"{tierName} item pool fully expanded!", UnityEngine.Color.green);
            }
        }

        private int GetExpansionCount(string tierName)
        {
            switch (tierName)
            {
                case "White": return whiteExpansions;
                case "Green": return greenExpansions;
                case "Red": return redExpansions;
                case "Boss": return bossExpansions;
                case "Lunar": return lunarExpansions;
                case "Equipment": return equipmentExpansions;
                default: return 0;
            }
        }

        private bool ExpandTier(HashSet<PickupIndex> whitelist, Queue<PickupIndex> expansionOrder)
        {
            if (expansionOrder.Count == 0) return false;

            var newItem = expansionOrder.Dequeue();
            whitelist.Add(newItem);

            var pickupDef = PickupCatalog.GetPickupDef(newItem);
            if (pickupDef != null)
            {
                Log.LogInfo($"Loot pool expanded: {Language.GetString(pickupDef.nameToken)}");
            }

            return true;
        }

        private void OnConfigReceived(bool configEnabled, long seed, int startWhite, int startGreen,
            int startRed, int startBoss, int startLunar, int startEquip, int perExpansion)
        {
            // Client receives config from host
            enabled = configEnabled;
            startingWhiteCount = startWhite;
            startingGreenCount = startGreen;
            startingRedCount = startRed;
            startingBossCount = startBoss;
            startingLunarCount = startLunar;
            startingEquipmentCount = startEquip;
            itemsPerExpansion = perExpansion;
            rng = new System.Random((int)seed);
        }

        private void OnExpansionReceived(string tierName, int count)
        {
            if (!enabled || !initialized) return;

            // Client catches up to host's expansion count
            int currentCount = GetExpansionCount(tierName);
            while (currentCount < count)
            {
                HashSet<PickupIndex> whitelist;
                Queue<PickupIndex> queue;
                switch (tierName)
                {
                    case "White": whitelist = whitelistedTier1; queue = tier1ExpansionOrder; whiteExpansions++; break;
                    case "Green": whitelist = whitelistedTier2; queue = tier2ExpansionOrder; greenExpansions++; break;
                    case "Red": whitelist = whitelistedTier3; queue = tier3ExpansionOrder; redExpansions++; break;
                    case "Boss": whitelist = whitelistedBoss; queue = bossExpansionOrder; bossExpansions++; break;
                    case "Lunar": whitelist = whitelistedLunar; queue = lunarExpansionOrder; lunarExpansions++; break;
                    case "Equipment": whitelist = whitelistedEquipment; queue = equipmentExpansionOrder; equipmentExpansions++; break;
                    default: return;
                }
                ExpandTier(whitelist, queue);
                currentCount++;
            }

            if (Run.instance != null)
            {
                PickupDropTable.RegenerateAll(Run.instance);
            }
        }

        public void Dispose()
        {
            Run.onRunStartGlobal -= OnRunStart;
            On.RoR2.BasicPickupDropTable.GenerateWeightedSelection -= BasicPickupDropTable_GenerateWeightedSelection;
            SyncLootPoolConfig.OnLootPoolConfigReceived -= OnConfigReceived;
            SyncLootPoolExpansion.OnLootPoolExpansionReceived -= OnExpansionReceived;

            initialized = false;
        }
    }
}
