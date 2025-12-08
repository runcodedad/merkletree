namespace MerkleTree.Smt;

/// <summary>
/// Represents the result of an SMT get operation.
/// </summary>
/// <remarks>
/// <para>
/// This result indicates whether a value was found for the given key and provides
/// the value if present. For sparse trees, most keys will not be present.
/// </para>
/// </remarks>
public sealed class SmtGetResult
{
    /// <summary>
    /// Gets a value indicating whether the key was found in the tree.
    /// </summary>
    public bool Found { get; }

    /// <summary>
    /// Gets the value associated with the key, if found.
    /// </summary>
    /// <remarks>
    /// This will be null if <see cref="Found"/> is false.
    /// </remarks>
    public ReadOnlyMemory<byte>? Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtGetResult"/> class for a found key.
    /// </summary>
    /// <param name="value">The value associated with the key.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty.</exception>
    private SmtGetResult(ReadOnlyMemory<byte> value)
    {
        if (value.IsEmpty)
            throw new ArgumentException("Value cannot be empty.", nameof(value));

        Found = true;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtGetResult"/> class for a not-found key.
    /// </summary>
    private SmtGetResult()
    {
        Found = false;
        Value = null;
    }

    /// <summary>
    /// Creates a result indicating that the key was found with the given value.
    /// </summary>
    /// <param name="value">The value associated with the key.</param>
    /// <returns>A new <see cref="SmtGetResult"/> instance.</returns>
    public static SmtGetResult CreateFound(ReadOnlyMemory<byte> value)
    {
        return new SmtGetResult(value);
    }

    /// <summary>
    /// Creates a result indicating that the key was not found.
    /// </summary>
    /// <returns>A new <see cref="SmtGetResult"/> instance.</returns>
    public static SmtGetResult CreateNotFound()
    {
        return new SmtGetResult();
    }
}
