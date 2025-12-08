using System.Text;

namespace MerkleTree.Smt.Persistence;

/// <summary>
/// In-memory reference implementation of SMT persistence interfaces.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a simple, thread-safe in-memory implementation of all SMT persistence
/// interfaces. It is intended for:
/// - Testing and development
/// - Validating persistence interface contracts
/// - Prototyping SMT operations
/// - Small trees that fit in memory
/// </para>
/// <para>
/// For production use with large trees or durability requirements, use a database-backed
/// or file-backed implementation instead.
/// </para>
/// <para><strong>Thread Safety:</strong></para>
/// <para>
/// This implementation is fully thread-safe using lock-based synchronization.
/// All operations can be safely called from multiple threads concurrently.
/// </para>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Node reads: O(1) average case using hash-based dictionary lookup</description></item>
/// <item><description>Node writes: O(1) average case per node</description></item>
/// <item><description>Batch writes: O(n) where n is the batch size</description></item>
/// <item><description>Snapshots: O(1) - only stores references, not copies</description></item>
/// </list>
/// </remarks>
public sealed class InMemorySmtStorage : ISmtNodeReader, ISmtNodeWriter, ISmtSnapshotManager, ISmtMetadataStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, byte[]> _nodesByHash = [];
    private readonly Dictionary<string, byte[]> _nodesByPath = [];
    private readonly Dictionary<string, SmtSnapshotInfo> _snapshots = [];
    private SmtMetadata? _metadata;
    private byte[]? _currentRoot;

    /// <summary>
    /// Gets the number of nodes currently stored.
    /// </summary>
    public int NodeCount
    {
        get
        {
            lock (_lock)
            {
                return _nodesByHash.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of snapshots currently stored.
    /// </summary>
    public int SnapshotCount
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.Count;
            }
        }
    }

    /// <summary>
    /// Clears all data from storage (nodes, snapshots, metadata).
    /// </summary>
    /// <remarks>
    /// This method is useful for testing and development. In production implementations,
    /// consider whether clearing all data should be allowed or require explicit confirmation.
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            _nodesByHash.Clear();
            _nodesByPath.Clear();
            _snapshots.Clear();
            _metadata = null;
            _currentRoot = null;
        }
    }

    #region ISmtNodeReader Implementation

    /// <inheritdoc/>
    public Task<SmtNodeBlob?> ReadNodeByHashAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default)
    {
        if (hash.IsEmpty)
            throw new ArgumentException("Hash cannot be empty.", nameof(hash));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var hashKey = ByteArrayToHex(hash.ToArray());
            if (_nodesByHash.TryGetValue(hashKey, out var serializedNode))
            {
                // Check if we have path information for this node
                var pathEntry = _nodesByPath.FirstOrDefault(kvp => kvp.Value.SequenceEqual(serializedNode));
                ReadOnlyMemory<bool>? path = null;
                
                if (!string.IsNullOrEmpty(pathEntry.Key))
                {
                    path = DecodePath(pathEntry.Key);
                }

                return Task.FromResult<SmtNodeBlob?>(
                    new SmtNodeBlob(hash, new ReadOnlyMemory<byte>(serializedNode), path));
            }

            return Task.FromResult<SmtNodeBlob?>(null);
        }
    }

    /// <inheritdoc/>
    public Task<SmtNodeBlob?> ReadNodeByPathAsync(
        ReadOnlyMemory<bool> path,
        CancellationToken cancellationToken = default)
    {
        if (path.IsEmpty)
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var pathKey = EncodePath(path);
            if (_nodesByPath.TryGetValue(pathKey, out var serializedNode))
            {
                // Find the hash for this node
                var hashEntry = _nodesByHash.FirstOrDefault(kvp => kvp.Value.SequenceEqual(serializedNode));
                if (!string.IsNullOrEmpty(hashEntry.Key))
                {
                    var hash = HexToByteArray(hashEntry.Key);
                    return Task.FromResult<SmtNodeBlob?>(
                        new SmtNodeBlob(new ReadOnlyMemory<byte>(hash), new ReadOnlyMemory<byte>(serializedNode), path));
                }
            }

            return Task.FromResult<SmtNodeBlob?>(null);
        }
    }

    /// <inheritdoc/>
    public Task<bool> NodeExistsAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default)
    {
        if (hash.IsEmpty)
            throw new ArgumentException("Hash cannot be empty.", nameof(hash));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var hashKey = ByteArrayToHex(hash.ToArray());
            return Task.FromResult(_nodesByHash.ContainsKey(hashKey));
        }
    }

    #endregion

    #region ISmtNodeWriter Implementation

    /// <inheritdoc/>
    public Task WriteBatchAsync(
        IReadOnlyList<SmtNodeBlob> nodes,
        CancellationToken cancellationToken = default)
    {
        if (nodes == null)
            throw new ArgumentNullException(nameof(nodes));

        if (nodes.Count == 0)
            return Task.CompletedTask;

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            foreach (var node in nodes)
            {
                if (node == null)
                    throw new ArgumentNullException(nameof(node));
                
                var hashKey = ByteArrayToHex(node.Hash.ToArray());
                var serializedNodeCopy = node.SerializedNode.ToArray();
                
                // Store by hash (idempotent - overwrites if exists)
                _nodesByHash[hashKey] = serializedNodeCopy;

                // Store by path if provided
                if (node.Path.HasValue && !node.Path.Value.IsEmpty)
                {
                    var pathKey = EncodePath(node.Path.Value);
                    _nodesByPath[pathKey] = serializedNodeCopy;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task WriteNodeAsync(
        SmtNodeBlob node,
        CancellationToken cancellationToken = default)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        return WriteBatchAsync(new[] { node }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // In-memory storage doesn't need flushing
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    #endregion

    #region ISmtSnapshotManager Implementation

    /// <inheritdoc/>
    public Task CreateSnapshotAsync(
        string snapshotName,
        ReadOnlyMemory<byte> rootHash,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (snapshotName == null)
            throw new ArgumentNullException(nameof(snapshotName));
        
        if (string.IsNullOrWhiteSpace(snapshotName))
            throw new ArgumentException("Snapshot name cannot be empty or whitespace.", nameof(snapshotName));

        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var snapshotInfo = new SmtSnapshotInfo(
                snapshotName,
                rootHash,
                DateTimeOffset.UtcNow,
                metadata);

            // Idempotent - overwrites if exists
            _snapshots[snapshotName] = snapshotInfo;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<SmtSnapshotInfo?> GetSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default)
    {
        if (snapshotName == null)
            throw new ArgumentNullException(nameof(snapshotName));
        
        if (string.IsNullOrWhiteSpace(snapshotName))
            throw new ArgumentException("Snapshot name cannot be empty or whitespace.", nameof(snapshotName));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_snapshots.TryGetValue(snapshotName, out var snapshot))
            {
                return Task.FromResult<SmtSnapshotInfo?>(snapshot);
            }

            return Task.FromResult<SmtSnapshotInfo?>(null);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListSnapshotsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var snapshotNames = _snapshots.Keys.ToList();
            return Task.FromResult<IReadOnlyList<string>>(snapshotNames);
        }
    }

    /// <inheritdoc/>
    public Task DeleteSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default)
    {
        if (snapshotName == null)
            throw new ArgumentNullException(nameof(snapshotName));
        
        if (string.IsNullOrWhiteSpace(snapshotName))
            throw new ArgumentException("Snapshot name cannot be empty or whitespace.", nameof(snapshotName));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            // Idempotent - no error if doesn't exist
            _snapshots.Remove(snapshotName);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ReadOnlyMemory<byte>> RestoreSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default)
    {
        if (snapshotName == null)
            throw new ArgumentNullException(nameof(snapshotName));
        
        if (string.IsNullOrWhiteSpace(snapshotName))
            throw new ArgumentException("Snapshot name cannot be empty or whitespace.", nameof(snapshotName));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_snapshots.TryGetValue(snapshotName, out var snapshot))
            {
                return Task.FromResult(snapshot.RootHash);
            }

            throw new InvalidOperationException($"Snapshot '{snapshotName}' does not exist.");
        }
    }

    #endregion

    #region ISmtMetadataStore Implementation

    /// <inheritdoc/>
    public Task StoreMetadataAsync(
        SmtMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            // Idempotent - overwrites if exists
            _metadata = metadata;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<SmtMetadata?> LoadMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult(_metadata);
        }
    }

    /// <inheritdoc/>
    public Task UpdateCurrentRootAsync(
        ReadOnlyMemory<byte> rootHash,
        CancellationToken cancellationToken = default)
    {
        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_metadata == null)
                throw new InvalidOperationException("Metadata must be stored before updating root hash.");

            // Idempotent - overwrites if same value
            _currentRoot = rootHash.ToArray();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ReadOnlyMemory<byte>?> GetCurrentRootAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_currentRoot == null)
                return Task.FromResult<ReadOnlyMemory<byte>?>(null);

            return Task.FromResult<ReadOnlyMemory<byte>?>(new ReadOnlyMemory<byte>(_currentRoot));
        }
    }

    /// <inheritdoc/>
    public Task<bool> MetadataExistsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult(_metadata != null);
        }
    }

    #endregion

    #region Helper Methods

    private static string EncodePath(ReadOnlyMemory<bool> path)
    {
        var bits = path.Span;
        var chars = new char[bits.Length];
        for (int i = 0; i < bits.Length; i++)
        {
            chars[i] = bits[i] ? '1' : '0';
        }
        return new string(chars);
    }

    private static ReadOnlyMemory<bool> DecodePath(string pathKey)
    {
        var bits = new bool[pathKey.Length];
        for (int i = 0; i < pathKey.Length; i++)
        {
            bits[i] = pathKey[i] == '1';
        }
        return new ReadOnlyMemory<bool>(bits);
    }

    private static string ByteArrayToHex(byte[] bytes)
    {
        var hex = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            hex.AppendFormat("{0:X2}", b);
        }
        return hex.ToString();
    }

    private static byte[] HexToByteArray(string hex)
    {
        int length = hex.Length;
        byte[] bytes = new byte[length / 2];
        for (int i = 0; i < length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }

    #endregion
}
