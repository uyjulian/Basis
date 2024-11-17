using System;
using static Basis.Scripts.Networking.NetworkedAvatar.BasisNetworkSendBase;

namespace Basis.Scripts.Networking.Compression
{
    /// <summary>
    /// Not thread-safe! Use with caution in single-threaded contexts.
    /// </summary>
    public class BasisCompressionOfMuscles
    {
        // Constants
        public const int BoneLength = 95;
        public const int ByteSize = 4; // Each muscle is a float (4 bytes)

        /// <summary>
        /// Compresses the muscle data into a byte buffer.
        /// </summary>
        /// <param name="muscles">The array of muscle data (floats) to compress.</param>
        /// <param name="buffer">The byte array to write the compressed data into.</param>
        /// <param name="offset">The offset in the buffer where the data should be written.</param>
        /// <exception cref="ArgumentException">Thrown if muscles array is not of correct length or buffer is too small.</exception>
        public static void CompressMusclesToBuffer(float[] muscles,ref byte[] buffer, ref int offset)
        {
            if (muscles == null || muscles.Length != BoneLength)
                throw new ArgumentException($"Muscles array must be of length {BoneLength}. Current length: {muscles?.Length ?? 0}.");

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer), "Buffer cannot be null.");

            if (buffer.Length - offset < BoneLength * ByteSize)
                throw new ArgumentException($"Buffer is too small to store the muscle data. Required: {BoneLength * ByteSize}, Available: {buffer.Length - offset}.");

            // Directly copy the muscle data into the buffer
            Buffer.BlockCopy(muscles, 0, buffer, offset, BoneLength * ByteSize);
            offset += BoneLength * ByteSize; // Update offset after writing
        }

        /// <summary>
        /// Decompresses the muscle data from a byte buffer back into muscle data.
        /// </summary>
        /// <param name="compressedData">The compressed byte data to decompress.</param>
        /// <param name="BasisAvatarData">The avatar buffer to store the decompressed muscle data.</param>
        /// <param name="offset">The offset in the compressed data to start reading from.</param>
        /// <exception cref="ArgumentException">Thrown if the compressed data is invalid or the avatar buffer is null.</exception>
        public static void DecompressMuscles(byte[] compressedData, ref AvatarBuffer BasisAvatarData, ref int offset)
        {
            if (compressedData == null)
                throw new ArgumentNullException(nameof(compressedData), "Compressed data cannot be null.");

            if (compressedData.Length - offset < BoneLength * ByteSize)
                throw new ArgumentException($"Not enough data to decompress. Required: {BoneLength * ByteSize}, Available: {compressedData.Length - offset}.");

            if (BasisAvatarData.Muscles == null || BasisAvatarData.Muscles.Length != BoneLength)
            {
                BasisAvatarData.Muscles = new float[BoneLength];
            }

            // Decompress the byte array into the muscles array starting at the given offset
            Buffer.BlockCopy(compressedData, offset, BasisAvatarData.Muscles, 0, BoneLength * ByteSize);

            // Update the offset after reading
            offset += BoneLength * ByteSize;
        }
    }
}