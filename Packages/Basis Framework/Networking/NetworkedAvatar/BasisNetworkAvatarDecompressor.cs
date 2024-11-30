using Basis.Scripts.Networking.Compression;
using Basis.Scripts.Networking.Recievers;
using UnityEngine;
using static Basis.Scripts.Networking.NetworkedAvatar.BasisNetworkSendBase;
using static SerializableDarkRift;
namespace Basis.Scripts.Networking.NetworkedAvatar
{
    public static class BasisNetworkAvatarDecompressor
    {
        /// <summary>
        /// Single API to handle all avatar decompression tasks.
        /// </summary>
        public static void DecompressAndProcessAvatar(BasisNetworkReceiver baseReceiver, ServerSideSyncPlayerMessage syncMessage)
        {
            // Update receiver state
            baseReceiver.LASM = syncMessage.avatarSerialization;
            AvatarBuffer avatarBuffer = BasisAvatarBufferPool.Rent();
            double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
            int Offset = 0;
            avatarBuffer.Position = BasisBitPackerExtensions.ReadVectorFloatFromBytes(ref syncMessage.avatarSerialization.array, ref Offset);
            avatarBuffer.Scale = BasisBitPackerExtensions.ReadUshortVectorFloatFromBytes(ref syncMessage.avatarSerialization.array, BasisNetworkReceiver.ScaleRanged, ref Offset);
            avatarBuffer.rotation = BasisBitPackerExtensions.ReadQuaternionFromBytes(ref syncMessage.avatarSerialization.array, BasisNetworkSendBase.RotationCompression, ref Offset);
            BasisBitPackerExtensions.ReadMusclesFromBytes(ref syncMessage.avatarSerialization.array, ref avatarBuffer.Muscles, ref Offset);
            avatarBuffer.timestamp = realtimeSinceStartupAsDouble;
            baseReceiver.AvatarDataBuffer.Add(avatarBuffer);

            baseReceiver.AverageRecTime = realtimeSinceStartupAsDouble - baseReceiver.LastRecTime;
            baseReceiver.LastRecTime = realtimeSinceStartupAsDouble;

            if (baseReceiver.HasStartAndEnd == false)
            {
                if (baseReceiver.Start.timestamp == 0)
                {
                    baseReceiver.Start = avatarBuffer;
                }
                else
                {
                    if (baseReceiver.End.timestamp == 0)
                    {
                        baseReceiver.End = avatarBuffer;
                        baseReceiver.HasStartAndEnd = true;
                    }
                }
            }
        }
    }
}