namespace MerkleTree.Smt;

/// <summary>
/// Represents a key-value pair for batch SMT operations.
/// </summary>
/// <remarks>
/// <para>
/// This structure is used for batch update and delete operations. For delete operations,
/// the value should be set to an empty array or null to indicate deletion.
/// </para>
/// </remarks>
public sealed class SmtKeyValue
{
    /// <summary>
    /// Gets the key.
    /// </summary>
    public ReadOnlyMemory<byte> Key { get; }

    /// <summary>
    /// Gets the value. Null or empty indicates a delete operation.
    /// </summary>
    public ReadOnlyMemory<byte>? Value { get; }

    /// <summary>
    /// Gets a value indicating whether this is a delete operation.
    /// </summary>
    public bool IsDelete => !Value.HasValue || Value.Value.IsEmpty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtKeyValue"/> class.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value. Null or empty indicates a delete operation.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    public SmtKeyValue(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte>? value)
    {
        if (key.IsEmpty)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        Key = key;
        Value = value;
    }

    /// <summary>
    /// Creates a key-value pair for an update operation.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>A new <see cref="SmtKeyValue"/> instance.</returns>
    public static SmtKeyValue CreateUpdate(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value)
    {
        return new SmtKeyValue(key, value);
    }

    /// <summary>
    /// Creates a key-value pair for a delete operation.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <returns>A new <see cref="SmtKeyValue"/> instance.</returns>
    public static SmtKeyValue CreateDelete(ReadOnlyMemory<byte> key)
    {
        return new SmtKeyValue(key, null);
    }
}
