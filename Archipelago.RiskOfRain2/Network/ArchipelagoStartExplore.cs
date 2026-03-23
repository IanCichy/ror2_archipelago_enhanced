using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message that signals the start of an exploration event in the Archipelago system.
/// </summary>
/// <remarks>This message is typically used to notify subscribers when an exploration phase should begin. It is
/// intended for use within networked multiplayer scenarios where synchronization of exploration events is
/// required.</remarks>
public class ArchipelagoStartExplore : INetMessage
{
    public static event Action OnArchipelagoStartExplore;

    public void Deserialize(NetworkReader reader)
    {

    }

    public void OnReceived()
    {
        OnArchipelagoStartExplore?.Invoke();
    }

    public void Serialize(NetworkWriter writer)
    {

    }
}