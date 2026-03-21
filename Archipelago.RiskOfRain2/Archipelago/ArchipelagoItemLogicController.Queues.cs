using System.Collections.Generic;
using System.Linq;
using Archipelago.RiskOfRain2.Extensions;
using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.Services;
using Archipelago.RiskOfRain2.UI;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;

namespace Archipelago.RiskOfRain2;

/// <summary>
/// Queue drain logic: processes items received from AP each frame.
/// </summary>
public partial class ArchipelagoItemLogicController
{
    private void RoR2Application_Update(On.RoR2.RoR2Application.orig_Update orig, RoR2Application self)
    {
        if (environmentReceivedQueue.Any())
        {
            HandleReceivedEnvironmentQueueItem();
        }
        if (stageReceivedQueue.Any())
        {
            HandleReceivedStageQueueItem();
        }
        if (poolReceivedQueue.Any())
        {
            HandleReceivedPoolQueueItem();
        }
        if (IsInGame)
        {
            if (itemReceivedQueue.Any())
            {
                HandleReceivedItemQueueItem();
            }

            if (fillerReceivedQueue.Any())
            {
                HandleReceivedFillerQueueItem();
            }
            if (trapReceivedQueue.Any())
            {
                HandleReceivedTrapQueueItem();
            }
        }

        orig(self);
    }

    private void HandleReceivedEnvironmentQueueItem()
    {
        KeyValuePair<long, string> itemReceived = environmentReceivedQueue.Dequeue();

        long itemIdReceived = itemReceived.Key;
        string itemNameReceived = itemReceived.Value;
        // The item ID encodes the Python/AP environment ID as (environmentRangeLower + pythonId).
        // cachedLocationsNames is keyed by Python IDs, so this lookup works directly.
        int pythonId = (int)(itemIdReceived - environmentRangeLower);
        Log.LogDebug($"Handling environment with pythonId {pythonId}, name {itemNameReceived}");
        StageBlockerService?.UnBlock(pythonId);
    }

    private void HandleReceivedFillerQueueItem()
    {
        KeyValuePair<long, string> itemReceived = fillerReceivedQueue.Dequeue();

        long itemIdReceived = itemReceived.Key;
        string itemNameReceived = itemReceived.Value;
        switch (itemIdReceived)
        {
            // Money
            case 37301:
                GiveMoneyToPlayers();
                ChatMessage.Send("<style=cIsUtility>[AP]</style> Received: <style=cShrine>Money</style>");
                break;
            // Lunar Coin
            case 37302:
                GiveLunarCoinToPlayers();
                ChatMessage.Send("<style=cIsUtility>[AP]</style> Received: <style=cShrine>Lunar Coin</style>");
                break;
            // EXP
            case 37303:
                GiveExperienceToPlayers();
                ChatMessage.Send("<style=cIsUtility>[AP]</style> Received: <style=cShrine>Experience</style>");
                break;
        }
    }

    private void HandleReceivedTrapQueueItem()
    {
        KeyValuePair<long, string> itemReceived = trapReceivedQueue.Dequeue();

        long itemIdReceived = itemReceived.Key;
        string itemNameReceived = itemReceived.Value;
        switch (itemIdReceived)
        {
            // Adds an extra boss to teleporter
            case 37401:
                MountainShrineTrap();
                break;
            // Increases monsters level by adding time to the clock.
            case 37402:
                TimeWarpTrap();
                break;
            // Immitate Combat Shrine.
            case 37403:
                SpawnMonstersTrap();
                break;
            case 37404:
                TeleportPlayer();
                break;
        }
    }

    private void HandleReceivedStageQueueItem()
    {
        KeyValuePair<long, string> itemReceived = stageReceivedQueue.Dequeue();

        long itemIdRecieved = itemReceived.Key;
        string itemNameReceived = itemReceived.Value;
        if (itemIdRecieved == 37505)
        {
            if (StageBlockerService != null)
            {
                StageBlockerService.AmountOfStages += 1;
                StageBlockerService.UnlockEnvironmentsForProgressiveStages(StageBlockerService.AmountOfStages);
            }
        }
        else if (StageBlockerService != null)
        {
            StageBlockerService.StageUnlocks[itemNameReceived] = true;
            // Parse the stage tier from the item name (e.g. "Stage 2" → 2)
            if (int.TryParse(itemNameReceived.Replace("Stage ", ""), out int tier))
            {
                StageBlockerService.UnlockEnvironmentsForStage(tier);
            }
        }
    }

