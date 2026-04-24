using Mirror;
using UnityEngine;

namespace NowWatchThisDrive
{
    public struct DriveClipTriggerMsg : NetworkMessage
    {
    }

    public struct DriveClipPlayMsg : NetworkMessage
    {
        public uint emitterNetId;
        public Vector3 position;
    }
}
