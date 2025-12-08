namespace MerkleTree.Smt.Persistence;

/// <summary>
/// Defines an interface for managing logical snapshots of SMT state.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction enables storage-agnostic snapshot management for SMTs.
/// Snapshots capture the complete state of a tree at a specific point in time,
/// identified by its root hash.
/// </para>
/// <para><strong>Snapshot Semantics:</strong></para>
/// <list type="bullet">
/// <item><description>A snapshot is a named, immutable point-in-time view of the tree</description></item>
/// <item><description>Snapshots are identified by name (string) and root hash</description></item>
/// <item><description>Multiple snapshots may reference the same root hash</description></item>
/// <item><description>Snapshot data is never modified after creation</description></item>
/// </list>
/// <para><strong>Concurrency Guarantees:</strong></para>
/// <list type="bullet">
/// <item><description>Snapshot operations must be thread-safe</description></item>
/// <item><description>Creating a snapshot must not interfere with ongoing tree operations</description></item>
/// <item><description>Deleting a snapshot must not affect other snapshots or current tree state</description></item>
/// </list>
/// <para><strong>Idempotency Guarantees:</strong></para>
/// <list type="bullet">
/// <item><description>Creating a snapshot with an existing name should overwrite or error (implementation-specific)</description></item>
/// <item><description>Deleting a non-existent snapshot should be idempotent (no error)</description></item>
/// </list>
/// <para><strong>Error Handling:</strong></para>
/// <para>
/// Implementations should throw exceptions for adapter-level errors (I/O failures, network issues, etc.).
/// These exceptions will be surfaced to the caller for appropriate handling.
/// </para>
/// </remarks>
public interface ISmtSnapshotManager
{
    /// <summary>
    /// Creates a logical snapshot of the current tree state.
    /// </summary>
    /// <param name="snapshotName">The unique name for this snapshot.</param>
    /// <param name="rootHash">The root hash of the tree at the time of snapshot.</param>
    /// <param name="metadata">Optional metadata associated with the snapshot.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the snapshot has been created.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshotName"/> or <paramref name="rootHash"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="snapshotName"/> is empty or <paramref name="rootHash"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs or snapshot name already exists (if implementation doesn't allow overwrites).</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// A snapshot captures the root hash and optionally additional metadata about the tree state.
    /// The actual node data is not duplicated; snapshots reference the existing nodes via the root hash.
    /// </para>
    /// <para>
    /// Implementations should:
    /// - Store the snapshot name, root hash, and metadata
    /// - Record the timestamp of snapshot creation
    /// - Ensure referenced nodes are not garbage collected while snapshot exists
    /// </para>
    /// <para>
    /// Snapshot names must be unique within the storage system. If a snapshot with the same name
    /// already exists, implementations may either:
    /// - Overwrite the existing snapshot (idempotent behavior)
    /// - Throw an exception (fail-fast behavior)
    /// </para>
    /// </remarks>
    Task CreateSnapshotAsync(
        string snapshotName,
        ReadOnlyMemory<byte> rootHash,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves information about a snapshot by name.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot to retrieve.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to the snapshot information if found, or null if the snapshot does not exist.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshotName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="snapshotName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Returns the snapshot information including root hash, creation timestamp, and any associated metadata.
    /// If the snapshot does not exist, returns null rather than throwing an exception.
    /// </para>
    /// </remarks>
    Task<SmtSnapshotInfo?> GetSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available snapshots.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to a collection of all snapshot names.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Returns the names of all available snapshots in the storage system.
    /// The collection may be empty if no snapshots exist.
    /// </para>
    /// <para>
    /// For large numbers of snapshots, implementations may return a lazy enumerable
    /// that fetches snapshot names on demand.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<string>> ListSnapshotsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a snapshot by name.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot to delete.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the snapshot has been deleted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshotName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="snapshotName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Deletes the snapshot metadata. The actual node data is not deleted as it may be
    /// referenced by other snapshots or the current tree state.
    /// </para>
    /// <para>
    /// This operation is idempotent: deleting a non-existent snapshot should succeed without error.
    /// </para>
    /// <para>
    /// After deletion, the snapshot name becomes available for reuse.
    /// </para>
    /// </remarks>
    Task DeleteSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the tree to a previous snapshot state.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot to restore.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to the root hash of the restored snapshot.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshotName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="snapshotName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the snapshot does not exist or an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Restores the tree to the state captured in the snapshot by returning the snapshot's root hash.
    /// The caller is responsible for updating the current tree state with this root hash.
    /// </para>
    /// <para>
    /// This operation does not modify the snapshot or any node data. It simply retrieves
    /// the root hash that can be used to access the snapshot's tree state.
    /// </para>
    /// <para>
    /// If the snapshot does not exist, throws <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    Task<ReadOnlyMemory<byte>> RestoreSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains information about a snapshot.
/// </summary>
/// <remarks>
/// This structure is returned by <see cref="ISmtSnapshotManager.GetSnapshotAsync"/>
/// and contains all the metadata associated with a snapshot.
/// </remarks>
public sealed class SmtSnapshotInfo
{
    /// <summary>
    /// Gets the unique name of the snapshot.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the root hash of the tree at the time of snapshot.
    /// </summary>
    public ReadOnlyMemory<byte> RootHash { get; }

    /// <summary>
    /// Gets the timestamp when the snapshot was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets optional metadata associated with the snapshot.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtSnapshotInfo"/> class.
    /// </summary>
    /// <param name="name">The snapshot name.</param>
    /// <param name="rootHash">The root hash.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or <paramref name="rootHash"/> is empty.</exception>
    public SmtSnapshotInfo(
        string name,
        ReadOnlyMemory<byte> rootHash,
        DateTimeOffset createdAt,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Snapshot name cannot be empty or whitespace.", nameof(name));

        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        Name = name;
        RootHash = rootHash;
        CreatedAt = createdAt;
        Metadata = metadata;
    }
}
