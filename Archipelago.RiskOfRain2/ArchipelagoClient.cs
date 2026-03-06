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
using UnityEngine.AddressableAssets;
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
        public bool IsConnected => session != null && session.Socket.Connected;

        internal DeathLinkHandler Deathlinkhandler { get; private set; }
        internal StageBlockerHandler Stageblockerhandler { get; private set; }
        internal LocationHandler Locationhandler { get; private set; }
        internal ShrineChanceHandler shrineChanceHelper { get; private set; }

        public ArchipelagoItemLogicController ItemLogic;

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
        private GameObject genericMenuButton;
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
        // Boss-kill victory detection: set when boss group defeated on a victory-eligible stage
        private bool bossDefeatedOnVictoryStage;

        // Cached slot data for session reuse across runs
        private bool cachedGoalIsExplore;
        private bool cachedDeathLinkEnabled;
        private uint cachedItemPickupStep = 3;
        private uint cachedShrineUseStep = 3;
        private Dictionary<string, object> cachedSlotData;

        // Cached ItemLogic state for restoring across runs
        private bool hasCachedRunState;
        private int cachedItemLogicPickupStep;
        private int cachedItemLogicTotalChecks;
        private int cachedItemLogicCurrentChecks;
        private int cachedItemLogicPickedUpItemCount;

        public ArchipelagoClient()
        {

        }

        public void Connect(string url, string slotName, string password = null)
        {
            // Cache credentials for reconnection
            lastServerUrl = url;
            lastSlotName = slotName;
            lastPassword = password;

            // Session reuse: if already connected, just set up a new run
            if (IsConnected)
            {
                ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cIsHealing>Reusing existing Archipelago session.</style>");
                CleanupRun();
                SetupRun();
                return;
            }

            // Stale session: clean up before creating a new one
            TeardownSession();

            ChatMessage.Send($"<style=cIsUtility>[AP]</style> Attempting to connect to Archipelago at {url}.");

            try
            {
                session = ArchipelagoSessionFactory.CreateSession(url);
            }
            catch (Exception e)
            {
                OnClientDisconnect?.Invoke(e.Message);
                return;
            }

            // On fresh connect (not reconnecting), reset item index and cached run state
            // so we don't replay already-received items or restore stale progress.
            // During reconnection, preserve both so AttemptReconnection can restore
            // the player's progress after the session is re-established.
            if (!reconnecting)
            {
                lastReceivedItemindex = 0;
                hasCachedRunState = false;
            }

            var result = session.TryConnectAndLogin("Risk of Rain 2", slotName, ItemsHandlingFlags.AllItems, new Version(0, 6, 4), password: password);

            if (!result.Successful)
            {
                LoginFailure failureResult = (LoginFailure)result;
                foreach (var err in failureResult.Errors)
                {
                    ChatMessage.Send($"<style=cIsUtility>[AP]</style> <style=cDeath>{err}</style>");
                    Log.LogError(err);
                }
                session = null;
                return;
            }

            LoginSuccessful successResult = (LoginSuccessful)result;
            ArchipelagoConnectButtonController.ChangeButtonWhenConnected();
            ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cIsHealing>Connected!</style>");

            // Parse and cache slot data (session-level, survives across runs)
            if (successResult.SlotData.TryGetValue("finalStageDeath", out var stageDeathObject))
            {
                finalStageDeath = Convert.ToBoolean(stageDeathObject);
            }
            // to keep this setting working in previous versions of AP
            // TODO remove at ap version 3.9
            else if (successResult.SlotData.TryGetValue("FinalStageDeath", out var oldStageDeathObject))
            {
                finalStageDeath = Convert.ToBoolean(oldStageDeathObject);
            }
            Log.LogDebug($"finalStageDeath {finalStageDeath} ");

            cachedItemPickupStep = 3;
            cachedShrineUseStep = 3;
            if (successResult.SlotData.TryGetValue("itemPickupStep", out var oitemPickupStep))
            {
                cachedItemPickupStep = Convert.ToUInt32(oitemPickupStep);
                Log.LogDebug($"itemPickupStep from slot data: {cachedItemPickupStep}");
                cachedItemPickupStep++; // Add 1 because the user's YAML will contain a value equal to "number of pickups before sent location"
            }
            if (successResult.SlotData.TryGetValue("shrineUseStep", out var oshrineUseStep))
            {
                cachedShrineUseStep = Convert.ToUInt32(oshrineUseStep);
                Log.LogDebug($"shrineUseStep from slot data: {cachedShrineUseStep}");
                cachedShrineUseStep++; // Add 1 because the user's YAML will contain a value equal to "number of pickups before sent location"
            }

            // DeathLink (session-level)
            deathLinkService = DeathLinkProvider.CreateDeathLinkService(session);
            Log.LogDebug("Starting DeathLink service");
            Deathlinkhandler = new DeathLinkHandler(deathLinkService);
            cachedDeathLinkEnabled = false;
            if (successResult.SlotData.TryGetValue("deathLink", out var enabledeathlink))
            {
                cachedDeathLinkEnabled = Convert.ToBoolean(enabledeathlink);
                if (cachedDeathLinkEnabled)
                {
                    deathLinkService.EnableDeathLink(); // deathlink should just be enabled, the DeathLinkHandler assumes it is already enabled
                }
            }

            // Cache goal mode and slot data for run reuse
            cachedGoalIsExplore = false;
            if (successResult.SlotData.TryGetValue("goal", out var classicmode))
            {
                cachedGoalIsExplore = Convert.ToBoolean(classicmode);
            }
            cachedSlotData = new Dictionary<string, object>(successResult.SlotData);

            // Victory conditions (session-level)
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
                    // False Son (Rebirth)
                    case "4":
                        acceptableEndings = new[] { DLC2Content.GameEndings.RebirthEndingDef };
                        acceptableLosses = new[] { "meridian" };
                        victoryCondition = "Rebirth";
                        break;
                    // Solus Heart — defeat Solus Heart in Neural Sanctum (scene: solusweb)
                    // Path: Solutional Haunt (Solus Wing) → Computational Exchange → Neural Sanctum (Solus Heart)
                    // No standard GameEndingDef — detected via boss-kill + scene-transition hook.
                    case "5":
                        acceptableEndings = new GameEndingDef[] { };
                        acceptableLosses = new[] { "solusweb" };
                        victoryCondition = "Solus Heart";
                        break;
                    default:
                        victoryCondition = "any";
                        acceptableEndings = new[] {
                            RoR2Content.GameEndings.MainEnding,
                            //RoR2Content.GameEndings.ObliterationEnding,
                            RoR2Content.GameEndings.LimboEnding,
                            DLC1Content.GameEndings.VoidEnding,
                            DLC2Content.GameEndings.RebirthEndingDef,
                            // Solus Heart has no GameEndingDef — handled via boss-kill hook
                        };
                        acceptableLosses = new[] {
                            "moon",
                            "moon2",
                            "voidraid",
                            "mysteryspace",
                            "limbo",
                            "meridian",
                            "solusweb",
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
                    DLC2Content.GameEndings.RebirthEndingDef,
                    // Solus Heart has no GameEndingDef — handled via boss-kill hook
                };
                acceptableLosses = new[] {
                    "moon",
                    "moon2",
                    "voidraid",
                    "mysteryspace",
                    "limbo",
                    "meridian",
                    "solusweb",
                };
            }

            // Progressive stages and seer portals (session-level, static fields)
            if (successResult.SlotData.TryGetValue("progressiveStages", out var progressive))
            {
                StageBlockerHandler.progressivesStages = Convert.ToBoolean(progressive);
            }
            if (successResult.SlotData.TryGetValue("showSeerPortals", out var showSeerPortals))
            {
                StageBlockerHandler.showSeerPortals = Convert.ToBoolean(showSeerPortals);
            }

            connectedPlayerName = session.Players.GetPlayerName(session.ConnectionInfo.Slot);
            genericMenuButton = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/UI/GenericMenuButton.prefab").WaitForCompletion();

            // Subscribe session-level events
            session.MessageLog.OnMessageReceived += Session_OnMessageReceived;
            session.Socket.SocketClosed += Session_SocketClosed;
            session.Socket.ErrorReceived += Socket_ErrorReceived;
            ArchipelagoConsoleCommand.OnArchipelagoReconnectCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled;
            Run.onRunStartGlobal += Run_onRunStartGlobal;

            // Stage unlock initialization (one-time, session-level)
            // Needed for backwards compatability
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

            // Set up the first run
            SetupRun();
        }

        /// <summary>
        /// Creates per-run state: ItemLogic, handlers, UI bars, game hooks.
        /// Called on first connect and on session reuse for subsequent runs.
        /// </summary>
        public void SetupRun()
        {
            isEndingAcceptable = false;

            ItemLogic = new ArchipelagoItemLogicController(session);

            // Initialize countdown objective for check progress
            ArchipelagoCheckCountdownController.ItemStep = (int)cachedItemPickupStep;
            ArchipelagoCheckCountdownController.ItemsPickedUp = 0;
            ArchipelagoCheckCountdownController.ShowItemCountdown = true;

            if (cachedGoalIsExplore)
            {
                Log.LogDebug("Setting up explore mode for run");
                Stageblockerhandler = new StageBlockerHandler();
                ItemLogic.Stageblockerhandler = Stageblockerhandler;
                Stageblockerhandler.BlockAll();
                Locationhandler = new LocationHandler(session, LocationHandler.buildTemplateFromSlotData(cachedSlotData));
                shrineChanceHelper = new ShrineChanceHandler();

                ArchipelagoCheckCountdownController.ShrineStep = (int)cachedShrineUseStep;
                ArchipelagoCheckCountdownController.ShrinesUsed = 0;
                ArchipelagoCheckCountdownController.ShowShrineCountdown = true;

                Locationhandler.itemPickupStep = cachedItemPickupStep;
                Locationhandler.shrineUseStep = cachedShrineUseStep;
            }
            else
            {
                Log.LogDebug("Setting up classic mode for run");
                ArchipelagoLocationsInEnvironmentController.RemoveObjective();
                ArchipelagoCheckCountdownController.ShowShrineCountdown = false;
                new AllChecksCompleteInStage().Send(NetworkDestination.Clients);
            }

            ArchipelagoCheckCountdownController.AddObjective();

            // Initialize ItemLogic location tracking from session state.
            // On first connect, Session_PacketReceived won't fire because ItemLogic
            // is created after TryConnectAndLogin. On session reuse, restore cached state.
            if (hasCachedRunState)
            {
                ItemLogic.ItemPickupStep = cachedItemLogicPickupStep;
                ItemLogic.TotalChecks = cachedItemLogicTotalChecks;
                ItemLogic.CurrentChecks = cachedItemLogicCurrentChecks;
                ItemLogic.PickedUpItemCount = cachedItemLogicPickedUpItemCount;
            }
            else
            {
                ItemLogic.InitializeFromConnectionState(!cachedGoalIsExplore, (int)cachedItemPickupStep);
            }

            ItemLogic.OnItemDropProcessed += ItemLogicHandler_ItemDropProcessed;
            if (cachedDeathLinkEnabled)
            {
                Deathlinkhandler?.Hook();
            }
            HookGame();

            // These messages are idempotent — safe to re-send on session reuse
            new ArchipelagoStartMessage().Send(NetworkDestination.Clients);
            if (!cachedGoalIsExplore)
            {
                new ArchipelagoStartClassic().Send(NetworkDestination.Clients);
            }
            else
            {
                new ArchipelagoStartExplore().Send(NetworkDestination.Clients);
            }

            // Enqueue all received items for this run. Handles both first connect
            // (items missed during login) and session reuse (re-granting items
            // for the new run since the player's inventory is empty).
            ItemLogic.ProcessAllReceivedItems();
            ItemLogic.Precollect();
        }

        /// <summary>
        /// Tears down per-run state. The AP session stays alive for reuse.
        /// </summary>
        public void CleanupRun()
        {
            // Re-entrance guard: Run_onRunDestroyGlobal and Session_SocketClosed can
            // both call CleanupRun() near-simultaneously. Prevent double-dispose.
            if (ItemLogic == null) return;

            UnhookGame();

            if (ItemLogic != null)
            {
                // Cache state before disposing for session reuse
                hasCachedRunState = true;
                cachedItemLogicPickupStep = ItemLogic.ItemPickupStep;
                cachedItemLogicTotalChecks = ItemLogic.TotalChecks;
                cachedItemLogicCurrentChecks = ItemLogic.CurrentChecks;
                cachedItemLogicPickedUpItemCount = ItemLogic.PickedUpItemCount;

                ItemLogic.OnItemDropProcessed -= ItemLogicHandler_ItemDropProcessed;
                ItemLogic.Dispose();
                ItemLogic = null;
            }

            ArchipelagoCheckCountdownController.RemoveObjective();

            // In the case the player joins a lobby that uses different settings, the previous objects may still exist and may be called again when hooks are started.
            // To prevent this, the old objects will be thrown away when cleaning up.
            Stageblockerhandler = null;
            Locationhandler = null;
            shrineChanceHelper = null;
            bossDefeatedOnVictoryStage = false;
        }

        /// <summary>
        /// Unsubscribes session-level events and nulls the session.
        /// Optionally disconnects the socket if still connected.
        /// </summary>
        private void TeardownSession(bool disconnect = false)
        {
            if (session == null) return;

            session.MessageLog.OnMessageReceived -= Session_OnMessageReceived;
            session.Socket.SocketClosed -= Session_SocketClosed;
            session.Socket.ErrorReceived -= Socket_ErrorReceived;
            ArchipelagoConsoleCommand.OnArchipelagoReconnectCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled;
            Run.onRunStartGlobal -= Run_onRunStartGlobal;

            if (disconnect && session.Socket.Connected)
            {
                session.Socket.DisconnectAsync();
            }

            session = null;
            Deathlinkhandler = null;
            deathLinkService = null;
            // NOTE: hasCachedRunState is intentionally NOT cleared here.
            // On dirty disconnect → reconnect, CleanupRun caches state before
            // TeardownSession runs. Clearing here would lose that cached state.
            // It is cleared on fresh connect (when !reconnecting) in Connect().
        }

        /// <summary>
        /// Full teardown: per-run state + session-level state.
        /// Only called on intentional disconnect or unrecoverable error.
        /// </summary>
        public void Dispose()
        {
            CleanupRun();
            TeardownSession(disconnect: true);
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
            ArchipelagoScoreboardController.Hook();

            Stageblockerhandler?.Hook();
            Locationhandler?.Hook();
            shrineChanceHelper?.Hook();
            ArchipelagoConsoleCommand.OnArchipelagoDeathLinkCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled;
            ArchipelagoConsoleCommand.OnArchipelagoFinalStageDeathCommandCalled += ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled;
            On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter += PortalDialerPreDialState_OnEnter;
            On.RoR2.BossGroup.OnDefeatedServer += BossGroup_OnDefeatedServer;
            RoR2.Stage.onStageStartGlobal += Stage_onStageStartGlobal_VictoryCheck;
        }

        private void PortalDialerPreDialState_OnEnter(On.RoR2.PortalDialerController.PortalDialerPreDialState.orig_OnEnter orig, PortalDialerController.PortalDialerPreDialState self)
        {
            ChatMessage.Send($"<style=cIsUtility>[AP]</style> Victory condition is <style=cIsUtility>{ArchipelagoClient.victoryCondition}</style>.");
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
            ArchipelagoScoreboardController.Unhook();

            Deathlinkhandler?.UnHook();
            Stageblockerhandler?.UnHook();
            Locationhandler?.UnHook();
            shrineChanceHelper?.UnHook();
            ArchipelagoConsoleCommand.OnArchipelagoDeathLinkCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoDeathLinkCommandCalled;
            ArchipelagoConsoleCommand.OnArchipelagoFinalStageDeathCommandCalled -= ArchipelagoConsoleCommand_OnArchipelagoFinalStageDeathCommandCalled;
            On.RoR2.PortalDialerController.PortalDialerPreDialState.OnEnter -= PortalDialerPreDialState_OnEnter;
            On.RoR2.BossGroup.OnDefeatedServer -= BossGroup_OnDefeatedServer;
            RoR2.Stage.onStageStartGlobal -= Stage_onStageStartGlobal_VictoryCheck;
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
            if (IsConnected && !string.IsNullOrEmpty(message))
            {
                var sayPacket = new SayPacket();
                sayPacket.Text = message;
                session.Socket.SendPacketAsync(sayPacket);
            }
        }

        private void ArchipelagoConsoleCommand_OnArchipelagoReconnectCommandCalled()
        {
            reconnecting = true;
            Dispose();
            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            OnClientDisconnect?.Invoke("Manual reconnect requested.");
        }

        private void ItemLogicHandler_ItemDropProcessed(int pickedUpCount)
        {
            int step = ItemLogic.ItemPickupStep;
            int current = pickedUpCount % step;
            ArchipelagoCheckCountdownController.UpdateItemCountdown(current, step);
            new SyncLocationCheckProgress(current, step).Send(NetworkDestination.Clients);
        }

        private void ChatBox_SubmitChat(On.RoR2.UI.ChatBox.orig_SubmitChat orig, ChatBox self)
        {
            var text = self.inputField.text;
            if (IsConnected && !string.IsNullOrEmpty(text))
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
            CleanupRun();
            TeardownSession();

            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            OnClientDisconnect?.Invoke(reason);
        }

        public IEnumerator<WaitForSeconds> AttemptReconnection()
        {
            Log.LogDebug("Attempting to reconnect!");
            if (!isInGame)
            {
                ArchipelagoConnectButtonController.ChangeButtonWhenDisconnected();
            }

            for (int attempt = 1; attempt <= 5; attempt++)
            {
                ChatMessage.Send($"<style=cIsUtility>[AP]</style> Reconnection attempt #{attempt}");
                yield return new WaitForSeconds(3f);

                try
                {
                    Connect(lastServerUrl, lastSlotName, lastPassword);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Reconnection attempt {attempt} failed: {ex.Message}");
                }

                if (IsConnected)
                {
                    ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cIsHealing>Reconnected to Archipelago.</style>");
                    // Guard with Run.instance: if the run ended while we were
                    // disconnected, isInGame may be stale (true) but the run is gone.
                    if (Locationhandler != null && isInGame && Run.instance != null)
                    {
                        Locationhandler.CatchUpSceneLocations(LocationHandler.sceneDef.cachedName);
                        Locationhandler.LoadItemPickupHooks();
                    }
                    reconnecting = false;
                    yield break;
                }
            }

            ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cDeath>Failed to reconnect after 5 attempts.</style>");
            Dispose();
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
            Log.LogDebug($"ending stage is {Stage.instance.sceneDef.cachedName}");
            return acceptableEndings.Contains(gameEndingDef) ||
                (finalStageDeath && gameEndingDef == RoR2Content.GameEndings.StandardLoss && acceptableLosses.Contains(Stage.instance.sceneDef.cachedName)) ||
                (finalStageDeath && gameEndingDef == RoR2Content.GameEndings.ObliterationEnding && acceptableLosses.Contains(Stage.instance.sceneDef.cachedName));
        }

        // Boss-kill victory detection for encounters that don't trigger a standard GameEndingDef.
        // Step 1: Flag when boss group is defeated on a victory-eligible stage.
        private void BossGroup_OnDefeatedServer(On.RoR2.BossGroup.orig_OnDefeatedServer orig, BossGroup self)
        {
            orig(self);
            if (Stage.instance == null) return;
            var sceneName = Stage.instance.sceneDef.cachedName;
            if (IsVictoryStageForBossKill(sceneName))
            {
                bossDefeatedOnVictoryStage = true;
                Log.LogDebug($"Boss defeated on victory stage: {sceneName}");
                ChatMessage.Send("<style=cIsUtility>[AP]</style> <style=cIsHealing>Boss defeated! Complete the stage to claim victory.</style>");
            }
        }

        // Step 2: When the next stage loads, check if we left a victory stage after a boss kill.
        // The isEndingAcceptable guard prevents double-sending if Run_BeginGameOver already handled it
        // (e.g. False Son via Rebirth ending).
        private void Stage_onStageStartGlobal_VictoryCheck(Stage stage)
        {
            if (!bossDefeatedOnVictoryStage || isEndingAcceptable) return;
            if (session == null || !session.Socket.Connected) return;
            bossDefeatedOnVictoryStage = false;

            Log.LogInfo($"Victory achieved via boss kill on victory stage (now on {stage.sceneDef.cachedName}).");
            isEndingAcceptable = true;

            var packet = new StatusUpdatePacket();
            packet.Status = ArchipelagoClientState.ClientGoal;
            session.Socket.SendPacketAsync(packet);

            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
        }

        // Which stages count for boss-kill victory detection, based on the current victory condition.
        private bool IsVictoryStageForBossKill(string sceneName)
        {
            // False Son on Prime Meridian
            if ((victoryCondition == "Rebirth" || victoryCondition == "any") && sceneName == "meridian")
                return true;
            // Solus Heart on Neural Sanctum
            if ((victoryCondition == "Solus Heart" || victoryCondition == "any") && sceneName == "solusweb")
                return true;
            return false;
        }

        // Session-level: automatically set up a new run when the session persists across runs.
        // After a run ends, CleanupRun() tears down per-run state but the AP session stays
        // alive. When the player starts a new run, this fires and re-creates per-run state.
        private void Run_onRunStartGlobal(Run obj)
        {
            if (IsConnected && ItemLogic == null)
            {
                Log.LogDebug("Session alive, auto-setting up new run.");
                SetupRun();
            }
        }

        // When exiting to menu/game this will run — only cleans up the run, session stays alive
        private void Run_onRunDestroyGlobal(Run obj)
        {
            isInGame = false;
            CleanupRun();
        }

        /// <summary>
        /// Intentional disconnect initiated by the user (e.g. console command).
        /// Performs synchronous full cleanup so callers don't need to wait for
        /// the async SocketClosed callback.
        /// </summary>
        public void Disconnect()
        {
            if (session == null) return;
            ArchipelagoConnectButtonController.ChangeButtonWhenDisconnected();
            Dispose();
            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            OnClientDisconnect?.Invoke("Disconnected.");
        }

        private void GameEndReportPanelController_Awake(On.RoR2.UI.GameEndReportPanelController.orig_Awake orig, GameEndReportPanelController self)
        {
            if (isEndingAcceptable && ReleasePromptPanel == null)
            {
                GameObject menuOutline;
                if (genericMenuButton != null)
                {
                    menuOutline = genericMenuButton.transform.Find("HoverOutline").gameObject;
                }
                else
                {
                    menuOutline = null;
                }

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
                    // Outline for collect menu buttons

/*                    if (menuOutline != null)
                    {
                        GameObject releaseOutline = GameObject.Instantiate(menuOutline);
                        releaseOutline.transform.SetParent(release.transform, false);
                        release.GetComponent<HGButton>().imageOnHover = releaseOutline.GetComponent<Image>();
                        release.GetComponent<HGButton>().showImageOnHover = true;
                        GameObject releaseCancelOutline = GameObject.Instantiate(menuOutline);
                        releaseCancelOutline.transform.SetParent(releaseCancel.transform, false);
                        releaseCancel.GetComponent<HGButton>().imageOnHover = releaseCancelOutline.GetComponent<Image>();
                        releaseCancel.GetComponent<HGButton>().showImageOnHover = true;
                    }
*/                }
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
                      //TODO Outline for collect menu buttons do not show up like in the release buttons.. no idea why

/*                    if (menuOutline != null)
                    {
                        GameObject collectOutline = GameObject.Instantiate(menuOutline);
                        collectOutline.transform.SetParent(collect.transform, false);
                        collect.GetComponent<HGButton>().imageOnHover = collectOutline.GetComponent<Image>();
                        collect.GetComponent<HGButton>().showImageOnHover = true;
                        GameObject collectCancelOutline = GameObject.Instantiate(menuOutline);
                        collectCancelOutline.transform.SetParent(collectCancel.transform, false);
                        collectCancel.GetComponent<HGButton>().imageOnHover = collectCancelOutline.GetComponent<Image>();
                        collectCancel.GetComponent<HGButton>().showImageOnHover = true;
                    }
*/              }
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
