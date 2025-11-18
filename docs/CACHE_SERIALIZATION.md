# Cache Serialization Format Specification

## Overview

This document specifies the binary serialization format for Merkle tree cache files. The format is designed to be platform-independent, deterministic, and optimized for efficient random access to cached nodes.

## Purpose

The cache serialization format allows saving and loading cached Merkle tree levels to/from persistent storage. This enables:

- **Faster proof generation**: Precomputed intermediate levels can be loaded instead of recomputed
- **Reduced memory usage**: Only necessary levels need to be kept in memory
- **Persistent caching**: Cache data survives application restarts
- **Efficient node lookup**: Fixed-size node records enable O(1) access within a level

## Format Version

Current version: **1**

The format includes a version byte to support future enhancements while maintaining backward compatibility.

## Binary Format Specification

All multi-byte integer values are encoded in **little-endian** byte order for cross-platform compatibility.

### File Structure

```
+---------------------------+
| Magic Number (4 bytes)    |  "MKTC" (Merkle Tree Cache)
+---------------------------+
| Format Version (1 byte)   |  Current version: 1
+---------------------------+
| Tree Height (4 bytes)     |  int32 - Total height of the tree
+---------------------------+
| Hash Function Name Length |  int32 - Length of hash function name
| (4 bytes)                 |
+---------------------------+
| Hash Function Name        |  UTF-8 encoded string
| (variable)                |  e.g., "SHA256", "SHA512", "BLAKE3"
+---------------------------+
| Hash Size In Bytes        |  int32 - Size of each hash (e.g., 32 for SHA-256)
| (4 bytes)                 |
+---------------------------+
| Start Level (4 bytes)     |  int32 - First cached level (inclusive)
+---------------------------+
| End Level (4 bytes)       |  int32 - Last cached level (inclusive)
+---------------------------+
| Number of Levels          |  int32 - Count of cached levels
| (4 bytes)                 |  (EndLevel - StartLevel + 1)
+---------------------------+
| Level Data (repeated)     |  See "Level Data Structure" below
+---------------------------+
```

### Level Data Structure

For each cached level (from StartLevel to EndLevel):

```
+---------------------------+
| Level Number (4 bytes)    |  int32 - The level number in the tree
+---------------------------+
| Node Count (8 bytes)      |  int64 - Number of nodes at this level
+---------------------------+
| Node Data (variable)      |  NodeCount × HashSizeInBytes bytes
|                           |  Consecutive hash values, no padding
+---------------------------+
```

### Field Descriptions

#### Header Fields

- **Magic Number**: 4-byte identifier "MKTC" (0x4D, 0x4B, 0x54, 0x43)
  - Used to quickly identify cache files and detect corruption
  - ASCII representation: "MKTC" (Merkle Tree Cache)

- **Format Version**: 1-byte version number (currently 1)
  - Allows future format changes while maintaining compatibility
  - Deserialization validates this matches the expected version

- **Tree Height**: 4-byte signed integer (int32)
  - The total height of the Merkle tree
  - Height 0 = single leaf, height 1 = two leaves, etc.
  - Must be non-negative

- **Hash Function Name Length**: 4-byte signed integer (int32)
  - Length in bytes of the UTF-8 encoded hash function name
  - Must be between 0 and 1024 (validation limit)

- **Hash Function Name**: Variable-length UTF-8 string
  - Name/identifier of the hash function (e.g., "SHA256", "SHA512", "BLAKE3")
  - Used to ensure cache matches the tree's hash function
  - Case-sensitive

- **Hash Size In Bytes**: 4-byte signed integer (int32)
  - Fixed size of each hash value in bytes
  - Examples: 32 for SHA-256/BLAKE3, 64 for SHA-512
  - Must be positive

- **Start Level**: 4-byte signed integer (int32)
  - First level included in the cache (inclusive)
  - Level 0 represents leaf nodes
  - Must be non-negative

- **End Level**: 4-byte signed integer (int32)
  - Last level included in the cache (inclusive)
  - Must be ≥ StartLevel and < TreeHeight

- **Number of Levels**: 4-byte signed integer (int32)
  - Count of cached levels
  - Always equals (EndLevel - StartLevel + 1)
  - Used for validation during deserialization

#### Level Data Fields

- **Level Number**: 4-byte signed integer (int32)
  - The specific level number this data represents
  - Must be within [StartLevel, EndLevel]

- **Node Count**: 8-byte signed integer (int64)
  - Number of nodes at this level
  - Supports large trees with billions of nodes
  - Must be non-negative

- **Node Data**: Variable-length byte array
  - Total size: NodeCount × HashSizeInBytes
  - Consecutive hash values with no padding or separators
  - Each hash is exactly HashSizeInBytes long
  - Node ordering: left-to-right as they appear in the tree

## Random Access

The format supports efficient random access to nodes:

1. **Within a level**: All nodes are fixed-size (HashSizeInBytes), so node N at a level can be accessed at:
   ```
   offset = level_data_start + (N × HashSizeInBytes)
   ```

2. **Across levels**: Level data is stored sequentially, so a file index could be built to jump directly to any level.

## Platform Independence

The format ensures cross-platform compatibility:

