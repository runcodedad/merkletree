# MerkleTree Copilot Instructions

This file provides coding guidelines and context for GitHub Copilot when working on the MerkleTree library.

## Project Overview

MerkleTree is a high-performance .NET library for creating and managing Merkle trees with cryptographic data structure support for data integrity verification. The library targets both .NET 10.0 and .NET Standard 2.1 for broad compatibility.

## Code Style and Standards

### General Guidelines

- Use C# 10+ features and modern .NET idioms
- Enable nullable reference types throughout the codebase
- Use implicit usings where appropriate
- Prefer explicit types for clarity in public APIs
- Use `var` for local variables when the type is obvious
- Follow PascalCase for public members and camelCase for private fields

### Documentation

- Always include XML documentation comments for:
  - All public types, methods, properties, and parameters
  - Complex internal logic that benefits from explanation
- Use `<summary>`, `<param>`, `<returns>`, `<exception>`, and `<remarks>` tags appropriately
- Document exceptions that may be thrown with `<exception>` tags
- Include code examples in `<example>` tags for complex APIs

### Naming Conventions

- Use PascalCase for classes, interfaces, methods, properties, and public fields
- Use camelCase for local variables and private fields
- Prefix interface names with 'I' (e.g., `IHashFunction`)
- Use descriptive names that clearly convey intent
- Avoid abbreviations unless they are well-known (e.g., SHA, UTF)

### Error Handling

- Validate all public method inputs and throw appropriate exceptions:
  - `ArgumentNullException` for null arguments
  - `ArgumentException` for invalid arguments
  - `ArgumentOutOfRangeException` for out-of-range values
  - `InvalidOperationException` for invalid state
- Include descriptive error messages that help users understand the problem
- Use `nameof()` for parameter names in exception messages

## Project Structure

### Namespaces

- `MerkleTree.Core` - Core tree implementation (MerkleTree, MerkleTreeNode, MerkleTreeStream, MerkleTreeMetadata)
- `MerkleTree.Hashing` - Hash function abstractions and implementations (SHA-256, SHA-512, BLAKE3)
- `MerkleTree.Proofs` - Merkle proof generation, verification, and serialization
- `MerkleTree.Cache` - Caching support for streaming tree operations

### Key Design Patterns

- **Binary tree structure**: Leaves at Level 0, parent nodes computed as Hash(left || right)
- **Domain-separated padding**: For odd leaf counts, unpaired nodes become left children with padding as Hash("MERKLE_PADDING" || node_hash) on the right
- **Streaming support**: Use `IEnumerable<byte[]>` and `IAsyncEnumerable<byte[]>` for large datasets
- **Deterministic behavior**: Same input always produces same tree structure and root hash

## Testing Guidelines

### Test Framework

- Use xUnit for all tests
- Place tests in `tests/MerkleTree.Tests/` mirroring the source structure
- Use xUnit's `[Fact]` attribute for tests without parameters
- Use `[Theory]` with `[InlineData]` for parameterized tests

### Test Structure

- Follow Arrange-Act-Assert pattern
- Use descriptive test method names: `MethodName_Scenario_ExpectedBehavior`
- Include XML documentation comments explaining what each test verifies
- Create helper methods for common test data setup (e.g., `CreateLeafData()`)
- Test edge cases: null inputs, empty collections, single items, odd/even counts

### Test Coverage

- Write tests for all public APIs
- Cover both success and failure paths
- Test boundary conditions and edge cases
- Verify exception throwing with `Assert.Throws<TException>()`
- Test async methods with proper async/await patterns

## Hash Functions

### Supported Algorithms

- **SHA-256** (default): 32-byte output, widely supported
- **SHA-512**: 64-byte output, higher security
- **BLAKE3**: 32-byte output, high performance (only on .NET 10.0)

### Usage

- Default to SHA-256 unless specified otherwise
- Accept `IHashFunction` in constructors for algorithm flexibility
- Hash function choice affects serialization output size
- BLAKE3 is only available in .NET 10.0 target due to package dependencies

## Performance Considerations

### Memory Efficiency

- Use streaming APIs (`MerkleTreeStream`) for large datasets that exceed available RAM
- Process data incrementally with `IEnumerable<byte[]>` or `IAsyncEnumerable<byte[]>`
- Use caching strategically for repeated proof generation on large trees
- Configure batch sizes appropriately for memory constraints

### Streaming Best Practices

- Use `MerkleTree` class for in-memory datasets that fit in RAM
- Use `MerkleTreeStream` class for datasets larger than available memory
- Enable caching when generating multiple proofs from the same large tree
- Cache top N levels (default 5) to balance memory usage and performance

## Common Patterns

### Creating a Merkle Tree

```csharp
// In-memory tree
var leafData = new List<byte[]> { data1, data2, data3 };
var tree = new MerkleTree(leafData);
byte[] rootHash = tree.GetRootHash();

// Streaming tree
var stream = new MerkleTreeStream();
var metadata = stream.Build(largeDataset);
```

### Generating and Verifying Proofs

```csharp
// Generate proof
var proof = tree.GenerateProof(leafIndex);

// Verify proof
var hashFunction = new Sha256HashFunction();
bool isValid = proof.Verify(rootHash, hashFunction);
```

### Using Cache for Streaming

```csharp
// Build with cache
var cacheConfig = new CacheConfiguration("merkle.cache", topLevelsToCache: 5);
var metadata = await stream.BuildAsync(leafData, cacheConfig);

// Load and use cache
var cache = CacheFileManager.LoadCache("merkle.cache");
var proof = await stream.GenerateProofAsync(leafData, leafIndex, leafCount, cache);
```

## Security Guidelines

- Never expose raw cryptographic operations without proper abstraction
- Validate all inputs before cryptographic operations
- Use defensive copying for byte arrays to prevent external modification
- Clear sensitive data from memory when no longer needed
- Follow secure coding practices for cryptographic implementations

## Build and Test Commands

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release

# Run all tests
dotnet test --configuration Release --verbosity normal

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Create NuGet package
dotnet pack -c Release
```

## Prohibited Patterns

- Do not use deprecated .NET APIs
- Do not expose mutable collections in public APIs - return read-only collections or copies
- Do not modify input byte arrays - always work with copies
- Do not use blocking calls in async methods - use async/await throughout
- Do not catch and swallow exceptions without proper logging or re-throwing
- Do not use `Task.Result` or `Task.Wait()` - use async/await instead

## Multi-Targeting Considerations

- Code targets both .NET 10.0 and .NET Standard 2.1
- Use conditional compilation (`#if NET10_0`) for .NET 10.0-specific features
- BLAKE3 hash function is only available in .NET 10.0 target
- Ensure all features work on both target frameworks unless explicitly documented
- Test both target frameworks before releasing changes

## Additional Context

- The library emphasizes deterministic behavior for reproducibility
- Proof serialization format is platform-independent and version-safe
- Tree structure uses left-to-right leaf ordering
- Padding strategy is domain-separated to prevent collision attacks
- All operations are thread-safe for read-only access
- Write operations should be externally synchronized
