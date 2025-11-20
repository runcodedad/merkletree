using System.Text;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Cache;
using MerkleTree.Core;

namespace MerkleTree.Tests.ErrorHandling;

/// <summary>
/// Tests for cache file error handling in BuildCacheFileAsync.
/// </summary>
public class CacheFileErrorHandlingTests
{
    [Fact]
    public async Task BuildCacheFileAsync_WithTruncatedLevelFile_ThrowsWithContext()
    {
        // Arrange - Create a temporary level file with truncated data
        var tempDir = Path.Combine(Path.GetTempPath(), $"merkletree_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var levelFile = Path.Combine(tempDir, "level_0.dat");
            var cacheFile = Path.Combine(tempDir, "cache.dat");
            
            // Write a truncated level file (say we claim 2 nodes but only write 1)
            await using (var fileStream = new FileStream(levelFile, FileMode.Create, FileAccess.Write))
            await using (var writer = new BinaryWriter(fileStream))
            {
                // Write first node
                writer.Write(32); // hash length
                writer.Write(new byte[32]); // hash data
                
                // Don't write second node, leaving file truncated
            }
            
            var allLevels = new List<(int level, string filePath, long nodeCount)>
            {
                (0, levelFile, 2) // Claim 2 nodes but file only has 1
            };
            
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CacheFileManager.BuildCacheFileAsync(
                    allLevels,
                    startLevel: 0,
                    endLevel: 0,
                    treeHeight: 1,
                    hashFunctionName: "SHA256",
                    hashSizeInBytes: 32,
                    cacheFile,
                    CancellationToken.None));
            
            Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("level 0", ex.Message);
            Assert.Contains("node 1", ex.Message); // Should mention which node failed
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BuildCacheFileAsync_WithInvalidHashLength_ThrowsWithContext()
    {
        // Arrange - Create a level file with negative hash length
        var tempDir = Path.Combine(Path.GetTempPath(), $"merkletree_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var levelFile = Path.Combine(tempDir, "level_0.dat");
            var cacheFile = Path.Combine(tempDir, "cache.dat");
            
            // Write a level file with invalid (negative) hash length
            await using (var fileStream = new FileStream(levelFile, FileMode.Create, FileAccess.Write))
            await using (var writer = new BinaryWriter(fileStream))
            {
                writer.Write(-32); // Invalid negative hash length
            }
            
            var allLevels = new List<(int level, string filePath, long nodeCount)>
            {
                (0, levelFile, 1)
            };
            
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CacheFileManager.BuildCacheFileAsync(
                    allLevels,
                    startLevel: 0,
                    endLevel: 0,
                    treeHeight: 1,
                    hashFunctionName: "SHA256",
                    hashSizeInBytes: 32,
                    cacheFile,
                    CancellationToken.None));
            
            Assert.Contains("invalid hash length", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("-32", ex.Message);
            Assert.Contains("level 0", ex.Message);
            Assert.Contains("node 0", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BuildCacheFileAsync_WithMismatchedHashSize_ThrowsWithContext()
    {
        // Arrange - Create a level file with wrong hash size
        var tempDir = Path.Combine(Path.GetTempPath(), $"merkletree_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var levelFile = Path.Combine(tempDir, "level_0.dat");
            var cacheFile = Path.Combine(tempDir, "cache.dat");
            
            // Write a level file with wrong hash size (16 instead of 32)
            await using (var fileStream = new FileStream(levelFile, FileMode.Create, FileAccess.Write))
            await using (var writer = new BinaryWriter(fileStream))
            {
                writer.Write(16); // Wrong hash length (expected 32)
                writer.Write(new byte[16]); // hash data
            }
            
            var allLevels = new List<(int level, string filePath, long nodeCount)>
            {
                (0, levelFile, 1)
            };
            
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CacheFileManager.BuildCacheFileAsync(
                    allLevels,
                    startLevel: 0,
                    endLevel: 0,
                    treeHeight: 1,
                    hashFunctionName: "SHA256",
                    hashSizeInBytes: 32,
                    cacheFile,
                    CancellationToken.None));
            
            Assert.Contains("hash length mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("expected 32", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("got 16", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("level 0", ex.Message);
            Assert.Contains("node 0", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BuildCacheFileAsync_WithPartialHashData_ThrowsWithContext()
    {
        // Arrange - Create a level file where hash data is truncated
        var tempDir = Path.Combine(Path.GetTempPath(), $"merkletree_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var levelFile = Path.Combine(tempDir, "level_0.dat");
            var cacheFile = Path.Combine(tempDir, "cache.dat");
            
            // Write a level file with truncated hash data
            await using (var fileStream = new FileStream(levelFile, FileMode.Create, FileAccess.Write))
            await using (var writer = new BinaryWriter(fileStream))
            {
                writer.Write(32); // Claim hash is 32 bytes
                writer.Write(new byte[16]); // But only write 16 bytes - file is truncated
            }
            
            var allLevels = new List<(int level, string filePath, long nodeCount)>
            {
                (0, levelFile, 1)
            };
            
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CacheFileManager.BuildCacheFileAsync(
                    allLevels,
                    startLevel: 0,
                    endLevel: 0,
                    treeHeight: 1,
                    hashFunctionName: "SHA256",
                    hashSizeInBytes: 32,
                    cacheFile,
                    CancellationToken.None));
            
            Assert.Contains("unexpected end of file", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("level 0", ex.Message);
            Assert.Contains("node 0", ex.Message);
            Assert.Contains("32 bytes", ex.Message); // Expected size
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BuildCacheFileAsync_WithNonexistentFile_ThrowsWithContext()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"merkletree_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var levelFile = Path.Combine(tempDir, "nonexistent.dat");
            var cacheFile = Path.Combine(tempDir, "cache.dat");
            
            var allLevels = new List<(int level, string filePath, long nodeCount)>
            {
                (0, levelFile, 1) // File doesn't exist
            };
            
            // Act & Assert
            // File I/O errors are wrapped in InvalidOperationException with context
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CacheFileManager.BuildCacheFileAsync(
                    allLevels,
                    startLevel: 0,
                    endLevel: 0,
                    treeHeight: 1,
                    hashFunctionName: "SHA256",
                    hashSizeInBytes: 32,
                    cacheFile,
                    CancellationToken.None));
            
            Assert.Contains("I/O error", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("level 0", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
