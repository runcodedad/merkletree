# SMT Core Operations Implementation Notes

## Overview

This document describes the implementation of core SMT operations (Get, Update, Delete, Batch Update) and documents the current status and known issues.

## Implementation Approach

### Key Design Decisions

1. **Storage-Agnostic**: Operations return nodes to persist rather than persisting directly
2. **Copy-on-Write**: Only modified nodes are recreated and returned
3. **Deterministic**: Batch updates are sorted by key hash for consistent ordering
4. **Minimal Changes**: Only nodes along the update path are modified

### Components Implemented

1. **Result Types**:
   - `SmtGetResult`: Contains found flag and optional value
   - `SmtUpdateResult`: Contains new root hash and list of nodes to persist
   - `SmtKeyValue`: Represents a key-value pair for batch operations

2. **Serialization**:
   - `SmtNodeSerializer`: Binary serialization with little-endian encoding
   - Preserves node type, hashes, and data
   - Deterministic and platform-independent

3. **Core Operations**:
   - `GetAsync`: Traverses tree from root following bit path
   - `UpdateAsync`: Creates new leaf and reconstructs path with copy-on-write
   - `DeleteAsync`: Replaces leaf with empty node
   - `BatchUpdateAsync`: Applies multiple updates with sorted, deterministic ordering

## Algorithm Details

### UpdateAsync Flow

1. Create new leaf node with key and value
2. Add leaf to nodes-to-persist list
3. Call UpdatePathAsync to reconstruct tree path
4. Return new root hash and nodes-to-persist

### UpdatePathAsync Algorithm

This is the core tree reconstruction algorithm:

1. **Collect Siblings** (Top-Down Traversal):
   - Start at root and traverse down following bit path
   - At each level, collect the sibling hash (opposite direction from bit path)
   - If node not found or is zero hash, use zero hash from table
   - Store siblings in array indexed by tree level

2. **Reconstruct Path** (Bottom-Up):
   - Start with leaf hash
   - For each level from (Depth-1) down to 0:
     - Get sibling hash for this level
     - Create internal node with current hash and sibling
     - Add internal node to nodes-to-persist
     - Current hash becomes the new internal node's hash
   - Return final hash (new root)

### GetAsync Flow

1. Start at root hash
2. For each level from 0 to (Depth-1):
   - Check if current hash is zero hash (key not present)
   - Read node from storage
   - If node is leaf, check if key hash matches
   - If node is internal, follow bit path direction
3. Return found/not-found result

## Current Issues

### Test Failures (8 out of 22 failing)

All failing tests involve retrieving values after insertion:
- `GetAsync_AfterUpdate_ReturnsValue`
- `UpdateAsync_ExistingKey_UpdatesValue`
- `UpdateAsync_MultipleKeys_CreatesCorrectTree`
- `DeleteAsync_OneOfMultipleKeys_LeavesOthers`
- `Update_CopyOnWrite_OldRootStillValid`
- `BatchUpdateAsync_MultipleUpdates_AllApplied`
- `BatchUpdateAsync_WithDeletes_AppliesCorrectly`
- `BatchUpdateAsync_ConflictingUpdates_LastWriteWins`

### Confirmed Root Cause: Depth-Limited Collision Handling

**Critical Issue**: Keys with identical depth-bit prefixes cannot coexist in the tree

**The Fundamental Problem**:
The current SMT implementation uses a fixed depth D and only creates D levels of internal nodes (levels 0 through D-1). When two keys have IDENTICAL bit paths through all D levels (same D-bit prefix), only the most recently inserted key remains accessible.

**Concrete Example**:
- Tree depth: 8
- Key 1: bit path `10100001 1111...` (first 8 bits)
- Key 2: bit path `10100001 0111...` (SAME first 8 bits, diverges at bit 8)
- After inserting Key 1: retrievable ✓
- After inserting Key 2: Key 2 retrievable ✓, but Key 1 NOT retrievable ✗

