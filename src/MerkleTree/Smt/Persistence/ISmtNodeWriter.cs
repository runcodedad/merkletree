using MerkleTree.Exceptions;

namespace MerkleTree.Smt.Persistence;

/// <summary>
/// Defines an interface for writing SMT nodes to persistent storage.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction enables storage-agnostic SMT implementations that can work with
/// any storage backend: in-memory, on-disk, remote databases, cloud storage, etc.
/// </para>
/// <para><strong>Concurrency Guarantees:</strong></para>
/// <list type="bullet">
/// <item><description>Implementations must support concurrent writes from multiple threads</description></item>
/// <item><description>Batch writes must be atomic: all nodes written or none</description></item>
/// <item><description>Write operations must not corrupt concurrent reads</description></item>
/// </list>
/// <para><strong>Idempotency Guarantees:</strong></para>
/// <list type="bullet">
/// <item><description>Writing the same node multiple times must be idempotent (no error, no duplicate data)</description></item>
/// <item><description>Hash-based writes are naturally idempotent: same hash + same data = same result</description></item>
/// </list>
/// <para><strong>Error Handling:</strong></para>
/// <para>
/// Implementations should wrap storage-level errors (I/O failures, network issues, database errors, etc.)
/// in <see cref="StorageAdapterException"/> to provide consistent error reporting.
/// Duplicate writes should be handled gracefully without errors.
/// </para>
/// </remarks>
public interface ISmtNodeWriter
{
    /// <summary>
    /// Writes a batch of nodes to storage atomically.
    /// </summary>
    /// <param name="nodes">The collection of node blobs to write.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when all nodes have been written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nodes"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nodes"/> contains invalid data.</exception>
    /// <exception cref="StorageAdapterException">Thrown when an adapter-level error occurs (I/O failure, database error, insufficient space, etc.).</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the primary method for persisting SMT nodes. Batch writes are more efficient
    /// than individual writes and enable atomic commits of tree updates.
    /// </para>
    /// <para><strong>Atomicity:</strong></para>
    /// <para>
    /// Implementations should strive to make batch writes atomic:
    /// - All nodes in the batch are written successfully, or
    /// - None of the nodes are written (rollback on failure)
    /// </para>
    /// <para>
    /// For storage systems that don't support transactions, implementations should
    /// document the consistency guarantees they provide.
    /// </para>
    /// <para><strong>Idempotency:</strong></para>
    /// <para>
    /// Writing the same node (same hash) multiple times should be idempotent:
    /// - If a node with the same hash already exists, it should be overwritten or ignored
    /// - No error should be thrown for duplicate writes
    /// - The final state should be identical regardless of duplicate writes
    /// </para>
    /// <para><strong>Performance Considerations:</strong></para>
    /// <para>
    /// Implementations should optimize for bulk writes:
    /// - Use batch insert operations where available
    /// - Buffer writes to minimize I/O operations
    /// - Consider using write-ahead logs for durability
    /// </para>
    /// </remarks>
    Task WriteBatchAsync(
        IReadOnlyList<SmtNodeBlob> nodes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a single node to storage.
    /// </summary>
    /// <param name="node">The node blob to write.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the node has been written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method writes a single node to storage. For better performance when writing
    /// multiple nodes, use <see cref="WriteBatchAsync"/> instead.
    /// </para>
    /// <para>
    /// Implementations may internally buffer single writes and flush them in batches
    /// for better performance.
    /// </para>
    /// <para>
    /// Like batch writes, single writes must be idempotent: writing the same node
    /// multiple times should not cause errors.
    /// </para>
    /// </remarks>
    Task WriteNodeAsync(
        SmtNodeBlob node,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any buffered writes to durable storage.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when all buffered writes are flushed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs during flush.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method ensures that all previously written nodes are durably persisted
    /// to the underlying storage medium. It should be called:
    /// - After completing a tree update operation
    /// - Before taking a snapshot
    /// - When durability guarantees are required
    /// </para>
    /// <para>
    /// For storage systems that don't buffer writes (e.g., in-memory), this method
    /// may be a no-op that completes immediately.
    /// </para>
    /// <para>
    /// For storage systems with buffering (e.g., file systems, databases with WAL),
    /// this method should ensure data is synced to durable storage (fsync, commit, etc.).
    /// </para>
    /// </remarks>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
