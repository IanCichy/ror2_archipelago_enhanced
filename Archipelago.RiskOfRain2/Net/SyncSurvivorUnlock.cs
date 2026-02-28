using System;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Net
{
    public class SyncSurvivorUnlock : INetMessage
    {
        public static event Action<int> OnSurvivorUnlockReceived;

        private int unlockCount;

        public SyncSurvivorUnlock()
        {
        }

        public SyncSurvivorUnlock(int unlockCount)
        {
            this.unlockCount = unlockCount;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(unlockCount);
        }

        public void Deserialize(NetworkReader reader)
        {
            unlockCount = reader.ReadInt32();
        }

        public void OnReceived()
        {
            OnSurvivorUnlockReceived?.Invoke(unlockCount);
        }
    }
}
