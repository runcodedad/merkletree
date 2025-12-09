namespace MerkleTree.Exceptions;

/// <summary>
/// Exception thrown when a Merkle proof verification fails.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when a well-formed proof fails verification because:
/// - The computed root hash doesn't match the expected root hash
/// - The proof path is invalid for the given tree structure
/// - Required verification data is missing
/// </para>
/// <para>
/// This exception is distinct from <see cref="MalformedProofException"/> which indicates
/// structural problems with the proof itself. ProofVerificationFailedException means
/// the proof structure is valid but the cryptographic verification failed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     var isValid = proof.Verify(rootHash, hashFunction);
///     if (!isValid)
///     {
///         throw new ProofVerificationFailedException(
///             "Proof verification failed: computed root does not match expected root",
///             "ROOT_HASH_MISMATCH",
///             computedRoot: computedHash,
///             expectedRoot: rootHash);
///     }
/// }
/// catch (ProofVerificationFailedException ex)
/// {
///     Console.WriteLine($"Verification failed: {ex.Message}");
///     Console.WriteLine($"Expected: {BitConverter.ToString(ex.ExpectedRoot)}");
///     Console.WriteLine($"Computed: {BitConverter.ToString(ex.ComputedRoot)}");
/// }
/// </code>
/// </example>
public class ProofVerificationFailedException : Exception
{
    /// <summary>
    /// Gets the error code identifying the specific verification failure reason.
    /// </summary>
    /// <remarks>
    /// Error codes allow programmatic handling of specific failure conditions.
    /// Examples: "ROOT_HASH_MISMATCH", "INVALID_PATH", "MISSING_SIBLING"
    /// </remarks>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the expected root hash that verification failed to produce.
    /// </summary>
    public byte[]? ExpectedRoot { get; }

    /// <summary>
    /// Gets the computed root hash that was produced during verification.
    /// </summary>
    public byte[]? ComputedRoot { get; }

    /// <summary>
    /// Gets additional details about the verification failure.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProofVerificationFailedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific failure reason.</param>
    /// <param name="expectedRoot">The expected root hash.</param>
    /// <param name="computedRoot">The computed root hash.</param>
    /// <param name="details">Optional additional details about the failure.</param>
    public ProofVerificationFailedException(
        string message,
        string errorCode,
        byte[]? expectedRoot = null,
        byte[]? computedRoot = null,
        string? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        ExpectedRoot = expectedRoot;
        ComputedRoot = computedRoot;
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProofVerificationFailedException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific failure reason.</param>
    /// <param name="innerException">The exception that caused this verification failure.</param>
    /// <param name="expectedRoot">The expected root hash.</param>
    /// <param name="computedRoot">The computed root hash.</param>
    /// <param name="details">Optional additional details about the failure.</param>
    public ProofVerificationFailedException(
        string message,
        string errorCode,
        Exception innerException,
        byte[]? expectedRoot = null,
        byte[]? computedRoot = null,
        string? details = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ExpectedRoot = expectedRoot;
        ComputedRoot = computedRoot;
        Details = details;
    }
}
