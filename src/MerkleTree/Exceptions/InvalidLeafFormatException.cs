namespace MerkleTree.Exceptions;

/// <summary>
/// Exception thrown when leaf data has an invalid or unexpected format.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when:
/// - Leaf data is null when a value is required
/// - Leaf data has an invalid size or structure
/// - Leaf data doesn't conform to expected encoding or schema
/// - Leaf index is out of valid range
/// </para>
/// <para>
/// This exception helps distinguish data validation errors at the leaf level
/// from structural problems with the tree or proof.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     tree.AddLeaf(leafData);
/// }
/// catch (InvalidLeafFormatException ex)
/// {
///     Console.WriteLine($"Invalid leaf format: {ex.Message}");
///     Console.WriteLine($"Error code: {ex.ErrorCode}");
///     if (ex.LeafIndex.HasValue)
///         Console.WriteLine($"At leaf index: {ex.LeafIndex}");
/// }
/// </code>
/// </example>
public class InvalidLeafFormatException : Exception
{
    /// <summary>
    /// Gets the error code identifying the specific format violation.
    /// </summary>
    /// <remarks>
    /// Error codes allow programmatic handling of specific format errors.
    /// Examples: "NULL_LEAF_DATA", "INVALID_LEAF_SIZE", "MALFORMED_ENCODING", "INVALID_INDEX"
    /// </remarks>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the index of the invalid leaf, if applicable.
    /// </summary>
    public long? LeafIndex { get; }

    /// <summary>
    /// Gets the expected leaf format or size, if applicable.
    /// </summary>
    public string? ExpectedFormat { get; }

    /// <summary>
    /// Gets additional details about the format violation.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidLeafFormatException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific format violation.</param>
    /// <param name="leafIndex">The index of the invalid leaf.</param>
    /// <param name="expectedFormat">The expected format or size.</param>
    /// <param name="details">Optional additional details about the violation.</param>
    public InvalidLeafFormatException(
        string message,
        string errorCode,
        long? leafIndex = null,
        string? expectedFormat = null,
        string? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        LeafIndex = leafIndex;
        ExpectedFormat = expectedFormat;
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidLeafFormatException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific format violation.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    /// <param name="leafIndex">The index of the invalid leaf.</param>
    /// <param name="expectedFormat">The expected format or size.</param>
    /// <param name="details">Optional additional details about the violation.</param>
    public InvalidLeafFormatException(
        string message,
        string errorCode,
        Exception innerException,
        long? leafIndex = null,
        string? expectedFormat = null,
        string? details = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        LeafIndex = leafIndex;
        ExpectedFormat = expectedFormat;
        Details = details;
    }
}
