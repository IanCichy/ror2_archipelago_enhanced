using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.RiskOfRain2.Net;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace Archipelago.RiskOfRain2
{
    public class ArchipelagoSkillRandomizer : IDisposable
    {
        // ======================================================================
        // SKILL EXCLUSION LISTS
        // Skills listed here stay on their original survivor and never enter
        // the global dealing pool. The owner still receives dealt skills from
        // the pool — these are just added on top.
        //
        // To find skill names: check BepInEx log for lines like:
        //   "Skill: 'SkillName' (token: TOKEN) from SurvivorBody [slot]"
        // Then add the skillName string here.
        // ======================================================================

        /// <summary>
        /// Survivors whose ALL non-primary skills should never enter the global pool.
        /// They keep their own skills and also receive dealt skills from the pool.
        /// Use the bodyPrefab name (e.g. "DrifterBody").
        /// </summary>
        private static readonly HashSet<string> excludedSurvivorBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DrifterBody",  // All skills rely on trash/scrap system
        };

        /// <summary>
        /// Individual skills that should never enter the global pool.
        /// The owning survivor keeps these skills alongside their dealt skills.
        /// Use SkillDef.skillName values (check BepInEx log to discover them).
        /// </summary>
        private static readonly HashSet<string> excludedSkillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Loader — gauntlet utilities rely on Loader's momentum/grapple mechanics
            "ChargedFist",
            "ThunderFist",
            // Mercenary — Blinding Assault (utility) dash chains don't work on other bodies
            "BlindingAssault",
            // Mercenary — Rising Thunder (secondary) relies on Mercenary's movement/combo system
            "RisingThunder",
            // Railgunner — HH44 Marksman (primary) relies on Railgunner's scope/weakpoint system
            "HH44Marksman",
            // Railgunner — Concussion Device (utility) doesn't work on other bodies
            "ConcussionDevice",
            // Seeker — Reprieve (utility) relies on Seeker's flight/barrier mechanics
            "Reprieve",
            // Acrid — Epidemic (special) doesn't work on other bodies
            "Epidemic",
            // CHEF — Oil Spill (utility) doesn't work on other bodies
            "OilSpill",
        };

        private bool enabled;
        private int totalSkillUnlocks;
        private int startingSkillCount;
        private int receivedSkillUnlocks;
        private long seed;

        // Global skill pools by slot type, shuffled by seed
        private List<SkillDef> allSecondaries;
        private List<SkillDef> allUtilities;
        private List<SkillDef> allSpecials;

        // Ordered survivor list for deterministic dealing
        private List<SurvivorDef> orderedSurvivors;

        // Store original family variants so we can restore on dispose
        private Dictionary<SkillFamily, SkillFamily.Variant[]> originalVariants;

        // Track which excluded skills belong to which survivor+slot for re-adding
        private Dictionary<string, List<SkillDef>> excludedSecondaries;
        private Dictionary<string, List<SkillDef>> excludedUtilities;
        private Dictionary<string, List<SkillDef>> excludedSpecials;

        private bool skillsCollected;
        private bool initialized;

        public ArchipelagoSkillRandomizer(bool enabled, int totalSkillUnlocks, int startingSkills, long seed)
        {
            this.enabled = enabled;
            this.totalSkillUnlocks = totalSkillUnlocks;
            this.startingSkillCount = startingSkills;
            this.receivedSkillUnlocks = 0;
            this.seed = seed;
            this.allSecondaries = new List<SkillDef>();
            this.allUtilities = new List<SkillDef>();
            this.allSpecials = new List<SkillDef>();
            this.orderedSurvivors = new List<SurvivorDef>();
            this.originalVariants = new Dictionary<SkillFamily, SkillFamily.Variant[]>();
            this.excludedSecondaries = new Dictionary<string, List<SkillDef>>();
            this.excludedUtilities = new Dictionary<string, List<SkillDef>>();
            this.excludedSpecials = new Dictionary<string, List<SkillDef>>();
        }

        public void Initialize()
        {
            if (!enabled) return;

            CollectAndShuffleSkills();
            ApplyAvailableSkills();

            Run.onRunStartGlobal += OnRunStart;

            SyncSkillConfig.OnSkillConfigReceived += OnConfigReceived;
            SyncSkillUnlock.OnSkillUnlockReceived += OnUnlockReceived;

            Log.LogDebug("ArchipelagoSkillRandomizer initialized (card-deal mode).");
        }

        /// <summary>
        /// Collect all secondary/utility/special skills from every survivor,
        /// shuffle each pool, and build an ordered survivor list for dealing.
        /// </summary>
        private void CollectAndShuffleSkills()
        {
            if (skillsCollected) return;

            allSecondaries.Clear();
            allUtilities.Clear();
            allSpecials.Clear();
            orderedSurvivors.Clear();
            excludedSecondaries.Clear();
            excludedUtilities.Clear();
            excludedSpecials.Clear();

            // Build ordered survivor list (sorted by name for determinism)
            orderedSurvivors = SurvivorCatalog.allSurvivorDefs
                .Where(s => s.bodyPrefab != null &&
                            s.bodyPrefab.GetComponent<SkillLocator>() != null)
                .OrderBy(s => s.cachedName ?? s.bodyPrefab.name)
                .ToList();

            foreach (var survivorDef in orderedSurvivors)
            {
                var bodyName = survivorDef.bodyPrefab.name;
                var locator = survivorDef.bodyPrefab.GetComponent<SkillLocator>();
                bool survivorExcluded = excludedSurvivorBodies.Contains(bodyName);

                Log.LogDebug($"Collecting skills for {bodyName} (excluded={survivorExcluded}):");

                CollectUniqueSkills(locator.secondary, allSecondaries, bodyName, "secondary",
                    survivorExcluded, excludedSecondaries);
                CollectUniqueSkills(locator.utility, allUtilities, bodyName, "utility",
                    survivorExcluded, excludedUtilities);
                CollectUniqueSkills(locator.special, allSpecials, bodyName, "special",
                    survivorExcluded, excludedSpecials);
            }

            // Shuffle each pool deterministically
            var rng = new System.Random((int)seed);
            allSecondaries = allSecondaries.OrderBy(_ => rng.Next()).ToList();
            allUtilities = allUtilities.OrderBy(_ => rng.Next()).ToList();
            allSpecials = allSpecials.OrderBy(_ => rng.Next()).ToList();

            int totalExcluded = excludedSecondaries.Values.Sum(l => l.Count) +
                                excludedUtilities.Values.Sum(l => l.Count) +
                                excludedSpecials.Values.Sum(l => l.Count);

            skillsCollected = true;
            Log.LogInfo($"Skill pools: {allSecondaries.Count} secondaries, " +
                        $"{allUtilities.Count} utilities, {allSpecials.Count} specials " +
                        $"across {orderedSurvivors.Count} survivors. " +
                        $"({totalExcluded} skills excluded from pool, kept on owners)");
        }

        private void CollectUniqueSkills(GenericSkill slot, List<SkillDef> targetList,
            string bodyName, string slotName, bool survivorExcluded,
            Dictionary<string, List<SkillDef>> excludedDict)
        {
            if (slot == null || slot.skillFamily == null) return;

            foreach (var variant in slot.skillFamily.variants)
            {
                if (variant.skillDef == null) continue;

                var skillName = variant.skillDef.skillName ?? ((ScriptableObject)variant.skillDef).name;
                Log.LogDebug($"  Skill: '{skillName}' (token: {variant.skillDef.skillNameToken}) " +
                             $"from {bodyName} [{slotName}]");

                bool isExcluded = survivorExcluded || excludedSkillNames.Contains(skillName);

                if (isExcluded)
                {
                    // Track this excluded skill so we can give it back to its owner
                    if (!excludedDict.ContainsKey(bodyName))
                        excludedDict[bodyName] = new List<SkillDef>();
                    if (!excludedDict[bodyName].Contains(variant.skillDef))
                        excludedDict[bodyName].Add(variant.skillDef);

                    Log.LogDebug($"    -> EXCLUDED from global pool (stays on {bodyName})");
                }
                else if (!targetList.Contains(variant.skillDef))
                {
                    targetList.Add(variant.skillDef);
                }
            }
        }

        /// <summary>
        /// How many skills each non-primary slot gets per survivor.
        /// Starts at 1, increases by 1 per Skill Unlock received.
        /// </summary>
        private int GetSkillsPerSlot()
        {
            return 1 + receivedSkillUnlocks;
        }

        /// <summary>
        /// Deal skills from the shuffled pools to each survivor like cards from a deck.
        /// Survivor 0 gets pool[0], pool[N], pool[2N]...
        /// Survivor 1 gets pool[1], pool[N+1], pool[2N+1]...
        /// Each survivor gets unique skills.
        /// </summary>
        private List<SkillDef> DealSkills(List<SkillDef> pool, int survivorIndex, int count)
        {
            int numSurvivors = orderedSurvivors.Count;
            var dealt = new List<SkillDef>();
            for (int round = 0; round < count; round++)
            {
                int idx = survivorIndex + round * numSurvivors;
                if (idx < pool.Count)
                {
                    dealt.Add(pool[idx]);
                }
            }
            return dealt;
        }

        /// <summary>
        /// Apply skill dealing to every survivor.
        /// Primary: untouched (survivor's own).
        /// Secondary/Utility/Special: dealt from global shuffled pools, unique per survivor.
        /// </summary>
        private void ApplyAvailableSkills()
        {
            int perSlot = GetSkillsPerSlot();

            for (int i = 0; i < orderedSurvivors.Count; i++)
            {
                var survivorDef = orderedSurvivors[i];
                var bodyName = survivorDef.bodyPrefab.name;
                var locator = survivorDef.bodyPrefab.GetComponent<SkillLocator>();

                var mySecondaries = DealSkills(allSecondaries, i, perSlot);
                var myUtilities = DealSkills(allUtilities, i, perSlot);
                var mySpecials = DealSkills(allSpecials, i, perSlot);

                // Re-add any excluded skills that originally belonged to this survivor
                AddBackExcludedSkills(mySecondaries, bodyName, excludedSecondaries);
                AddBackExcludedSkills(myUtilities, bodyName, excludedUtilities);
                AddBackExcludedSkills(mySpecials, bodyName, excludedSpecials);

                // Primary: leave untouched
                InjectGlobalSkills(locator.secondary, mySecondaries);
                InjectGlobalSkills(locator.utility, myUtilities);
                InjectGlobalSkills(locator.special, mySpecials);
            }
        }

        private void AddBackExcludedSkills(List<SkillDef> dealt, string bodyName,
            Dictionary<string, List<SkillDef>> excludedDict)
        {
            if (excludedDict.TryGetValue(bodyName, out var excluded))
            {
                foreach (var skill in excluded)
                {
                    if (!dealt.Contains(skill))
                        dealt.Add(skill);
                }
            }
        }

        private void InjectGlobalSkills(GenericSkill slot, List<SkillDef> available)
        {
            if (slot == null || slot.skillFamily == null) return;

            var family = slot.skillFamily;

            // Store original variants for restoration on dispose (only once)
            if (!originalVariants.ContainsKey(family))
            {
                originalVariants[family] = family.variants.ToArray();
            }

            if (available.Count == 0)
            {
                // Fallback: keep the first original variant
                family.variants = new[] { originalVariants[family][0] };
                return;
            }

            // Create new variants from dealt skills
            var template = originalVariants[family][0];
            var newVariants = available.Select(sd =>
            {
                var v = template;
                v.skillDef = sd;
                v.unlockableDef = null;
                return v;
            }).ToArray();

            family.variants = newVariants;
        }

        private void OnRunStart(Run run)
        {
            initialized = true;

            int perSlot = GetSkillsPerSlot();

            ChatMessage.SendColored(
                $"Skill randomizer: own primary + {perSlot} dealt skill(s) per slot. " +
                $"Unlocks: {receivedSkillUnlocks}/{totalSkillUnlocks}",
                Color.yellow);

            Log.LogInfo($"Skill randomizer active. {perSlot} per slot, " +
                        $"Unlocks: {receivedSkillUnlocks}/{totalSkillUnlocks}");
        }

        public void HandleSkillUnlock()
        {
            if (!enabled) return;

            receivedSkillUnlocks++;
            int perSlot = GetSkillsPerSlot();

            Log.LogInfo($"Skill unlock received! Now at {receivedSkillUnlocks}/{totalSkillUnlocks}, " +
                        $"{perSlot} per slot");

            ChatMessage.SendColored(
                $"Skill unlocked! Now {perSlot} skill(s) per slot. " +
                $"({receivedSkillUnlocks}/{totalSkillUnlocks})",
                Color.green);

            ApplyAvailableSkills();

            new SyncSkillUnlock(receivedSkillUnlocks).Send(NetworkDestination.Clients);
        }

        private void OnConfigReceived(bool configEnabled, long configSeed, int totalUnlocks,
            int currentUnlocks, int configStartingSkills)
        {
            enabled = configEnabled;
            seed = configSeed;
            totalSkillUnlocks = totalUnlocks;
            receivedSkillUnlocks = currentUnlocks;
            startingSkillCount = configStartingSkills;

            skillsCollected = false;
            CollectAndShuffleSkills();
            ApplyAvailableSkills();
        }

        private void OnUnlockReceived(int newUnlockCount)
        {
            if (!enabled || !initialized) return;

            receivedSkillUnlocks = newUnlockCount;
            ApplyAvailableSkills();
        }

        public void Dispose()
        {
            Run.onRunStartGlobal -= OnRunStart;
            SyncSkillConfig.OnSkillConfigReceived -= OnConfigReceived;
            SyncSkillUnlock.OnSkillUnlockReceived -= OnUnlockReceived;

            // Restore original skill family variants
            foreach (var kvp in originalVariants)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.variants = kvp.Value;
                }
            }
            originalVariants.Clear();

            skillsCollected = false;
            initialized = false;
        }
    }
}
