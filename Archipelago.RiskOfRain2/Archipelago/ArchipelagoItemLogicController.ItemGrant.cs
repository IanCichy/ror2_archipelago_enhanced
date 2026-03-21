using System;
using System.Threading;
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.UI;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;
using KinematicCharacterController;

namespace Archipelago.RiskOfRain2;

/// <summary>
/// Item granting, traps, classic-mode drop hooks, and pickup notifications.
/// </summary>
public partial class ArchipelagoItemLogicController
{
    private void GiveEquipmentToPlayers(PickupIndex pickupIndex, PlayerCharacterMasterController player)
    {
        var inventory = player.master.inventory;
        var activeEquipment = inventory.GetEquipment(inventory.activeEquipmentSlot);
        if (!activeEquipment.Equals(EquipmentState.empty))
        {
            var playerBody = player.master.GetBodyObject();

            if (playerBody == null)
            {
                //TODO: maybe deal with this
                return;
            }

            var pickupInfo = new GenericPickupController.CreatePickupInfo()
            {
                pickupIndex = PickupCatalog.FindPickupIndex(activeEquipment.equipmentIndex),
                position = playerBody.transform.position,
                rotation = Quaternion.identity
            };
            GenericPickupController.CreatePickup(pickupInfo);
        }

        inventory.SetEquipmentIndex(PickupCatalog.GetPickupDef(pickupIndex)?.equipmentIndex ?? EquipmentIndex.None);
        if (!NetworkServer.active)
        {
            CharacterMasterNotificationQueue.PushPickupNotification(player.master, pickupIndex);
            return;
        }
        DisplayPickupNotification(pickupIndex, player);
    }

    private void GiveItemToPlayers(PickupIndex pickupIndex, PlayerCharacterMasterController player)
    {
        var inventory = player.master.inventory;
        inventory.GiveItemPermanent(PickupCatalog.GetPickupDef(pickupIndex)?.itemIndex ?? ItemIndex.None);
        if (!NetworkServer.active)
        {
            CharacterMasterNotificationQueue.PushPickupNotification(player.master, pickupIndex);
            return;
        }
        DisplayPickupNotification(pickupIndex, player);
    }

    private void GiveMoneyToPlayers()
    {
        foreach (var player in PlayerCharacterMasterController.instances)
        {
            var coefficient = Run.instance.difficultyCoefficient;
            uint money = (uint)(100 * coefficient);
            Log.LogDebug($"Received {money}");
            player.master.money += money;
            Chat.SendBroadcastChat(new Chat.PlayerPickupChatMessage
            {
                subjectAsCharacterBody = player.master.GetBody(),
                baseToken = "PLAYER_PICKUP",
                pickupToken = $"${money}!!!",
                pickupColor = Color.green,
                pickupQuantity = 1
            });
        }
    }

