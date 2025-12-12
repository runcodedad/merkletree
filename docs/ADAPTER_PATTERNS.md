# Storage Adapter Patterns

This document provides reference architectural patterns for implementing storage adapters for the MerkleTree library. These patterns ensure the core library remains storage-agnostic and portable.

## Table of Contents

- [Overview](#overview)
- [Core Interfaces](#core-interfaces)
- [Adapter Pattern Overview](#adapter-pattern-overview)
- [Reference Patterns](#reference-patterns)
- [Best Practices](#best-practices)
- [Error Handling](#error-handling)
- [Testing Adapters](#testing-adapters)

## Overview

The MerkleTree library defines storage abstractions through interfaces. Users implement these interfaces to connect the library to their chosen storage backend. This architecture provides:

- **Portability**: Use any storage technology without modifying the core library
- **Flexibility**: Swap storage implementations without changing application code
- **Testability**: Mock storage for unit tests
- **No vendor lock-in**: Not tied to specific databases or cloud providers

## Core Interfaces

The Sparse Merkle Tree (SMT) implementation uses four persistence interfaces:

### ISmtNodeReader
Reads nodes from storage by hash or path.

**Key methods:**
- `ReadNodeByHashAsync(hash)` - Retrieve node by its hash
- `ReadNodeByPathAsync(path)` - Retrieve node by tree path
- `NodeExistsAsync(hash)` - Check if node exists

### ISmtNodeWriter
Writes nodes to storage.

**Key methods:**
- `WriteBatchAsync(nodes)` - Write multiple nodes atomically
- `WriteNodeAsync(node)` - Write single node
- `FlushAsync()` - Ensure durability

### ISmtSnapshotManager
Manages tree snapshots for versioning.

**Key methods:**
- `CreateSnapshotAsync(name, rootHash)` - Create named snapshot
- `GetSnapshotAsync(name)` - Retrieve snapshot
- `ListSnapshotsAsync()` - List all snapshots
- `DeleteSnapshotAsync(name)` - Delete snapshot

### ISmtMetadataStore
Stores tree metadata (hash algorithm, depth, zero hashes).

**Key methods:**
- `ReadMetadataAsync()` - Read metadata
- `WriteMetadataAsync(metadata)` - Write metadata

## Adapter Pattern Overview

```
┌──────────────────────────────────────────────────┐
│         MerkleTree Core Library                  │
│  (Storage-agnostic, defines interfaces only)     │
└──────────────────┬───────────────────────────────┘
                   │ Defines interfaces:
                   │ ISmtNodeReader
                   │ ISmtNodeWriter
                   │ ISmtSnapshotManager
                   │ ISmtMetadataStore
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│         Your Application Code                    │
│  (Implements adapters for your storage)          │
└──────────────────┬───────────────────────────────┘
                   │ Implements interfaces
                   │ using your chosen storage
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│      Your Storage Backend                        │
│  (Database, Cloud, File System, etc.)            │
└──────────────────────────────────────────────────┘
```

## Reference Patterns

### Pattern 1: In-Memory Adapter (Reference Implementation)

**Use case:** Testing, development, small datasets

**Structure:**
```csharp
public class InMemorySmtStorage : 
    ISmtNodeReader, 
    ISmtNodeWriter, 
    ISmtSnapshotManager, 
    ISmtMetadataStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, byte[]> _nodesByHash = new();
    private readonly Dictionary<string, byte[]> _nodesByPath = new();
    private readonly Dictionary<string, SmtSnapshotInfo> _snapshots = new();
    private SmtMetadata? _metadata;
    
    // Implement all interface methods using in-memory dictionaries
}
```

**Characteristics:**
- All data stored in memory (dictionaries)
- Thread-safe with lock-based synchronization
- Fast for small datasets
- Data lost when process exits (not durable)
- Provided by library as reference implementation

### Pattern 2: Relational Database Adapter

**Use case:** Production applications, ACID requirements, complex queries

**Structure (Conceptual - User implements):**
```csharp
// User's code - not in MerkleTree library
public class SqlServerSmtAdapter : 
    ISmtNodeReader, 
    ISmtNodeWriter, 
    ISmtSnapshotManager, 
    ISmtMetadataStore
{
    private readonly string _connectionString;
    private readonly IDbConnection _connection;
    
    public SqlServerSmtAdapter(string connectionString)
    {
        _connectionString = connectionString;
        // Initialize connection
    }
    
    public async Task<SmtNodeBlob?> ReadNodeByHashAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken ct = default)
    {
        try
        {
            // Execute SQL query to retrieve node
            var sql = "SELECT NodeType, Hash, Data FROM SmtNodes WHERE Hash = @Hash";
            // ... execute query, map results to SmtNodeBlob
        }
        catch (SqlException ex)
        {
            throw new StorageAdapterException("DATABASE_ERROR", 
                "Failed to read node from database", ex);
        }
    }
    
    public async Task WriteBatchAsync(
        IReadOnlyList<SmtNodeBlob> nodes,
        CancellationToken ct = default)
    {
        try
        {
            // Use transaction for atomicity
            using var transaction = await _connection.BeginTransactionAsync(ct);
            
            // Bulk insert or upsert nodes
            foreach (var node in nodes)
            {
                var sql = @"
                    INSERT INTO SmtNodes (Hash, NodeType, Data) 
                    VALUES (@Hash, @NodeType, @Data)
                    ON CONFLICT (Hash) DO UPDATE SET Data = @Data";
                // ... execute for each node
            }
            
            await transaction.CommitAsync(ct);
        }
        catch (SqlException ex)
        {
            throw new StorageAdapterException("DATABASE_ERROR", 
                "Failed to write batch to database", ex);
        }
    }
    
    // Implement other interface methods...
}
```

**Database Schema Example:**
```sql
CREATE TABLE SmtNodes (
    Hash BINARY(32) PRIMARY KEY,
    NodeType INT NOT NULL,
    Data VARBINARY(MAX),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    INDEX IX_NodeType (NodeType)
);

CREATE TABLE SmtSnapshots (
    Name NVARCHAR(255) PRIMARY KEY,
    RootHash BINARY(32) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    Description NVARCHAR(MAX)
);

CREATE TABLE SmtMetadata (
    Id INT PRIMARY KEY DEFAULT 1,
    HashAlgorithm NVARCHAR(50) NOT NULL,
    TreeDepth INT NOT NULL,
    ZeroHashTable VARBINARY(MAX) NOT NULL,
    Version INT NOT NULL
);
```

### Pattern 3: Document Database Adapter

**Use case:** Schema-less storage, JSON documents, horizontal scaling

**Structure (Conceptual - User implements):**
```csharp
// User's code - not in MerkleTree library
public class MongoDbSmtAdapter : 
    ISmtNodeReader, 
    ISmtNodeWriter, 
    ISmtSnapshotManager, 
    ISmtMetadataStore
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<NodeDocument> _nodes;
    private readonly IMongoCollection<SnapshotDocument> _snapshots;
    private readonly IMongoCollection<MetadataDocument> _metadata;
    
    public MongoDbSmtAdapter(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
        _nodes = _database.GetCollection<NodeDocument>("nodes");
        _snapshots = _database.GetCollection<SnapshotDocument>("snapshots");
        _metadata = _database.GetCollection<MetadataDocument>("metadata");
        
        // Create indexes
        CreateIndexes();
    }
    
    private void CreateIndexes()
    {
        // Index on hash for fast lookups
        _nodes.Indexes.CreateOne(
            new CreateIndexModel<NodeDocument>(
                Builders<NodeDocument>.IndexKeys.Ascending(x => x.Hash),
                new CreateIndexOptions { Unique = true }
            )
        );
    }
    
    public async Task<SmtNodeBlob?> ReadNodeByHashAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken ct = default)
    {
        try
        {
            var hashBase64 = Convert.ToBase64String(hash.ToArray());
            var filter = Builders<NodeDocument>.Filter.Eq(x => x.Hash, hashBase64);
            var doc = await _nodes.Find(filter).FirstOrDefaultAsync(ct);
            
            return doc != null ? ConvertToBlob(doc) : null;
        }
        catch (MongoException ex)
        {
            throw new StorageAdapterException("DATABASE_ERROR", 
                "Failed to read node from MongoDB", ex);
        }
    }
    
    // Document models
    private class NodeDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public int NodeType { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
    
    // Implement other interface methods...
}
```

### Pattern 4: Cloud Storage Adapter

**Use case:** Cloud-native applications, serverless, distributed systems

**Structure (Conceptual - User implements):**
```csharp
// User's code - not in MerkleTree library
public class S3SmtAdapter : 
    ISmtNodeReader, 
    ISmtNodeWriter, 
    ISmtSnapshotManager, 
    ISmtMetadataStore
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _prefix;
    
    public S3SmtAdapter(string bucketName, string prefix = "smt/")
    {
        _s3Client = new AmazonS3Client();
        _bucketName = bucketName;
        _prefix = prefix;
    }
    
    public async Task<SmtNodeBlob?> ReadNodeByHashAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken ct = default)
    {
        try
        {
            var key = $"{_prefix}nodes/{Convert.ToBase64String(hash.ToArray())}";
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };
            
            var response = await _s3Client.GetObjectAsync(request, ct);
            
            using var stream = response.ResponseStream;
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, ct);
            
            return DeserializeBlob(memoryStream.ToArray());
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null; // Node doesn't exist
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageAdapterException("CLOUD_STORAGE_ERROR", 
                "Failed to read node from S3", ex);
        }
    }
    
    public async Task WriteBatchAsync(
        IReadOnlyList<SmtNodeBlob> nodes,
        CancellationToken ct = default)
    {
        try
        {
            // S3 doesn't have native batch operations
            // Write nodes in parallel for better performance
            var tasks = nodes.Select(node => WriteNodeToS3Async(node, ct));
            await Task.WhenAll(tasks);
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageAdapterException("CLOUD_STORAGE_ERROR", 
                "Failed to write batch to S3", ex);
        }
    }
    
    private async Task WriteNodeToS3Async(SmtNodeBlob node, CancellationToken ct)
    {
        var hashBase64 = Convert.ToBase64String(node.Hash.ToArray());
        var key = $"{_prefix}nodes/{hashBase64}";
        
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            ContentBody = SerializeBlob(node),
            ContentType = "application/octet-stream"
        };
        
        await _s3Client.PutObjectAsync(request, ct);
    }
    
    // Implement other interface methods...
}
```

### Pattern 5: Hybrid Adapter

**Use case:** Combine multiple storage backends (cache + persistent store)

**Structure (Conceptual - User implements):**
```csharp
// User's code - not in MerkleTree library
public class CachedDatabaseAdapter : 
    ISmtNodeReader, 
    ISmtNodeWriter, 
    ISmtSnapshotManager, 
    ISmtMetadataStore
{
    private readonly ISmtNodeReader _persistentReader;
    private readonly ISmtNodeWriter _persistentWriter;
    private readonly IMemoryCache _cache;
    
    public CachedDatabaseAdapter(
        ISmtNodeReader persistentReader,
        ISmtNodeWriter persistentWriter,
        IMemoryCache cache)
    {
        _persistentReader = persistentReader;
        _persistentWriter = persistentWriter;
        _cache = cache;
    }
    
    public async Task<SmtNodeBlob?> ReadNodeByHashAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken ct = default)
    {
        var cacheKey = Convert.ToBase64String(hash.ToArray());
        
        // Try cache first
        if (_cache.TryGetValue(cacheKey, out SmtNodeBlob? cachedNode))
        {
            return cachedNode;
        }
        
        // Cache miss - read from persistent storage
        var node = await _persistentReader.ReadNodeByHashAsync(hash, ct);
        
        // Add to cache if found
        if (node != null)
        {
            _cache.Set(cacheKey, node, TimeSpan.FromMinutes(10));
        }
        
        return node;
    }
    
    public async Task WriteBatchAsync(
        IReadOnlyList<SmtNodeBlob> nodes,
        CancellationToken ct = default)
    {
        // Write to persistent storage
        await _persistentWriter.WriteBatchAsync(nodes, ct);
        
        // Update cache
        foreach (var node in nodes)
        {
            var cacheKey = Convert.ToBase64String(node.Hash.ToArray());
            _cache.Set(cacheKey, node, TimeSpan.FromMinutes(10));
        }
    }
    
    // Implement other interface methods...
}
```

## Best Practices

### 1. Thread Safety

All adapter implementations must be thread-safe:
- Concurrent reads should always be safe
- Write operations should use appropriate synchronization
- Consider using connection pools for database adapters

```csharp
// Good: Thread-safe with lock
private readonly object _lock = new();

public async Task WriteBatchAsync(...)
{
    lock (_lock)
    {
        // Write operations
    }
}
```

### 2. Idempotency

Write operations should be idempotent:
- Writing the same node multiple times should not error
- Use UPSERT operations (INSERT ... ON CONFLICT UPDATE)
- Hash-based storage is naturally idempotent

```sql
-- Good: Idempotent upsert
INSERT INTO SmtNodes (Hash, NodeType, Data) 
VALUES (@Hash, @NodeType, @Data)
ON CONFLICT (Hash) DO UPDATE SET Data = @Data;
```

### 3. Atomicity

Batch writes should be atomic when possible:
- Use database transactions
- Implement rollback on partial failure
- Document consistency guarantees if not fully atomic

```csharp
// Good: Atomic batch write with transaction
using var transaction = await connection.BeginTransactionAsync();
try
{
    foreach (var node in nodes)
    {
        await WriteNodeAsync(node, transaction);
    }
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 4. Error Handling

Wrap storage-specific errors in `StorageAdapterException`:

```csharp
public async Task<SmtNodeBlob?> ReadNodeByHashAsync(...)
{
    try
    {
        // Storage operation
    }
    catch (SqlException ex)
    {
        throw new StorageAdapterException(
            errorCode: "DATABASE_ERROR",
            message: "Failed to read node from SQL Server",
            innerException: ex
        );
    }
    catch (IOException ex)
    {
        throw new StorageAdapterException(
            errorCode: "IO_ERROR",
            message: "Failed to read node from file system",
            innerException: ex
        );
    }
}
```

### 5. Resource Management

Properly manage connections and resources:

```csharp
// Good: Implement IAsyncDisposable for cleanup
public class DatabaseAdapter : ISmtNodeReader, IAsyncDisposable
{
    private readonly DbConnection _connection;
    
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}

// Usage
await using var adapter = new DatabaseAdapter(connectionString);
var tree = new SparseMerkleTree(hashFunction, adapter, ...);
```

### 6. Performance Optimization

Optimize for common operations:
- Use indexes on hash columns
- Batch operations when possible
- Implement connection pooling
- Consider caching for frequently accessed nodes
- Use async I/O throughout

## Error Handling

### Standard Error Codes

Use consistent error codes in `StorageAdapterException`:

- `"IO_ERROR"` - File system errors
- `"DATABASE_ERROR"` - Database connection or query errors
- `"NETWORK_ERROR"` - Network communication errors
- `"CLOUD_STORAGE_ERROR"` - Cloud provider errors
- `"SERIALIZATION_ERROR"` - Data serialization/deserialization errors
- `"AUTHENTICATION_ERROR"` - Authentication failures
- `"AUTHORIZATION_ERROR"` - Permission denied
- `"STORAGE_FULL"` - Out of storage space
- `"CORRUPTION_ERROR"` - Data corruption detected

### Error Handling Pattern

```csharp
public async Task<SmtNodeBlob?> ReadNodeByHashAsync(
    ReadOnlyMemory<byte> hash,
    CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(hash);
    
    if (hash.Length == 0)
        throw new ArgumentException("Hash cannot be empty", nameof(hash));
    
    try
    {
        // Storage operation
        return await PerformStorageRead(hash, ct);
    }
    catch (OperationCanceledException)
    {
        // Let cancellation exceptions propagate
        throw;
    }
    catch (StorageAdapterException)
    {
        // Already wrapped - propagate
        throw;
    }
    catch (Exception ex) when (IsTransientError(ex))
    {
        // Wrap transient errors for retry logic
        throw new StorageAdapterException(
            "TRANSIENT_ERROR",
            "Temporary storage error, may succeed on retry",
            ex
        );
    }
    catch (Exception ex)
    {
        // Wrap unexpected errors
        throw new StorageAdapterException(
            "UNKNOWN_ERROR",
            "Unexpected error reading node",
            ex
        );
    }
}
```

## Testing Adapters

### Unit Testing Pattern

```csharp
public class DatabaseAdapterTests
{
    [Fact]
    public async Task ReadNodeByHashAsync_ExistingNode_ReturnsNode()
    {
        // Arrange
        var adapter = new SqlServerSmtAdapter(testConnectionString);
        var hash = new byte[32];
        RandomNumberGenerator.Fill(hash);
        
        var originalNode = new SmtNodeBlob(
            SmtNodeType.Leaf,
            hash,
            new byte[] { 1, 2, 3 }
        );
        
        await adapter.WriteNodeAsync(originalNode);
        
        // Act
        var retrievedNode = await adapter.ReadNodeByHashAsync(hash);
        
        // Assert
        Assert.NotNull(retrievedNode);
        Assert.Equal(originalNode.Hash.ToArray(), retrievedNode.Hash.ToArray());
        Assert.Equal(originalNode.Data.ToArray(), retrievedNode.Data.ToArray());
    }
    
    [Fact]
    public async Task WriteBatchAsync_DuplicateNodes_IsIdempotent()
    {
        // Arrange
        var adapter = new SqlServerSmtAdapter(testConnectionString);
        var node = CreateTestNode();
        
        // Act - Write same node twice
        await adapter.WriteBatchAsync(new[] { node });
        await adapter.WriteBatchAsync(new[] { node });
        
        // Assert - Should not throw, node should exist once
        var retrieved = await adapter.ReadNodeByHashAsync(node.Hash);
        Assert.NotNull(retrieved);
    }
}
```

### Integration Testing

Test adapters with the actual SMT implementation:

```csharp
[Fact]
public async Task SparseMerkleTree_WithDatabaseAdapter_WorksEndToEnd()
{
    // Arrange
    var adapter = new SqlServerSmtAdapter(testConnectionString);
    var hashFunction = new Sha256HashFunction();
    var tree = new SparseMerkleTree(
        hashFunction, 
        adapter,  // reader
        adapter,  // writer
        adapter,  // snapshot manager
        adapter   // metadata store
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

## Summary

- **Core library defines interfaces only** - no storage implementation
- **Users implement adapters** - for their specific storage needs
- **Multiple patterns available** - in-memory, SQL, NoSQL, cloud, hybrid
- **Best practices** - thread safety, idempotency, atomicity, error handling
- **Thorough testing** - unit tests for adapter, integration tests with SMT

This architecture ensures the MerkleTree library remains:
- ✅ Storage-agnostic
- ✅ Blockchain-neutral
- ✅ Portable across environments
- ✅ Testable without external dependencies
- ✅ Flexible for any use case
