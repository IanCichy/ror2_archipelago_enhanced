using Archipelago.RiskOfRain2.UI;
using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message that signals the start of an Archipelago session.
/// </summary>
/// <remarks>This message is typically used to notify connected clients or systems that an Archipelago session has
/// begun. It triggers the <see cref="OnArchipelagoSessionStart"/> event when received, allowing subscribers to perform
/// initialization or respond to the session start.</remarks>
public class ArchipelagoStartMessage : INetMessage
{
    public static event Action OnArchipelagoSessionStart;

    public void Deserialize(NetworkReader reader)
    {

    }

    public void OnReceived()
    {
        ArchipelagoTotalChecksObjectiveController.AddObjective();
        OnArchipelagoSessionStart?.Invoke();
    }

    public void Serialize(NetworkWriter writer)
    {

    }
}