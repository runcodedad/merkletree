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
- **Type-safe**: Full C# type safety with nullable reference types enabled
- **XML documentation**: IntelliSense support for better developer experience
- **Well-tested**: Comprehensive test coverage
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

// Use a different hash algorithm (default is SHA256)
var treeSHA512 = new MerkleTree(leafData, HashAlgorithmName.SHA512);
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
  - Support for multiple hash algorithms (SHA256, SHA384, SHA512, MD5, SHA1)
  - Comprehensive test coverage (23+ tests)

## Support

For questions, issues, or feature requests, please [open an issue](https://github.com/runcodedad/merkletree/issues) on GitHub.

## Authors

- **runcodedad** - Initial work

## Acknowledgments

- Inspired by the original Merkle tree concept by Ralph Merkle
- Built with modern .NET best practices
