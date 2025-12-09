namespace MerkleTree.Exceptions;

/// <summary>
/// Exception thrown when a Merkle proof has an invalid or malformed structure.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when deserializing or validating a proof that:
/// - Has inconsistent hash sizes
/// - Has mismatched sibling counts vs. tree height
/// - Contains null or invalid sibling hashes
/// - Has corrupted binary data
/// </para>
/// <para>
/// This exception type allows callers to distinguish structural proof errors
/// from verification failures (where the proof is well-formed but doesn't match the root).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     var proof = MerkleProof.Deserialize(data);
/// }
/// catch (MalformedProofException ex)
/// {
///     Console.WriteLine($"Invalid proof structure: {ex.Message}");
///     Console.WriteLine($"Error code: {ex.ErrorCode}");
/// }
/// </code>
/// </example>
public class MalformedProofException : Exception
{
    /// <summary>
    /// Gets the error code identifying the specific type of malformation.
    /// </summary>
    /// <remarks>
    /// Error codes allow programmatic handling of specific error conditions.
    /// Examples: "INCONSISTENT_HASH_SIZE", "SIBLING_COUNT_MISMATCH", "NULL_SIBLING_HASH"
    /// </remarks>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets additional details about the error for debugging purposes.
    /// </summary>
    /// <remarks>
    /// May contain information such as:
    /// - Expected vs. actual values
    /// - Index of the malformed element
    /// - Position in the serialized data where the error occurred
    /// </remarks>
    public string? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MalformedProofException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific malformation type.</param>
    /// <param name="details">Optional additional details about the error.</param>
    public MalformedProofException(string message, string errorCode, string? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MalformedProofException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific malformation type.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    /// <param name="details">Optional additional details about the error.</param>
    public MalformedProofException(string message, string errorCode, Exception innerException, string? details = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Details = details;
    }
}
