namespace MerkleTree.Smt;

/// <summary>
/// Represents the result of an SMT update, delete, or batch update operation.
/// </summary>
/// <remarks>
/// <para>
/// This result contains the new root hash after the operation and a list of nodes
/// that need to be persisted to storage. The SMT core does not perform persistence
/// directly; instead, it returns the nodes that have changed, and the caller is
/// responsible for persisting them using the appropriate storage adapter.
/// </para>
/// <para>
/// This design ensures that SMT operations remain storage-agnostic and deterministic.
/// </para>
/// </remarks>
public sealed class SmtUpdateResult
{
    /// <summary>
    /// Gets the new root hash after the update operation.
    /// </summary>
    /// <remarks>
    /// This hash represents the root of the tree after applying the update.
    /// It can be used to verify the tree state and generate proofs.
    /// </remarks>
    public ReadOnlyMemory<byte> NewRootHash { get; }

    /// <summary>
    /// Gets the list of nodes that need to be persisted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the nodes that were created or modified during the update operation.
    /// The caller should persist these nodes using a storage adapter that implements
    /// <see cref="Persistence.ISmtNodeWriter"/>.
    /// </para>
    /// <para>
    /// The list includes only the nodes that changed during the operation, following
    /// copy-on-write semantics. Unchanged nodes are not included.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Persistence.SmtNodeBlob> NodesToPersist { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtUpdateResult"/> class.
    /// </summary>
    /// <param name="newRootHash">The new root hash after the update.</param>
    /// <param name="nodesToPersist">The list of nodes that need to be persisted.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="newRootHash"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nodesToPersist"/> is null.</exception>
    public SmtUpdateResult(
        ReadOnlyMemory<byte> newRootHash,
        IReadOnlyList<Persistence.SmtNodeBlob> nodesToPersist)
    {
        if (newRootHash.IsEmpty)
            throw new ArgumentException("New root hash cannot be empty.", nameof(newRootHash));

        if (nodesToPersist == null)
            throw new ArgumentNullException(nameof(nodesToPersist));

        NewRootHash = newRootHash;
        NodesToPersist = nodesToPersist;
    }
}
