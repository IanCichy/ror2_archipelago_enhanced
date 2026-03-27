using Archipelago.RiskOfRain2.UI;
using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message that signals the addition of objectives for the next stage in the game progression.
/// </summary>
/// <remarks>This type is typically used in multiplayer scenarios to notify clients or systems when new objectives
/// should be added for the upcoming stage. It implements the INetMessage interface to support network serialization and
/// deserialization.</remarks>
public class NextStageObjectives : INetMessage
{
    public static event Action OnNextStageObjectives;

    public void Deserialize(NetworkReader reader)
    {

    }

    public void OnReceived()
    {
        ArchipelagoLocationsInEnvironmentController.AddObjective();
        OnNextStageObjectives?.Invoke();
    }

    public void Serialize(NetworkWriter writer)
    {

    }
}