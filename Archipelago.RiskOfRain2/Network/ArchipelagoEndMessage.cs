using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message that signals the end of an Archipelago session.
/// </summary>
/// <remarks>This message is typically used to notify connected components that the Archipelago session has
/// concluded. When received, it triggers the static OnArchipelagoSessionEnd event, allowing subscribers to perform any
/// necessary cleanup or state updates in response to the session ending.</remarks>
public class ArchipelagoEndMessage : INetMessage
{
    public static event Action OnArchipelagoSessionEnd;

    public void Deserialize(NetworkReader reader)
    {
    }

    public void OnReceived()
    {
        if (OnArchipelagoSessionEnd != null)
        {
            OnArchipelagoSessionEnd();
        }
    }

    public void Serialize(NetworkWriter writer)
    {
    }
}