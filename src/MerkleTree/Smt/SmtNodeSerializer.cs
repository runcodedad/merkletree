using System.Buffers.Binary;

namespace MerkleTree.Smt;

/// <summary>
/// Provides serialization and deserialization for SMT nodes.
/// </summary>
/// <remarks>
/// <para>
/// This serializer uses a compact binary format for SMT nodes, ensuring
/// deterministic and platform-independent serialization.
/// </para>
/// <para>
/// The serialization format uses little-endian byte order for all numeric values
/// to ensure cross-platform compatibility.
/// </para>
/// </remarks>
internal static class SmtNodeSerializer
{
    /// <summary>
    /// Serializes an SMT node to a binary format.
    /// </summary>
    /// <param name="node">The node to serialize.</param>
    /// <returns>The serialized node data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the node type is unknown.</exception>
    public static byte[] Serialize(SmtNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        return node.NodeType switch
        {
            SmtNodeType.Empty => SerializeEmpty((SmtEmptyNode)node),
            SmtNodeType.Leaf => SerializeLeaf((SmtLeafNode)node),
            SmtNodeType.Internal => SerializeInternal((SmtInternalNode)node),
            _ => throw new ArgumentException($"Unknown node type: {node.NodeType}", nameof(node))
        };
    }

    /// <summary>
    /// Deserializes an SMT node from binary format.
    /// </summary>
    /// <param name="data">The serialized node data.</param>
    /// <returns>The deserialized node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the data is invalid or the node type is unknown.</exception>
    public static SmtNode Deserialize(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty.", nameof(data));

        var nodeType = (SmtNodeType)data.Span[0];

        return nodeType switch
        {
            SmtNodeType.Empty => DeserializeEmpty(data),
            SmtNodeType.Leaf => DeserializeLeaf(data),
            SmtNodeType.Internal => DeserializeInternal(data),
            _ => throw new ArgumentException($"Unknown node type: {nodeType}", nameof(data))
        };
    }

