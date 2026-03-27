using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message used to handle teleportation events within the Archipelago system.
/// </summary>
/// <remarks>This class implements the INetMessage interface to support serialization and deserialization of
/// teleportation-related network messages. When a message of this type is received, the OnArchipelagoTeleportClient
/// event is raised to notify subscribers. This class is typically used in multiplayer scenarios where teleportation
/// events need to be communicated between clients and servers.</remarks>
public class ArchipelagoTeleportClient : INetMessage
{
    public static event Action OnArchipelagoTeleportClient;

    public void Deserialize(NetworkReader reader)
    {

    }

    public void OnReceived()
    {
        OnArchipelagoTeleportClient?.Invoke();
    }

    public void Serialize(NetworkWriter writer)
    {

    }
}