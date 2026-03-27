using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message that signals the start of the classic Archipelago mode.
/// </summary>
/// <remarks>This class is used to notify subscribers when the classic Archipelago mode should begin, typically as
/// part of a multiplayer network protocol. The associated event, OnArchipelagoStartClassic, is raised when the message
/// is received. This type implements INetMessage to support serialization and deserialization for network
/// transmission.</remarks>
public class ArchipelagoStartClassic : INetMessage
{
    public static event Action OnArchipelagoStartClassic;

    public void Deserialize(NetworkReader reader)
    {

    }

    public void OnReceived()
    {
        OnArchipelagoStartClassic?.Invoke();
    }

    public void Serialize(NetworkWriter writer)
    {

    }
}