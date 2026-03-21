using Archipelago.RiskOfRain2.Console;
using Archipelago.RiskOfRain2.Network;
using Archipelago.RiskOfRain2.Services;
using Archipelago.RiskOfRain2.UI;
using BepInEx;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using RoR2.Networking;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2;

[BepInDependency("com.bepis.r2api")]
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class ArchipelagoPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "ror2-Archipelago";
    public const string PluginAuthor = "chichi2020";
    public const string PluginName = "ror2-Archipelago";
    public const string PluginVersion = "2.0.0";

    public static BepInEx.Configuration.ConfigEntry<bool> SatelliteEntry { get; set; }
    public static BepInEx.Configuration.ConfigEntry<string> SlotNameEntry { get; set; }
    public static BepInEx.Configuration.ConfigEntry<string> ServerNameEntry { get; set; }
    public static BepInEx.Configuration.ConfigEntry<int> PortEntry { get; set; }
    public static BepInEx.Configuration.ConfigEntry<string> PasswordEntry { get; set; }
    internal static ArchipelagoPlugin Instance { get; private set; }
    //public string bundleName = "connectbundle";
    //public static AssetBundle localAssetBundle { get; private set; }

    private ArchipelagoClient AP;
    private ClientItemsService ClientItems;
    //private bool isInLobbyConfigLoaded = false;
    internal static string apServerUri = "archipelago.gg";
    internal static int apServerPort = 38281;
    private bool willConnectToAP = true;
    private bool isPlayingAP = false;
    private bool isReconnecting = false;
    internal static string apSlotName = "";
    //private string apSlotName;
    internal static string apPassword;

    public ArchipelagoPlugin()
    {

    }

    public void Awake()
    {
        Log.Init(Logger);

        CreateConfigurations();

        apSlotName = SlotNameEntry.Value;
        apServerUri = ServerNameEntry.Value;
        apServerPort = PortEntry.Value;
        apPassword = PasswordEntry.Value;

        Instance = this;
        AP = new ArchipelagoClient();
        ArchipelagoConnectButtonController.OnConnectClick += OnClick_ConnectToArchipelagoWithButton;
        AP.OnClientDisconnect += AP_OnClientDisconnect;
        Run.onRunDestroyGlobal += Run_onRunDestroyGlobal;
        ArchipelagoStartMessage.OnArchipelagoSessionStart += ArchipelagoStartMessage_OnArchipelagoSessionStart;
        ArchipelagoEndMessage.OnArchipelagoSessionEnd += ArchipelagoEndMessage_OnArchipelagoSessionEnd;
        ArchipelagoConsoleCommand.OnArchipelagoCommandCalled += ArchipelagoConsoleCommand_ArchipelagoCommandCalled;
        ArchipelagoConsoleCommand.OnArchipelagoDisconnectCommandCalled += ArchipelagoConsoleCommand_ArchipelagoDisconnectCommandCalled;
        NetworkManagerSystem.onStopClientGlobal += GameNetworkManager_onStopClientGlobal;
        On.RoR2.UI.ChatBox.SubmitChat += ChatBox_SubmitChat;
        AssetBundleHelper.LoadBundle();

        CreateLobbyFields();

        NetworkingAPI.RegisterMessageType<SyncLocationCheckProgress>();
        NetworkingAPI.RegisterMessageType<ArchipelagoStartMessage>();
        NetworkingAPI.RegisterMessageType<ArchipelagoEndMessage>();
        NetworkingAPI.RegisterMessageType<SyncTotalCheckProgress>();
        NetworkingAPI.RegisterMessageType<AllChecksComplete>();
        NetworkingAPI.RegisterMessageType<AllChecksCompleteInStage>();
        NetworkingAPI.RegisterMessageType<ArchipelagoChatMessage>();
        NetworkingAPI.RegisterMessageType<SyncCurrentEnvironmentCheckProgress>();
        NetworkingAPI.RegisterMessageType<NextStageObjectives>();
        NetworkingAPI.RegisterMessageType<ArchipelagoTeleportClient>();
        NetworkingAPI.RegisterMessageType<SyncShrineCheckProgress>();
        NetworkingAPI.RegisterMessageType<ArchipelagoStartExplore>();
        NetworkingAPI.RegisterMessageType<ArchipelagoStartClassic>();

        CommandHelper.AddToConsoleWhenReady();
    }

    public void Start()
    {
        var connectButton = new GameObject("ArchipelagoConnectButtonController");
        connectButton.AddComponent<ArchipelagoConnectButtonController>();

        // Register AFTER the controller so this hook wraps it.
        // When CharacterSelectController.Awake fires, the controller's
        // hook creates the button first, then ours updates it.
        On.RoR2.UI.CharacterSelectController.Awake += CharacterSelectController_Awake;
    }

    private void CharacterSelectController_Awake(On.RoR2.UI.CharacterSelectController.orig_Awake orig, CharacterSelectController self)
    {
        orig(self);
        // If the AP session survived from a previous run, update the
        // button to show "Disconnect" so the player knows they're still connected.
        if (AP.IsConnected)
        {
            ArchipelagoConnectButtonController.ChangeButtonWhenConnected();
        }
    }

    private void GameNetworkManager_onStopClientGlobal()
    {
        if (!NetworkServer.active && isPlayingAP)
        {
            ArchipelagoCheckCountdownController.RemoveObjective();
        }
    }

    private void ChatBox_SubmitChat(On.RoR2.UI.ChatBox.orig_SubmitChat orig, RoR2.UI.ChatBox self)
    {
        if (!NetworkServer.active && isPlayingAP)
        {
            new ArchipelagoChatMessage(self.inputField.text).Send(NetworkDestination.Server);
            self.inputField.text = "";
            orig(self);
        }
        else
        {
            orig(self);
        }
    }

    private void ArchipelagoEndMessage_OnArchipelagoSessionEnd()
    {
        // This is for clients that are in a lobby but not the host of the lobby.
        // Clean up objectives when session ends.
        if (!NetworkServer.active && isPlayingAP)
        {
            ArchipelagoCheckCountdownController.RemoveObjective();
        }
    }

    private void AP_OnClientDisconnect(string reason)
    {
        Log.LogWarning($"Archipelago client was disconnected from the server: {reason}");
        ChatMessage.Send($"<style=cIsUtility>[AP]</style> <style=cDeath>Archipelago client was disconnected from the server. {reason}</style>");

        if (isPlayingAP && !isReconnecting && AP.reconnecting)
        {
            isReconnecting = true;
            StartCoroutine(ReconnectAndReset());
        }
    }

    private System.Collections.IEnumerator ReconnectAndReset()
    {
        yield return StartCoroutine(AP.AttemptReconnection());
        isReconnecting = false;
    }
    public void OnClick_ConnectToArchipelagoWithButton()
    {
        // Toggle: if already connected, disconnect instead
        if (AP.IsConnected)
        {
            AP.Disconnect();
            isPlayingAP = false;
            return;
        }

        isPlayingAP = true;
        string url = apServerUri + ":" + apServerPort;

        Log.LogDebug($"Server {apServerUri} Port: {apServerPort} Slot: {apSlotName} Password: {apPassword}");

        AP.Connect(url, apSlotName, apPassword);
        SlotNameEntry.Value = apSlotName;
    }
    private void ArchipelagoConsoleCommand_ArchipelagoCommandCalled(string url, int port, string slot, string password)
    {
        willConnectToAP = true;
        isPlayingAP = true;
        url = url + ":" + port;

        AP.Connect(url, slot, password);
        //StartCoroutine(AP.AttemptConnection());
    }
    private void ArchipelagoConsoleCommand_ArchipelagoDisconnectCommandCalled()
    {
        AP.Disconnect();
    }
    /// <summary>
    /// Server -> Client packet responder. Should not run on server.
    /// </summary>
    private void ArchipelagoStartMessage_OnArchipelagoSessionStart()
    {
        if (!NetworkServer.active)
        {
            // Clean up previous handler to prevent hook leaks on session reuse
            // or reconnection (ArchipelagoStartMessage can be received multiple times).
            ClientItems?.Unregister();
            ClientItems = new ClientItemsService();
            ClientItems?.Register();
            isPlayingAP = true;
        }
    }
    private void Run_onRunDestroyGlobal(Run obj)
    {
        if (isPlayingAP)
        {
            ArchipelagoTotalChecksObjectiveController.RemoveObjective();
            ArchipelagoLocationsInEnvironmentController.RemoveObjective();

        }
    }
    private void CreateLobbyFields()
    {
        ArchipelagoConnectButtonController.OnSlotChanged = (newValue) => apSlotName = newValue;
        ArchipelagoConnectButtonController.OnPasswordChanged = (newValue) => apPassword = newValue;
        ArchipelagoConnectButtonController.OnUrlChanged = (newValue) => apServerUri = newValue;
        ArchipelagoConnectButtonController.OnPortChanged = ChangePort;
    }
    private void CreateConfigurations()
    {
        SatelliteEntry = Config.Bind<bool>(
            "HighlightSatellite",
            "satellite",
            true,
            "This will highlight all satellites");
        SlotNameEntry = Config.Bind<string>(
            "SlotName",
            "slotName",
            "",
            "Change the default slot name");
        ServerNameEntry = Config.Bind<string>(
            "ServerName",
            "serverName",
            "archipelago.gg",
            "Change the default server name");
        PortEntry = Config.Bind<int>(
            "Port",
            "port",
            38281,
            "Change the default port");
        PasswordEntry = Config.Bind<string>(
            "Password",
            "password",
            "",
            "Change the default password");
    }

    private string ChangePort(string newValue)
    {
        apServerPort = int.Parse(newValue);
        return newValue;
    }
}