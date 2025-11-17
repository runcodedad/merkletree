using System.Text;
using Xunit;
using MerkleTree.Hashing;

namespace MerkleTree.Tests.Hashing;

/// <summary>
/// Tests for the Sha256HashFunction class.
/// </summary>
public class Sha256HashFunctionTests
{
    /// <summary>
    /// Helper method to create test data.
    /// </summary>
    private static byte[] CreateTestData(string data)
    {
        return Encoding.UTF8.GetBytes(data);
    }

    [Fact]
    public void Name_ReturnsSHA256()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act
        var name = hashFunction.Name;

        // Assert
        Assert.Equal("SHA-256", name);
    }

    [Fact]
    public void HashSizeInBytes_Returns32()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act
        var size = hashFunction.HashSizeInBytes;

        // Assert
        Assert.Equal(32, size);
    }

    [Fact]
    public void ComputeHash_ProducesDeterministicOutput()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
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
        var hashFunction = new Sha256HashFunction();
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
        var hashFunction = new Sha256HashFunction();
        var data = CreateTestData("test");

        // Act
        var hash = hashFunction.ComputeHash(data);

        // Assert
        Assert.Equal(hashFunction.HashSizeInBytes, hash.Length);
    }
}
