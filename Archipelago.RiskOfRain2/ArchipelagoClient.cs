using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.RiskOfRain2.Console;
using Archipelago.RiskOfRain2.Handlers;
using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.UI;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Archipelago.RiskOfRain2
{
    //TODO: perhaps only use particular drops as fodder for item pickups (i.e. only chest drops/interactable drops) then set options based on them maybe
    public class ArchipelagoClient : IDisposable
    {
        public delegate void ClientDisconnected(string reason);
        public event ClientDisconnected OnClientDisconnect;

        public string lastServerUrl { get; set; }
        public string lastSlotName { get; set; }
        public string lastPassword { get; set; }
        internal DeathLinkHandler Deathlinkhandler { get; private set; }
        internal StageBlockerHandler Stageblockerhandler { get; private set; }
        internal LocationHandler Locationhandler { get; private set; }
        internal ShrineChanceHandler shrineChanceHelper { get; private set; }

        public ArchipelagoItemLogicController ItemLogic;
        public ArchipelagoLocationCheckProgressBarUI itemCheckBar;
        public ArchipelagoLocationCheckProgressBarUI shrineCheckBar;

        public ArchipelagoLootPoolController LootPoolController;
        public ArchipelagoSkillRandomizer SkillRandomizer;
        public ArchipelagoSurvivorController SurvivorController;

        private ArchipelagoSession session;
        private DeathLinkService deathLinkService;
        private bool finalStageDeath = false;
        private bool isEndingAcceptable = false;
        public GameObject ReleasePanel;
        public GameObject CollectPanel;
        public GameObject ReleasePromptPanel;
        public GameObject CollectPromptPanel;
        public delegate void ReleaseClick(bool prompt);
        public static ReleaseClick OnReleaseClick;
        public delegate void CollectClick(bool prompt);
        public static CollectClick OnCollectClick;
        public bool reconnecting { get; set; } = false;
        public static int lastReceivedItemindex { get; set; } = 0;
        public static bool isInGame { get; set; } = false;
        //public static ReleaseClick OnButtonClick;
        public static string connectedPlayerName;
        public static string victoryCondition;
        // Acceptable ending types
        private GameEndingDef[] acceptableEndings;
        // Acceptable stages to die on
        private string[] acceptableLosses;

        // Loot pool limiting settings
        private bool lootPoolLimiting = false;
        private long lootPoolSeed = 0;
        private int itemsPerExpansion = 1;
        private int startingWhiteItems = 3;
        private int startingGreenItems = 2;
        private int startingRedItems = 1;
        private int startingBossItems = 1;
        private int startingLunarItems = 1;
        private int startingEquipment = 1;

        // Skill randomization settings
        private bool skillRandomization = false;
        private long skillSeed = 0;
        private int totalSkillUnlocks = 10;
        private int startingSkills = 4;

        // Survivor locking settings
        private bool survivorLocking = false;
        private long survivorSeed = 0;
        private int totalSurvivorUnlocks = 5;

        // Per-run settings cached from slot data
        private uint itemPickupStep = 3;
        private uint shrineUseStep = 3;
        private bool isExploreMode = false;
        private bool enableDeathLink = false;
        private Dictionary<string, object> cachedSlotData;

        public void Connect(string url, string slotName, string password = null)
        {
            if (session != null && session.Socket.Connected)
            {
                // Already connected - clean up old run state and set up for a new run
                Log.LogDebug("Already connected, setting up new run");
                CleanupRun();
                SetupRun();
                return;
            }

            ChatMessage.SendColored($"Attempting to connect to Archipelago at {url}.", Color.green);

            lastServerUrl = url;
            lastSlotName = slotName;
            lastPassword = password;
            try
            {
                session = ArchipelagoSessionFactory.CreateSession(url);
            }
            catch (Exception e)
            {
                OnClientDisconnect(e.Message);
            }

            var result = session.TryConnectAndLogin("Risk of Rain 2", slotName, ItemsHandlingFlags.AllItems, new Version(0, 6, 4), password: password);

            if (!result.Successful)
            {
                LoginFailure failureResult = (LoginFailure)result;
                foreach (var err in failureResult.Errors)
                {
                    ChatMessage.SendColored(err, Color.red);
                    Log.LogError(err);
                }
                Dispose();
                return;
            }

            LoginSuccessful successResult = (LoginSuccessful)result;
            ArchipelagoConnectButtonController.ChangeButtonWhenConnected();
            ChatMessage.SendColored("Connected!", Color.green);

            // Read all slot data into instance fields
            if (successResult.SlotData.TryGetValue("finalStageDeath", out var stageDeathObject))
                finalStageDeath = Convert.ToBoolean(stageDeathObject);
            // to keep this setting working in previous versions of AP
            // TODO remove at ap version 3.9
            else if (successResult.SlotData.TryGetValue("FinalStageDeath", out var oldStageDeathObject))
                finalStageDeath = Convert.ToBoolean(oldStageDeathObject);
            Log.LogDebug($"finalStageDeath {finalStageDeath} ");

            itemPickupStep = 3;
            shrineUseStep = 3;
            if (successResult.SlotData.TryGetValue("itemPickupStep", out var oitemPickupStep))
            {
                itemPickupStep = Convert.ToUInt32(oitemPickupStep);
                Log.LogDebug($"itemPickupStep from slot data: {itemPickupStep}");
                itemPickupStep++; // Add 1 because the user's YAML will contain a value equal to "number of pickups before sent location"
            }
            if (successResult.SlotData.TryGetValue("shrineUseStep", out var oshrineUseStep))
            {
                shrineUseStep = Convert.ToUInt32(oshrineUseStep);
                Log.LogDebug($"shrineUseStep from slot data: {shrineUseStep}");
                shrineUseStep++; // Add 1 because the user's YAML will contain a value equal to "number of pickups before sent location"
            }

            enableDeathLink = false;
            if (successResult.SlotData.TryGetValue("deathLink", out var enabledeathlink))
                enableDeathLink = Convert.ToBoolean(enabledeathlink);

            isExploreMode = false;
            if (successResult.SlotData.TryGetValue("goal", out var classicmode))
                isExploreMode = Convert.ToBoolean(classicmode);

            if (successResult.SlotData.TryGetValue("progressiveStages", out var progressive))
                StageBlockerHandler.progressivesStages = Convert.ToBoolean(progressive);
            if (successResult.SlotData.TryGetValue("showSeerPortals", out var showSeerPortals))
                StageBlockerHandler.showSeerPortals = Convert.ToBoolean(showSeerPortals);

            if (successResult.SlotData.TryGetValue("victory", out var victory))
            {
                switch (victory.ToString())
                {
                    // Mithrix
                    case "1":
                        acceptableEndings = new[] { RoR2Content.GameEndings.MainEnding };
                        acceptableLosses = new[] { "moon", "moon2" };
                        victoryCondition = "Mithrix";
                        break;
                    // Voidling
                    case "2":
                        acceptableEndings = new[] { DLC1Content.GameEndings.VoidEnding };
                        acceptableLosses = new[] { "voidraid" };
                        victoryCondition = "Voidling";
                        break;
                    // Limbo
                    case "3":
                        acceptableEndings = new[] { RoR2Content.GameEndings.LimboEnding };
                        acceptableLosses = new[] { "mysteryspace", "limbo" };
                        victoryCondition = "Limbo";
                        break;
                    case "4":
                        acceptableEndings = new[] { DLC2Content.GameEndings.RebirthEndingDef };
                        acceptableLosses = new[] { "meridian" };
                        victoryCondition = "Rebirth";
                        break;
                    // Solus Wing (AC/DLC3) - ending found via isWin fallback since DLC3Content isn't in GameLibs
                    case "5":
                        acceptableEndings = new GameEndingDef[0];
                        acceptableLosses = new[] { "solutionalhaunt", "solusweb" };
                        victoryCondition = "Solus Wing";
                        break;
                    default:
                        victoryCondition = "any";
                        acceptableEndings = new[] {
                            RoR2Content.GameEndings.MainEnding,
                            //RoR2Content.GameEndings.ObliterationEnding,
                            RoR2Content.GameEndings.LimboEnding,
                            DLC1Content.GameEndings.VoidEnding,
                            DLC2Content.GameEndings.RebirthEndingDef
                        };
                        acceptableLosses = new[] {
                            "moon",
                            "moon2",
                            "voidraid",
                            "mysteryspace",
                            "limbo",
                            "meridian",
                            "solutionalhaunt",
                            "solusweb"
                        };
                        break;
                }
            }
            else
            {
                victoryCondition = "any";
                acceptableEndings = new[] {
                    RoR2Content.GameEndings.MainEnding,
                    //RoR2Content.GameEndings.ObliterationEnding,
                    RoR2Content.GameEndings.LimboEnding,
                    DLC1Content.GameEndings.VoidEnding,
                    DLC2Content.GameEndings.RebirthEndingDef
                };
                acceptableLosses = new[] {
                    "moon",
                    "moon2",
                    "voidraid",
                    "mysteryspace",
                    "limbo",
                    "meridian",
                    "solutionalhaunt",
                    "solusweb"
                };
            }

            // Read custom feature slot data
            if (successResult.SlotData.TryGetValue("lootPoolLimiting", out var lootLimitObj))
                lootPoolLimiting = Convert.ToBoolean(lootLimitObj);
            if (successResult.SlotData.TryGetValue("lootPoolSeed", out var lootSeedObj))
                lootPoolSeed = Convert.ToInt64(lootSeedObj);
            if (successResult.SlotData.TryGetValue("itemsPerExpansion", out var perExpObj))
                itemsPerExpansion = Convert.ToInt32(perExpObj);
            if (successResult.SlotData.TryGetValue("startingWhiteItems", out var whiteObj))
                startingWhiteItems = Convert.ToInt32(whiteObj);
            if (successResult.SlotData.TryGetValue("startingGreenItems", out var greenObj))
                startingGreenItems = Convert.ToInt32(greenObj);
            if (successResult.SlotData.TryGetValue("startingRedItems", out var redObj))
                startingRedItems = Convert.ToInt32(redObj);
            if (successResult.SlotData.TryGetValue("startingBossItems", out var bossObj))
                startingBossItems = Convert.ToInt32(bossObj);
            if (successResult.SlotData.TryGetValue("startingLunarItems", out var lunarObj))
                startingLunarItems = Convert.ToInt32(lunarObj);
            if (successResult.SlotData.TryGetValue("startingEquipment", out var equipObj))
                startingEquipment = Convert.ToInt32(equipObj);
            if (successResult.SlotData.TryGetValue("skillRandomization", out var skillRandObj))
                skillRandomization = Convert.ToBoolean(skillRandObj);
            if (successResult.SlotData.TryGetValue("skillSeed", out var skillSeedObj))
                skillSeed = Convert.ToInt64(skillSeedObj);
            if (successResult.SlotData.TryGetValue("totalSkillUnlocks", out var totalSkillObj))
                totalSkillUnlocks = Convert.ToInt32(totalSkillObj);
            if (successResult.SlotData.TryGetValue("startingSkills", out var startSkillsObj))
                startingSkills = Convert.ToInt32(startSkillsObj);
            if (successResult.SlotData.TryGetValue("survivorLocking", out var survLockObj))
                survivorLocking = Convert.ToBoolean(survLockObj);
            if (successResult.SlotData.TryGetValue("survivorSeed", out var survSeedObj))
                survivorSeed = Convert.ToInt64(survSeedObj);
            if (successResult.SlotData.TryGetValue("totalSurvivorUnlocks", out var totalSurvObj))
                totalSurvivorUnlocks = Convert.ToInt32(totalSurvObj);

            // Cache slot data for re-setup on subsequent runs
            cachedSlotData = successResult.SlotData;
            connectedPlayerName = session.Players.GetPlayerName(session.ConnectionInfo.Slot);

            // Subscribe session-level hooks (only on fresh connection)
            session.MessageLog.OnMessageReceived += Session_OnMessageReceived;
            session.Socket.SocketClosed += Session_SocketClosed;
            session.Socket.ErrorReceived += Socket_ErrorReceived;

            SetupRun();
        }

        /// <summary>
        /// Sets up per-run handlers, hooks, and UI using cached slot data.
        /// Called from Connect() on fresh connection and on subsequent runs.
        /// </summary>
        private void SetupRun()
        {
            isEndingAcceptable = false;

            ItemLogic = new ArchipelagoItemLogicController(session);
            itemCheckBar = null;
            shrineCheckBar = null;
            if (!isInGame)
            {
                lastReceivedItemindex = 0;
            }

            // DeathLink
            deathLinkService = DeathLinkProvider.CreateDeathLinkService(session);
            Log.LogDebug("Starting DeathLink service");
            Deathlinkhandler = new DeathLinkHandler(deathLinkService);
            if (enableDeathLink)
            {
                deathLinkService.EnableDeathLink();
                Deathlinkhandler?.Hook();
            }

            // Game mode setup
            if (!isExploreMode)
            {
                Log.LogDebug("Client detected classic_mode");
                ArchipelagoLocationsInEnvironmentController.RemoveObjective();
                new AllChecksCompleteInStage().Send(NetworkDestination.Clients);
                // classic mode startup is handled within ArchipelagoItemLogicController.Session_PacketReceived
            }
            else
            {
                Log.LogDebug("Client detected explore_mode");
                // only start the new location handler for explore mode
                Stageblockerhandler = new StageBlockerHandler();
                ItemLogic.Stageblockerhandler = Stageblockerhandler;
                Stageblockerhandler.BlockAll();
                Locationhandler = new LocationHandler(session, LocationHandler.buildTemplateFromSlotData(cachedSlotData));
                shrineChanceHelper = new ShrineChanceHandler();

                itemCheckBar = new ArchipelagoLocationCheckProgressBarUI(new Vector2(-40, 0), Vector2.zero, "Item Check Progress:");
                shrineCheckBar = new ArchipelagoLocationCheckProgressBarUI(new Vector2(0, 170), new Vector2(50, -50), "Shrine Check Progress:");
                shrineCheckBar.ItemPickupStep = (int)shrineUseStep;

                Locationhandler.itemBar = itemCheckBar;
                Locationhandler.shrineBar = shrineCheckBar;
                Locationhandler.itemPickupStep = itemPickupStep;
                Locationhandler.shrineUseStep = shrineUseStep;
            }

            // Initialize custom feature controllers
            LootPoolController = new ArchipelagoLootPoolController(
                lootPoolLimiting, startingWhiteItems, startingGreenItems, startingRedItems,
                startingBossItems, startingLunarItems, startingEquipment, lootPoolSeed,
                itemsPerExpansion);
            LootPoolController.Initialize();
            ItemLogic.OnLootPoolExpansionReceived += LootPoolController.ExpandPool;

            SkillRandomizer = new ArchipelagoSkillRandomizer(
                skillRandomization, totalSkillUnlocks, startingSkills, skillSeed);
            SkillRandomizer.Initialize();
            ItemLogic.OnSkillUnlockReceived += SkillRandomizer.HandleSkillUnlock;

            SurvivorController = new ArchipelagoSurvivorController(
                survivorLocking, totalSurvivorUnlocks, survivorSeed);
            SurvivorController.Initialize();
            ItemLogic.OnSurvivorUnlockReceived += SurvivorController.HandleSurvivorUnlock;

            // Send custom feature configs to clients
            if (lootPoolLimiting)
            {
                new Net.SyncLootPoolConfig(true, lootPoolSeed, startingWhiteItems, startingGreenItems,
                    startingRedItems, startingBossItems, startingLunarItems, startingEquipment,
                    itemsPerExpansion).Send(NetworkDestination.Clients);
            }
            if (skillRandomization)
            {
                new Net.SyncSkillConfig(true, skillSeed, totalSkillUnlocks, 0, startingSkills)
                    .Send(NetworkDestination.Clients);
            }
            if (survivorLocking)
            {
                new Net.SyncSurvivorConfig(true, survivorSeed, totalSurvivorUnlocks, 0)
                    .Send(NetworkDestination.Clients);
            }

            // make the bar if it has not been created because classic mode or the slot data was missing
            if (null == itemCheckBar)
            {
                Log.LogDebug("Setting up bar for classic");
                itemCheckBar = new ArchipelagoLocationCheckProgressBarUI(Vector2.zero, Vector2.zero);
                SyncLocationCheckProgress.OnLocationSynced += itemCheckBar.UpdateCheckProgress; // the item bar updates from the netcode in classic mode
            }
            itemCheckBar.ItemPickupStep = (int)itemPickupStep;

            ItemLogic.OnItemDropProcessed += ItemLogicHandler_ItemDropProcessed;
            HookGame();
            new ArchipelagoStartMessage().Send(NetworkDestination.Clients);
            if (!isExploreMode)
            {
                new ArchipelagoStartClassic().Send(NetworkDestination.Clients);
            }
            else
            {
                new ArchipelagoStartExplore().Send(NetworkDestination.Clients);
            }

            ItemLogic.Precollect();
            // Needed for backwards compatibility
            if (session.Items.GetItemName(37501) == null)
            {
                StageBlockerHandler.stageUnlocks["Stage 1"] = true;
                StageBlockerHandler.stageUnlocks["Stage 2"] = true;
                StageBlockerHandler.stageUnlocks["Stage 3"] = true;
                StageBlockerHandler.stageUnlocks["Stage 4"] = true;
            }
            else if (!isInGame)
            {
                StageBlockerHandler.stageUnlocks["Stage 1"] = false;
                StageBlockerHandler.stageUnlocks["Stage 2"] = false;
                StageBlockerHandler.stageUnlocks["Stage 3"] = false;
                StageBlockerHandler.stageUnlocks["Stage 4"] = false;
            }
        }

        /// <summary>
        /// Tears down per-run state (handlers, hooks, UI) but keeps the AP session alive.
        /// Called when a run ends so the player can start a new run without reconnecting.
        /// </summary>
        private void CleanupRun()
        {
            if (ItemLogic != null)
            {
                ItemLogic.OnItemDropProcessed -= ItemLogicHandler_ItemDropProcessed;
                if (LootPoolController != null) ItemLogic.OnLootPoolExpansionReceived -= LootPoolController.ExpandPool;
                if (SkillRandomizer != null) ItemLogic.OnSkillUnlockReceived -= SkillRandomizer.HandleSkillUnlock;
                if (SurvivorController != null) ItemLogic.OnSurvivorUnlockReceived -= SurvivorController.HandleSurvivorUnlock;
                ItemLogic.Dispose();
            }

            if (itemCheckBar != null)
            {
                SyncLocationCheckProgress.OnLocationSynced -= itemCheckBar.UpdateCheckProgress;
                itemCheckBar.Dispose();
            }

            if (shrineCheckBar != null)
            {
                shrineCheckBar.Dispose();
            }

            LootPoolController?.Dispose();
            SkillRandomizer?.Dispose();
            SurvivorController?.Dispose();

            UnhookGame();

            // Null out per-run objects so they can be recreated for the next run
            Stageblockerhandler = null;
            Locationhandler = null;
            itemCheckBar = null;
            shrineCheckBar = null;
            LootPoolController = null;
            SkillRandomizer = null;
            SurvivorController = null;
        }

        public void Dispose()
        {
            CleanupRun();

            // Unhook session-level events
            if (session != null)
            {
                session.MessageLog.OnMessageReceived -= Session_OnMessageReceived;
                session.Socket.SocketClosed -= Session_SocketClosed;
                session.Socket.ErrorReceived -= Socket_ErrorReceived;
            }
            session = null;
        }

        private void HookGame()
        {
            On.RoR2.UI.ChatBox.SubmitChat += ChatBox_SubmitChat;
            RoR2.Run.onRunDestroyGlobal += Run_onRunDestroyGlobal;
            On.RoR2.Run.BeginGameOver += Run_BeginGameOver;
            ArchipelagoChatMessage.OnChatReceivedFromClient += ArchipelagoChatMessage_OnChatReceivedFromClient;
            ReleasePanel = AssetBundleHelper.LoadPrefab("ReleasePrompt");
            CollectPanel = AssetBundleHelper.LoadPrefab("CollectPrompt");
            On.RoR2.UI.GameEndReportPanelController.Awake += GameEndReportPanelController_Awake;
            OnReleaseClick += WillRelease;
            OnCollectClick += WillCollect;
            On.RoR2.SceneObjectToggleGroup.Awake += SceneObjectToggleGroup_Awake;

            Stageblockerhandler?.Hook();
            Locationhandler?.Hook();
            shrineChanceHelper?.Hook();
            ArchipelagoConsoleCommand.OnArchipelagoDeathLinkCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled;
            ArchipelagoConsoleCommand.OnArchipelagoFinalStageDeathCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled;
            ArchipelagoConsoleCommand.OnArchipelagoReconnectCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled;
            On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter += PortalDialerPreDialState_OnEnter;
        }

        private void PortalDialerPreDialState_OnEnter(On.RoR2.PortalDialerController.PortalDialerPreDialState.orig_OnEnter orig, PortalDialerController.PortalDialerPreDialState self)
        {
            ChatMessage.SendColored($"Victory conditon is {ArchipelagoClient.victoryCondition}.", Color.magenta);
            orig(self);
        }

        private void UnhookGame()
        {
            On.RoR2.UI.ChatBox.SubmitChat -= ChatBox_SubmitChat;
            RoR2.Run.onRunDestroyGlobal -= Run_onRunDestroyGlobal;
            On.RoR2.Run.BeginGameOver -= Run_BeginGameOver;
            ArchipelagoChatMessage.OnChatReceivedFromClient -= ArchipelagoChatMessage_OnChatReceivedFromClient;
            On.RoR2.UI.GameEndReportPanelController.Awake -= GameEndReportPanelController_Awake;
            OnReleaseClick -= WillRelease;
            OnCollectClick -= WillCollect;
            On.RoR2.SceneObjectToggleGroup.Awake -= SceneObjectToggleGroup_Awake;

            Deathlinkhandler?.UnHook();
            Stageblockerhandler?.UnHook();
            Locationhandler?.UnHook();
            shrineChanceHelper?.UnHook();
            ArchipelagoConsoleCommand.OnArchipelagoDeathLinkCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled;
            ArchipelagoConsoleCommand.OnArchipelagoFinalStageDeathCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled;
            ArchipelagoConsoleCommand.OnArchipelagoReconnectCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled;
            On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter -= PortalDialerPreDialState_OnEnter;
        }
        private void SceneObjectToggleGroup_Awake(On.RoR2.SceneObjectToggleGroup.orig_Awake orig, SceneObjectToggleGroup self)
        {
            Log.LogDebug($"Scene group length {self.toggleGroups.Length}");
            if (self.toggleGroups != null)
            {
                for (var i = 0; i < self.toggleGroups.Length; i++)
                {
                    if (self.toggleGroups[i].objects != null && self.toggleGroups[i].objects[0] != null)
                    {
                        if (self.toggleGroups[i].objects[0].name == "NewtStatue" || self.toggleGroups[i].objects[0].name == "NewtStatue (1)")
                        {
                            Log.LogDebug($"Scene Object Toggle Group min:{self.toggleGroups[i].minEnabled} max:{self.toggleGroups[i].maxEnabled}");
                            Log.LogDebug("Changing newt alters min and max values");
                            self.toggleGroups[i].minEnabled = 1;
                            self.toggleGroups[i].maxEnabled = 2;
                            Log.LogDebug($"Scene Object Toggle Group  min:{self.toggleGroups[i].minEnabled} max:{self.toggleGroups[i].maxEnabled}");
                            break;
                        }
                    }

                }
            }
            orig(self);



        }
        private void ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled(bool link)
        {
            if (link)
            {
                Deathlinkhandler?.Hook();
                deathLinkService.EnableDeathLink();
            }
            else
            {
                Deathlinkhandler?.UnHook();
                deathLinkService.DisableDeathLink();
            }
        }
        private void ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled(bool finalstage)
        {
            finalStageDeath = finalstage;
        }
        private void ArchipelagoChatMessage_OnChatReceivedFromClient(string message)
        {
            if (session.Socket.Connected && !string.IsNullOrEmpty(message))
            {
                var sayPacket = new SayPacket();
                sayPacket.Text = message;
                session.Socket.SendPacketAsync(sayPacket);
            }
        }

        private void ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled()
        {
            reconnecting = true;
            Session_SocketClosed("Making sure to be disconnected before reconnecting.");
        }

        private void ItemLogicHandler_ItemDropProcessed(int pickedUpCount)
        {
            if (itemCheckBar != null)
            {
                itemCheckBar.CurrentItemCount = pickedUpCount;
                if ((itemCheckBar.CurrentItemCount % ItemLogic.ItemPickupStep) == 0)
                {
                    itemCheckBar.CurrentItemCount = 0;
                }
                else
                {
                    itemCheckBar.CurrentItemCount = itemCheckBar.CurrentItemCount % ItemLogic.ItemPickupStep;
                }
            }
            new SyncLocationCheckProgress(itemCheckBar.CurrentItemCount, itemCheckBar.ItemPickupStep).Send(NetworkDestination.Clients);
        }

        private void ChatBox_SubmitChat(On.RoR2.UI.ChatBox.orig_SubmitChat orig, ChatBox self)
        {
            var text = self.inputField.text;
            if (session.Socket.Connected && !string.IsNullOrEmpty(text))
            {
                var sayPacket = new SayPacket();
                sayPacket.Text = text;
                session.Socket.SendPacketAsync(sayPacket);

                self.inputField.text = string.Empty;
                orig(self);
            }
            else
            {
                orig(self);
            }
        }

        private void Socket_ErrorReceived(Exception e, string message)
        {
            Log.LogDebug($"Error received: {e}, message: {message}");
            reconnecting = true;
            Session_SocketClosed(message);
        }

        private void Session_SocketClosed(string reason)
        {
            Dispose();
            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);

            if (OnClientDisconnect != null)
            {
                OnClientDisconnect(reason);
            }
        }

        public IEnumerator<WaitForSeconds> AttemptReconnection()
        {
            Log.LogDebug("Attempting to reconnect!");
            var retryCounter = 0;
            if (!isInGame)
            {
                ArchipelagoConnectButtonController.ChangeButtonWhenDisconnected();
            }
            while ((session == null || !session.Socket.Connected)&& retryCounter < 5)
            {
                ChatMessage.Send($"Connection attempt #{retryCounter+1}");
                retryCounter++;
                yield return new WaitForSeconds(3f);
                Connect(lastServerUrl, lastSlotName, lastPassword);
            }

            if (session == null || !session.Socket.Connected)
            {
                ChatMessage.SendColored("Could not connect to Archipelago.", Color.red);
                Dispose();
            }
            else if (session != null && session.Socket.Connected)
            {
                ChatMessage.SendColored("Established Archipelago connection.", Color.green);
                new ArchipelagoStartMessage().Send(NetworkDestination.Clients);
                if (Locationhandler != null && isInGame)
                {
                    Locationhandler.CatchUpSceneLocations(LocationHandler.sceneDef.cachedName);
                    Locationhandler.LoadItemPickupHooks();
                }
            }

            reconnecting = false;
        }
        private void Session_OnMessageReceived(LogMessage message)
        {
            Thread thread = new Thread(() => Session_OnMessageReceived_Thread(message));
            thread.Start();
            Thread.Sleep(20);
        }
        private void Session_OnMessageReceived_Thread(LogMessage message)
        {
            string text = "";
            foreach (var part in message.Parts)
            {
                var hex = part.Color.R.ToString("X2") + part.Color.G.ToString("X2") + part.Color.B.ToString("X2");
                text += $"<color=#{hex}>" + part + "</color>";
            }

            ChatMessage.Send(text);
        }
        private void Run_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
        {
            // If ending is acceptable, finish the archipelago run.
            if (IsEndingAcceptable(gameEndingDef))
            {  
                isEndingAcceptable = true;
                // Auto-complete all remaining locations. Substitute for deprecated forced_auto_forfeit.
                //session.Locations.CompleteLocationChecks(session.Locations.AllMissingLocations.ToArray());
             
                var packet = new StatusUpdatePacket();
                packet.Status = ArchipelagoClientState.ClientGoal;
                session.Socket.SendPacketAsync(packet);

                new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            }
            orig(self, gameEndingDef);
        }

        private bool IsEndingAcceptable(GameEndingDef gameEndingDef)
        {
            var stageName = Stage.instance.sceneDef.cachedName;
            Log.LogDebug($"ending stage is {stageName}, ending is {gameEndingDef.cachedName}, isWin={gameEndingDef.isWin}");

            if (acceptableEndings.Contains(gameEndingDef))
                return true;

            // Handle DLC endings we can't statically reference (e.g., DLC3/AC Solus Wing)
            // Accept any win ending (except obliteration) on an expected final stage
            if (gameEndingDef.isWin &&
                gameEndingDef != RoR2Content.GameEndings.ObliterationEnding &&
                acceptableLosses != null &&
                acceptableLosses.Contains(stageName))
            {
                Log.LogInfo($"Accepting ending '{gameEndingDef.cachedName}' on stage '{stageName}' via isWin fallback");
                return true;
            }

            // finalStageDeath: dying or obliterating on a final stage counts
            if (finalStageDeath && acceptableLosses != null && acceptableLosses.Contains(stageName))
            {
                if (gameEndingDef == RoR2Content.GameEndings.StandardLoss ||
                    gameEndingDef == RoR2Content.GameEndings.ObliterationEnding)
                    return true;
            }

            return false;
        }

        // When exiting to menu/game this will run - clean up per-run state but keep session alive
        private void Run_onRunDestroyGlobal(Run obj)
        {
            isInGame = false;
            lastReceivedItemindex = 0;
            CleanupRun();
        }

        public void Disconnect()
        {
            if (session != null && session.Socket.Connected)
            {
                ArchipelagoConnectButtonController.ChangeButtonWhenDisconnected();
                session.Socket.DisconnectAsync();
            }
        }
        private void GameEndReportPanelController_Awake(On.RoR2.UI.GameEndReportPanelController.orig_Awake orig, GameEndReportPanelController self)
        {
            if (isEndingAcceptable && ReleasePromptPanel == null)
            {
                var releasePermission = Convert.ToString(session.RoomState.ReleasePermissions);
                var collectPermission = Convert.ToString(session.RoomState.CollectPermissions);
                bool canRelease = (releasePermission == "Goal" || releasePermission == "Enabled");
                bool canCollect = (collectPermission == "Goal" || collectPermission == "Enabled");
                Log.LogDebug($"can release {releasePermission} can collect {collectPermission}");
                Log.LogDebug($"release? {canRelease} collect? {canCollect}");
                var gameEndReportPanel = self.transform.Find("SafeArea (JUICED)/BodyArea");
                if (canRelease)
                {
                    var rp = GameObject.Instantiate(ReleasePanel);
                    rp.transform.SetParent(gameEndReportPanel.transform, false);
                    rp.transform.localPosition = new Vector3(0, 0, 0);
                    rp.transform.localScale = Vector3.one;
                    var release = self.transform.Find("SafeArea (JUICED)/BodyArea/ReleasePrompt(Clone)/Panel/Release/").gameObject;
                    release.AddComponent<HGButton>();
                    var releaseCancel = self.transform.Find("SafeArea (JUICED)/BodyArea/ReleasePrompt(Clone)/Panel/Cancel/").gameObject;
                    releaseCancel.AddComponent<HGButton>();
                    release.GetComponent<HGButton>().onClick.AddListener(() => { OnReleaseClick(true); });
                    releaseCancel.GetComponent<HGButton>().onClick.AddListener(() => { OnReleaseClick(false); });
                    ReleasePromptPanel = self.transform.Find("SafeArea (JUICED)/BodyArea/ReleasePrompt(Clone)").gameObject;
                }
                if (canCollect)
                {
                    var cp = GameObject.Instantiate(CollectPanel);
                    cp.transform.SetParent(gameEndReportPanel.transform, false);
                    cp.transform.localPosition = new Vector3(0, 0, 0);
                    cp.transform.localScale = Vector3.one;
                    var collect = self.transform.Find("SafeArea (JUICED)/BodyArea/CollectPrompt(Clone)/Panel/Collect/").gameObject;
                    collect.AddComponent<HGButton>();
                    var collectCancel = self.transform.Find("SafeArea (JUICED)/BodyArea/CollectPrompt(Clone)/Panel/Cancel/").gameObject;
                    collectCancel.AddComponent<HGButton>();
                    collect.GetComponent<HGButton>().onClick.AddListener(() => { OnCollectClick(true); });
                    collectCancel.GetComponent<HGButton>().onClick.AddListener(() => { OnCollectClick(false); });
                    CollectPromptPanel = self.transform.Find("SafeArea (JUICED)/BodyArea/CollectPrompt(Clone)").gameObject;
                    CollectPromptPanel.SetActive(false);
                }
                if (canCollect && !canRelease)
                {
                    CollectPromptPanel.SetActive(true);
                }



            }
            orig(self);
        }
        private void WillRelease(bool prompt)
        {
            var sayPacket = new SayPacket();
            if (prompt && isEndingAcceptable)
            {
                Log.LogDebug($"Releasing the rest of the items {isEndingAcceptable}");
                sayPacket.Text = "!release";
                session.Socket.SendPacketAsync(sayPacket);
            }
            ReleasePromptPanel.SetActive(false);
            if (CollectPromptPanel != null) 
            {
                CollectPromptPanel.SetActive(true);
            }
        }

        private void WillCollect(bool prompt)
        {
            var sayPacket = new SayPacket();
            if (prompt && isEndingAcceptable)
            {
                Log.LogDebug($"Collect the rest of the items {isEndingAcceptable}");
                sayPacket.Text = "!collect";
                session.Socket.SendPacketAsync(sayPacket);
            }
            CollectPromptPanel?.SetActive(false);

        }
    }
}
