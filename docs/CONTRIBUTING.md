# Contributing to MerkleTree

Thank you for your interest in contributing to the MerkleTree library! This guide outlines our architecture constraints, design principles, and best practices for maintaining a storage-agnostic and blockchain-neutral implementation.

## Table of Contents

- [Architecture Principles](#architecture-principles)
- [Core Constraints](#core-constraints)
- [Adapter Pattern](#adapter-pattern)
- [Prohibited Dependencies](#prohibited-dependencies)
- [Code Guidelines](#code-guidelines)
- [Testing Requirements](#testing-requirements)
- [CI Enforcement](#ci-enforcement)
- [Getting Started](#getting-started)

## Architecture Principles

The MerkleTree library follows these core architectural principles:

### 1. Storage Agnosticism

The library **never** directly depends on specific storage implementations. Instead, it defines abstract interfaces that can be implemented by any storage backend.

**Rationale:** 
- Maximum portability across different environments (cloud, on-premise, embedded)
- Users can choose storage solutions that fit their requirements
- Library remains lightweight without heavy dependencies
- Easier to test and maintain

### 2. Blockchain Neutrality

The library provides cryptographic data structures without coupling to any blockchain platform or cryptocurrency implementation.

**Rationale:**
- Useful beyond blockchain applications (distributed systems, version control, data integrity)
- No bias toward specific blockchain platforms or consensus mechanisms
- Users can integrate with any blockchain technology
- Library focuses on core cryptographic primitives

### 3. Interface-Based Design

All external dependencies (storage, I/O, network) must be abstracted behind interfaces.

**Rationale:**
- Enables dependency injection and testing
- Supports multiple implementations
- Clean separation of concerns
- Facilitates mocking in tests

## Core Constraints

### ✅ ALLOWED in Core Library

**Temporary file operations** (internal use only):
```csharp
// OK: Internal temporary files for streaming operations
string tempDir = Path.Combine(Path.GetTempPath(), $"merkletree_{Guid.NewGuid():N}");
Directory.CreateDirectory(tempDir);
```

**Abstraction interfaces**:
```csharp
// OK: Defining storage abstractions
public interface ISmtNodeReader
{
    Task<SmtNodeBlob?> ReadNodeByHashAsync(ReadOnlyMemory<byte> hash);
}
```

**In-memory reference implementations**:
```csharp
// OK: Reference implementation for testing and small datasets
public class InMemorySmtStorage : ISmtNodeReader, ISmtNodeWriter
{
    private readonly Dictionary<string, byte[]> _nodes = new();
    // ...
}
```

### ❌ PROHIBITED in Core Library

**Direct database dependencies**:
```csharp
// NOT ALLOWED: Direct database dependencies
using System.Data.SqlClient;
using MongoDB.Driver;
using Microsoft.EntityFrameworkCore;
using Dapper;
```

**Blockchain-specific logic**:
```csharp
// NOT ALLOWED: Blockchain-specific implementations
using Nethereum.Web3;
using NBitcoin;
using Solnet;
```

**Cloud provider SDKs**:
```csharp
// NOT ALLOWED: Direct cloud provider dependencies
using Amazon.DynamoDB;
using Microsoft.Azure.Cosmos;
using Google.Cloud.Firestore;
```

**File-based persistence APIs** (except for internal temp files):
```csharp
// NOT ALLOWED: Public APIs that require file paths
public void SaveToFile(string filePath) { } // Bad
public void LoadFromDatabase(string connectionString) { } // Bad
```

## Adapter Pattern

Users should implement storage adapters outside the core library. Here are architectural patterns for common scenarios:

### Database Adapter Pattern

**CORRECT - User implements adapter:**

```
User's Application Code:
├── MyDatabaseAdapter.cs           // Implements ISmtNodeReader, ISmtNodeWriter
│   └── Uses EntityFramework/Dapper/etc to talk to database
│
└── Program.cs
    └── var storage = new MyDatabaseAdapter(connectionString);
    └── var tree = new SparseMerkleTree(hashFunction, storage);
```

**Key points:**
- Storage implementation lives in **user's code**, not in MerkleTree library
- User chooses their preferred database technology
- Adapter implements library's persistence interfaces
- Library remains database-agnostic

### Blockchain Integration Pattern

**CORRECT - User implements integration:**

```
User's Blockchain Application:
├── ChiaProofAdapter.cs            // Converts MerkleProof to Chia format
├── EthereumAdapter.cs             // Converts for Ethereum/Solidity
├── BlockchainWriter.cs            // Writes proofs to blockchain
│
└── Program.cs
    └── var tree = new MerkleTree(leaves);
    └── var proof = tree.GenerateProof(index);
    └── var chiaProof = ChiaProofAdapter.Convert(proof);
    └── await blockchain.SubmitProof(chiaProof);
```

**Key points:**
- Blockchain-specific logic lives in **user's code**
- Library provides generic proof structures
- User adapts proofs to their blockchain format
- Library supports all blockchains through generic interfaces

### Cloud Storage Adapter Pattern

**CORRECT - User implements cloud adapter:**

```
User's Cloud Application:
├── S3StorageAdapter.cs            // AWS S3 implementation
├── BlobStorageAdapter.cs          // Azure Blob Storage implementation
│   └── Both implement ISmtNodeReader, ISmtNodeWriter
│
└── Program.cs
    └── var storage = new S3StorageAdapter(bucketName, credentials);
    └── var tree = new SparseMerkleTree(hashFunction, storage);
```

## Prohibited Dependencies

The following NuGet packages and namespaces are **prohibited** in the core `MerkleTree` project:

### Database Packages
- ❌ `Microsoft.EntityFrameworkCore.*`
- ❌ `Dapper`
- ❌ `System.Data.SqlClient`
- ❌ `Npgsql`
- ❌ `MongoDB.Driver`
- ❌ `MySql.Data`
- ❌ `SQLite`
- ❌ Any database-specific drivers

### Blockchain Packages
- ❌ `Nethereum.*`
- ❌ `NBitcoin`
- ❌ `Solnet`
- ❌ Any blockchain-specific SDKs

### Cloud Provider Packages
- ❌ `AWSSDK.*`
- ❌ `Azure.*` (except Azure.Core for abstractions)
- ❌ `Google.Cloud.*`
- ❌ Any cloud provider-specific SDKs

### Allowed Framework Namespaces
- ✅ `System.*` (standard library)
- ✅ `System.IO` (for temp files only)
- ✅ `System.Threading.*`
- ✅ `System.Buffers.*`
- ✅ `System.Collections.*`
- ✅ `System.Security.Cryptography.*`

## Code Guidelines

### Naming Conventions

- Use `PascalCase` for classes, methods, properties
- Use `_camelCase` for private fields
- Prefix interfaces with `I` (e.g., `IHashFunction`, `ISmtNodeReader`)

### Nullable Reference Types

- Always enable: `<Nullable>enable</Nullable>`
- Use `ArgumentNullException.ThrowIfNull(parameter)` for null checks
- Never use `!` null-forgiving operator

### Async/Await

- Use async for all I/O operations
- Suffix async methods with `Async`
- Always return `Task` or `Task<T>`
- Accept `CancellationToken` as last parameter with default value
- Check `cancellationToken.ThrowIfCancellationRequested()` in loops

### Error Handling

Storage adapters should wrap implementation-specific errors:

```csharp
// In user's adapter implementation:
try
{
    // Database or file operation
}
catch (IOException ex)
{
    throw new StorageAdapterException("IO_ERROR", "Failed to read node", ex);
}
catch (SqlException ex)
{
    throw new StorageAdapterException("DATABASE_ERROR", "Database connection failed", ex);
}
```

### Documentation

- Document all public APIs with XML documentation
- Include `<summary>`, `<param>`, `<returns>`, `<exception>`
- Add `<example>` with `<code>` for complex APIs
- Explain architectural patterns in `<remarks>`

## Testing Requirements

### Unit Tests

- Use xUnit framework
- Use NSubstitute for mocking interfaces
- Follow Arrange-Act-Assert pattern
- Name tests: `MethodName_Scenario_ExpectedBehavior`

### Storage Independence

**Tests MUST NOT require:**
- ❌ External databases running
- ❌ Blockchain nodes running
- ❌ Cloud services configured
- ❌ Persistent file storage (except temp files)

**Tests MUST use:**
- ✅ `InMemorySmtStorage` for SMT tests
- ✅ Mocked interfaces with NSubstitute
- ✅ Temporary files that are cleaned up
- ✅ Deterministic test data

**Example:**

```csharp
[Fact]
public async Task SparseMerkleTree_InsertAndGet_ReturnsCorrectValue()
{
    // Arrange - Use in-memory storage, no external dependencies
    var storage = new InMemorySmtStorage();
    var hashFunction = new Sha256HashFunction();
    var tree = new SparseMerkleTree(
        hashFunction, 
        storage,  // reader
        storage,  // writer
        storage,  // snapshot manager
        storage   // metadata store
    );
    
    var key = Encoding.UTF8.GetBytes("test-key");
    var value = Encoding.UTF8.GetBytes("test-value");
    
    // Act
    await tree.InsertAsync(key, value);
    var result = await tree.GetAsync(key);
    
    // Assert
    Assert.True(result.Found);
    Assert.Equal(value, result.Value.ToArray());
}
```

### Coverage Requirements

- Target 90%+ code coverage for business logic
- All public APIs must have tests
- Edge cases and error paths must be tested
- Determinism tests for cross-platform compatibility

## CI Enforcement

Our continuous integration pipeline enforces these constraints automatically:

### 1. Banned Package Check

Verifies that prohibited NuGet packages are not referenced:

```bash
# Check for banned packages in MerkleTree.csproj
dotnet list src/MerkleTree/MerkleTree.csproj package | \
  grep -E "EntityFrameworkCore|MongoDB|Dapper|AWSSDK|Azure\.|Nethereum|NBitcoin"
```

### 2. Banned Import Check

Verifies that prohibited namespaces are not imported:

```bash
# Check for banned using directives in source files
grep -r "using System.Data" src/MerkleTree --include="*.cs" && exit 1
grep -r "using MongoDB" src/MerkleTree --include="*.cs" && exit 1
grep -r "using Nethereum" src/MerkleTree --include="*.cs" && exit 1
# ... etc
```

### 3. Test Independence Verification

Ensures tests can run without external dependencies:

```bash
# Tests must pass without external services
dotnet test --no-restore --configuration Release
```

### 4. Code Coverage

Maintains minimum code coverage thresholds:

```bash
dotnet test --collect:"XPlat Code Coverage"
# Minimum threshold: 60% with target of 80%
```

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Git

### Building

```bash
git clone https://github.com/runcodedad/merkletree.git
cd merkletree
dotnet restore
dotnet build
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Before Submitting PR

1. **Verify no prohibited dependencies:**
   ```bash
   dotnet list src/MerkleTree/MerkleTree.csproj package
   ```

2. **Check for banned imports:**
   ```bash
   grep -r "using System.Data\|using MongoDB\|using Nethereum" src/MerkleTree --include="*.cs"
   ```

3. **Run all tests:**
   ```bash
   dotnet test
   ```

4. **Check code coverage:**
   ```bash
   dotnet test --collect:"XPlat Code Coverage"
   ```

5. **Build in Release mode:**
   ```bash
   dotnet build --configuration Release
   ```

## Examples of Good Architecture

### Storage Interface Definition (Core Library)

```csharp
// In MerkleTree library - defines the contract
namespace MerkleTree.Smt.Persistence;

public interface ISmtNodeWriter
{
    Task WriteBatchAsync(IReadOnlyList<SmtNodeBlob> nodes, CancellationToken ct = default);
}
```

### Storage Implementation (User Code)

```csharp
// In user's application - implements the contract
using MerkleTree.Smt.Persistence;
using Microsoft.EntityFrameworkCore; // OK - user's code can use EF

public class EFCoreSmtStorage : ISmtNodeWriter
{
    private readonly MyDbContext _context;
    
    public EFCoreSmtStorage(MyDbContext context)
    {
        _context = context;
    }
    
    public async Task WriteBatchAsync(
        IReadOnlyList<SmtNodeBlob> nodes, 
        CancellationToken ct = default)
    {
        // User's implementation using EF Core
        var entities = nodes.Select(n => new NodeEntity 
        { 
            Hash = Convert.ToBase64String(n.Hash.ToArray()),
            Data = n.Data.ToArray()
        });
        
        _context.Nodes.AddRange(entities);
        await _context.SaveChangesAsync(ct);
    }
}
```

### Usage (User Code)

```csharp
// In user's application
var dbContext = new MyDbContext(connectionString);
var storage = new EFCoreSmtStorage(dbContext);

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction, storage, storage, storage, storage);

// Use tree with database-backed storage
await tree.InsertAsync(key, value);
```

## Questions?

If you have questions about these constraints or need clarification on architectural patterns:

1. Check existing persistence interfaces in `src/MerkleTree/Smt/Persistence/`
2. Review `InMemorySmtStorage` as a reference implementation
3. Read the test suite for usage examples
4. Open a discussion on GitHub: https://github.com/runcodedad/merkletree/discussions

## License

By contributing to MerkleTree, you agree that your contributions will be licensed under the MIT License.
