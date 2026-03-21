using Archipelago.RiskOfRain2.UI;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message that communicates the progress of total checks synchronization between clients and the
/// server.
/// </summary>
/// <remarks>This class is typically used in multiplayer scenarios to update clients on the current and total
/// number of checks completed as part of a synchronization objective. It implements the INetMessage interface to
/// support serialization and deserialization for network transmission.</remarks>
public class SyncTotalCheckProgress : INetMessage
{
    int currentChecks;
    int totalChecks;

    public SyncTotalCheckProgress()
    {

    }

    public SyncTotalCheckProgress(int current, int total)
    {
        currentChecks = current;
        totalChecks = total;
    }

    public void Deserialize(NetworkReader reader)
    {
        currentChecks = reader.ReadInt32();
        totalChecks = reader.ReadInt32();
    }

    public void OnReceived()
    {
        ArchipelagoTotalChecksObjectiveController.CurrentChecks = currentChecks;
        ArchipelagoTotalChecksObjectiveController.TotalChecks = totalChecks;
    }

    public void Serialize(NetworkWriter writer)
    {
        writer.Write(currentChecks);
        writer.Write(totalChecks);
    }
}