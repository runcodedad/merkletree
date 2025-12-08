using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerkleTree.Smt.Persistence;

/// <summary>
/// Defines an interface for reading SMT nodes from persistent storage.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction enables storage-agnostic SMT implementations that can work with
/// any storage backend: in-memory, on-disk, remote databases, cloud storage, etc.
/// </para>
/// <para><strong>Concurrency Guarantees:</strong></para>
/// <list type="bullet">
/// <item><description>Implementations must be thread-safe for concurrent reads</description></item>
/// <item><description>Read operations must not be affected by concurrent writes</description></item>
/// <item><description>Reads must always return the latest committed data or null</description></item>
/// </list>
/// <para><strong>Idempotency Guarantees:</strong></para>
/// <list type="bullet">
/// <item><description>Multiple reads of the same hash must return identical data</description></item>
/// <item><description>Read operations must not modify any state</description></item>
/// </list>
/// <para><strong>Error Handling:</strong></para>
/// <para>
/// Implementations should throw exceptions for adapter-level errors (I/O failures, network issues, etc.).
/// These exceptions will be surfaced to the caller for appropriate handling.
/// Missing nodes should return null rather than throwing exceptions.
/// </para>
/// </remarks>
public interface ISmtNodeReader
{
    /// <summary>
    /// Reads a node by its hash.
    /// </summary>
    /// <param name="hash">The hash of the node to retrieve.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to the node blob if found, or null if the node does not exist.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hash"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs (I/O failure, corrupted data, etc.).</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the primary method for retrieving nodes during tree traversal and proof generation.
    /// The hash uniquely identifies the node within the tree.
    /// </para>
    /// <para>
    /// If the node does not exist, this method returns null rather than throwing an exception.
    /// This allows SMT operations to distinguish between missing nodes (expected for sparse trees)
    /// and actual errors.
    /// </para>
    /// <para>
    /// Implementations should optimize for read performance as this method may be called
    /// frequently during tree operations.
    /// </para>
    /// </remarks>
    Task<SmtNodeBlob?> ReadNodeByHashAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a node by its path through the tree.
    /// </summary>
    /// <param name="path">The bit-path from root to the node.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to the node blob if found, or null if no node exists at that path.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method is optional for basic SMT operations but is useful for:
    /// - Path-based queries (finding the value at a specific key position)
    /// - Debugging and diagnostics
    /// - Snapshot operations that enumerate nodes by path
    /// </para>
    /// <para>
    /// Implementations that do not support path-based indexing may always return null
    /// or throw <see cref="NotSupportedException"/>.
    /// </para>
    /// <para>
    /// The path is a sequence of bits where false = left child, true = right child.
    /// Path length must not exceed the tree depth specified in the metadata.
    /// </para>
    /// </remarks>
    Task<SmtNodeBlob?> ReadNodeByPathAsync(
        ReadOnlyMemory<bool> path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a node with the specified hash exists in storage.
    /// </summary>
    /// <param name="hash">The hash of the node to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to true if the node exists, false otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hash"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is a lightweight existence check that should be faster than reading the full node.
    /// Implementations should optimize this for performance where possible (e.g., using bloom filters,
    /// index-only lookups, etc.).
    /// </para>
    /// <para>
    /// This method is useful for:
    /// - Validating node availability before proof generation
    /// - Checking if a subtree is already persisted
    /// - Implementing caching strategies
    /// </para>
    /// </remarks>
    Task<bool> NodeExistsAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default);
}
