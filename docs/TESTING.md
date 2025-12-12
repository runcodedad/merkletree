# Testing Guide

This document describes the testing infrastructure, patterns, and best practices for the MerkleTree library.

## Table of Contents

- [Overview](#overview)
- [Test Structure](#test-structure)
- [Running Tests](#running-tests)
- [Test Categories](#test-categories)
- [Writing Tests](#writing-tests)
- [Test Utilities](#test-utilities)
- [CI Integration](#ci-integration)
- [Coverage](#coverage)

## Overview

The MerkleTree library has comprehensive test coverage (90%+) across all components:

- **Standard Merkle Trees**: Core functionality, streaming, caching, and proofs
- **Sparse Merkle Trees (SMT)**: Operations, persistence, determinism, and proofs
- **Reference Implementations**: In-memory adapters for testing
- **Property-Based Tests**: Randomized testing of invariants
- **Test Vectors**: Known values for regression testing

### Test Statistics

- **Total Tests**: 596+
- **Test Files**: 28
- **Test Framework**: xUnit 2.9+
- **Code Coverage**: 90%+ for business logic

## Test Structure

```
tests/
├── MerkleTree.Tests/
│   ├── Cache/                    # Cache and serialization tests
│   ├── Core/                     # Standard Merkle tree tests
│   │   ├── DeterminismTests.cs   # Cross-platform determinism
│   │   ├── MerkleTreeTests.cs    # Core functionality
│   │   └── MerkleTreeStreamTests.cs
│   ├── EdgeCases/                # Edge case and boundary tests
│   ├── ErrorHandling/            # Error scenarios
│   ├── Exceptions/               # Exception types and handling
│   ├── Hashing/                  # Hash function tests
│   ├── Integration/              # End-to-end workflows
│   ├── Performance/              # Performance and scalability
│   ├── Proofs/                   # Proof generation and verification
│   │   ├── MerkleProofTests.cs   # Standard tree proofs
│   │   └── SmtProofTests.cs      # SMT proofs
│   └── Smt/                      # Sparse Merkle Tree tests
│       ├── SmtDeterminismTests.cs    # SMT determinism
│       ├── SmtPropertyTests.cs       # Property-based tests
│       ├── SmtTestVectors.cs         # Regression test vectors
│       ├── SmtOperationsTests.cs     # CRUD operations
│       ├── SparseMerkleTreeTests.cs  # Core model
│       ├── SmtMetadataTests.cs       # Metadata serialization
│       ├── SmtNodeTests.cs           # Node types
│       ├── ZeroHashTableTests.cs     # Zero-hash computation
│       └── Persistence/
│           └── InMemorySmtStorageTests.cs  # Reference adapter
```

## Running Tests

### All Tests

```bash
dotnet test
```

### Specific Test File

```bash
dotnet test --filter "FullyQualifiedName~SmtDeterminismTests"
```

### Specific Test Method

```bash
dotnet test --filter "FullyQualifiedName~Property_InsertedKeyIsRetrievable"
```

### With Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Verbose Output

```bash
dotnet test --verbosity detailed
```

## Test Categories

### 1. Unit Tests

Test individual methods and classes in isolation.

**Example:**
```csharp
[Fact]
public void CreateLeafNode_WithValidParameters_CreatesNode()
{
    // Arrange
    var tree = new SparseMerkleTree(new Sha256HashFunction());
    var key = Encoding.UTF8.GetBytes("key");
    var value = Encoding.UTF8.GetBytes("value");

    // Act
    var node = tree.CreateLeafNode(key, value);

    // Assert
    Assert.Equal(SmtNodeType.Leaf, node.NodeType);
    Assert.NotEmpty(node.Hash.ToArray());
}
```

### 2. Integration Tests

Test complete workflows across multiple components.

**Example:**
```csharp
[Fact]
public async Task EndToEnd_InsertGenerateProofVerify_Success()
{
    // Arrange
    var tree = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
    var storage = new InMemorySmtStorage();
    
    // Act - Insert
    var key = Encoding.UTF8.GetBytes("user:alice");
    var value = Encoding.UTF8.GetBytes("balance:1000");
    var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
    await storage.WriteBatchAsync(result.NodesToPersist);
    
    // Generate proof
    var proof = await tree.GenerateInclusionProofAsync(key, result.NewRootHash, storage);
    
    // Verify proof
    bool isValid = proof.Verify(result.NewRootHash.Span.ToArray(), new Sha256HashFunction(), tree.ZeroHashes);
    
    // Assert
    Assert.True(isValid);
}
```

### 3. Determinism Tests

Ensure operations produce identical results across platforms and instances.

**Location:** `tests/MerkleTree.Tests/Core/DeterminismTests.cs`, `tests/MerkleTree.Tests/Smt/SmtDeterminismTests.cs`

**Example:**
```csharp
[Fact]
public async Task SingleInsert_MultipleTrees_ProducesSameRoot()
{
    // Create three separate trees
    var tree1 = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
    var tree2 = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
    var tree3 = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
    
    // Same operation on all three
    // ... insert same key-value pair ...
    
    // Roots should be identical
    Assert.True(root1.Span.SequenceEqual(root2.Span));
    Assert.True(root2.Span.SequenceEqual(root3.Span));
}
```

### 4. Property-Based Tests

Verify invariants hold for randomized inputs.

**Location:** `tests/MerkleTree.Tests/Smt/SmtPropertyTests.cs`

**Example:**
```csharp
[Theory]
[InlineData(50)]
public async Task Property_InsertedKeyIsRetrievable(int iterations)
{
    // Property: For all key-value pairs inserted, Get(key) returns value
    for (int i = 0; i < iterations; i++)
    {
        var tree = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
        var storage = new InMemorySmtStorage();
        var key = GenerateRandomKey();
        var value = GenerateRandomValue();

        // Insert
        var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
        await storage.WriteBatchAsync(result.NodesToPersist);

        // Retrieve
        var getResult = await tree.GetAsync(key, result.NewRootHash, storage);
        
        // Verify property holds
        Assert.True(getResult.Found);
        Assert.True(getResult.Value.Value.Span.SequenceEqual(value));
    }
}
```

### 5. Test Vectors

Known inputs with expected outputs for regression testing.

**Location:** `tests/MerkleTree.Tests/Smt/SmtTestVectors.cs`

**Example:**
```csharp
[Fact]
public async Task SingleKey_StandardInput_ProducesExpectedRoot()
{
    var tree = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
    var storage = new InMemorySmtStorage();

    var key = Encoding.UTF8.GetBytes("test");
    var value = Encoding.UTF8.GetBytes("value");

    var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
    await storage.WriteBatchAsync(result.NodesToPersist);

    var actualRootHex = Convert.ToHexString(result.NewRootHash.Span);

    // Expected root for this specific input
    // If this fails, it indicates a breaking change
    // var expectedRootHex = "KNOWN_VALUE_HERE";
    // Assert.Equal(expectedRootHex, actualRootHex);
}
```

### 6. Error Handling Tests

Verify exceptions are thrown for invalid inputs.

**Example:**
```csharp
[Fact]
public async Task UpdateAsync_NullKey_ThrowsArgumentNullException()
{
    var tree = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
    var storage = new InMemorySmtStorage();
    var value = Encoding.UTF8.GetBytes("value");

    await Assert.ThrowsAsync<ArgumentNullException>(
        async () => await tree.UpdateAsync(null!, value, tree.ZeroHashes[tree.Depth], storage));
}
```

## Writing Tests

### Test Naming Convention

```
MethodName_Scenario_ExpectedBehavior
```

Examples:
- `UpdateAsync_ValidInput_UpdatesValue`
- `GenerateProof_NonExistentKey_ReturnsNull`
- `Serialize_MultipleInvocations_ProducesSameOutput`

### Arrange-Act-Assert Pattern

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and dependencies
    var tree = new SparseMerkleTree(new Sha256HashFunction());
    var storage = new InMemorySmtStorage();
    var input = CreateTestInput();

    // Act - Execute the method being tested
    var result = await tree.SomeMethodAsync(input, storage);

    // Assert - Verify the expected outcome
    Assert.NotNull(result);
    Assert.Equal(expectedValue, result.Value);
}
```

### Using InMemorySmtStorage for Tests

The `InMemorySmtStorage` class provides a complete in-memory implementation of all SMT persistence interfaces, perfect for testing:

```csharp
[Fact]
public async Task ExampleTest_UsingInMemoryStorage()
{
    // Create in-memory storage (no file system required)
    var storage = new InMemorySmtStorage();
    
    // Initialize tree
    var tree = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
    await storage.StoreMetadataAsync(tree.Metadata);
    
    // Perform operations
    var key = Encoding.UTF8.GetBytes("key");
    var value = Encoding.UTF8.GetBytes("value");
    var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
    await storage.WriteBatchAsync(result.NodesToPersist);
    
    // Verify
    var getResult = await tree.GetAsync(key, result.NewRootHash, storage);
    Assert.True(getResult.Found);
    
    // Clean up (if needed for next test)
    storage.Clear();
}
```

### Test Data Generation

For property-based tests, generate random but deterministic data:

```csharp
private readonly Random _random = new Random(42); // Fixed seed for reproducibility

private byte[] GenerateRandomKey()
{
    var key = new byte[_random.Next(1, 100)];
    _random.NextBytes(key);
    return key;
}

private byte[] GenerateRandomValue()
{
    var value = new byte[_random.Next(1, 200)];
    _random.NextBytes(value);
    return value;
}
```

### Cleanup

Use `IDisposable` or `IAsyncDisposable` for cleanup:

```csharp
public class MyTests : IDisposable
{
    private readonly string _tempFile;

    public MyTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void TestThatUsesFile()
    {
        // Test uses _tempFile
        // Cleanup happens automatically
    }
}
```

## Test Utilities

### Hash Function Helpers

```csharp
var sha256 = new Sha256HashFunction();
var sha512 = new Sha512HashFunction();
var blake3 = new Blake3HashFunction(); // .NET 10+ only
```

### Data Conversion

```csharp
// String to bytes
var bytes = Encoding.UTF8.GetBytes("text");

// Bytes to hex
var hex = Convert.ToHexString(bytes);

// Hex to bytes
var bytes = Convert.FromHexString(hex);
```

### Assertion Helpers

```csharp
// Byte array comparison
Assert.True(array1.SequenceEqual(array2));

// ReadOnlyMemory comparison
Assert.True(memory1.Span.SequenceEqual(memory2.Span));

// Collection assertions
Assert.NotEmpty(collection);
Assert.Equal(expectedCount, collection.Count);
Assert.Contains(expectedItem, collection);
```

## CI Integration

Tests run automatically on every pull request via GitHub Actions.

**Configuration:** `.github/workflows/pr-check.yml`

```yaml
- name: Run tests with coverage
  run: dotnet test --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage"
```

### Coverage Reporting

Code coverage is reported as a comment on pull requests:

- **Green**: ≥80% coverage
- **Yellow**: 60-80% coverage
- **Red**: <60% coverage

### Coverage Thresholds

- **Minimum**: 60% (warning threshold)
- **Target**: 80% (passing threshold)
- **Business Logic**: 90%+ expected

## Coverage

### Viewing Coverage Reports

After running tests with coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Reports are generated in `coverage/**/coverage.cobertura.xml`.

### Excluding From Coverage

Use `[ExcludeFromCodeCoverage]` attribute:

```csharp
[ExcludeFromCodeCoverage]
public class GeneratedCode
{
    // This code won't count toward coverage
}
```

### Coverage by Component

| Component | Coverage | Notes |
|-----------|----------|-------|
| Core Merkle Trees | 95%+ | Fully tested |
| Sparse Merkle Trees | 90%+ | Core operations tested |
| Persistence | 90%+ | Reference implementation |
| Proofs | 95%+ | Generation and verification |
| Hashing | 100% | All hash functions |
| Serialization | 95%+ | All formats |
| Cache | 90%+ | File and memory |
| Error Handling | 90%+ | Exception scenarios |

## Best Practices

1. **Test Public APIs Only**: Don't test private methods directly
2. **One Assertion Per Test**: Or closely related assertions
3. **Descriptive Names**: Test names should explain what's being tested
4. **Fast Tests**: Keep unit tests under 100ms
5. **Isolated Tests**: No dependencies between tests
6. **Deterministic**: Tests should pass consistently
7. **Use InMemorySmtStorage**: For SMT tests, always use in-memory storage
8. **Clean Up**: Dispose of resources properly
9. **Document Edge Cases**: Explain why edge cases exist
10. **Test Negative Cases**: Don't just test the happy path

## Common Patterns

### Testing Async Methods

```csharp
[Fact]
public async Task AsyncMethod_Scenario_Behavior()
{
    // Use async/await pattern
    var result = await SomeAsyncMethod();
    Assert.NotNull(result);
}
```

### Testing Exceptions

```csharp
[Fact]
public async Task Method_InvalidInput_ThrowsException()
{
    await Assert.ThrowsAsync<ArgumentException>(
        async () => await MethodAsync(invalidInput));
}
```

### Testing Collections

```csharp
[Fact]
public void Method_ReturnsCollection_HasExpectedItems()
{
    var result = Method();
    
    Assert.NotEmpty(result);
    Assert.Equal(expectedCount, result.Count);
    Assert.All(result, item => Assert.NotNull(item));
}
```

### Parameterized Tests

```csharp
[Theory]
[InlineData(8)]
[InlineData(16)]
[InlineData(32)]
public void Method_VariousInputs_ProducesExpectedResult(int input)
{
    var result = Method(input);
    Assert.NotNull(result);
}
```

## Troubleshooting

### Tests Fail Intermittently

- Check for race conditions
- Ensure tests are isolated
- Use fixed seeds for random data
- Check for file system cleanup issues

### Tests Fail on CI But Pass Locally

- Check platform-specific code
- Verify environment variables
- Check file path separators
- Review CI logs for clues

### Low Coverage

- Add tests for untested paths
- Test error conditions
- Test edge cases
- Review coverage report to find gaps

## References

- [xUnit Documentation](https://xunit.net/)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Code Coverage Tools](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage)
