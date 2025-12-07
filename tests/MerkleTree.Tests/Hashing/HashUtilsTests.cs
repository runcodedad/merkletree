using System.Text;
using Xunit;
using MerkleTree.Hashing;

namespace MerkleTree.Tests.Hashing;

/// <summary>
/// Tests for the HashUtils utility class.
/// </summary>
public class HashUtilsTests
{
    [Fact]
    public void GetBitPath_WithValidKey_ReturnsCorrectPath()
    {
        // Arrange
        // Binary: 10101010 = 0xAA
        var key = new byte[] { 0xAA };
        
        // Act
        var path = HashUtils.GetBitPath(key, 8);
        
        // Assert
        Assert.Equal(8, path.Length);
        Assert.True(path[0]);  // 1
        Assert.False(path[1]); // 0
        Assert.True(path[2]);  // 1
        Assert.False(path[3]); // 0
        Assert.True(path[4]);  // 1
        Assert.False(path[5]); // 0
        Assert.True(path[6]);  // 1
        Assert.False(path[7]); // 0
    }

    [Fact]
    public void GetBitPath_WithMultipleBytes_ReturnsCorrectPath()
    {
        // Arrange
        // Binary: 11110000 01010101 = 0xF0 0x55
        var key = new byte[] { 0xF0, 0x55 };
        
        // Act
        var path = HashUtils.GetBitPath(key, 16);
        
        // Assert
        Assert.Equal(16, path.Length);
        
        // First byte: 11110000
        Assert.True(path[0]);
        Assert.True(path[1]);
        Assert.True(path[2]);
        Assert.True(path[3]);
        Assert.False(path[4]);
        Assert.False(path[5]);
        Assert.False(path[6]);
        Assert.False(path[7]);
        
        // Second byte: 01010101
        Assert.False(path[8]);
        Assert.True(path[9]);
        Assert.False(path[10]);
        Assert.True(path[11]);
        Assert.False(path[12]);
        Assert.True(path[13]);
        Assert.False(path[14]);
        Assert.True(path[15]);
    }

    [Fact]
    public void GetBitPath_WithPartialLength_ReturnsOnlyRequestedBits()
    {
        // Arrange
        var key = new byte[] { 0xFF, 0x00 }; // 11111111 00000000
        
        // Act - Request only 8 bits
        var path = HashUtils.GetBitPath(key, 8);
        
        // Assert
        Assert.Equal(8, path.Length);
        Assert.All(path, bit => Assert.True(bit)); // First byte is all 1s
    }

    [Fact]
    public void GetBitPath_WithFullLength_ReturnsAllBits()
    {
        // Arrange
        var key = new byte[] { 0xAA, 0x55 };
        
        // Act
        var path = HashUtils.GetBitPath(key); // No length specified
        
        // Assert
        Assert.Equal(16, path.Length);
    }

    [Fact]
    public void GetBitPath_WithNullKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => HashUtils.GetBitPath(null!, 8));
    }

    [Fact]
    public void GetBitPath_WithEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var key = Array.Empty<byte>();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => HashUtils.GetBitPath(key, 8));
    }

    [Fact]
    public void GetBitPath_WithZeroBitLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var key = new byte[] { 0xAA };
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => HashUtils.GetBitPath(key, 0));
    }

    [Fact]
    public void GetBitPath_WithNegativeBitLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var key = new byte[] { 0xAA };
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => HashUtils.GetBitPath(key, -1));
    }

    [Fact]
    public void GetBitPath_WithBitLengthExceedingKeySize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var key = new byte[] { 0xAA }; // 8 bits
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => HashUtils.GetBitPath(key, 9));
    }

    [Fact]
    public void GetBitPath_IsDeterministic()
    {
        // Arrange
        var key = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        
        // Act
        var path1 = HashUtils.GetBitPath(key, 32);
        var path2 = HashUtils.GetBitPath(key, 32);
        
        // Assert
        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetBitPath_WithHashOutput_ProducesValidPath()
    {
        // This tests the primary use case: deriving a bit path from a hash
        
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var data = Encoding.UTF8.GetBytes("test-key");
        var hash = hashFunction.ComputeHash(data);
        
        // Act
        var path = HashUtils.GetBitPath(hash, 256); // SHA-256 produces 256 bits
        
        // Assert
        Assert.Equal(256, path.Length);
        Assert.NotNull(path);
    }

    [Fact]
    public void GetBitPath_DifferentKeysProduceDifferentPaths()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var key1 = Encoding.UTF8.GetBytes("key1");
        var key2 = Encoding.UTF8.GetBytes("key2");
        var hash1 = hashFunction.ComputeHash(key1);
        var hash2 = hashFunction.ComputeHash(key2);
        
        // Act
        var path1 = HashUtils.GetBitPath(hash1, 256);
        var path2 = HashUtils.GetBitPath(hash2, 256);
        
        // Assert
        Assert.NotEqual(path1, path2);
    }

    [Fact]
    public void GetBitPath_FirstBitsMatchExpectedPattern()
    {
        // Arrange - Create a key where we know the bit pattern
        var key = new byte[] { 0x80 }; // Binary: 10000000
        
        // Act
        var path = HashUtils.GetBitPath(key, 8);
        
        // Assert - First bit should be 1, rest should be 0
        Assert.True(path[0]);
        for (int i = 1; i < 8; i++)
        {
            Assert.False(path[i]);
        }
    }

    [Fact]
    public void GetBitPath_CanExtractSingleBit()
    {
        // Arrange
        var key = new byte[] { 0xFF }; // All bits set
        
        // Act
        var path = HashUtils.GetBitPath(key, 1);
        
        // Assert
        Assert.Single(path);
        Assert.True(path[0]); // MSB is 1
    }

    [Fact]
    public void GetBitPath_WorksWithLargeKeys()
    {
        // Arrange - Simulate a SHA-512 hash (64 bytes)
        var key = new byte[64];
        for (int i = 0; i < 64; i++)
        {
            key[i] = (byte)(i % 256);
        }
        
        // Act
        var path = HashUtils.GetBitPath(key, 512);
        
        // Assert
        Assert.Equal(512, path.Length);
    }

    [Fact]
    public void GetBitPath_AllZeros_ProducesAllFalsePath()
    {
        // Arrange
        var key = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        
        // Act
        var path = HashUtils.GetBitPath(key, 32);
        
        // Assert
        Assert.Equal(32, path.Length);
        Assert.All(path, bit => Assert.False(bit));
    }

    [Fact]
    public void GetBitPath_AllOnes_ProducesAllTruePath()
    {
        // Arrange
        var key = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        
        // Act
        var path = HashUtils.GetBitPath(key, 32);
        
        // Assert
        Assert.Equal(32, path.Length);
        Assert.All(path, bit => Assert.True(bit));
    }
}
