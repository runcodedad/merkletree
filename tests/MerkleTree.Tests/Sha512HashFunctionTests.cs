using System.Text;
using Xunit;

namespace MerkleTree.Tests;

/// <summary>
/// Tests for the Sha512HashFunction class.
/// </summary>
public class Sha512HashFunctionTests
{
    /// <summary>
    /// Helper method to create test data.
    /// </summary>
    private static byte[] CreateTestData(string data)
    {
        return Encoding.UTF8.GetBytes(data);
    }

    [Fact]
    public void Name_ReturnsSHA512()
    {
        // Arrange
        var hashFunction = new Sha512HashFunction();

        // Act
        var name = hashFunction.Name;

        // Assert
        Assert.Equal("SHA-512", name);
    }

    [Fact]
    public void HashSizeInBytes_Returns64()
    {
        // Arrange
        var hashFunction = new Sha512HashFunction();

        // Act
        var size = hashFunction.HashSizeInBytes;

        // Assert
        Assert.Equal(64, size);
    }

    [Fact]
    public void ComputeHash_ProducesDeterministicOutput()
    {
        // Arrange
        var hashFunction = new Sha512HashFunction();
        var data = CreateTestData("test data");

        // Act
        var hash1 = hashFunction.ComputeHash(data);
        var hash2 = hashFunction.ComputeHash(data);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }

    [Fact]
    public void ComputeHash_DifferentDataProducesDifferentHash()
    {
        // Arrange
        var hashFunction = new Sha512HashFunction();
        var data1 = CreateTestData("test data 1");
        var data2 = CreateTestData("test data 2");

        // Act
        var hash1 = hashFunction.ComputeHash(data1);
        var hash2 = hashFunction.ComputeHash(data2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsCorrectLength()
    {
        // Arrange
        var hashFunction = new Sha512HashFunction();
        var data = CreateTestData("test");

        // Act
        var hash = hashFunction.ComputeHash(data);

        // Assert
        Assert.Equal(hashFunction.HashSizeInBytes, hash.Length);
    }
}
