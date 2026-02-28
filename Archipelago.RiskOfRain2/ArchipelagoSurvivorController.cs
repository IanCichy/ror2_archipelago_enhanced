using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.RiskOfRain2.Net;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace Archipelago.RiskOfRain2
{
    public class ArchipelagoSurvivorController : IDisposable
    {
        private bool enabled;
        private int totalSurvivorUnlocks;
        private int receivedSurvivorUnlocks;

        private System.Random rng;

        // Ordered list of all survivors, shuffled deterministically
        private List<SurvivorDef> survivorUnlockOrder;

        // Set of currently unlocked survivor body prefab names
        private HashSet<string> unlockedSurvivorNames;

        private bool initialized;

        public ArchipelagoSurvivorController(bool enabled, int totalSurvivorUnlocks, long seed)
        {
            this.enabled = enabled;
            this.totalSurvivorUnlocks = totalSurvivorUnlocks;
            this.receivedSurvivorUnlocks = 0;
            this.rng = new System.Random((int)seed);
            this.survivorUnlockOrder = new List<SurvivorDef>();
            this.unlockedSurvivorNames = new HashSet<string>();
        }

        public void Initialize()
        {
            if (!enabled) return;

            // Build unlock order immediately so filtering works on the first lobby visit
            BuildUnlockOrder();

            On.RoR2.UI.CharacterSelectController.Awake += CharacterSelectController_Awake;

            SyncSurvivorConfig.OnSurvivorConfigReceived += OnConfigReceived;
            SyncSurvivorUnlock.OnSurvivorUnlockReceived += OnUnlockReceived;

            Run.onRunStartGlobal += OnRunStart;

            Log.LogDebug("ArchipelagoSurvivorController initialized.");
        }

        private void OnRunStart(Run run)
        {
            Log.LogInfo($"Survivor locking active - {survivorUnlockOrder.Count} survivors, " +
                        $"{unlockedSurvivorNames.Count} unlocked. " +
                        $"Unlocks: {receivedSurvivorUnlocks}/{totalSurvivorUnlocks}");
        }

        private void BuildUnlockOrder()
        {
            survivorUnlockOrder.Clear();
            unlockedSurvivorNames.Clear();

            // Collect all valid survivors
            var allSurvivors = SurvivorCatalog.allSurvivorDefs
                .Where(s => s.bodyPrefab != null && !s.hidden)
                .ToList();

            // Shuffle deterministically
            survivorUnlockOrder = allSurvivors.OrderBy(x => rng.Next()).ToList();

            // First survivor is always unlocked
            if (survivorUnlockOrder.Count > 0)
            {
                var firstName = GetSurvivorName(survivorUnlockOrder[0]);
                unlockedSurvivorNames.Add(firstName);
                Log.LogInfo($"Starting survivor: {firstName}");
            }

            // Unlock additional survivors based on received unlocks
            for (int i = 1; i <= receivedSurvivorUnlocks && i < survivorUnlockOrder.Count; i++)
            {
                var name = GetSurvivorName(survivorUnlockOrder[i]);
                unlockedSurvivorNames.Add(name);
            }

            initialized = true;
        }

        private string GetSurvivorName(SurvivorDef def)
        {
            return def.cachedName ?? def.bodyPrefab.name;
        }

        private bool IsSurvivorUnlocked(SurvivorDef def)
        {
            if (def == null || def.bodyPrefab == null) return true;
            var name = GetSurvivorName(def);
            return unlockedSurvivorNames.Contains(name);
        }

        private void CharacterSelectController_Awake(
            On.RoR2.UI.CharacterSelectController.orig_Awake orig,
            RoR2.UI.CharacterSelectController self)
        {
            orig(self);

            if (!enabled || !initialized) return;

            // Hide locked survivor icons in the character select UI
            // The UI uses SurvivorIconController components in children of the CharacterSelectController
            try
            {
                var icons = self.GetComponentsInChildren<SurvivorIconController>(true);
                bool selectedUnlocked = false;

                foreach (var icon in icons)
                {
                    // SurvivorIconController stores a survivorIndex
                    var def = SurvivorCatalog.GetSurvivorDef(icon.survivorIndex);
                    if (def != null && !IsSurvivorUnlocked(def))
                    {
                        icon.gameObject.SetActive(false);
                    }
                    else if (!selectedUnlocked && def != null)
                    {
                        // Auto-select the first unlocked survivor
                        selectedUnlocked = true;
                    }
                }

                Log.LogDebug($"Character select filtered: {unlockedSurvivorNames.Count} survivors visible");
            }
            catch (Exception e)
            {
                Log.LogWarning($"Failed to filter character select UI: {e.Message}");
            }
        }

        public void HandleSurvivorUnlock()
        {
            if (!enabled) return;

            receivedSurvivorUnlocks++;
            Log.LogInfo($"Survivor unlock received! Now at {receivedSurvivorUnlocks}/{totalSurvivorUnlocks}");

            if (receivedSurvivorUnlocks < survivorUnlockOrder.Count)
            {
                var newSurvivor = survivorUnlockOrder[receivedSurvivorUnlocks];
                var survivorName = GetSurvivorName(newSurvivor);
                unlockedSurvivorNames.Add(survivorName);

                var displayName = Language.GetString(newSurvivor.displayNameToken);
                ChatMessage.SendColored($"Survivor unlocked: {displayName}!", Color.yellow);
            }

            new SyncSurvivorUnlock(receivedSurvivorUnlocks).Send(NetworkDestination.Clients);
        }

        private void OnConfigReceived(bool configEnabled, long seed, int totalUnlocks, int currentUnlocks)
        {
            enabled = configEnabled;
            totalSurvivorUnlocks = totalUnlocks;
            receivedSurvivorUnlocks = currentUnlocks;
            rng = new System.Random((int)seed);

            // Rebuild with new config
            if (enabled)
            {
                BuildUnlockOrder();
            }
        }

        private void OnUnlockReceived(int newUnlockCount)
        {
            if (!enabled || !initialized) return;

            // Catch up to the host's unlock count
            while (receivedSurvivorUnlocks < newUnlockCount && receivedSurvivorUnlocks < survivorUnlockOrder.Count - 1)
            {
                receivedSurvivorUnlocks++;
                var newSurvivor = survivorUnlockOrder[receivedSurvivorUnlocks];
                var survivorName = GetSurvivorName(newSurvivor);
                unlockedSurvivorNames.Add(survivorName);
            }
        }

        public void Dispose()
        {
            On.RoR2.UI.CharacterSelectController.Awake -= CharacterSelectController_Awake;
            SyncSurvivorConfig.OnSurvivorConfigReceived -= OnConfigReceived;
            SyncSurvivorUnlock.OnSurvivorUnlockReceived -= OnUnlockReceived;
            Run.onRunStartGlobal -= OnRunStart;

            initialized = false;
        }
    }
}
