using Archipelago.RiskOfRain2.UI;
using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message indicating that all checks have been completed in the current stage.
/// </summary>
/// <remarks>This message is typically used to notify interested components when all required checks or objectives
/// in a stage are finished. Subscribers to the <see cref="OnAllChecksCompleteInStage"/> event can perform additional
/// actions in response to this notification.</remarks>
public class AllChecksCompleteInStage : INetMessage
{
    public static event Action OnAllChecksCompleteInStage;

    public void Deserialize(NetworkReader reader)
    {

    }

    public void OnReceived()
    {
        ArchipelagoLocationsInEnvironmentController.RemoveObjective();
        OnAllChecksCompleteInStage?.Invoke();
    }

    public void Serialize(NetworkWriter writer)
    {

    }
}