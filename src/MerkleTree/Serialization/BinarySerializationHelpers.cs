namespace MerkleTree.Serialization;

/// <summary>
/// Provides endianness-independent binary serialization helpers.
/// </summary>
/// <remarks>
/// All methods use little-endian byte order for cross-platform determinism.
/// This ensures that serialized data is identical across different architectures
/// and operating systems, regardless of the native endianness.
/// </remarks>
internal static class BinarySerializationHelpers
{
    /// <summary>
    /// Writes a 32-bit signed integer to a byte array in little-endian format.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="offset">The offset in the buffer to start writing.</param>
    public static void WriteInt32LittleEndian(int value, byte[] buffer, int offset)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    /// <summary>
    /// Writes a 64-bit signed integer to a byte array in little-endian format.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="offset">The offset in the buffer to start writing.</param>
    public static void WriteInt64LittleEndian(long value, byte[] buffer, int offset)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
        buffer[offset + 4] = (byte)(value >> 32);
        buffer[offset + 5] = (byte)(value >> 40);
        buffer[offset + 6] = (byte)(value >> 48);
        buffer[offset + 7] = (byte)(value >> 56);
    }

    /// <summary>
    /// Reads a 32-bit signed integer from a byte array in little-endian format.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The offset in the buffer to start reading.</param>
    /// <returns>The read value.</returns>
    public static int ReadInt32LittleEndian(byte[] buffer, int offset)
    {
        return buffer[offset] |
               (buffer[offset + 1] << 8) |
               (buffer[offset + 2] << 16) |
               (buffer[offset + 3] << 24);
    }

    /// <summary>
    /// Reads a 64-bit signed integer from a byte array in little-endian format.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The offset in the buffer to start reading.</param>
    /// <returns>The read value.</returns>
    public static long ReadInt64LittleEndian(byte[] buffer, int offset)
    {
        uint lo = (uint)(buffer[offset] |
                        (buffer[offset + 1] << 8) |
                        (buffer[offset + 2] << 16) |
                        (buffer[offset + 3] << 24));
        
        uint hi = (uint)(buffer[offset + 4] |
                        (buffer[offset + 5] << 8) |
                        (buffer[offset + 6] << 16) |
                        (buffer[offset + 7] << 24));
        
        return ((long)hi << 32) | lo;
    }
}
