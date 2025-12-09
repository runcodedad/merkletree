namespace MerkleTree.Exceptions;

/// <summary>
/// Exception thrown when tree metadata is invalid or incompatible.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when:
/// - Metadata has an unsupported or incompatible version
/// - Hash algorithm identifier is unknown or not supported
/// - Tree depth is outside the valid range
/// - Zero-hash table is missing or malformed
/// - Serialization format version is incompatible
/// </para>
/// <para>
/// Metadata validation errors often indicate version incompatibilities,
/// corrupted data, or attempts to mix data from different tree configurations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     var metadata = SmtMetadata.Deserialize(data);
/// }
/// catch (InvalidMetadataException ex)
/// {
///     Console.WriteLine($"Invalid metadata: {ex.Message}");
///     Console.WriteLine($"Error code: {ex.ErrorCode}");
///     if (ex.ExpectedVersion.HasValue &amp;&amp; ex.ActualVersion.HasValue)
///     {
///         Console.WriteLine($"Expected version: {ex.ExpectedVersion}");
///         Console.WriteLine($"Actual version: {ex.ActualVersion}");
///     }
/// }
/// </code>
/// </example>
public class InvalidMetadataException : Exception
{
    /// <summary>
    /// Gets the error code identifying the specific metadata validation failure.
    /// </summary>
    /// <remarks>
    /// Error codes allow programmatic handling of specific validation failures.
    /// Examples: "UNSUPPORTED_VERSION", "UNKNOWN_HASH_ALGORITHM", "INVALID_DEPTH", "MALFORMED_ZERO_HASH_TABLE"
    /// </remarks>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the expected version number, if applicable.
    /// </summary>
    public int? ExpectedVersion { get; }

    /// <summary>
    /// Gets the actual version number that was encountered.
    /// </summary>
    public int? ActualVersion { get; }

    /// <summary>
    /// Gets the name of the metadata field that is invalid, if applicable.
    /// </summary>
    public string? FieldName { get; }

    /// <summary>
    /// Gets additional details about the validation failure.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidMetadataException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific validation failure.</param>
    /// <param name="fieldName">The name of the invalid field.</param>
    /// <param name="expectedVersion">The expected version number.</param>
    /// <param name="actualVersion">The actual version number encountered.</param>
    /// <param name="details">Optional additional details about the failure.</param>
    public InvalidMetadataException(
        string message,
        string errorCode,
        string? fieldName = null,
        int? expectedVersion = null,
        int? actualVersion = null,
        string? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        FieldName = fieldName;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidMetadataException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific validation failure.</param>
    /// <param name="innerException">The exception that caused this validation failure.</param>
    /// <param name="fieldName">The name of the invalid field.</param>
    /// <param name="expectedVersion">The expected version number.</param>
    /// <param name="actualVersion">The actual version number encountered.</param>
    /// <param name="details">Optional additional details about the failure.</param>
    public InvalidMetadataException(
        string message,
        string errorCode,
        Exception innerException,
        string? fieldName = null,
        int? expectedVersion = null,
        int? actualVersion = null,
        string? details = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        FieldName = fieldName;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
        Details = details;
    }
}
