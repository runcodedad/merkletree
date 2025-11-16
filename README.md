# MerkleTree

A high-performance .NET library for creating and managing Merkle trees, providing cryptographic data structure support for data integrity verification and efficient data validation.

## Overview

Merkle trees (also known as hash trees) are a fundamental cryptographic data structure used in various applications including:

- **Blockchain technology**: Efficiently verify transactions and blocks
- **Distributed systems**: Verify data consistency across nodes
- **Data integrity**: Detect tampering or corruption in datasets
- **Version control systems**: Efficiently compare and synchronize data

This library provides a robust, well-tested implementation of Merkle trees for .NET applications.

## Features

- **Multi-targeting support**: Compatible with .NET 10.0 and .NET Standard 2.1
- **High performance**: Optimized for speed and memory efficiency
- **Root serialization**: Deterministic binary serialization with configurable hash sizes
- **Streaming support**: Build trees from large datasets without loading everything into memory
- **Async/await support**: Process data asynchronously with `IAsyncEnumerable<byte[]>`
- **Batch processing**: Control memory usage with configurable batch sizes
- **Type-safe**: Full C# type safety with nullable reference types enabled
- **XML documentation**: IntelliSense support for better developer experience
- **Well-tested**: Comprehensive test coverage (112+ tests)
- **Open source**: MIT licensed

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package MerkleTree
```

Or via Package Manager Console:

```powershell
Install-Package MerkleTree
```

## Quick Start

### In-Memory Merkle Tree

```csharp
using MerkleTree;
using System.Text;

// Create leaf data from your input
var leafData = new List<byte[]>
{
    Encoding.UTF8.GetBytes("data1"),
    Encoding.UTF8.GetBytes("data2"),
    Encoding.UTF8.GetBytes("data3")
};

// Build the Merkle tree
var tree = new MerkleTree(leafData);

// Get the root hash
byte[] rootHash = tree.GetRootHash();
Console.WriteLine($"Root Hash: {Convert.ToHexString(rootHash)}");

// Or get full metadata
var metadata = tree.GetMetadata();
Console.WriteLine($"Root Hash: {Convert.ToHexString(metadata.RootHash)}");
Console.WriteLine($"Height: {metadata.Height}, Leaves: {metadata.LeafCount}");

// Use a different hash algorithm (default is SHA256)
var treeSHA512 = new MerkleTree(leafData, new Sha512HashFunction());
```

### Streaming Merkle Tree Builder

For large datasets that don't fit in memory, use the streaming builder:

```csharp
using MerkleTree;
using System.Text;

// Create a builder
var builder = new MerkleTreeBuilder();

// Stream data from any source (file, database, network, etc.)
var leafData = GenerateLeaves(); // IEnumerable<byte[]>

// Build tree with batch processing
var metadata = builder.BuildInBatches(leafData, batchSize: 1000);

Console.WriteLine($"Root Hash: {Convert.ToHexString(metadata.RootHash)}");
Console.WriteLine($"Tree Height: {metadata.Height}");
Console.WriteLine($"Leaf Count: {metadata.LeafCount}");
```

### Async Streaming

```csharp
// For async data sources
var builder = new MerkleTreeBuilder();
var asyncLeaves = ReadLeavesFromStreamAsync(); // IAsyncEnumerable<byte[]>

