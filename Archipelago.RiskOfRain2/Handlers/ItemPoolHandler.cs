using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace Archipelago.RiskOfRain2.Handlers
{
    public class ItemPoolHandler : IHandler
    {
        // Per-tier allowed sets (O(1) lookup)
        private HashSet<ItemIndex> allowedWhite = new HashSet<ItemIndex>();
        private HashSet<ItemIndex> allowedGreen = new HashSet<ItemIndex>();
        private HashSet<ItemIndex> allowedRed = new HashSet<ItemIndex>();
        private HashSet<ItemIndex> allowedBoss = new HashSet<ItemIndex>();
        private HashSet<ItemIndex> allowedLunar = new HashSet<ItemIndex>();
        private HashSet<ItemIndex> allowedVoid = new HashSet<ItemIndex>();
        private HashSet<EquipmentIndex> allowedEquipment = new HashSet<EquipmentIndex>();

        // Deterministic shuffled orderings per tier (set once at init, never changes)
        private List<ItemIndex> shuffledWhite;
        private List<ItemIndex> shuffledGreen;
        private List<ItemIndex> shuffledRed;
        private List<ItemIndex> shuffledBoss;
        private List<ItemIndex> shuffledLunar;
        private List<ItemIndex> shuffledVoid;
        private List<EquipmentIndex> shuffledEquipment;

        // Items per expansion (from slot data)
        private int perWhiteExpansion;
        private int perGreenExpansion;
        private int perRedExpansion;
        private int perBossExpansion;
        private int perLunarExpansion;
        private int perVoidExpansion;
        private int perEquipmentExpansion;

        public bool PoolEnabled { get; private set; }

        // Static accessors for UI
        public static bool IsActive { get; private set; }
        public static event Action OnPoolChanged;

        // Tier info for UI display
        public struct TierInfo
        {
            public string Name;
            public int Current;
            public int Total;
        }

        public static ItemPoolHandler Instance { get; private set; }

        public TierInfo[] GetTierSummary()
        {
            return new[]
            {
                new TierInfo { Name = "White", Current = allowedWhite.Count, Total = shuffledWhite?.Count ?? 0 },
                new TierInfo { Name = "Green", Current = allowedGreen.Count, Total = shuffledGreen?.Count ?? 0 },
                new TierInfo { Name = "Red", Current = allowedRed.Count, Total = shuffledRed?.Count ?? 0 },
                new TierInfo { Name = "Boss", Current = allowedBoss.Count, Total = shuffledBoss?.Count ?? 0 },
                new TierInfo { Name = "Lunar", Current = allowedLunar.Count, Total = shuffledLunar?.Count ?? 0 },
                new TierInfo { Name = "Void", Current = allowedVoid.Count, Total = shuffledVoid?.Count ?? 0 },
                new TierInfo { Name = "Equipment", Current = allowedEquipment.Count, Total = shuffledEquipment?.Count ?? 0 },
            };
        }

        /// <summary>
        /// Returns all items for the given tier page index (0=White..6=Equipment).
        /// Each entry is (itemIndex or equipmentIndex as int, isAllowed).
        /// For equipment tier (index 6), cast the int back to EquipmentIndex.
        /// </summary>
        public List<(int index, bool allowed)> GetTierItems(int tierPage)
        {
            var result = new List<(int, bool)>();
            switch (tierPage)
            {
                case 0: foreach (var i in shuffledWhite) result.Add(((int)i, allowedWhite.Contains(i))); break;
                case 1: foreach (var i in shuffledGreen) result.Add(((int)i, allowedGreen.Contains(i))); break;
                case 2: foreach (var i in shuffledRed) result.Add(((int)i, allowedRed.Contains(i))); break;
                case 3: foreach (var i in shuffledBoss) result.Add(((int)i, allowedBoss.Contains(i))); break;
                case 4: foreach (var i in shuffledLunar) result.Add(((int)i, allowedLunar.Contains(i))); break;
                case 5: foreach (var i in shuffledVoid) result.Add(((int)i, allowedVoid.Contains(i))); break;
                case 6: foreach (var i in shuffledEquipment) result.Add(((int)i, allowedEquipment.Contains(i))); break;
            }
            return result;
        }

        public void Initialize(Dictionary<string, object> slotData)
        {
            PoolEnabled = true;
            Instance = this;
            IsActive = true;

            // Read config from slot data
            string seed = slotData.ContainsKey("seed") ? slotData["seed"].ToString() : "0";
            int startWhite = GetInt(slotData, "startingWhitePool", 5);
            int startGreen = GetInt(slotData, "startingGreenPool", 3);
            int startRed = GetInt(slotData, "startingRedPool", 1);
            int startBoss = GetInt(slotData, "startingBossPool", 1);
            int startLunar = GetInt(slotData, "startingLunarPool", 0);
            int startVoid = GetInt(slotData, "startingVoidPool", 0);
            int startEquipment = GetInt(slotData, "startingEquipmentPool", 3);

            perWhiteExpansion = GetInt(slotData, "itemsPerWhiteExpansion", 3);
            perGreenExpansion = GetInt(slotData, "itemsPerGreenExpansion", 3);
            perRedExpansion = GetInt(slotData, "itemsPerRedExpansion", 3);
            perBossExpansion = GetInt(slotData, "itemsPerBossExpansion", 2);
            perLunarExpansion = GetInt(slotData, "itemsPerLunarExpansion", 1);
            perVoidExpansion = GetInt(slotData, "itemsPerVoidExpansion", 1);
            perEquipmentExpansion = GetInt(slotData, "itemsPerEquipmentExpansion", 4);

            var rng = new System.Random(seed.GetHashCode());

            // Build shuffled lists per tier from ItemCatalog
            shuffledWhite = GetItemsByTier(ItemTier.Tier1);
            shuffledGreen = GetItemsByTier(ItemTier.Tier2);
            shuffledRed = GetItemsByTier(ItemTier.Tier3);
            shuffledBoss = GetItemsByTier(ItemTier.Boss);
            shuffledLunar = GetItemsByTier(ItemTier.Lunar);
            shuffledVoid = GetVoidItems();
            shuffledEquipment = GetAllEquipment();

            Shuffle(shuffledWhite, rng);
            Shuffle(shuffledGreen, rng);
            Shuffle(shuffledRed, rng);
            Shuffle(shuffledBoss, rng);
            Shuffle(shuffledLunar, rng);
            Shuffle(shuffledVoid, rng);
            ShuffleEquipment(shuffledEquipment, rng);

            // Populate starting pools
            PopulateStarting(allowedWhite, shuffledWhite, startWhite);
            PopulateStarting(allowedGreen, shuffledGreen, startGreen);
            PopulateStarting(allowedRed, shuffledRed, startRed);
            PopulateStarting(allowedBoss, shuffledBoss, startBoss);
            PopulateStarting(allowedLunar, shuffledLunar, startLunar);
            PopulateStarting(allowedVoid, shuffledVoid, startVoid);
            PopulateStartingEquipment(allowedEquipment, shuffledEquipment, startEquipment);

            Log.LogDebug($"ItemPoolHandler initialized: White {allowedWhite.Count}/{shuffledWhite.Count}, " +
                         $"Green {allowedGreen.Count}/{shuffledGreen.Count}, " +
                         $"Red {allowedRed.Count}/{shuffledRed.Count}, " +
                         $"Boss {allowedBoss.Count}/{shuffledBoss.Count}, " +
                         $"Lunar {allowedLunar.Count}/{shuffledLunar.Count}, " +
                         $"Void {allowedVoid.Count}/{shuffledVoid.Count}, " +
                         $"Equipment {allowedEquipment.Count}/{shuffledEquipment.Count}");
        }

        public bool IsItemAllowed(ItemIndex item)
        {
            if (!PoolEnabled) return true;
            var def = ItemCatalog.GetItemDef(item);
            if (def == null) return true;

            switch (def.tier)
            {
                case ItemTier.Tier1: return allowedWhite.Contains(item);
                case ItemTier.Tier2: return allowedGreen.Contains(item);
                case ItemTier.Tier3: return allowedRed.Contains(item);
                case ItemTier.Boss: return allowedBoss.Contains(item);
                case ItemTier.Lunar: return allowedLunar.Contains(item);
                case ItemTier.VoidTier1:
                case ItemTier.VoidTier2:
                case ItemTier.VoidTier3:
                case ItemTier.VoidBoss:
                    return allowedVoid.Contains(item);
                default:
                    return true; // Unknown tiers pass through
            }
        }

        public bool IsEquipmentAllowed(EquipmentIndex equip)
        {
            if (!PoolEnabled) return true;
            return allowedEquipment.Contains(equip);
        }

        /// <summary>
        /// Expands the pool for the given AP item ID (37101-37107).
        /// Returns list of newly unlocked item/equipment display names.
        /// </summary>
        public List<string> ExpandPool(long itemId)
        {
            var newNames = new List<string>();
            int tierIndex = (int)(itemId - 37100);

            switch (tierIndex)
            {
                case 1: ExpandTier(allowedWhite, shuffledWhite, perWhiteExpansion, newNames); break;
                case 2: ExpandTier(allowedGreen, shuffledGreen, perGreenExpansion, newNames); break;
                case 3: ExpandTier(allowedRed, shuffledRed, perRedExpansion, newNames); break;
                case 4: ExpandTier(allowedBoss, shuffledBoss, perBossExpansion, newNames); break;
                case 5: ExpandTier(allowedLunar, shuffledLunar, perLunarExpansion, newNames); break;
                case 6: ExpandTier(allowedVoid, shuffledVoid, perVoidExpansion, newNames); break;
                case 7: ExpandEquipmentTier(allowedEquipment, shuffledEquipment, perEquipmentExpansion, newNames); break;
            }

            OnPoolChanged?.Invoke();
            return newNames;
        }

        public void Hook()
        {
            On.RoR2.BasicPickupDropTable.GenerateWeightedSelection += FilterDropTable;
            On.RoR2.PickupTransmutationManager.RebuildPickupGroups += FilterTransmutation;
        }

        public void UnHook()
        {
            On.RoR2.BasicPickupDropTable.GenerateWeightedSelection -= FilterDropTable;
            On.RoR2.PickupTransmutationManager.RebuildPickupGroups -= FilterTransmutation;
            Instance = null;
            IsActive = false;
        }

        #region Drop Table Hooks

        private void FilterDropTable(
            On.RoR2.BasicPickupDropTable.orig_GenerateWeightedSelection orig,
            BasicPickupDropTable self,
            Run run)
        {
            orig(self, run);
            if (!PoolEnabled) return;

            for (int i = self.selector.Count - 1; i >= 0; i--)
            {
                var choice = self.selector.GetChoice(i);
                UniquePickup pickup = choice.value;
                PickupDef def = PickupCatalog.GetPickupDef(pickup.pickupIndex);
                if (def == null) continue;

                if (def.itemIndex != ItemIndex.None && !IsItemAllowed(def.itemIndex))
                {
                    self.selector.ModifyChoiceWeight(i, 0f);
                }
                else if (def.equipmentIndex != EquipmentIndex.None && !IsEquipmentAllowed(def.equipmentIndex))
                {
                    self.selector.ModifyChoiceWeight(i, 0f);
                }
            }
        }

        private void FilterTransmutation(
            On.RoR2.PickupTransmutationManager.orig_RebuildPickupGroups orig)
        {
            orig();
            // After transmutation groups are rebuilt, we don't need to filter here
            // because the GenerateWeightedSelection hook covers printers/scrappers.
            // This hook is reserved for future needs (e.g., Command artifact grid).
        }

        #endregion

        #region Helpers

        private static int GetInt(Dictionary<string, object> data, string key, int defaultValue)
        {
            if (data.TryGetValue(key, out var val))
            {
                return Convert.ToInt32(val);
            }
            return defaultValue;
        }

        private static List<ItemIndex> GetItemsByTier(ItemTier tier)
        {
            return ItemCatalog.allItemDefs
                .Where(d => d.tier == tier && !d.hidden)
                .OrderBy(d => d.itemIndex)
                .Select(d => d.itemIndex)
                .ToList();
        }

        private static List<ItemIndex> GetVoidItems()
        {
            return ItemCatalog.allItemDefs
                .Where(d => (d.tier == ItemTier.VoidTier1 ||
                             d.tier == ItemTier.VoidTier2 ||
                             d.tier == ItemTier.VoidTier3 ||
                             d.tier == ItemTier.VoidBoss) && !d.hidden)
                .OrderBy(d => d.itemIndex)
                .Select(d => d.itemIndex)
                .ToList();
        }

        private static List<EquipmentIndex> GetAllEquipment()
        {
            return EquipmentCatalog.allEquipment
                .Select(i => EquipmentCatalog.GetEquipmentDef(i))
                .Where(d => d != null && d.canDrop && !d.isLunar)
                .OrderBy(d => d.equipmentIndex)
                .Select(d => d.equipmentIndex)
                .ToList();
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private static void ShuffleEquipment(List<EquipmentIndex> list, System.Random rng)
        {
            Shuffle(list, rng);
        }

        private static void PopulateStarting(HashSet<ItemIndex> allowed, List<ItemIndex> shuffled, int count)
        {
            for (int i = 0; i < count && i < shuffled.Count; i++)
            {
                allowed.Add(shuffled[i]);
            }
        }

        private static void PopulateStartingEquipment(HashSet<EquipmentIndex> allowed, List<EquipmentIndex> shuffled, int count)
        {
            for (int i = 0; i < count && i < shuffled.Count; i++)
            {
                allowed.Add(shuffled[i]);
            }
        }

        private static void ExpandTier(HashSet<ItemIndex> allowed, List<ItemIndex> shuffled, int perExpansion, List<string> newNames)
        {
            int start = allowed.Count;
            for (int i = start; i < start + perExpansion && i < shuffled.Count; i++)
            {
                if (allowed.Add(shuffled[i]))
                {
                    var def = ItemCatalog.GetItemDef(shuffled[i]);
                    if (def != null)
                    {
                        newNames.Add(Language.GetString(def.nameToken));
                    }
                }
            }
        }

        private static void ExpandEquipmentTier(HashSet<EquipmentIndex> allowed, List<EquipmentIndex> shuffled, int perExpansion, List<string> newNames)
        {
            int start = allowed.Count;
            for (int i = start; i < start + perExpansion && i < shuffled.Count; i++)
            {
                if (allowed.Add(shuffled[i]))
                {
                    var def = EquipmentCatalog.GetEquipmentDef(shuffled[i]);
                    if (def != null)
                    {
                        newNames.Add(Language.GetString(def.nameToken));
                    }
                }
            }
        }

        #endregion
    }
}
