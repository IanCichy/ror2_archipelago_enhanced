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
using Archipelago.RiskOfRain2.Services;
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

namespace Archipelago.RiskOfRain2;

//TODO: perhaps only use particular drops as fodder for item pickups (i.e. only chest drops/interactable drops) then set options based on them maybe
public partial class ArchipelagoClient : IDisposable
{
    public delegate void ClientDisconnected(string reason);
    public event ClientDisconnected OnClientDisconnect;

    public string lastServerUrl { get; set; }
    public string lastSlotName { get; set; }
    public string lastPassword { get; set; }
    public bool IsConnected => session != null && session.Socket.Connected;

    internal DeathLinkManager DeathLinkManager { get; private set; }
    internal StageBlockerService StageBlockerService { get; private set; }
    internal LocationCheckService LocationCheckService { get; private set; }
    internal ShrineChanceService ShrineChanceService { get; private set; }
    internal ItemPoolService ItemPoolService { get; private set; }

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
    public static string ConnectedPlayerName;
    public static string victoryCondition;
    // Acceptable ending types
    private GameEndingDef[] acceptableEndings;
    // Acceptable stages to die on
    private string[] acceptableLosses;
    // Boss-kill victory detection: set when boss group defeated on a victory-eligible stage
    private bool bossDefeatedOnVictoryStage;

    // Cached slot data for session reuse across runs
    private bool cachedItemPoolLimiting;
    private bool cachedLegacyStagesUnlocked;
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
}