var metadata = await builder.BuildAsync(asyncLeaves);
Console.WriteLine($"Root Hash: {Convert.ToHexString(metadata.RootHash)}");
```

## Tree Structure and Design

This implementation provides a **binary Merkle tree** with support for **non-power-of-two leaf counts** using a **domain-separated padding strategy**.

### Key Features

#### Binary Tree Structure
- **Leaves at Level 0**: Input data forms the bottom layer of the tree
- **Parent nodes**: Computed as `Hash(left_child || right_child)`
- **Left-to-right ordering**: Leaves are processed in the order provided
- **Fully deterministic**: Same input always produces the same tree structure

#### Non-Power-of-Two Support

When the number of leaves (or nodes at any level) is odd, the tree uses a **domain-separated padding strategy**:

1. The unpaired node becomes the **left child** of a parent node
2. A **padding node** is created as the **right child**
3. The padding hash is computed as: `Hash("MERKLE_PADDING" || unpaired_node_hash)`

This approach ensures:
- ✅ **Deterministic behavior**: Same input always produces same tree
- ✅ **Security**: Padding cannot be confused with legitimate data
- ✅ **Transparency**: Padding nodes are clearly distinguishable from data nodes

**Example with 3 leaves:**
```
Level 2:          Root
                 /    \
Level 1:    H(L1||L2)  H(L3||Pad)
           /    \      /    \
Level 0:  L1    L2    L3   Pad
```

Where `Pad = Hash("MERKLE_PADDING" || L3)`

#### Orientation Rules

- **Leaf processing**: Left-to-right in the order provided in the input array
- **Parent hash computation**: Always `Hash(left_child || right_child)`
- **Unpaired nodes**: Become the left child, with padding as the right child

## Streaming Support

The library includes a `MerkleTreeStream` class designed for processing large datasets that exceed available memory:

### Features

- **Memory-efficient**: Process datasets larger than available RAM
- **Streaming input**: Accept leaves from `IEnumerable<byte[]>` or `IAsyncEnumerable<byte[]>`
- **Batch processing**: Control memory usage with configurable batch sizes
- **Incremental processing**: Build levels incrementally without materializing the entire dataset
- **Deterministic results**: Produces identical root hashes to in-memory `MerkleTree` class

### Use Cases

1. **Large file processing**: Process multi-gigabyte files with fixed-size records
2. **Database streaming**: Build trees from database query results without loading all rows
3. **Network streaming**: Process data received over network connections
4. **Async I/O**: Efficiently handle async data sources with `BuildAsync`

### Example: Processing a Large File

```csharp
var builder = new MerkleTreeStream();

// Read fixed-size records from file
IEnumerable<byte[]> ReadRecordsFromFile(string path, int recordSize)
{
    using var stream = File.OpenRead(path);
    var buffer = new byte[recordSize];
    
    while (stream.Read(buffer, 0, recordSize) == recordSize)
    {
        yield return buffer.ToArray();
    }
}

var records = ReadRecordsFromFile("largefile.dat", recordSize: 32);
var metadata = builder.BuildInBatches(records, batchSize: 1000);

Console.WriteLine($"Processed {metadata.LeafCount:N0} records");
Console.WriteLine($"Root Hash: {Convert.ToHexString(metadata.RootHash)}");
Console.WriteLine($"Tree Height: {metadata.Height}");
```

### Metadata

The `MerkleTreeMetadata` class provides information about the constructed tree:

- **Root**: The root node of the tree (`MerkleTreeNode`)
- **RootHash**: The Merkle root hash (convenience property from `Root.Hash`)
- **Height**: The height of the tree (0 for single leaf, 1 for two leaves, etc.)
- **LeafCount**: The total number of leaves processed

Both `MerkleTree.GetMetadata()` and `MerkleTreeStream.Build()` return `MerkleTreeMetadata` for consistency.

For more details and examples, see [docs/STREAMING.md](docs/STREAMING.md).

## Root Serialization

The library provides deterministic binary serialization for Merkle tree roots, enabling storage and transmission of root hashes in a fixed-size binary format.

### Features

- **Fixed-size output**: Serialized roots match the hash function size (32 bytes for SHA-256/BLAKE3, 64 bytes for SHA-512)
- **Deterministic**: Same tree always produces the same serialized output
- **Validated**: Serialization checks for null hashes; deserialization validates input
- **Round-trip safe**: `Deserialize(Serialize(node))` preserves the root hash

### Basic Usage

```csharp
using MerkleTree;
using System.Text;

