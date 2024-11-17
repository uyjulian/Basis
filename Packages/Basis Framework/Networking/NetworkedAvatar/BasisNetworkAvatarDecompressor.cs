using Basis.Scripts.Networking.Compression;
using System;
using Unity.Mathematics;
using UnityEngine;
using static Basis.Scripts.Networking.NetworkedAvatar.BasisNetworkSendBase;
using static SerializableDarkRift;

namespace Basis.Scripts.Networking.NetworkedAvatar
{
    public static class BasisNetworkAvatarDecompressor
    {
        public static void DeCompress(BasisNetworkSendBase Base, ServerSideSyncPlayerMessage ServerSideSyncPlayerMessage)
        {
            Base.LASM = ServerSideSyncPlayerMessage.avatarSerialization;
            AvatarBuffer AvatarBuffer = new AvatarBuffer();
            DecompressAvatar(ref AvatarBuffer, Base.LASM, Base.PositionRanged, Base.ScaleRanged);
            double TimeasDouble = Time.realtimeSinceStartupAsDouble;
            Base.LastAvatarDelta = (float)(TimeasDouble - Base.TimeAsDoubleWhenLastSync);
            Base.TimeAsDoubleWhenLastSync = TimeasDouble;
            AvatarBuffer.timestamp = TimeasDouble;
            // Add new rotation data to the buffer
            Base.AvatarDataBuffer.Add(AvatarBuffer);

            // Sort buffer by timestamp
            Base.AvatarDataBuffer.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
        }
        public static void DecompressAvatar(ref AvatarBuffer AvatarData, LocalAvatarSyncMessage LASM, BasisRangedUshortFloatData PositionRanged, BasisRangedUshortFloatData ScaleRanged)
        {
            if (LASM.array != null && LASM.array.Length != 0)
            {
                int offset = 0;

                // Decompress Position
                AvatarData.Position = ReadVectorFloat(LASM.array, ref offset);

                // Decompress Scale
                AvatarData.Scale = DecompressUShortVector3(LASM.array, ScaleRanged, ref offset);

                // Decompress Rotation
                DecompressQuaternion(LASM.array, ref AvatarData.rotation, ref offset);

                // Decompress Muscles
                BasisCompressionOfMuscles.DecompressMuscles(LASM.array, ref AvatarData,ref offset);
            }
            else
            {
                Debug.LogError("Array was null or empty!");
                AvatarData.Scale = Vector3.zero;
                AvatarData.Position = Vector3.zero;
                AvatarData.rotation = Quaternion.identity;
            }
        }
        // Decompress a quaternion from a byte array (only decompressing w)
        public static void DecompressQuaternion(byte[] packet, ref quaternion quaternion, ref int offset)
        {
            // Ensure the packet has enough data
            if (packet.Length < offset + 14)
                throw new ArgumentException("Packet is too small to contain quaternion data.");

            // Read x, y, z as floats
            quaternion.value.x = BitConverter.ToSingle(packet, offset);
            quaternion.value.y = BitConverter.ToSingle(packet, offset + 4);
            quaternion.value.z = BitConverter.ToSingle(packet, offset + 8);

            // Read and decompress w
            ushort compressedW = BitConverter.ToUInt16(packet, offset + 12);
            quaternion.value.w = BasisNetworkSendBase.RotationCompressor.Decompress(compressedW);

            // Update offset after reading quaternion
            offset += 14;
        }

        // Decompress a Vector3 (Position or Scale) from a byte array with the provided compression data
        public static Vector3 DecompressUShortVector3(byte[] packet, BasisRangedUshortFloatData compressor, ref int offset)
        {
            // Decompress each component (2 bytes per ushort)
            ushort x = BitConverter.ToUInt16(packet, offset);
            ushort y = BitConverter.ToUInt16(packet, offset + 2);
            ushort z = BitConverter.ToUInt16(packet, offset + 4);

            // Use the compressor to decompress the ushort values to floats
            float xVal = compressor.Decompress(x);
            float yVal = compressor.Decompress(y);
            float zVal = compressor.Decompress(z);

            // Update offset after reading the Vector3
            offset += 6;

            // Return the decompressed Vector3
            return new Vector3(xVal, yVal, zVal);
        }

        // Reads a Vector3 from a byte array and updates the offset
        public static Vector3 ReadVectorFloat(byte[] array, ref int offset)
        {
            // Read each component as a float (4 bytes per float)
            float x = BitConverter.ToSingle(array, offset);
            float y = BitConverter.ToSingle(array, offset + sizeof(float));
            float z = BitConverter.ToSingle(array, offset + 2 * sizeof(float));

            // Update the offset after reading the Vector3
            offset += 12;

            return new Vector3(x, y, z);
        }
    }
}