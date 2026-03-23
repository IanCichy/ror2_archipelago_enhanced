using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message used to transmit chat text between clients and the server in an Archipelago session.
/// </summary>
/// <remarks>This class is typically used to send or receive chat messages as part of the multiplayer
/// communication system. When a message is received, the <see cref="OnChatReceivedFromClient"/> event is raised to
/// notify subscribers. Instances of this class are serialized and deserialized using the provided network reader and
/// writer.</remarks>
public class ArchipelagoChatMessage : INetMessage
{
    public static event Action<string> OnChatReceivedFromClient;
    private string message;

    public ArchipelagoChatMessage(string message)
    {
        this.message = message;
    }

    public ArchipelagoChatMessage()
    {

    }

    public void Deserialize(NetworkReader reader)
    {
        message = reader.ReadString();
    }

    public void OnReceived()
    {
        OnChatReceivedFromClient?.Invoke(message);
    }

    public void Serialize(NetworkWriter writer)
    {
        writer.Write(message);
    }
}