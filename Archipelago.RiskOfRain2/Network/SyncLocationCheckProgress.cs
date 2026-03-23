using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message used to synchronize the progress of location checks, such as item pickups, between
/// clients and servers.
/// </summary>
/// <remarks>This class is typically used in multiplayer scenarios to communicate the current state of item pickup
/// progress. It provides serialization and deserialization methods for network transmission and raises the
/// OnLocationSynced event when a synchronization message is received.</remarks>
public class SyncLocationCheckProgress : INetMessage
{
    public delegate void LocationCheckSyncHandler(int count, int step);
    public static event LocationCheckSyncHandler OnLocationSynced;

    int itemPickupCount;
    int itemPickupStep;

    public SyncLocationCheckProgress()
    {

    }

    public SyncLocationCheckProgress(int itemCount, int pickupStep)
    {
        itemPickupCount = itemCount;
        itemPickupStep = pickupStep;
    }

    public void Deserialize(NetworkReader reader)
    {
        itemPickupStep = reader.ReadInt32();
        itemPickupCount = reader.ReadInt32();
    }

    public void OnReceived()
    {
        OnLocationSynced?.Invoke(itemPickupCount, itemPickupStep);
    }

    public void Serialize(NetworkWriter writer)
    {
        writer.Write(itemPickupStep);
        writer.Write(itemPickupCount);
    }
}