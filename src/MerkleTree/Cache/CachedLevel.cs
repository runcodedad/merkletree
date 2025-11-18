namespace MerkleTree.Cache;

/// <summary>
/// Represents all nodes at a specific level in a Merkle tree cache.
/// </summary>
/// <remarks>
/// This class stores the hash values of all nodes at a particular level,
/// allowing for efficient random access by node index.
/// </remarks>
public class CachedLevel
{
    /// <summary>
    /// Gets the level number in the tree.
    /// </summary>
    /// <remarks>
    /// Level 0 represents the leaves. Higher levels are closer to the root.
    /// </remarks>
    public int Level { get; }

    /// <summary>
    /// Gets the hash values of all nodes at this level.
    /// </summary>
    /// <remarks>
    /// Each element is a hash value. The array index corresponds to the node's
    /// position at this level (0-based, left-to-right).
    /// All hashes should have the same length.
    /// </remarks>
    public byte[][] Nodes { get; }

    /// <summary>
    /// Gets the number of nodes at this level.
    /// </summary>
    public long NodeCount => Nodes.LongLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedLevel"/> class.
    /// </summary>
    /// <param name="level">The level number in the tree.</param>
    /// <param name="nodes">The hash values of all nodes at this level.</param>
    /// <exception cref="ArgumentNullException">Thrown when nodes is null.</exception>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public CachedLevel(int level, byte[][] nodes)
    {
        if (nodes == null)
            throw new ArgumentNullException(nameof(nodes));
        if (level < 0)
            throw new ArgumentException("Level must be non-negative.", nameof(level));
        
        // Validate that no node is null
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null)
                throw new ArgumentException($"Node at index {i} is null.", nameof(nodes));
        }

        Level = level;
        Nodes = nodes;
    }

    /// <summary>
    /// Gets the hash value of a node at the specified index.
    /// </summary>
    /// <param name="index">The 0-based index of the node at this level.</param>
    /// <returns>The hash value of the node.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public byte[] GetNode(long index)
    {
        if (index < 0 || index >= Nodes.LongLength)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range [0, {Nodes.LongLength}).");
        
        return Nodes[index];
    }
}