    private static byte[] SerializeEmpty(SmtEmptyNode node)
    {
        // Format: [NodeType:1][Level:4][Hash:N]
        var hashSpan = node.Hash.Span;
        var data = new byte[1 + 4 + hashSpan.Length];
        
        data[0] = (byte)SmtNodeType.Empty;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1, 4), node.Level);
        hashSpan.CopyTo(data.AsSpan(5));

        return data;
    }

    private static SmtNode DeserializeEmpty(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 5)
            throw new ArgumentException("Invalid empty node data: insufficient length.", nameof(data));

        var level = BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(1, 4));
        var hash = data.Slice(5);

        return new SmtEmptyNode(level, hash);
    }

    private static byte[] SerializeLeaf(SmtLeafNode node)
    {
        // Format: [NodeType:1][KeyHashLength:4][KeyHash:N][ValueLength:4][Value:M][NodeHash:H][OriginalKeyLength:4][OriginalKey:K]
        var keyHashSpan = node.KeyHash.Span;
        var valueSpan = node.Value.Span;
        var nodeHashSpan = node.Hash.Span;
        ReadOnlySpan<byte> originalKeySpan = node.OriginalKey.HasValue ? node.OriginalKey.Value.Span : ReadOnlySpan<byte>.Empty;

        var totalLength = 1 + 4 + keyHashSpan.Length + 4 + valueSpan.Length + nodeHashSpan.Length + 4 + originalKeySpan.Length;
        var data = new byte[totalLength];
        
        var offset = 0;
        data[offset++] = (byte)SmtNodeType.Leaf;
        
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), keyHashSpan.Length);
        offset += 4;
        keyHashSpan.CopyTo(data.AsSpan(offset));
        offset += keyHashSpan.Length;
        
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), valueSpan.Length);
        offset += 4;
        valueSpan.CopyTo(data.AsSpan(offset));
        offset += valueSpan.Length;
        
        nodeHashSpan.CopyTo(data.AsSpan(offset));
        offset += nodeHashSpan.Length;
        
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), originalKeySpan.Length);
        offset += 4;
        if (originalKeySpan.Length > 0)
        {
            originalKeySpan.CopyTo(data.AsSpan(offset));
        }

        return data;
    }

    private static SmtNode DeserializeLeaf(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 9)
            throw new ArgumentException("Invalid leaf node data: insufficient length.", nameof(data));

        var offset = 1;
        var keyHashLength = BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(offset, 4));
        offset += 4;
        
        if (data.Length < offset + keyHashLength + 4)
            throw new ArgumentException("Invalid leaf node data: insufficient length for key hash.", nameof(data));
        
        var keyHash = data.Slice(offset, keyHashLength);
        offset += keyHashLength;
        
        var valueLength = BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(offset, 4));
        offset += 4;
        
        if (data.Length < offset + valueLength)
            throw new ArgumentException("Invalid leaf node data: insufficient length for value.", nameof(data));
        
        var value = data.Slice(offset, valueLength);
        offset += valueLength;
        
        // Determine node hash length (should be consistent with hash function)
        var remainingLength = data.Length - offset;
        if (remainingLength < 4)
            throw new ArgumentException("Invalid leaf node data: insufficient length for node hash.", nameof(data));
        
        // Read original key length
        var originalKeyLengthOffset = data.Length - 4;
        var originalKeyLength = BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(originalKeyLengthOffset, 4));
        
        var nodeHashLength = originalKeyLengthOffset - offset - originalKeyLength;
        if (nodeHashLength <= 0)
            throw new ArgumentException("Invalid leaf node data: invalid hash length.", nameof(data));
        
        var nodeHash = data.Slice(offset, nodeHashLength);
        offset += nodeHashLength;
        
        ReadOnlyMemory<byte>? originalKey = null;
        if (originalKeyLength > 0)
        {
            originalKey = data.Slice(offset, originalKeyLength);
        }

        return new SmtLeafNode(keyHash, value, nodeHash, originalKey);
    }

    private static byte[] SerializeInternal(SmtInternalNode node)
    {
        // Format: [NodeType:1][LeftHashLength:4][LeftHash:N][RightHashLength:4][RightHash:M][NodeHash:H]
        var leftHashSpan = node.LeftHash.Span;
        var rightHashSpan = node.RightHash.Span;
        var nodeHashSpan = node.Hash.Span;

        var data = new byte[1 + 4 + leftHashSpan.Length + 4 + rightHashSpan.Length + nodeHashSpan.Length];
        
        var offset = 0;
        data[offset++] = (byte)SmtNodeType.Internal;
        
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), leftHashSpan.Length);
        offset += 4;
        leftHashSpan.CopyTo(data.AsSpan(offset));
        offset += leftHashSpan.Length;
        
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), rightHashSpan.Length);
        offset += 4;
        rightHashSpan.CopyTo(data.AsSpan(offset));
        offset += rightHashSpan.Length;
        
        nodeHashSpan.CopyTo(data.AsSpan(offset));

        return data;
    }

    private static SmtNode DeserializeInternal(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 9)
            throw new ArgumentException("Invalid internal node data: insufficient length.", nameof(data));

        var offset = 1;
        var leftHashLength = BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(offset, 4));
        offset += 4;
        
        if (data.Length < offset + leftHashLength + 4)
            throw new ArgumentException("Invalid internal node data: insufficient length for left hash.", nameof(data));
        
        var leftHash = data.Slice(offset, leftHashLength);
        offset += leftHashLength;
        
        var rightHashLength = BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(offset, 4));
        offset += 4;
        
        if (data.Length < offset + rightHashLength)
            throw new ArgumentException("Invalid internal node data: insufficient length for right hash.", nameof(data));
        
        var rightHash = data.Slice(offset, rightHashLength);
        offset += rightHashLength;
        
        var nodeHash = data.Slice(offset);

        return new SmtInternalNode(leftHash, rightHash, nodeHash);
    }
}
