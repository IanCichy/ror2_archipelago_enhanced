using Archipelago.RiskOfRain2.UI;
using R2API.Networking.Interfaces;
using System;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message that indicates all checks have been completed.
/// </summary>
/// <remarks>This message is typically used to notify interested components when all required checks or objectives
/// have been fulfilled. Subscribers to the OnAllChecksComplete event can perform additional actions in response to this
/// notification.</remarks>
public class AllChecksComplete : INetMessage
{
    public static event Action OnAllChecksComplete;

    public void Deserialize(NetworkReader reader)
    {

    }

    public void OnReceived()
    {
        ArchipelagoTotalChecksObjectiveController.RemoveObjective();
        if (OnAllChecksComplete != null)
        {
            OnAllChecksComplete();
        }
    }

    public void Serialize(NetworkWriter writer)
    {

    }
}