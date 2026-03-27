////////////////////////////////////////////////////////////////////////////////////////////////////
/// Bazaar Between Time Handler Features:
/// - Lunar Shop Replacement aka AP Checks Shop

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.RiskOfRain2.Network;
using Archipelago.RiskOfRain2.UI;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Services
{
    public class BazaarService : IService
    {
        private const long BazaarShopLocationIdStart = 82251;

        private readonly ArchipelagoSession session;
        private readonly Action<int> sendLocation;

        private Queue<long> bazaarShopCheckQueue = new Queue<long>();
        private int bazaarShopChecksTotal = 0;
        private HashSet<ShopTerminalBehavior> apCheckTerminals = new HashSet<ShopTerminalBehavior>();
        private Dictionary<ShopTerminalBehavior, long> terminalLocationIds = new Dictionary<ShopTerminalBehavior, long>();
        private Dictionary<long, ScoutedItemInfo> bazaarShopScoutedItems = new Dictionary<long, ScoutedItemInfo>();

        public BazaarService(ArchipelagoSession session, Action<int> sendLocation)
        {
            this.session = session;
            this.sendLocation = sendLocation;
        }

        public void Register()
        {
            On.RoR2.SceneDirector.PopulateScene += SceneDirector_PopulateScene_Bazaar;
            On.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;
        }

        public void Unregister()
        {
            On.RoR2.SceneDirector.PopulateScene -= SceneDirector_PopulateScene_Bazaar;
            On.RoR2.PurchaseInteraction.OnInteractionBegin -= PurchaseInteraction_OnInteractionBegin;
        }

        public void InitializeBazaarShopQueue(int totalChecks)
        {
            bazaarShopChecksTotal = totalChecks;
            bazaarShopCheckQueue.Clear();
            bazaarShopScoutedItems.Clear();

            var locationsToScout = new List<long>();
            for (int i = 0; i < totalChecks; i++)
            {
                long locationId = BazaarShopLocationIdStart + i;
                if (!session.Locations.AllLocationsChecked.Contains(locationId))
                {
                    bazaarShopCheckQueue.Enqueue(locationId);
                    locationsToScout.Add(locationId);
                }
            }

            Log.LogDebug($"Bazaar shop queue initialized with {bazaarShopCheckQueue.Count} checks.");

            if (locationsToScout.Count > 0)
            {
                session.Locations.ScoutLocationsAsync(locationsToScout.ToArray())
                    .ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            Log.LogError($"Failed to scout Bazaar shop locations: {task.Exception}");
                            return;
                        }
                        bazaarShopScoutedItems = task.Result;
                        Log.LogDebug($"Scouted {bazaarShopScoutedItems.Count} Bazaar shop locations.");
                    });
            }
        }

        private void SceneDirector_PopulateScene_Bazaar(
            On.RoR2.SceneDirector.orig_PopulateScene orig,
            SceneDirector self)
        {
            orig(self);

            if (LocationCheckService.CurrentSceneDef == null ||
                LocationCheckService.CurrentSceneDef.cachedName != "bazaar") return;

            ReplaceBazaarShopTerminals();
        }

        private void ReplaceBazaarShopTerminals()
        {
            if (bazaarShopCheckQueue.Count == 0)
            {
                Log.LogDebug("No Bazaar shop checks remaining, skipping replacement.");
                return;
            }

            var allTerminals = UnityEngine.Object.FindObjectsOfType<ShopTerminalBehavior>();
            Log.LogDebug($"Found {allTerminals.Length} ShopTerminalBehavior instances in Bazaar.");

            foreach (var terminal in allTerminals)
            {
                if (bazaarShopCheckQueue.Count == 0) break;

                var purchaseInteraction = terminal.GetComponent<PurchaseInteraction>();
                if (purchaseInteraction == null) continue;
                if (purchaseInteraction.costType != CostTypeIndex.LunarCoin) continue;

                Log.LogDebug($"Found Lunar Coin terminal: {terminal.gameObject.name}");

                if (terminal.gameObject.name.Contains("Reroll") ||
                    terminal.gameObject.name.Contains("Cleanse") ||
                    terminal.gameObject.name.Contains("Scrapper"))
                {
                    Log.LogDebug($"Skipping terminal: {terminal.gameObject.name}");
                    continue;
                }

                long locationId = bazaarShopCheckQueue.Dequeue();
                terminalLocationIds[terminal] = locationId;

                string displayName = GetBazaarShopTerminalName(locationId);
                string tokenKey = $"BAZAAR_AP_CHECK_{locationId}_NAME";
                string tokenContext = $"BAZAAR_AP_CHECK_{locationId}_CONTEXT";
                LanguageAPI.Add(tokenKey, displayName);
                LanguageAPI.Add(tokenContext, $"Purchase <style=cIsUtility>{displayName}</style>");

                apCheckTerminals.Add(terminal);
                purchaseInteraction.displayNameToken = tokenKey;
                purchaseInteraction.contextToken = tokenContext;
                terminal.SetPickup(UniquePickup.none, true);

                Log.LogDebug($"Replaced {terminal.gameObject.name} with AP check ID {locationId} ({displayName}). {bazaarShopCheckQueue.Count} remaining in queue.");
            }
        }

        private string GetBazaarShopTerminalName(long locationId)
        {
            if (bazaarShopScoutedItems.TryGetValue(locationId, out var scoutedItem))
            {
                string itemName = scoutedItem.ItemName ?? "Unknown Item";
                string playerName = session.Players.GetPlayerName(scoutedItem.Player) ?? "Unknown Player";
                return $"{playerName}'s {itemName}";
            }
            return "Archipelago Item";
        }

        private void PurchaseInteraction_OnInteractionBegin(
            On.RoR2.PurchaseInteraction.orig_OnInteractionBegin orig,
            PurchaseInteraction self,
            Interactor interactor)
        {
            if (LocationCheckService.CurrentSceneDef?.cachedName == "bazaar")
            {
                var shopTerminal = self.GetComponent<ShopTerminalBehavior>();
                if (shopTerminal != null && apCheckTerminals.Contains(shopTerminal))
                {
                    if (terminalLocationIds.TryGetValue(shopTerminal, out long locationId))
                    {
                        Log.LogDebug($"Bazaar shop AP check purchased, sending location {locationId}.");

                        apCheckTerminals.Remove(shopTerminal);
                        terminalLocationIds.Remove(shopTerminal);

                        var characterBody = interactor.GetComponent<CharacterBody>();
                        if (characterBody != null)
                        {
                            var networkUser = Util.LookUpBodyNetworkUser(characterBody);
                            if (networkUser != null)
                            {
                                networkUser.DeductLunarCoins((uint)self.cost);
                            }
                        }

                        ArchipelagoTotalChecksObjectiveController.CurrentChecks++;
                        int currentChecks = ArchipelagoTotalChecksObjectiveController.CurrentChecks;
                        int totalChecks = ArchipelagoTotalChecksObjectiveController.TotalChecks;
                        new SyncTotalCheckProgress(currentChecks, totalChecks).Send(NetworkDestination.Clients);

                        sendLocation((int)locationId);

                        shopTerminal.SetPickup(UniquePickup.none, false);
                        shopTerminal.SetHasBeenPurchased(true);
                        self.enabled = false; // corrects second lunar coin interaction issue
                    }
                    return;
                }
            }

            orig(self, interactor);
        }

        public void OnSceneChanged()
        {
            // Re-enqueue any unpurchased Bazaar shop checks
            foreach (var kvp in terminalLocationIds)
            {
                Log.LogDebug($"Re-enqueuing unpurchased Bazaar shop check {kvp.Value}.");
                bazaarShopCheckQueue.Enqueue(kvp.Value);
            }
            apCheckTerminals.Clear();
            terminalLocationIds.Clear();
        }
    }
}