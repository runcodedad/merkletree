using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerkleTree.Smt.Persistence;

/// <summary>
/// Defines an interface for storing and retrieving SMT metadata.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction enables storage-agnostic metadata management for SMTs.
/// Metadata includes critical information needed to reconstruct and verify the tree:
/// hash algorithm, tree depth, zero-hash table, and versioning information.
/// </para>
/// <para><strong>Metadata Persistence:</strong></para>
/// <list type="bullet">
/// <item><description>Metadata must be stored alongside the tree nodes for deterministic reconstruction</description></item>
/// <item><description>Metadata is immutable once the tree is created (except for root hash updates)</description></item>
/// <item><description>Multiple trees may share the same metadata if they have identical configuration</description></item>
/// </list>
/// <para><strong>Concurrency Guarantees:</strong></para>
/// <list type="bullet">
/// <item><description>Metadata operations must be thread-safe</description></item>
/// <item><description>Reading metadata must not be affected by concurrent writes</description></item>
/// <item><description>Writing metadata must be atomic (all or nothing)</description></item>
/// </list>
/// <para><strong>Idempotency Guarantees:</strong></para>
/// <list type="bullet">
/// <item><description>Storing identical metadata multiple times must be idempotent</description></item>
/// <item><description>Updating the current root to the same value must be idempotent</description></item>
/// </list>
/// <para><strong>Error Handling:</strong></para>
/// <para>
/// Implementations should throw exceptions for adapter-level errors (I/O failures, network issues, etc.).
/// These exceptions will be surfaced to the caller for appropriate handling.
/// </para>
/// </remarks>
public interface ISmtMetadataStore
{
    /// <summary>
    /// Stores SMT metadata to persistent storage.
    /// </summary>
    /// <param name="metadata">The metadata to store.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the metadata has been stored.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs (I/O failure, insufficient space, etc.).</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Stores the complete SMT metadata including:
    /// - Hash algorithm identifier
    /// - Tree depth
    /// - Zero-hash table
    /// - SMT core version
    /// - Serialization format version
    /// </para>
    /// <para>
    /// This operation should be performed when:
    /// - Creating a new tree
    /// - Migrating to a new storage backend
    /// - Updating tree configuration (if supported)
    /// </para>
    /// <para>
    /// Implementations should:
    /// - Store metadata in a well-known location or with a well-known key
    /// - Use the serialization format from <see cref="SmtMetadata.Serialize"/>
    /// - Ensure metadata is written atomically
    /// - Verify metadata integrity after writing
    /// </para>
    /// <para>
    /// If metadata already exists, implementations may either:
    /// - Overwrite the existing metadata (idempotent behavior)
    /// - Validate that new metadata is compatible with existing metadata
    /// - Throw an exception if metadata conflicts (fail-fast behavior)
    /// </para>
    /// </remarks>
    Task StoreMetadataAsync(
        SmtMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads SMT metadata from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to the stored metadata, or null if no metadata exists.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs or metadata is corrupted.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Loads the complete SMT metadata from storage. This should be called when:
    /// - Opening an existing tree
    /// - Verifying tree configuration
    /// - Preparing for proof verification
    /// </para>
    /// <para>
    /// If no metadata exists in storage (e.g., tree hasn't been created yet),
    /// returns null rather than throwing an exception.
    /// </para>
    /// <para>
    /// Implementations should:
    /// - Read metadata from the well-known location
    /// - Deserialize using <see cref="SmtMetadata.Deserialize"/>
    /// - Validate metadata integrity (checksums, versions, etc.)
    /// - Throw <see cref="InvalidOperationException"/> if metadata is corrupted
    /// </para>
    /// <para>
    /// After loading, callers should verify that the metadata version is compatible
    /// with the current implementation.
    /// </para>
    /// </remarks>
    Task<SmtMetadata?> LoadMetadataAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the current root hash in metadata.
    /// </summary>
    /// <param name="rootHash">The new root hash.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the root hash has been updated.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootHash"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootHash"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when metadata doesn't exist or an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Updates the current root hash to reflect the latest tree state. This is typically
    /// called after a batch of tree updates has been committed to storage.
    /// </para>
    /// <para>
    /// Implementations should:
    /// - Store the root hash separately from the full metadata for efficient updates
    /// - Ensure the update is atomic
    /// - Record a timestamp of when the root was updated
    /// </para>
    /// <para>
    /// This operation is idempotent: updating to the same root hash multiple times
    /// should not cause errors.
    /// </para>
    /// <para>
    /// If metadata doesn't exist, throws <see cref="InvalidOperationException"/>.
    /// Callers should ensure metadata is stored before updating the root hash.
    /// </para>
    /// </remarks>
    Task UpdateCurrentRootAsync(
        ReadOnlyMemory<byte> rootHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current root hash.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to the current root hash, or null if no root has been set.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Returns the most recently updated root hash. This represents the current state
    /// of the tree.
    /// </para>
    /// <para>
    /// Returns null if:
    /// - The tree has just been created and no updates have been made
    /// - Metadata exists but no root has been set yet
    /// </para>
    /// <para>
    /// This is a lightweight operation that should be faster than loading full metadata.
    /// Implementations should optimize for read performance.
    /// </para>
    /// </remarks>
    Task<ReadOnlyMemory<byte>?> GetCurrentRootAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if metadata exists in storage.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A task that resolves to true if metadata exists, false otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when an adapter-level error occurs.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is a lightweight existence check that should be faster than loading full metadata.
    /// Useful for:
    /// - Checking if a tree has been initialized
    /// - Validating storage state before operations
    /// - Implementing initialization logic
    /// </para>
    /// </remarks>
    Task<bool> MetadataExistsAsync(
        CancellationToken cancellationToken = default);
}