var leafData = new List<byte[]>
{
    Encoding.UTF8.GetBytes("data1"),
    Encoding.UTF8.GetBytes("data2"),
    Encoding.UTF8.GetBytes("data3")
};

var tree = new MerkleTree(leafData);

// Serialize root to fixed-size binary
byte[] serialized = tree.Root.Serialize();  // 32 bytes for SHA-256
Console.WriteLine($"Serialized: {Convert.ToHexString(serialized)}");

// Deserialize back to a node
var root = MerkleTreeNode.Deserialize(serialized);
Console.WriteLine($"Deserialized: {Convert.ToHexString(root.Hash)}");
```

### Using Metadata Convenience Methods

```csharp
// Serialize using metadata
var metadata = tree.GetMetadata();
byte[] rootBytes = metadata.SerializeRoot();

// Deserialize from binary
var deserializedRoot = MerkleTreeMetadata.DeserializeRoot(rootBytes);
```

### Multi-Hash Function Support

Different hash functions produce different output sizes:

```csharp
// SHA-256 produces 32 bytes
var treeSha256 = new MerkleTree(leafData, new Sha256HashFunction());
var sha256Root = treeSha256.Root.Serialize();  // 32 bytes

// SHA-512 produces 64 bytes
var treeSha512 = new MerkleTree(leafData, new Sha512HashFunction());
var sha512Root = treeSha512.Root.Serialize();  // 64 bytes

// BLAKE3 produces 32 bytes
var treeBlake3 = new MerkleTree(leafData, new Blake3HashFunction());
var blake3Root = treeBlake3.Root.Serialize();  // 32 bytes
```

### When to Use Serialization vs Direct Hash Access

- **Use `Serialize()`**: When you need a validated, serializable representation for storage or transmission
- **Use `Hash` property**: For direct access in internal operations, testing, or debugging
- **Use `GetRootHash()`**: Convenience method that returns the root hash directly

The serialization format is simply the raw hash bytes, providing minimal overhead while maintaining deterministic behavior.

## Requirements

- **.NET 10.0** or later, or
- **.NET Standard 2.1** compatible runtime

## Building from Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later

### Build Steps

```bash
# Clone the repository
git clone https://github.com/runcodedad/merkletree.git
cd merkletree

# Restore dependencies and build
dotnet restore
dotnet build

# Run tests (when available)
dotnet test

# Create NuGet package
dotnet pack -c Release
```

## Documentation

Detailed documentation will be available as the library develops. For now, refer to:

- XML documentation comments in the source code
- IntelliSense in your IDE
- [GitHub repository](https://github.com/runcodedad/merkletree)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Version History

### 1.0.0 (Current Development)
- Initial project setup
- NuGet package configuration
- Multi-targeting support (.NET 10.0 and .NET Standard 2.1)
- **Merkle tree implementation with non-power-of-two leaf support**
  - Binary tree structure with leaves at Level 0
  - Domain-separated padding strategy for odd leaf counts
  - Fully deterministic tree structure based on leaf ordering
  - Support for multiple hash algorithms (SHA-256, SHA-512, BLAKE3)
- **Streaming Merkle tree builder**
  - Process datasets larger than available RAM
  - Support for `IEnumerable<byte[]>` and `IAsyncEnumerable<byte[]>`
  - Batch processing with configurable batch sizes
  - Incremental level-by-level tree construction
  - Returns tree metadata (root hash, height, leaf count)
  - Produces identical results to in-memory construction
- **Root serialization** (NEW)
  - Fixed-size binary serialization for Merkle roots
  - Deterministic format matching hash function size
  - Validated serialization and deserialization
  - Round-trip safe with defensive copying
- Comprehensive test coverage (112+ tests)

## Support

For questions, issues, or feature requests, please [open an issue](https://github.com/runcodedad/merkletree/issues) on GitHub.

## Authors

- **runcodedad** - Initial work

## Acknowledgments

- Inspired by the original Merkle tree concept by Ralph Merkle
- Built with modern .NET best practices
