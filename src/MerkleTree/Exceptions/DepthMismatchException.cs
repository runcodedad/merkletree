namespace MerkleTree.Exceptions;

/// <summary>
/// Exception thrown when tree depth constraints are violated.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when:
/// - A proof's tree height doesn't match the expected tree configuration
/// - SMT operations reference a depth that exceeds the configured maximum
/// - Bit paths are too long or too short for the configured tree depth
/// - Metadata specifies an incompatible tree depth
/// </para>
/// <para>
/// Depth mismatches often indicate configuration errors or attempts to use
/// data from one tree configuration with a different tree configuration.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     // Attempting to use a proof from a depth-16 tree with a depth-32 tree
///     var result = tree.VerifyProof(proof);
/// }
/// catch (DepthMismatchException ex)
/// {
///     Console.WriteLine($"Depth mismatch: {ex.Message}");
///     Console.WriteLine($"Expected depth: {ex.ExpectedDepth}");
///     Console.WriteLine($"Actual depth: {ex.ActualDepth}");
/// }
/// </code>
/// </example>
public class DepthMismatchException : Exception
{
    /// <summary>
    /// Gets the error code identifying the specific depth mismatch scenario.
    /// </summary>
    /// <remarks>
    /// Error codes allow programmatic handling of specific mismatch conditions.
    /// Examples: "PROOF_HEIGHT_MISMATCH", "PATH_LENGTH_INVALID", "DEPTH_EXCEEDED"
    /// </remarks>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the expected tree depth or height.
    /// </summary>
    public int? ExpectedDepth { get; }

    /// <summary>
    /// Gets the actual tree depth or height that was encountered.
    /// </summary>
    public int? ActualDepth { get; }

    /// <summary>
    /// Gets additional details about the depth mismatch.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DepthMismatchException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific mismatch scenario.</param>
    /// <param name="expectedDepth">The expected tree depth.</param>
    /// <param name="actualDepth">The actual tree depth encountered.</param>
    /// <param name="details">Optional additional details about the mismatch.</param>
    public DepthMismatchException(
        string message,
        string errorCode,
        int? expectedDepth = null,
        int? actualDepth = null,
        string? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        ExpectedDepth = expectedDepth;
        ActualDepth = actualDepth;
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DepthMismatchException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific mismatch scenario.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    /// <param name="expectedDepth">The expected tree depth.</param>
    /// <param name="actualDepth">The actual tree depth encountered.</param>
    /// <param name="details">Optional additional details about the mismatch.</param>
    public DepthMismatchException(
        string message,
        string errorCode,
        Exception innerException,
        int? expectedDepth = null,
        int? actualDepth = null,
        string? details = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ExpectedDepth = expectedDepth;
        ActualDepth = actualDepth;
        Details = details;
    }
}
