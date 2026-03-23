using Archipelago.RiskOfRain2.UI;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Network;

/// <summary>
/// Represents a network message used to synchronize the progress of environment checks, such as chests, shrines, and
/// other interactables, for the current scene.
/// </summary>
/// <remarks>This message is typically sent between clients and servers to ensure that all parties have consistent
/// information about the state of interactable objects in the current environment. It includes counts for various
/// interactable types and the scene identifier. This class implements the INetMessage interface for network
/// serialization and deserialization.</remarks>
public class SyncCurrentEnvironmentCheckProgress : INetMessage
{
    string scene;
    int chest;
    int shrine;
    int scavanger;
    int scanner;
    int newt;

    public SyncCurrentEnvironmentCheckProgress(string scene, int chest, int shrine, int scavanger, int scanner, int newt)
    {
        this.scene = scene;
        this.chest = chest;
        this.shrine = shrine;
        this.scavanger = scavanger;
        this.scanner = scanner;
        this.newt = newt;
    }

    public SyncCurrentEnvironmentCheckProgress()
    {

    }

    public void Deserialize(NetworkReader reader)
    {
        scene = reader.ReadString();
        chest = reader.ReadInt32();
        shrine = reader.ReadInt32();
        scavanger = reader.ReadInt32();
        scanner = reader.ReadInt32();
        newt = reader.ReadInt32();
    }

    public void OnReceived()
    {
        ArchipelagoLocationsInEnvironmentController.CurrentScene = scene;
        ArchipelagoLocationsInEnvironmentController.CurrentChests = chest;
        ArchipelagoLocationsInEnvironmentController.CurrentShrines = shrine;
        ArchipelagoLocationsInEnvironmentController.CurrentScavangers = scavanger;
        ArchipelagoLocationsInEnvironmentController.CurrentScanners = scanner;
        ArchipelagoLocationsInEnvironmentController.CurrentNewts = newt;
    }

    public void Serialize(NetworkWriter writer)
    {
        writer.Write(scene);
        writer.Write(chest);
        writer.Write(shrine);
        writer.Write(scavanger);
        writer.Write(scanner);
        writer.Write(newt);
    }
}