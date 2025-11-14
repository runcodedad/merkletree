# Implement Core Merkle Tree Building from Streaming Input

## Description:
Implement the ability to build a Merkle tree from a large collection of fixed-size leaf values using streaming/chunked input, without requiring the entire dataset to reside in memory.

### Requirements:
- Accept leaves as fixed-size binary blobs (e.g., 32 bytes)
- Support reading leaves from streams, chunked file reads, and memory buffers
- Build Level 0 (leaves) incrementally without loading entire dataset in RAM
- Build upper levels incrementally by reading two children, hashing into parent, and emitting to next level
- Continue processing until reaching the root
- Output final Merkle root and metadata (tree height, leaf count)

### Acceptance Criteria:
- Can process datasets larger than available RAM
- Supports incremental leaf batch processing
- Produces deterministic Merkle root
- Returns tree metadata (height, leaf count)