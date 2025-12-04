# Merkle Proof Serialization Format

This document specifies the binary serialization format for Merkle proofs, enabling compact storage and transmission of proofs across different systems and platforms.

## Overview

The serialization format provides:
- **Deterministic encoding**: Same proof always produces identical binary output
- **Platform independence**: Works across different architectures and operating systems
- **Compact representation**: Minimal overhead beyond the essential proof data
- **Version support**: Future-proof with format versioning
- **Multi-hash support**: Compatible with different hash functions (SHA-256, SHA-512, BLAKE3, etc.)

## Format Specification

### Binary Layout

The serialized proof consists of the following fields in order:

| Field | Type | Size | Description |
|-------|------|------|-------------|
| Version | `byte` | 1 byte | Format version number (currently 1) |
| Tree Height | `int32` | 4 bytes | Height of the Merkle tree |
| Leaf Index | `int64` | 8 bytes | Zero-based index of the leaf in the tree |
| Leaf Value Length | `int32` | 4 bytes | Length of the leaf value in bytes |
| Leaf Value | `byte[]` | Variable | Raw bytes of the leaf value |
| Hash Size | `int32` | 4 bytes | Size of each hash in bytes |
| Orientation Bits Length | `int32` | 4 bytes | Number of bytes used for orientation bits |
| Orientation Bits | `byte[]` | Variable | Packed boolean array (8 bits per byte) |
| Sibling Hashes | `byte[]` | Variable | Concatenated sibling hashes |

### Field Details

#### Version (1 byte)
- Current version: `1`
- Used for future format evolution
- Deserialization rejects unsupported versions

#### Tree Height (4 bytes)
- Little-endian signed 32-bit integer
- Represents the number of levels from leaf to root
- Must be non-negative
- Zero indicates a single-leaf tree

#### Leaf Index (8 bytes)
- Little-endian signed 64-bit integer
- Zero-based position of the leaf in the tree
- Must be non-negative
- Supports trees with up to 2^63-1 leaves

#### Leaf Value Length (4 bytes)
- Little-endian signed 32-bit integer
- Number of bytes in the leaf value
- Must be non-negative
- Maximum: 2^31-1 bytes (2GB)

#### Leaf Value (variable)
- Raw bytes of the original leaf data
- Length specified by Leaf Value Length field
- No encoding or transformation applied

#### Hash Size (4 bytes)
- Little-endian signed 32-bit integer
- Size of each sibling hash in bytes
- Must be non-negative
- Typical values:
  - 32 bytes for SHA-256 and BLAKE3
  - 64 bytes for SHA-512

#### Orientation Bits Length (4 bytes)
- Little-endian signed 32-bit integer
- Number of bytes used to store orientation bits
- Calculated as: `(TreeHeight + 7) / 8` (rounded up)
- Zero for single-leaf trees

#### Orientation Bits (variable)
- Boolean array packed into bytes (LSB first)
- Each bit indicates sibling position:
  - `0` (false): sibling is on the left, current node is on the right
  - `1` (true): sibling is on the right, current node is on the left
- Bit packing example for 9 bits:
  - Bits 0-7 in byte 0
  - Bit 8 in byte 1 (remaining bits are zero)
- Unused bits in the last byte are set to zero

#### Sibling Hashes (variable)
- Concatenated hash values
- Total size: `TreeHeight × Hash Size` bytes
- Hashes stored in order from leaf to root
- No separators between hashes

### Size Calculation

Total size formula:
```
TotalSize = 1 + 4 + 8 + 4 + LeafValueLength + 4 + 4 + OrientationBytesLength + (TreeHeight × HashSize)
```

Minimum size (single leaf, no sibling hashes):
```
MinSize = 1 + 4 + 8 + 4 + LeafValueLength + 4 + 4 = 25 + LeafValueLength bytes
```

## Usage Examples

### Serialization

```csharp
using MerkleTree.Core;
using MerkleTree.Proofs;
using System.Text;

var leafData = new List<byte[]>
{
    Encoding.UTF8.GetBytes("data1"),
    Encoding.UTF8.GetBytes("data2"),
    Encoding.UTF8.GetBytes("data3")
};

var tree = new MerkleTree(leafData);
var proof = tree.GenerateProof(1);

// Serialize to binary format
byte[] serialized = proof.Serialize();

// Save to file
File.WriteAllBytes("proof.bin", serialized);

// Or transmit over network
await networkStream.WriteAsync(serialized);
```

### Deserialization

```csharp
using MerkleTree.Proofs;
using MerkleTree.Hashing;

// Load from file
byte[] serialized = File.ReadAllBytes("proof.bin");

// Deserialize
var proof = MerkleProof.Deserialize(serialized);

// Verify the proof
var hashFunction = new Sha256HashFunction();
bool isValid = proof.Verify(expectedRootHash, hashFunction);
```

### Cross-Platform Transmission

```csharp
// Server side: serialize and send
var proof = tree.GenerateProof(leafIndex);
var serialized = proof.Serialize();
await SendToClient(serialized);

// Client side: receive and deserialize
var received = await ReceiveFromServer();
var proof = MerkleProof.Deserialize(received);
var isValid = proof.Verify(trustedRootHash, new Sha256HashFunction());
```

## Format Properties

### Determinism

- Same proof always produces identical binary output
- Byte-for-byte reproducible across:
  - Different program executions
  - Different machines and architectures
  - Different .NET runtime versions

### Platform Independence

