# Sparse Merkle Tree (SMT) Metadata Structure

## Overview

The SMT metadata structure provides essential information for deterministic reproduction of Sparse Merkle Tree roots and proofs across machines, platforms, and implementations. This document describes the metadata structure, versioning strategy, and migration expectations.

## Metadata Components

The `SmtMetadata` class contains the following core components:

### 1. Hash Algorithm ID
- **Type**: `string`
- **Purpose**: Identifies the cryptographic hash function used for tree construction
- **Examples**: `"SHA-256"`, `"SHA-512"`, `"BLAKE3"`
- **Determinism**: Must match the hash function name exactly across all implementations

### 2. Tree Depth
- **Type**: `int`
- **Purpose**: Specifies the number of levels in the tree
- **Range**: Minimum 1, typical values: 8-256
- **Capacity**: A depth of N supports up to 2^N keys
  - Depth 8 → 256 keys
  - Depth 16 → 65,536 keys
  - Depth 32 → 4,294,967,296 keys
  - Depth 64 → 18,446,744,073,709,551,616 keys
  - Depth 256 → 2^256 keys (common for cryptographic applications)

### 3. Zero-Hash Table
- **Type**: `ZeroHashTable`
- **Purpose**: Precomputed hash values for empty subtrees at each level
- **Size**: Contains `depth + 1` entries (one per level, including root)
- **Computation**: Deterministic, based on hash algorithm and tree depth
- **See**: [Zero-Hash Table Generation](#zero-hash-table-generation)

### 4. SMT Core Version
- **Type**: `int`
- **Purpose**: Tracks the version of the SMT core implementation
- **Current**: Version 1
- **Breaking Changes**: Incremented when tree structure, hashing strategy, or core algorithms change incompatibly

### 5. Serialization Format Version
- **Type**: `int`
- **Purpose**: Tracks the binary serialization format version
- **Current**: Version 1
- **Breaking Changes**: Incremented when the serialization format changes incompatibly

## Zero-Hash Table Generation

The zero-hash table is computed deterministically using domain-separated hashing to ensure consistency across implementations and prevent collision attacks.

### Algorithm

```
Level 0 (leaf):
  zero[0] = Hash(0x00 || empty_bytes)
  
Level N (internal, N > 0):
  zero[N] = Hash(0x01 || zero[N-1] || zero[N-1])
```

Where:
- `0x00` is the leaf domain separator
- `0x01` is the internal node domain separator
- `empty_bytes` is an empty byte array
- `||` denotes concatenation

### Properties

1. **Deterministic**: Same hash algorithm and depth always produce identical tables
2. **Unique**: All zero-hashes at different levels are unique
3. **Domain-Separated**: Collision attacks between leaves and internal nodes are prevented
4. **Platform-Independent**: Computation is identical across all platforms and implementations

### Example (SHA-256, Depth 8)

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var zeroHashes = ZeroHashTable.Compute(hashFunction, depth: 8);

// Access zero-hash for level 0 (empty leaf)
byte[] leafZeroHash = zeroHashes[0];

// Access zero-hash for level 8 (empty root)
byte[] rootZeroHash = zeroHashes[8];

// Verify the table is correct
bool isValid = zeroHashes.Verify(hashFunction); // Returns true
```

## Creating Metadata

### Option 1: Factory Method (Recommended)

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var metadata = SmtMetadata.Create(hashFunction, treeDepth: 256);

Console.WriteLine($"Algorithm: {metadata.HashAlgorithmId}");
Console.WriteLine($"Depth: {metadata.TreeDepth}");
Console.WriteLine($"Core Version: {metadata.SmtCoreVersion}");
Console.WriteLine($"Format Version: {metadata.SerializationFormatVersion}");
```

### Option 2: Constructor (Advanced)

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var zeroHashes = ZeroHashTable.Compute(hashFunction, 256);

var metadata = new SmtMetadata(
    hashAlgorithmId: hashFunction.Name,
    treeDepth: 256,
    zeroHashes: zeroHashes,
    smtCoreVersion: 1,
    serializationFormatVersion: 1
);
```

## Serialization

The metadata can be serialized to a platform-independent binary format for storage or transmission.

### Binary Format (Version 1)

```
Offset | Size | Description
-------|------|------------
0      | 4    | Serialization format version (little-endian)
4      | 4    | SMT core version (little-endian)
8      | 4    | Tree depth (little-endian)
12     | 4    | Hash algorithm ID length (little-endian)
16     | N    | Hash algorithm ID string (UTF-8)
16+N   | M    | Zero-hash table (serialized)
```

All numeric values use little-endian byte order for cross-platform compatibility.

### Serialization Example

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var metadata = SmtMetadata.Create(hashFunction, 256);

// Serialize to bytes
byte[] serialized = metadata.Serialize();

// Save to file (example)
File.WriteAllBytes("smt_metadata.bin", serialized);

// Deserialize from bytes
byte[] loaded = File.ReadAllBytes("smt_metadata.bin");
var deserialized = SmtMetadata.Deserialize(loaded);

// Verify integrity
bool matches = deserialized.HashAlgorithmId == metadata.HashAlgorithmId
            && deserialized.TreeDepth == metadata.TreeDepth;
```

## Determinism Guarantees

The SMT metadata system provides strong determinism guarantees:

1. **Cross-Platform**: Identical metadata on Windows, Linux, macOS, and any .NET-supported platform
2. **Cross-Implementation**: Same hash algorithm and depth produce identical zero-hash tables in any correct implementation
3. **Cross-Version**: Metadata from version N can be read by version N (forward compatibility requires explicit migration)
4. **Reproducible**: Same inputs always produce bit-identical serialized output

### Testing Determinism

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

// Create metadata multiple times
var metadata1 = SmtMetadata.Create(new Sha256HashFunction(), 256);
var metadata2 = SmtMetadata.Create(new Sha256HashFunction(), 256);

// Verify zero-hashes are identical
for (int level = 0; level <= 256; level++)
{
    Assert.Equal(metadata1.ZeroHashes[level], metadata2.ZeroHashes[level]);
}

// Verify serialization is deterministic
byte[] serialized1 = metadata1.Serialize();
byte[] serialized2 = metadata2.Serialize();
Assert.Equal(serialized1, serialized2);
```

## Versioning and Migration

### Version Changes

**SMT Core Version** changes indicate breaking changes to:
- Tree structure or organization
- Hashing strategy or domain separation
- Core algorithms (insert, update, delete, proof generation)
- Zero-hash computation method

**Serialization Format Version** changes indicate breaking changes to:
- Binary serialization format
- Field order or encoding
- Data types or sizes

### Migration Strategy

When versions change, migration may be required:

1. **Non-Breaking Changes**: Patch versions don't change metadata versions
2. **Breaking Changes**: Major versions increment metadata versions
3. **Manual Migration**: Tooling to migrate old states is out-of-scope; migration must be handled manually

### Migration Example

```csharp
// Version 1 metadata
var v1Metadata = SmtMetadata.Deserialize(oldSerializedData);

if (v1Metadata.SmtCoreVersion != SmtMetadata.CurrentSmtCoreVersion)
{
    Console.WriteLine("WARNING: Metadata from older SMT core version.");
    Console.WriteLine($"Current: {SmtMetadata.CurrentSmtCoreVersion}, " +
                     $"Loaded: {v1Metadata.SmtCoreVersion}");
    
    // Manual migration required
    // 1. Extract tree data using old version
    // 2. Rebuild tree with new version
    // 3. Verify root hashes match expectations
}
```

### Breaking Change Documentation

Breaking changes will be documented in release notes with:
- Clear description of what changed
- Migration steps (if available)
- Compatibility matrix
- Deprecation timeline (if applicable)

## Validation

### Validating Metadata Integrity

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var metadata = SmtMetadata.Deserialize(serializedData);

// 1. Check versions
if (metadata.SmtCoreVersion != SmtMetadata.CurrentSmtCoreVersion)
{
    Console.WriteLine("WARNING: Version mismatch");
}

// 2. Verify zero-hash table
var hashFunction = GetHashFunction(metadata.HashAlgorithmId);
bool isValid = metadata.ZeroHashes.Verify(hashFunction);

if (!isValid)
{
    throw new InvalidOperationException("Zero-hash table verification failed. " +
                                       "Data may be corrupted or computed with different algorithm.");
}

// 3. Validate depth range
if (metadata.TreeDepth < 1 || metadata.TreeDepth > 256)
{
    throw new InvalidOperationException($"Invalid tree depth: {metadata.TreeDepth}");
}
```

## Usage in Core Operations

The metadata is used throughout SMT operations:

1. **Initialization**: Create metadata when initializing a new SMT
2. **Persistence**: Store metadata alongside tree nodes for later reconstruction
3. **Proof Generation**: Include metadata hash in proofs for verification
4. **Proof Verification**: Load metadata to verify proofs and check compatibility
5. **Tree Comparison**: Compare metadata to ensure trees are compatible for merging or comparison

### Example: Proof Verification

```csharp
// Verifier receives: proof, key, value, metadata
var metadata = SmtMetadata.Deserialize(metadataBytes);
var hashFunction = GetHashFunction(metadata.HashAlgorithmId);

// Verify the proof uses correct metadata
bool proofValid = VerifyProof(
    proof,
    key,
    value,
    metadata.ZeroHashes,
    hashFunction
);
```

## Best Practices

1. **Always Store Metadata**: Save metadata alongside tree data for reproducibility
2. **Version Checks**: Check version compatibility before loading serialized data
3. **Verify Zero-Hashes**: Use `ZeroHashTable.Verify()` to detect corruption
4. **Document Changes**: Record metadata hash in logs when creating trees
5. **Test Determinism**: Include determinism tests in your test suite
6. **Avoid Mixing**: Don't mix nodes from trees with different metadata

## References

- [SMT Roadmap](./SMT_Roadmap.md) - Implementation plan and phases
- [Hash Abstraction](../src/MerkleTree/Hashing/IHashFunction.cs) - Hash function interface
- [Domain-Separated Hashing](../src/MerkleTree/Core/MerkleTreeBase.cs) - Hashing strategy

## API Documentation

Full API documentation is available in the XML documentation comments in the source code:
- [SmtMetadata.cs](../src/MerkleTree/Smt/SmtMetadata.cs)
- [ZeroHashTable.cs](../src/MerkleTree/Smt/ZeroHashTable.cs)
