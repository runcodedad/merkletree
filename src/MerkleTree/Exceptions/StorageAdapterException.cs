namespace MerkleTree.Exceptions;

/// <summary>
/// Exception thrown when a storage adapter encounters an error.
/// </summary>
/// <remarks>
/// <para>
/// This exception wraps errors from the persistence layer, such as:
/// - I/O failures (disk full, permission denied, etc.)
/// - Network errors (connection timeout, host unreachable, etc.)
/// - Database errors (connection failures, constraint violations, etc.)
/// - Corrupted data detected during read operations
/// </para>
/// <para>
/// Storage adapters should wrap their implementation-specific exceptions
/// in this type to provide a consistent error interface to callers.
/// The original exception should be preserved as the InnerException.
/// </para>
/// <para>
/// This exception allows the core Merkle tree logic to remain storage-agnostic
/// while still surfacing meaningful error information to clients.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In a storage adapter implementation:
/// public async Task&lt;SmtNodeBlob?&gt; ReadNodeByHashAsync(
///     ReadOnlyMemory&lt;byte&gt; hash,
///     CancellationToken cancellationToken = default)
/// {
///     try
///     {
///         // Database or file I/O operation
///         return await _database.ReadNodeAsync(hash);
///     }
///     catch (IOException ioEx)
///     {
///         throw new StorageAdapterException(
///             "Failed to read node from disk",
///             "IO_ERROR",
///             ioEx,
///             operation: "ReadNodeByHash");
///     }
///     catch (SqlException sqlEx)
///     {
///         throw new StorageAdapterException(
///             "Database query failed",
///             "DATABASE_ERROR",
///             sqlEx,
///             operation: "ReadNodeByHash");
///     }
/// }
/// 
/// // In client code:
/// try
/// {
///     var node = await storage.ReadNodeByHashAsync(hash);
/// }
/// catch (StorageAdapterException ex)
/// {
///     Console.WriteLine($"Storage error: {ex.Message}");
///     Console.WriteLine($"Operation: {ex.Operation}");
///     Console.WriteLine($"Underlying error: {ex.InnerException?.Message}");
/// }
/// </code>
/// </example>
public class StorageAdapterException : Exception
{
    /// <summary>
    /// Gets the error code identifying the specific storage error type.
    /// </summary>
    /// <remarks>
    /// Error codes allow programmatic handling of specific storage errors.
    /// Examples: "IO_ERROR", "DATABASE_ERROR", "NETWORK_ERROR", "CORRUPTED_DATA", "PERMISSION_DENIED"
    /// </remarks>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the name of the storage operation that failed.
    /// </summary>
    /// <remarks>
    /// Examples: "ReadNodeByHash", "WriteBatch", "CreateSnapshot", "LoadMetadata"
    /// </remarks>
    public string? Operation { get; }

    /// <summary>
    /// Gets additional details about the storage error.
    /// </summary>
    /// <remarks>
    /// May contain information such as:
    /// - File path or database table name
    /// - Hash or key that was being accessed
    /// - Retry attempt count
    /// </remarks>
    public string? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageAdapterException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific storage error type.</param>
    /// <param name="operation">The name of the operation that failed.</param>
    /// <param name="details">Optional additional details about the error.</param>
    public StorageAdapterException(
        string message,
        string errorCode,
        string? operation = null,
        string? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Operation = operation;
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageAdapterException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying the specific storage error type.</param>
    /// <param name="innerException">The underlying exception from the storage implementation.</param>
    /// <param name="operation">The name of the operation that failed.</param>
    /// <param name="details">Optional additional details about the error.</param>
    public StorageAdapterException(
        string message,
        string errorCode,
        Exception innerException,
        string? operation = null,
        string? details = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Operation = operation;
        Details = details;
    }
}