- Uses little-endian byte order via `BinaryPrimitives` for guaranteed cross-platform consistency
- No alignment padding or structure packing
- No pointer-dependent data
- Works across:
  - Windows, Linux, macOS
  - x86, x64, ARM, and big-endian architectures
  - .NET Framework, .NET Core, .NET 5+

### Validation

Deserialization performs extensive validation:
- Version compatibility check
- Non-negative values for counts and lengths
- Sufficient data for declared sizes
- No extra bytes after valid data
- Orientation bits length matches tree height

### Error Handling

Common exceptions during deserialization:

| Exception | Cause |
|-----------|-------|
| `ArgumentNullException` | Data is null |
| `ArgumentException` | Invalid format or corrupted data |
| `ArgumentException` (version) | Unsupported format version |
| `ArgumentException` (truncated) | Data too short for declared content |
| `ArgumentException` (extra) | Extra bytes after valid data |

## Compatibility

### Hash Function Compatibility

The format supports any hash function:
- SHA-256 (32 bytes)
- SHA-512 (64 bytes)
- BLAKE3 (32 bytes)
- Any custom hash function

Hash size is encoded in the format, enabling automatic detection.

### Version Evolution

Future format versions may:
- Add optional fields
- Support compression
- Include metadata
- Add checksums

Version 1 implementations will reject future versions to prevent silent failures.

## Performance Characteristics

| Operation | Time Complexity | Space Complexity |
|-----------|----------------|------------------|
| Serialize | O(n) | O(n) |
| Deserialize | O(n) | O(n) |

Where n = TreeHeight + LeafValueLength

Typical performance:
- Serialization: < 1 μs for small proofs (< 10 levels)
- Deserialization: < 1 μs for small proofs (< 10 levels)
- Zero-copy operations where possible

## Best Practices

### Storage

1. **File Storage**: Use `.proof.bin` extension
2. **Database Storage**: Use `BLOB` or `BYTEA` columns
3. **Cache Keys**: Use Base64 encoding: `Convert.ToBase64String(serialized)`

### Transmission

1. **HTTP**: Use binary content type: `application/octet-stream`
2. **gRPC**: Use `bytes` field type
3. **WebSocket**: Send as binary frames

### Security

1. **Integrity**: Consider adding HMAC for tamper detection
2. **Encryption**: Encrypt serialized data if needed
3. **Validation**: Always verify proofs after deserialization
4. **Size Limits**: Enforce maximum proof size to prevent DoS

### Optimization

1. **Batch Operations**: Serialize/deserialize proofs in parallel
2. **Compression**: Apply external compression if needed (e.g., gzip)
3. **Pooling**: Reuse byte buffers for serialization

## Format Comparison

Compared to other serialization formats:

| Format | Size Overhead | Speed | Type Safety | Determinism |
|--------|--------------|-------|-------------|-------------|
| Binary (this) | Minimal | Fast | High | Yes |
| JSON | High | Slow | Low | Yes |
| Protocol Buffers | Low | Fast | High | Yes |
| MessagePack | Medium | Fast | Medium | Yes |

This binary format is optimized for:
- Minimal size overhead
- Maximum speed
- Strong type validation
- Deterministic output

## Example Serialized Data

For a proof with:
- Tree Height: 2
- Leaf Index: 1
- Leaf Value: "test" (4 bytes)
- Hash Size: 32 bytes (SHA-256)
- 2 sibling hashes

Binary layout (hexadecimal):
```
01                          # Version = 1
02 00 00 00                 # Tree Height = 2 (little-endian)
01 00 00 00 00 00 00 00     # Leaf Index = 1 (little-endian)
04 00 00 00                 # Leaf Value Length = 4
74 65 73 74                 # Leaf Value = "test"
20 00 00 00                 # Hash Size = 32
01 00 00 00                 # Orientation Bits Length = 1
02                          # Orientation Bits = 0b00000010 (bit 1 set)
[32 bytes of first hash]    # Sibling Hash 0
[32 bytes of second hash]   # Sibling Hash 1
```

Total size: 1 + 4 + 8 + 4 + 4 + 4 + 4 + 1 + 64 = 94 bytes

## Implementation Notes

### Bit Packing Algorithm

Orientation bits are packed LSB-first:
```csharp
// Packing
for (int i = 0; i < treeHeight; i++)
{
    if (siblingIsRight[i])
    {
        int byteIndex = i / 8;
        int bitIndex = i % 8;
        orientationBytes[byteIndex] |= (byte)(1 << bitIndex);
    }
}

// Unpacking
for (int i = 0; i < treeHeight; i++)
{
    int byteIndex = i / 8;
    int bitIndex = i % 8;
    siblingIsRight[i] = (data[offset + byteIndex] & (1 << bitIndex)) != 0;
}
```

### Endianness

All multi-byte integers use little-endian byte order for cross-platform consistency:
- `BinaryPrimitives.WriteInt32LittleEndian()`, `BinaryPrimitives.WriteInt64LittleEndian()` for serialization
- `BinaryPrimitives.ReadInt32LittleEndian()`, `BinaryPrimitives.ReadInt64LittleEndian()` for deserialization

This ensures that serialized proofs are identical across all platforms (x86, x64, ARM, big-endian systems, etc.), regardless of the native endianness of the system.

## See Also

- [Proof Generation Documentation](PROOF_GENERATION.md) - Complete guide to generating and verifying Merkle proofs
- [Streaming Documentation](STREAMING.md) - Details on streaming tree construction
- [README](../README.md) - Getting started and overview
