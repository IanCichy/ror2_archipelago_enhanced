using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.RiskOfRain2.Extensions;
using Archipelago.RiskOfRain2.Services;
using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.UI;
using R2API.Networking;
using R2API.Utils;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;
using System.Collections.ObjectModel;
using KinematicCharacterController;

namespace Archipelago.RiskOfRain2;

public partial class ArchipelagoItemLogicController : IDisposable
    {
        public int PickedUpItemCount { get; set; }
        public int ItemPickupStep { get; set; }
        public long ItemStartId { get; private set; }
        public int CurrentChecks { get; set; }
        public int TotalChecks { get; set; }
        System.Random rnd = new System.Random();

        internal StageBlockerService StageBlockerService { get; set; }
        internal ItemPoolService ItemPoolService { get; set; }

        public long[] ChecksTogether { get; set; }
        public long[] MissingChecks { get; set; }

        public delegate void ItemDropProcessedHandler(int pickedUpCount);
        public event ItemDropProcessedHandler OnItemDropProcessed;

        private bool finishedAllChecks = false;
        private ArchipelagoSession session;
        private Queue<KeyValuePair<long, string>> itemReceivedQueue = new Queue<KeyValuePair<long, string>>();
        private Queue<KeyValuePair<long, string>> environmentReceivedQueue = new Queue<KeyValuePair<long, string>>();
        private Queue<KeyValuePair<long, string>> fillerReceivedQueue = new Queue<KeyValuePair<long, string>>();
        private Queue<KeyValuePair<long, string>> trapReceivedQueue = new Queue<KeyValuePair<long, string>>();
        private Queue<KeyValuePair<long, string>> stageReceivedQueue = new Queue<KeyValuePair<long, string>>();
        private Queue<KeyValuePair<long, string>> poolReceivedQueue = new Queue<KeyValuePair<long, string>>();
        // TODO get magic numbers from somewhere else (eg move to LocationCheckService.cs)
        private const long environmentRangeLower = 37700;
        private const long environmentRangeUpper = 37999;
        private const long fillerRangeLower = 37300;
        private const long fillerRangeUpper = 37399;
        private const long trapRangeLower = 37400;
        private const long trapRangeUpper = 37499;
        private const long poolRangeLower = 37100;
        private const long poolRangeUpper = 37199;
        private const long stageRangeLower = 37500;
        private const long stageRangeUpper = 37599;
        private bool spawnedMonster = false;
        private bool monsterShrineRecently = false;
        private bool teleportedRecently = false;
        private bool exitedPod = false;
        private UniquePickup[] skippedItems;

        private GameObject smokescreenPrefab;
        private CombatDirector combatDirector;

        private bool IsInGame
        {
            get
            {
                return (RoR2Application.isInSinglePlayer || RoR2Application.isInMultiPlayer) && Run.instance != null && exitedPod;
            }
        }

        public ArchipelagoItemLogicController(ArchipelagoSession session)
        {
            this.session = session;
            // get the initial id from the seed for backwards compatibility
            ItemStartId = session.Locations.GetLocationIdFromName("Risk of Rain 2", "ItemPickup1");

            // TODO all the hooks for ArchipelagoItemLogicController should probably be moved into a hook method
            On.RoR2.RoR2Application.Update += RoR2Application_Update;
            On.RoR2.SceneDirector.Start += SceneDirector_Start;
            session.Socket.PacketReceived += Session_PacketReceived;
            session.Items.ItemReceived += Items_ItemReceived;
            On.RoR2.CombatDirector.Awake += CombatDirector_Awake;
            On.RoR2.SurvivorPodController.OnPassengerExit += SurvivorPodController_OnPassengerExit;
            Log.LogDebug("Okay finished hooking.");
            smokescreenPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Junk/Bandit/SmokescreenEffect.prefab").WaitForCompletion();
            
            Log.LogDebug("Okay, finished getting prefab.");
            Log.LogDebug($"smokescreen {smokescreenPrefab}");

            skippedItems = [
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixBlue.equipmentIndex)),
                //new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixEcho.equipmentIndex)), // Causes NRE... Not sure why.
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixHaunted.equipmentIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixLunar.equipmentIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixPoison.equipmentIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixRed.equipmentIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixWhite.equipmentIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.MiscPickups.LunarCoin.miscPickupIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Items.ArtifactKey.itemIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Bomb.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Command.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.EliteOnly.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Enigma.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.FriendlyFire.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Glass.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.MixEnemy.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.MonsterTeamGainsItems.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.RandomSurvivorOnRespawn.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Sacrifice.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.ShadowClone.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.SingleMonsterType.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Swarms.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.TeamDeath.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.WeakAssKnees.artifactIndex)),
                new UniquePickup(PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.WispOnDeath.artifactIndex)),               
            ];
            Log.LogDebug("Ok, finished browsing catalog.");
        }

        private void SceneDirector_Start(On.RoR2.SceneDirector.orig_Start orig, SceneDirector self)
        {
            orig(self);
            exitedPod = true;
            ArchipelagoClient.isInGame = true;
        }

        private void SurvivorPodController_OnPassengerExit(On.RoR2.SurvivorPodController.orig_OnPassengerExit orig, SurvivorPodController self, GameObject passenger)
        {
            orig(self, passenger);
            // prevent teleport on exiting pod
            Thread thread = new Thread(() => TeleportedRecently());
            thread.Start();
            teleportedRecently = true;
            exitedPod = true;
            ArchipelagoClient.isInGame = true;
        }

        private void CombatDirector_Awake(On.RoR2.CombatDirector.orig_Awake orig, CombatDirector self)
        {
            orig(self);
            combatDirector = self;
        }

        private void Items_ItemReceived(ReceivedItemsHelper helper)
        {
            var newItem = helper.DequeueItem();
            if (ArchipelagoClient.lastReceivedItemindex < helper.AllItemsReceived.Count)
            {
                EnqueueItem(newItem.ItemId);
                ArchipelagoClient.lastReceivedItemindex = helper.AllItemsReceived.Count;
            }
            else if (environmentRangeLower <= newItem.ItemId && newItem.ItemId <= environmentRangeUpper)
            {
                EnqueueItem(newItem.ItemId);
            }
        }
        private void Check_Locations(ReadOnlyCollection<long> item)
        {
            long[] missing = new long[item.Count];
            item.CopyTo(missing, 0);
            if (MissingChecks != null)
            {
                for(int i = 0; i < missing.Length; i++)
                {
                    var missingList = new List<long>(MissingChecks);
                    var missingIndex = Array.IndexOf(MissingChecks, missing[i]);
                    missingList.RemoveAt(missingIndex);
                    MissingChecks = missingList.ToArray();
                }
                Update_MissingChecks();
            }

        }
        // TODO This does not work on your own items being collected
        private void Update_MissingChecks()
        {
            if(MissingChecks.Count() > 0 && ChecksTogether != null)
            {
                var missingIndex = Array.IndexOf(ChecksTogether, MissingChecks[0]);
                Log.LogInfo($"Last item collected is {missingIndex}/{TotalChecks} next missing id is {MissingChecks[0]}");
                CurrentChecks = missingIndex;
                PickedUpItemCount = missingIndex * ItemPickupStep;
                ArchipelagoTotalChecksObjectiveController.CurrentChecks = CurrentChecks;
            }
            
        }
        /// <summary>
        /// Initializes location tracking state from the current session.
        /// Called from SetupRun() because ItemLogic is created AFTER TryConnectAndLogin(),
        /// so it misses the Connected packet that Session_PacketReceived would have handled.
        /// </summary>
        public void InitializeFromConnectionState(bool isClassic, int itemPickupStep)
        {
            Log.LogDebug($"InitializeFromConnectionState classic={isClassic}");

            if (isClassic)
            {
                On.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet_CreatePickupInfo;
                On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop;
                session.Locations.CheckedLocationsUpdated += Check_Locations;
            }

            ItemPickupStep = itemPickupStep;

            var locationsChecked = session.Locations.AllLocationsChecked;
            var missingLocations = session.Locations.AllMissingLocations;
            TotalChecks = locationsChecked.Count + missingLocations.Count;
            ChecksTogether = locationsChecked.Concat(missingLocations).OrderBy(n => n).ToArray();
            MissingChecks = missingLocations.ToArray();
            Log.LogDebug($"Missing Checks {missingLocations.Count} totalChecks {TotalChecks} Locations Checked {locationsChecked.Count}");

            if (ItemStartId == -1)
            {
                ItemStartId = session.Locations.GetLocationIdFromName("Risk of Rain 2", "ItemPickup1");
                if (ItemStartId == -1) ItemStartId = 38000;
            }

            if (missingLocations.Count == 0)
            {
                CurrentChecks = TotalChecks;
                finishedAllChecks = true;
            }
            else if (isClassic)
            {
                var missingIndex = Array.IndexOf(ChecksTogether, missingLocations[0]);
                Log.LogInfo($"Missing index is {missingIndex} first missing id is {missingLocations[0]}");
                ItemStartId = ChecksTogether[0];
                Log.LogInfo($"ItemStartId {ItemStartId}");
                CurrentChecks = missingIndex;
            }
            else
            {
                CurrentChecks = ChecksTogether.Length - missingLocations.Count;
            }

            ArchipelagoTotalChecksObjectiveController.CurrentChecks = CurrentChecks;
            ArchipelagoTotalChecksObjectiveController.TotalChecks = TotalChecks;

            new SyncTotalCheckProgress(CurrentChecks, TotalChecks).Send(NetworkDestination.Clients);
            PickedUpItemCount = CurrentChecks * ItemPickupStep;
        }

        private void Session_PacketReceived(ArchipelagoPacketBase packet)
        {
            switch (packet.PacketType)
            {
                case ArchipelagoPacketType.Connected:
                    {
                        var connectedPacket = packet as ConnectedPacket;

                        // hook the classic location handler if not using EnvironmentsAsItems
                        bool classic;
                        if (connectedPacket.SlotData.TryGetValue("goal", out var classicmodeobject))
                        {
                            classic = !Convert.ToBoolean(classicmodeobject);
                        }
                        else classic = true;

                        Log.LogDebug($"Detected classic_mode from ArchipelagoItemLogicController? {classic}");

                        // TODO maybe this should be moved into a hook method with the other hooks from the constructor
                        if (classic)
                        {

                            On.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet_CreatePickupInfo;
                            On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop;
                            
                            session.Locations.CheckedLocationsUpdated += Check_Locations;
                        }
                        else
                        {
                            On.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet_CreatePickupInfo;
                            On.RoR2.ChestBehavior.ItemDrop -= ChestBehavior_ItemDrop;

                            session.Locations.CheckedLocationsUpdated -= Check_Locations;
                        }


                        // Add 1 because the user's YAML will contain a value equal to "number of pickups before sent location"
                        ItemPickupStep = Convert.ToInt32(connectedPacket.SlotData["itemPickupStep"]) + 1;
                        // TODO ItemPickupStep should be set by ArchipelagoClient.cs instead of here (for consistency)
                        TotalChecks = connectedPacket.LocationsChecked.Count() + connectedPacket.MissingChecks.Count();
                        ChecksTogether = connectedPacket.LocationsChecked.Concat(connectedPacket.MissingChecks).ToArray();
                        ChecksTogether = ChecksTogether.OrderBy(n => n).ToArray();
                        MissingChecks = connectedPacket.MissingChecks;
                        Log.LogDebug($"Missing Checks {connectedPacket.MissingChecks.Count()} totalChecks {TotalChecks} Locations Checked {connectedPacket.LocationsChecked.Count()}");

                        // in the case the id is incorrectly set, attempt to set it again
                        if (ItemStartId == -1)
                        {
                            ItemStartId = session.Locations.GetLocationIdFromName("Risk of Rain 2", "ItemPickup1");
                            // in case that fails, just manually set it to a default value
                            if (ItemStartId == -1) ItemStartId = 38000;
                            // NOTE: that this solution will sometimes result in the id just being blatently wrong the first time someone attempts to join a seed.
                            // A more rubust way of checking the first id could be done but is not worth the effort.
                            // The player can just restart the lobby and the datapackage should be fixed.

                            // TODO maybe go back and write a more rubust way to make sure the CurrentChecks make sense when the DataPackage Packet is recieved
                        }

                        if (connectedPacket.MissingChecks.Count() == 0)
                        {
                            CurrentChecks = TotalChecks;
                            finishedAllChecks = true;
                        }
                        // resume pickups with the first missing item
                        else if (classic)
                        {
                            var missingIndex = Array.IndexOf(ChecksTogether, connectedPacket.MissingChecks[0]);
                            Log.LogInfo($"Missing index is {missingIndex} first missing id is {connectedPacket.MissingChecks[0]}");
                            ItemStartId = ChecksTogether[0];
                            Log.LogInfo($"ItemStartId {ItemStartId}");
                            CurrentChecks = missingIndex;
                        } else
                        {
                            CurrentChecks = ChecksTogether.Length - connectedPacket.MissingChecks.Count();
                        }

                        ArchipelagoTotalChecksObjectiveController.CurrentChecks = CurrentChecks;
                        ArchipelagoTotalChecksObjectiveController.TotalChecks = TotalChecks;

                        new SyncTotalCheckProgress(CurrentChecks, TotalChecks).Send(NetworkDestination.Clients);
                        // Add up pickedUpItemCount so that resuming a game is possible. The intended behavior is that you immediately receive
                        // all of the items you are granted. This is for restarting (in case you lose a run but are not in commencement). 
                        PickedUpItemCount = CurrentChecks * ItemPickupStep;
                        break;
                    }
/*                case ArchipelagoPacketType.ReceivedItems:
                    var receivedItemsPacket = (ReceivedItemsPacket)packet;


                    break;*/
            }
        }



        public void EnqueueItem(long itemId)
        {
            // convert the itemId to a name here instead of in the main loop
            // this prevents a call to the session in the RoR2Application_Update
            var itemName = session.Items.GetItemName(itemId);
            // We will keep track of the item id as well as since the name cannot be converted back to an id.

            // Separate the environments and items so that the environments can be precollected
            //  when the run starts.
            if (environmentRangeLower <= itemId && itemId <= environmentRangeUpper)
            {
                environmentReceivedQueue.Enqueue(new KeyValuePair<long, string>(itemId, itemName));
            }
            else if (fillerRangeLower <= itemId && itemId <= fillerRangeUpper)
            {
                fillerReceivedQueue.Enqueue(new KeyValuePair<long, string>(itemId, itemName));
            }
            else if (trapRangeLower <= itemId && itemId <= trapRangeUpper) {
                trapReceivedQueue.Enqueue(new KeyValuePair<long, string>(itemId, itemName));
            }
            else if (poolRangeLower <= itemId && itemId <= poolRangeUpper)
            {
                poolReceivedQueue.Enqueue(new KeyValuePair<long, string>(itemId, itemName));
            }
            else if (stageRangeLower <= itemId && itemId <= stageRangeUpper)
            {
                stageReceivedQueue.Enqueue(new KeyValuePair<long, string>(itemId, itemName));
            }
            else
            {
                itemReceivedQueue.Enqueue(new KeyValuePair<long, string>(itemId, itemName));
            }

        }

        public void Dispose()
        {
            On.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet_CreatePickupInfo;
            On.RoR2.ChestBehavior.ItemDrop -= ChestBehavior_ItemDrop;
            On.RoR2.RoR2Application.Update -= RoR2Application_Update;
            On.RoR2.SceneDirector.Start -= SceneDirector_Start;
            On.RoR2.CombatDirector.Awake -= CombatDirector_Awake;
            On.RoR2.SurvivorPodController.OnPassengerExit -= SurvivorPodController_OnPassengerExit;

            if (session != null)
            {
                session.Socket.PacketReceived -= Session_PacketReceived;
                session.Items.ItemReceived -= Items_ItemReceived;
                session = null;
            }
        }

        /// <summary>
        /// Enqueues all items from the session's AllItemsReceived list and drains
        /// the library's internal queue. Handles two cases:
        /// 1. First connect: ItemLogic is created AFTER TryConnectAndLogin, so the
        ///    initial item burst is in AllItemsReceived but was missed by ItemReceived.
        /// 2. Session reuse (run 2+): the player's inventory is empty but all items
        ///    are still in AllItemsReceived and need to be re-granted.
        /// </summary>
        public void ProcessAllReceivedItems()
        {
            var helper = session.Items;
            var allItems = helper.AllItemsReceived;
            Log.LogDebug($"ProcessAllReceivedItems: enqueuing {allItems.Count} items");
            for (int i = 0; i < allItems.Count; i++)
            {
                EnqueueItem(allItems[i].ItemId);
            }
            ArchipelagoClient.lastReceivedItemindex = allItems.Count;

            // Drain the library's internal queue so future ItemReceived events
            // don't return stale items that we've already processed above.
            while (helper.DequeueItem() != null) { }
        }

        /**
         * At the start of a run, we need to precollect all environments before environments are picked for stages.
         */
        public void Precollect()
        {
            while (environmentReceivedQueue.Any())
            {
                Log.LogDebug("Precollecting environment...");
                HandleReceivedEnvironmentQueueItem();
            }
        }

}

