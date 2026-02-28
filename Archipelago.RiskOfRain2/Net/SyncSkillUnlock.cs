using System;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Net
{
    public class SyncSkillUnlock : INetMessage
    {
        public static event Action<int> OnSkillUnlockReceived;

        private int unlockCount;

        public SyncSkillUnlock()
        {
        }

        public SyncSkillUnlock(int unlockCount)
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
            OnSkillUnlockReceived?.Invoke(unlockCount);
        }
    }
}