    private void GiveLunarCoinToPlayers()
    {
        foreach (var player in PlayerCharacterMasterController.instances)
        {
            GameObject lunarCoin = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/GenericPickup.prefab").WaitForCompletion();
            SpawnCard spawnCard = ScriptableObject.CreateInstance<SpawnCard>();
            spawnCard.prefab = lunarCoin;

            Xoroshiro128Plus xoroshiro128PlusRadioScanner = new Xoroshiro128Plus(RoR2Application.rng);
            if (DirectorCore.instance != null)
            {
                var card = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Direct,
                    spawnOnTarget = player.master.GetBody().transform,
                    minDistance = 1f,
                    maxDistance = 10f,
                }, xoroshiro128PlusRadioScanner));
                var position = card.transform.position;
                card.GetComponent<GenericPickupController>().pickupIndex = PickupCatalog.FindPickupIndex(RoR2Content.MiscPickups.LunarCoin.miscPickupIndex);
                Log.LogDebug($"coin position {position + new Vector3(0, 10, 0)}");
                NetworkServer.Spawn(card);
            }
        }
    }

    private void GiveExperienceToPlayers()
    {
        foreach (var player in PlayerCharacterMasterController.instances)
        {
            player.master.GiveExperience(1000);
            Chat.SendBroadcastChat(new Chat.PlayerPickupChatMessage
            {
                subjectAsCharacterBody = player.master.GetBody(),
                baseToken = "PLAYER_PICKUP",
                pickupToken = "1000 XP",
                pickupColor = Color.white,
                pickupQuantity = 1
            });
        }
    }

    private void MountainShrineTrap()
    {
        if (!monsterShrineRecently)
        {
            ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cShrine>The Mountain has invited you for a challenge..</style>");
            TeleporterInteraction.instance.AddShrineStack();
            monsterShrineRecently = true;
            Thread thread = new Thread(() => MountainShrineRecently());
            thread.Start();
            PlayShrineSound();
        }
    }

    private void MountainShrineRecently()
    {
        Thread.Sleep(2000);
        Log.LogDebug("You can get another mountain trap now.");
        monsterShrineRecently = false;
    }

    private void PlayShrineSound()
    {
        if (PlayerCharacterMasterController.instances != null)
        {
            EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ShrineUseEffect"), new EffectData
            {
                origin = PlayerCharacterMasterController.instances[0].body.transform.position,
            }, true);
        }
    }

    private void SpawnMonstersTrap()
    {
        if (combatDirector != null && !spawnedMonster)
        {
            var player = PlayerCharacterMasterController.instances[0];
            if (player.master.GetBody() == null)
            {
                return;
            }
            spawnedMonster = true;
            Thread thread = new Thread(() => SpawnedMonstersRecently());
            thread.Start();
            var coefficient = Run.instance.difficultyCoefficient;
            combatDirector.monsterCredit = 100f * coefficient;
            Log.LogDebug($"player position {player.master.GetBody().transform.localPosition} monster credit  100 * {coefficient} =  {100 * coefficient}");
            combatDirector.SpendAllCreditsOnMapSpawns(player.master.GetBody().transform);
            ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cDeath>Incoming Monsters!!</style>");
            PlayShrineSound();
        }
    }

    private void SpawnedMonstersRecently()
    {
        Thread.Sleep(2000);
        Log.LogDebug("You can get another monster trap now.");
        spawnedMonster = false;
    }

    // TODO The currently spawns players to the center of the map aka (0, 0, 0) where we would want it to be a random location.
    private void TeleportPlayer()
    {
        if (!teleportedRecently)
        {
            foreach (NetworkUser local in NetworkUser.readOnlyLocalPlayersList)
            {
                if (local)
                {
                    SpawnCard spawnCard = ScriptableObject.CreateInstance<SpawnCard>();
                    spawnCard = LegacyResourcesAPI.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscBarrel1");

                    Xoroshiro128Plus xoroshiro128PlusRadioScanner = new Xoroshiro128Plus(RoR2Application.rng);
                    if (DirectorCore.instance != null)
                    {
                        var card = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, new DirectorPlacementRule
                        {
                            placementMode = DirectorPlacementRule.PlacementMode.Random
                        }, xoroshiro128PlusRadioScanner));
                        var position = card.transform.position;
                        var directorPlacement = new DirectorPlacementRule
                        {
                            placementMode = DirectorPlacementRule.PlacementMode.Random,
                            minDistance = 5f,
                            maxDistance = 20f,
                        };
                        Log.LogDebug($"directorPlacemnet {directorPlacement.targetPosition} card position {position + new Vector3(0, 10, 0)} player position {local.master.transform.position}");
                        var body = local.master.GetBody();
                        body.GetComponentInChildren<KinematicCharacterMotor>().SetPosition(position + new Vector3(0, 10, 0));
                        new ArchipelagoTeleportClient().Send(NetworkDestination.Clients);
                        card.SetActive(false);
                    }
                }
            }
        }
    }

    private void TeleportedRecently()
    {
        Thread.Sleep(2000);
        Log.LogDebug("You can teleport again");
        teleportedRecently = false;
    }

    private void TimeWarpTrap()
    {
        var time = Run.instance.GetRunStopwatch();
        time += 180;
        Run.instance.SetRunStopwatch(time);
        ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cDeath>Monsters grow stronger with time!</style>");
        TeamManager.instance.SetTeamLevel(TeamIndex.Monster, 1);
    }

    private void DisplayPickupNotification(PickupIndex index, PlayerCharacterMasterController player)
    {
        CharacterMasterNotificationQueue notificationQueueForMaster = CharacterMasterNotificationQueue.GetNotificationQueueForMaster(player.master);
        PickupDef pickupDef = PickupCatalog.GetPickupDef(index);
        ItemIndex itemIndex = pickupDef.itemIndex;
        if (itemIndex != ItemIndex.None)
        {
            notificationQueueForMaster.PushNotification(new CharacterMasterNotificationQueue.NotificationInfo(ItemCatalog.GetItemDef(itemIndex), null), 2f);
        }
        EquipmentIndex equipmentIndex = pickupDef.equipmentIndex;
        if (equipmentIndex != EquipmentIndex.None)
        {
            notificationQueueForMaster.PushNotification(new CharacterMasterNotificationQueue.NotificationInfo(EquipmentCatalog.GetEquipmentDef(equipmentIndex), null), 2f);
        }
        var color = pickupDef.baseColor;
        var index_text = pickupDef.nameToken;
        Chat.SendBroadcastChat(new Chat.PlayerPickupChatMessage
        {
            subjectAsCharacterBody = player.master.GetBody(),
            baseToken = "PLAYER_PICKUP",
            pickupToken = index_text,
            pickupColor = color,
            pickupQuantity = 1
        });
    }

    private void ChestBehavior_ItemDrop(On.RoR2.ChestBehavior.orig_ItemDrop orig, ChestBehavior self)
    {
        var spawnItem = finishedAllChecks || HandleItemDrop();

        if (OnItemDropProcessed != null)
        {
            OnItemDropProcessed(PickedUpItemCount);
        }

        if (spawnItem) orig(self);

        new SyncTotalCheckProgress(finishedAllChecks ? TotalChecks : CurrentChecks, TotalChecks).Send(NetworkDestination.Clients);

        if (finishedAllChecks)
        {
            ArchipelagoTotalChecksObjectiveController.RemoveObjective();
            new AllChecksComplete().Send(NetworkDestination.Clients);
        }
    }

    private void PickupDropletController_CreatePickupDroplet_CreatePickupInfo(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 orig, GenericPickupController.CreatePickupInfo pickupInfo, Vector3 position, Vector3 velocity)
    {
        if (Array.IndexOf(skippedItems, pickupInfo._pickupState) >= 0)
        {
            orig(pickupInfo, position, velocity);
            return;
        }

        // Run `HandleItemDrop()` first so that the `PickedUpItemCount` is incremented by the time `ItemDropProcessed()` is called.
        var spawnItem = finishedAllChecks || HandleItemDrop();

        if (OnItemDropProcessed != null)
        {
            OnItemDropProcessed(PickedUpItemCount);
        }

        if (spawnItem)
        {
            orig(pickupInfo, position, velocity);
        }

        if (!spawnItem)
        {
            EffectManager.SpawnEffect(smokescreenPrefab, new EffectData() { origin = position }, true);
        }

        new SyncTotalCheckProgress(finishedAllChecks ? TotalChecks : CurrentChecks, TotalChecks).Send(NetworkDestination.Clients);

        if (finishedAllChecks)
        {
            ArchipelagoTotalChecksObjectiveController.RemoveObjective();
            new AllChecksComplete().Send(NetworkDestination.Clients);
        }
    }

    private bool HandleItemDrop()
    {
        PickedUpItemCount += 1;
        Log.LogDebug($"PickedUpItemCount + 1 {PickedUpItemCount}  ItemPickupStep {ItemPickupStep}");
        if ((PickedUpItemCount % ItemPickupStep) == 0)
        {
            CurrentChecks++;
            var itemSendName = $"ItemPickup{CurrentChecks}";
            var itemLocationId = ItemStartId + CurrentChecks - 1; // because CurrentChecks is incremented first, subtract one to use the current id
            Log.LogDebug($"Sent out location {itemSendName} (id: {itemLocationId})");

            var packet = new LocationChecksPacket();
            packet.Locations = new List<long> { itemLocationId }.ToArray();

            session.Socket.SendPacketAsync(packet);
            if (CurrentChecks == TotalChecks)
            {
                ArchipelagoTotalChecksObjectiveController.CurrentChecks = ArchipelagoTotalChecksObjectiveController.TotalChecks;
                finishedAllChecks = true;
            }
            return false;
        }
        return true;
    }
}
