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

### Confirmed Root Cause

**Issue**: Keys with shared bit-path prefixes cause tree corruption

**Example**:
- Key 0: bit path `10101111` 
- Key 1: bit path `10100001` 
- They share the prefix `101000` (first 6 bits match)
- After inserting both keys, Key 0 remains retrievable but Key 1 is not found

**Analysis**:
When inserting Key 0 into an empty tree, internal nodes are created along its path with zero-hash siblings for empty branches. When inserting Key 1 which shares a prefix:

1. UpdatePathAsync traverses down following Key 1's bit path
2. At the point where paths diverge (level 6 in example), it finds a zero-hash
3. The algorithm treats this as an empty branch and creates new nodes
4. However, Key 0's leaf exists deeper in the tree on a different branch
5. The tree structure becomes inconsistent - Key 0's nodes still exist but are no longer reachable from the new root

**The Core Problem**:
The current implementation doesn't handle "tree splitting" when keys collide at a prefix level. In an SMT, when two keys share a prefix, the tree must be restructured to accommodate both leaves at their proper depths, with shared internal nodes for the common prefix and branching at the divergence point.

**What Should Happen**:
1. Detect when a new key's path intersects with an existing key's path
2. Restructure the tree to push both leaves to their correct full-depth positions
3. Create intermediate internal nodes connecting both branches
4. Ensure all nodes along both paths are properly maintained

**Current Behavior**:
Lines 884-893 in UpdatePathAsync treat any non-internal node as terminal, filling remaining siblings with zero-hashes. This works for empty trees but fails when an existing structure needs reorganization.

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
