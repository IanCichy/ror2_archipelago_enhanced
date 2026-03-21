using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message used to synchronize shrine check progress between clients in a multiplayer session.
/// </summary>
/// <remarks>This class is typically used in multiplayer scenarios to communicate the current state of shrine
/// pickups, such as the number of shrines checked and the current step in the process. The static event OnShrineSynced
/// is raised when a message is received, allowing subscribers to update local state accordingly.</remarks>
public class SyncShrineCheckProgress : INetMessage
{
    public delegate void ShrineCheckSyncHandler(int count, int step);
    public static event ShrineCheckSyncHandler OnShrineSynced;

    int itemPickupCount;
    int itemPickupStep;

    public SyncShrineCheckProgress()
    {

    }

    public SyncShrineCheckProgress(int shrineCount, int shrinePickupStep)
    {
        itemPickupCount = shrineCount;
        itemPickupStep = shrinePickupStep;
    }

    public void Deserialize(NetworkReader reader)
    {
        itemPickupStep = reader.ReadInt32();
        itemPickupCount = reader.ReadInt32();
    }

    public void OnReceived()
    {
        OnShrineSynced?.Invoke(itemPickupCount, itemPickupStep);
    }

    public void Serialize(NetworkWriter writer)
    {
        writer.Write(itemPickupStep);
        writer.Write(itemPickupCount);
    }
}