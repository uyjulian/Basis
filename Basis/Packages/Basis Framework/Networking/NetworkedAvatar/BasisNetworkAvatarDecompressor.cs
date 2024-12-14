using Basis.Scripts.Networking.Compression;
using Basis.Scripts.Networking.Recievers;
using System;
using static SerializableBasis;
using Vector3 = UnityEngine.Vector3;
namespace Basis.Scripts.Networking.NetworkedAvatar
{
    public static class BasisNetworkAvatarDecompressor
    {
        /// <summary>
        /// Single API to handle all avatar decompression tasks.
        /// </summary>
        public static void DecompressAndProcessAvatar(BasisNetworkReceiver baseReceiver, ServerSideSyncPlayerMessage syncMessage)
        {
            byte[] Array = syncMessage.avatarSerialization.array;
            if (Array == null)
            {
                throw new ArgumentException("Byte array is null");
            }
            AvatarBuffer avatarBuffer = new AvatarBuffer();
            int Offset = 0;
            avatarBuffer.Position = BasisUnityBitPackerExtensions.ReadVectorFloatFromBytes(ref Array, ref Offset);
            avatarBuffer.rotation = BasisUnityBitPackerExtensions.ReadQuaternionFromBytes(ref Array, BasisNetworkSendBase.RotationCompression, ref Offset);
            BasisUnityBitPackerExtensions.ReadMusclesFromBytes(ref Array, ref avatarBuffer.Muscles, ref Offset);
            int length = Array.Length;
            if (Offset == length)
            {
                avatarBuffer.Scale = Vector3.one;
            }
            else
            {
                if (length > Offset + 6)//we have 3 ushorts
                {
                    avatarBuffer.Scale = BasisUnityBitPackerExtensions.ReadUshortVectorFloatFromBytes(ref Array, BasisNetworkReceiver.ScaleRanged, ref Offset);
                }
                else
                {
                    //we have just one
                    float Size = BasisUnityBitPackerExtensions.ReadUshortFloatFromBytes(ref Array, BasisNetworkReceiver.ScaleRanged, ref Offset);
                    avatarBuffer.Scale = new Unity.Mathematics.float3(Size, Size, Size);
                }
            }
            avatarBuffer.SecondsInterval = syncMessage.interval / 1000.0f;
            baseReceiver.EnQueueAvatarBuffer(ref avatarBuffer);
        }
    }
}