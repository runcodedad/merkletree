using System.Text;
using Xunit;

namespace MerkleTree.Tests;

/// <summary>
/// Tests for the Blake3HashFunction class.
/// </summary>
public class Blake3HashFunctionTests
{
    /// <summary>
    /// Helper method to create test data.
    /// </summary>
    private static byte[] CreateTestData(string data)
    {
        return Encoding.UTF8.GetBytes(data);
    }

    [Fact]
    public void Name_ReturnsBLAKE3()
    {
        // Arrange
        var hashFunction = new Blake3HashFunction();

        // Act
        var name = hashFunction.Name;

        // Assert
        Assert.Equal("BLAKE3", name);
    }

    [Fact]
    public void HashSizeInBytes_Returns32()
    {
        // Arrange
        var hashFunction = new Blake3HashFunction();

        // Act
        var size = hashFunction.HashSizeInBytes;

        // Assert
        Assert.Equal(32, size);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void ComputeHash_ProducesDeterministicOutput()
    {
        // Arrange
        var hashFunction = new Blake3HashFunction();
        var data = CreateTestData("test data");

        // Act
        var hash1 = hashFunction.ComputeHash(data);
        var hash2 = hashFunction.ComputeHash(data);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(32, hash1.Length);
    }

    [Fact]
    public void ComputeHash_DifferentDataProducesDifferentHash()
    {
        // Arrange
        var hashFunction = new Blake3HashFunction();
        var data1 = CreateTestData("test data 1");
        var data2 = CreateTestData("test data 2");

        // Act
        var hash1 = hashFunction.ComputeHash(data1);
        var hash2 = hashFunction.ComputeHash(data2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ProducesDifferentHashThanSha256()
    {
        // Arrange
        var sha256 = new Sha256HashFunction();
        var blake3 = new Blake3HashFunction();
        var data = CreateTestData("test data");

        // Act
        var sha256Hash = sha256.ComputeHash(data);
        var blake3Hash = blake3.ComputeHash(data);

        // Assert
        Assert.NotEqual(sha256Hash, blake3Hash);
        Assert.Equal(32, sha256Hash.Length);
        Assert.Equal(32, blake3Hash.Length);
    }

    [Fact]
    public void ComputeHash_ReturnsCorrectLength()
    {
        // Arrange
        var hashFunction = new Blake3HashFunction();
        var data = CreateTestData("test");

        // Act
        var hash = hashFunction.ComputeHash(data);

        // Assert
        Assert.Equal(hashFunction.HashSizeInBytes, hash.Length);
    }
#else
    [Fact]
    public void ComputeHash_ThrowsPlatformNotSupportedOnNetStandard21()
    {
        // Arrange
        var hashFunction = new Blake3HashFunction();
        var data = CreateTestData("test data");

        // Act & Assert
        Assert.Throws<PlatformNotSupportedException>(() => hashFunction.ComputeHash(data));
    }
#endif
}