**What Happens During Key 2 Insert**:
1. Traversal follows the path `10100001` through levels 0-7
2. At each level, both keys go the same direction (share all 8 bits)
3. Siblings are collected correctly (zero-hashes since path matches)
4. Reconstruction creates new internal nodes at levels 0-7
5. At level 7, new node is created with children based on bit 7
6. But both keys have the same bit 7! So level 7 node points to Key 2's leaf
7. Key 1's leaf is still in storage but unreachable from new root

**Why Immediate Retrieval Works**:
Immediate retrieval after Key 2 insert works because the tree is freshly built for Key 2. But Key 1's path through the tree has been overwritten by Key 2's path.

**Root Cause Analysis**:
The implementation doesn't extend beyond depth D. When keys have paths that diverge at bit D or later, the tree structure can't accommodate both keys. Only D levels of internal nodes are created (0 through D-1), and leaves are conceptually at level D. Two keys with identical D-bit prefixes need to diverge at level D or deeper, but the tree doesn't create nodes that deep.

**Current Behavior**:
- Insert creates D internal nodes + 1 leaf (9 nodes for depth 8)
- Each insert with the same D-bit prefix overwrites the previous tree path
- Old leaves remain in storage but are orphaned (unreachable from new root)
- Copy-on-write creates new nodes, but doesn't preserve paths for colliding prefixes

**What Should Happen** (Standard SMT Design):
1. When two keys have the same D-bit prefix, create internal nodes beyond level D-1
2. Continue creating internal nodes until paths diverge
3. Both leaves become children (directly or indirectly) of the divergence point
4. Tree effectively has depth > D for certain branches

**Alternative Solutions**:
1. **Dynamic Depth Extension**: Allow tree to grow beyond configured depth when needed
2. **Leaf Chaining**: At collision points, use a linked list or secondary structure
3. **Deeper Tree**: Use larger fixed depth (e.g., 256 for SHA-256) to minimize collisions
4. **Error on Collision**: Detect and reject keys with identical D-bit prefixes

**Test Coverage**:
- `SmtCollisionTest.TwoKeysWithIdenticalDepthBitPrefix_BothShouldBeRetrievable`: Demonstrates the bug
- `SmtPropertyTests.Property_MultipleInserts_AllKeysRetrievable`: Fails due to this issue
- `SmtDebugTest.Debug_FiveInserts_AllShouldBeRetrievable`: Shows Keys 1 & 2 collision

## Next Steps for Debugging

1. **Add Logging**: Temporarily add console output to track:
   - Number of nodes created
   - Hash of each node created
   - Traversal path in GetAsync
   - Which nodes are found/not found in storage

2. **Simplify Test Case**: Create minimal test with depth=2 or depth=3
   - Easier to trace through manually
   - Can verify expected tree structure by hand

3. **Test Serialization in Isolation**: Verify serialize/deserialize works correctly
   - Create node, serialize, deserialize, compare all fields

4. **Manual Tree Construction**: Build a simple tree by hand and verify:
   - Create leaf manually
   - Create internal nodes manually
   - Store in storage
   - Try to retrieve with GetAsync

5. **Compare with Reference Implementation**: If available, compare algorithm
   - Check how other SMT implementations handle zero hashes
   - Verify bit path interpretation

## Code Locations

- Core operations: `src/MerkleTree/Smt/SparseMerkleTree.cs` (lines 447-863)
- Result types: `src/MerkleTree/Smt/SmtGetResult.cs`, `SmtUpdateResult.cs`, `SmtKeyValue.cs`
- Serialization: `src/MerkleTree/Smt/SmtNodeSerializer.cs`
- Tests: `tests/MerkleTree.Tests/Smt/SmtOperationsTests.cs`

## Testing Status

- Total tests: 22
- Passing: 14
- Failing: 8
- All passing tests are for:
  - Null/empty input validation
  - Empty tree operations
  - Deterministic batch ordering
  - Single operations without subsequent reads

All failing tests involve reading back inserted data, suggesting the core read/write cycle has a bug.