    private void HandleReceivedPoolQueueItem()
    {
        KeyValuePair<long, string> itemReceived = poolReceivedQueue.Dequeue();

        long itemIdReceived = itemReceived.Key;
        string itemNameReceived = itemReceived.Value;

        if (ItemPoolService == null) return;

        var newItems = ItemPoolService.ExpandPool(itemIdReceived);
        if (newItems.Count > 0)
        {
            string tierColor = GetPoolTierColor(itemIdReceived);
            string tierName = itemNameReceived?.Replace(" Pool Expansion", "") ?? "Unknown";
            string itemList = string.Join(", ", newItems);
            ChatMessage.Send($"<style=cIsUtility>[AP]</style> <color={tierColor}>{tierName}</color> pool expanded! Now available: <color={tierColor}>{itemList}</color>");
        }
    }

    private static string GetPoolTierColor(long itemId)
    {
        int tierIndex = (int)(itemId - 37100) - 1; // 37101→0 (White), 37107→6 (Equipment)
        if (tierIndex >= 0 && tierIndex < ItemPoolService.TierHexColors.Length)
            return ItemPoolService.TierHexColors[tierIndex];
        return ItemPoolService.TierHexColors[0];
    }

    private void HandleReceivedItemQueueItem()
    {
        KeyValuePair<long, string> itemReceived = itemReceivedQueue.Dequeue();

        long itemIdRecieved = itemReceived.Key;
        string itemNameReceived = itemReceived.Value;

        Log.LogDebug($"Handling item with itemid {itemIdRecieved} with name {itemNameReceived}");

        switch (itemIdRecieved)
        {
            // TODO move the magic numbers to variables
            // "Common Item"
            case 37002:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var common = Run.instance.availableTier1DropList.PickRandom();
                    GiveItemToPlayers(common, player);
                }
                break;
            // "Uncommon Item"
            case 37003:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var uncommon = Run.instance.availableTier2DropList.PickRandom();
                    GiveItemToPlayers(uncommon, player);
                }

                break;
            // "Legendary Item"
            case 37004:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var legendary = Run.instance.availableTier3DropList.PickRandom();
                    GiveItemToPlayers(legendary, player);
                }

                break;
            // "Boss Item"
            case 37005:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var boss = Run.instance.availableBossDropList.PickRandom();
                    GiveItemToPlayers(boss, player);
                }
                break;
            // "Lunar Item"
            case 37006:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var lunar = Run.instance.availableLunarCombinedDropList.PickRandom();
                    var pickupDef = PickupCatalog.GetPickupDef(lunar);
                    if (pickupDef.itemIndex != ItemIndex.None)
                    {
                        GiveItemToPlayers(lunar, player);
                    }
                    else if (pickupDef.equipmentIndex != EquipmentIndex.None)
                    {
                        GiveEquipmentToPlayers(lunar, player);
                    }
                }
                break;

            // "Equipment"
            case 37007:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var equipment = Run.instance.availableEquipmentDropList.PickRandom();
                    GiveEquipmentToPlayers(equipment, player);
                }
                break;
            // "Item Scrap, White"
            case 37008:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapWhite.itemIndex), player);
                }
                break;
            // "Item Scrap, Green"
            case 37009:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapGreen.itemIndex), player);
                }
                break;
            // "Item Scrap, Red"
            case 37010:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapRed.itemIndex), player);
                }
                break;
            // "Item Scrap, Yellow"
            case 37011:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapYellow.itemIndex), player);
                }
                break;
            // "Void Item"
            case 37012:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    int voidWeight = 70 + 40 + 10 + 5;
                    int voidChoice = rnd.Next(voidWeight);
                    var voidItem = new PickupIndex();
                    if (voidChoice <= 70)
                    {
                        voidItem = Run.instance.availableVoidTier1DropList.PickRandom();
                    }
                    else if (voidChoice <= 110)
                    {
                        voidItem = Run.instance.availableVoidTier2DropList.PickRandom();
                    }
                    else if (voidChoice <= 120)
                    {
                        voidItem = Run.instance.availableVoidTier3DropList.PickRandom();
                    }
                    else
                    {
                        voidItem = Run.instance.availableVoidBossDropList.PickRandom();
                    }
                    GiveItemToPlayers(voidItem, player);
                }
                break;
            // Beads of Fealty
            case 37013:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.LunarTrinket.itemIndex), player);
                }
                break;
            // Radar Scanner Equipment
            case 37014:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    GiveEquipmentToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Equipment.Scanner.equipmentIndex), player);
                }
                break;
            // "Dio's Best Friend"
            case 37001:
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ExtraLife.itemIndex), player);
                }
                break;

        }
    }
}