- **Endianness**: All multi-byte integers use little-endian encoding
- **Text Encoding**: Hash function names use UTF-8
- **Fixed Sizes**: Hash sizes are explicitly stored, not assumed
- **No Alignment**: No padding or alignment requirements

## Validation

During deserialization, the following validations are performed:

1. **Magic Number**: Must match "MKTC" exactly
2. **Version**: Must be a supported version (currently only version 1)
3. **Data Length**: File must contain enough bytes for all declared fields
4. **Hash Function Name**: Length must be reasonable (0-1024 bytes)
5. **Hash Size**: Must be positive
6. **Level Range**: StartLevel ≤ EndLevel < TreeHeight
7. **Level Count**: Must match (EndLevel - StartLevel + 1)
8. **Level Numbers**: Each level must be in the expected range
9. **Node Counts**: Must be non-negative
10. **Complete Data**: All declared node data must be present
11. **No Extra Data**: File must not contain trailing bytes

## Usage Examples

### Creating Cache Data

```csharp
using MerkleTree.Cache;

// Create metadata
var metadata = new CacheMetadata(
    treeHeight: 10,
    hashFunctionName: "SHA256",
    hashSizeInBytes: 32,
    startLevel: 3,
    endLevel: 6
);

// Create levels with node data
var levels = new Dictionary<int, CachedLevel>();
for (int level = 3; level <= 6; level++)
{
    // Create sample nodes for this level
    int nodeCount = CalculateNodeCountAtLevel(level);
    byte[][] nodes = new byte[nodeCount][];
    for (int i = 0; i < nodeCount; i++)
    {
        nodes[i] = GetNodeHashAtLevel(level, i);
    }
    levels[level] = new CachedLevel(level, nodes);
}

// Create cache data
var cacheData = new CacheData(metadata, levels);
```

### Serializing to File

```csharp
using MerkleTree.Cache;

// Serialize to bytes
byte[] serialized = CacheSerializer.Serialize(cacheData);

// Write to file
File.WriteAllBytes("merkle_cache.bin", serialized);
```

### Deserializing from File

```csharp
using MerkleTree.Cache;

// Read from file
byte[] data = File.ReadAllBytes("merkle_cache.bin");

// Deserialize
CacheData cacheData = CacheSerializer.Deserialize(data);

// Access cached data
var level5 = cacheData.GetLevel(5);
byte[] nodeHash = level5.GetNode(42);
```

### Random Access Example

```csharp
// Access specific nodes efficiently
var metadata = cacheData.Metadata;
Console.WriteLine($"Cache contains levels {metadata.StartLevel} to {metadata.EndLevel}");

// Get all nodes at level 4
var level4 = cacheData.GetLevel(4);
for (long i = 0; i < level4.NodeCount; i++)
{
    byte[] hash = level4.GetNode(i);
    // Process hash...
}
```

## File Size Calculation

The size of a cache file can be calculated as:

```
Size = HeaderSize + SumOfLevelSizes

where:
  HeaderSize = 4 (magic) + 1 (version) + 4 (height) + 
               4 (name length) + NameLength + 
               4 (hash size) + 4 (start) + 4 (end) + 4 (count)
             = 29 + NameLength

  LevelSize = 4 (level number) + 8 (node count) + 
              (NodeCount × HashSizeInBytes)
```

### Example Sizes

For a cache with:
- Hash function: "SHA256" (7 bytes)
- Levels 3-6 (4 levels)
- 1000 nodes per level average
- Hash size: 32 bytes

```
HeaderSize ≈ 29 + 7 = 36 bytes
LevelSize ≈ 12 + (1000 × 32) = 32,012 bytes per level
TotalSize ≈ 36 + (4 × 32,012) = 128,084 bytes ≈ 125 KB
```

## Security Considerations

1. **No Authentication**: The format does not include checksums or digital signatures
   - Applications should implement their own integrity checks if needed
   - Consider using HMAC or digital signatures for cache files in untrusted environments

2. **Denial of Service**: Malicious files could declare extremely large node counts
   - Deserialization validates that declared data is present
   - Applications should enforce reasonable size limits

3. **Hash Function Identification**: The format stores the hash function name as a string
   - Applications must validate the hash function matches expectations
   - Do not blindly trust the hash function name

## Future Extensions

The format version allows for future enhancements while maintaining compatibility:

- **Version 2 could add**: Checksums, compression, partial level caching
- **Backward compatibility**: Older versions should reject newer format versions
- **Forward compatibility**: Newer versions can support reading older formats

## Reference Implementation

The reference implementation is provided in the `MerkleTree.Cache` namespace:

- **CacheMetadata**: Metadata structure
- **CachedLevel**: Single level data structure
- **CacheData**: Complete cache data structure
- **CacheSerializer**: Serialization/deserialization implementation

## Related Documentation

- [Proof Generation](PROOF_GENERATION.md) - How caches are used during proof generation
- [Proof Serialization](PROOF_SERIALIZATION.md) - Binary format for Merkle proofs
- [Streaming](STREAMING.md) - Large-scale tree construction

## Version History

- **Version 1** (Current): Initial format specification
  - Magic number "MKTC"
  - Little-endian encoding
  - UTF-8 hash function names
  - Fixed-size node records
  - Support for multiple hash functions
