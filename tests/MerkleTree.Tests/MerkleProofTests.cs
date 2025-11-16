using System.Text;
using Xunit;

namespace MerkleTree.Tests;

/// <summary>
/// Tests for Merkle proof generation and verification.
/// </summary>
public class MerkleProofTests
{
    /// <summary>
    /// Helper method to create leaf data from strings.
    /// </summary>
    private static List<byte[]> CreateLeafData(params string[] data)
    {
        return data.Select(s => Encoding.UTF8.GetBytes(s)).ToList();
    }

    [Fact]
    public void GenerateProof_WithSingleLeaf_GeneratesEmptyProof()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1");
        var tree = new MerkleTree(leafData);

        // Act
        var proof = tree.GenerateProof(0);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(0, proof.LeafIndex);
        Assert.Equal(0, proof.TreeHeight);
        Assert.Empty(proof.SiblingHashes);
        Assert.Empty(proof.SiblingIsRight);
        Assert.Equal(leafData[0], proof.LeafValue);
    }

    [Fact]
    public void GenerateProof_WithTwoLeaves_GeneratesValidProof()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTree(leafData);

        // Act - Generate proof for first leaf
        var proof0 = tree.GenerateProof(0);

        // Assert
        Assert.Equal(0, proof0.LeafIndex);
        Assert.Equal(1, proof0.TreeHeight);
        Assert.Single(proof0.SiblingHashes);
        Assert.Single(proof0.SiblingIsRight);
        Assert.True(proof0.SiblingIsRight[0]); // Sibling is on the right
        Assert.Equal(leafData[0], proof0.LeafValue);

        // Act - Generate proof for second leaf
        var proof1 = tree.GenerateProof(1);

        // Assert
        Assert.Equal(1, proof1.LeafIndex);
        Assert.Equal(1, proof1.TreeHeight);
        Assert.Single(proof1.SiblingHashes);
        Assert.Single(proof1.SiblingIsRight);
        Assert.False(proof1.SiblingIsRight[0]); // Sibling is on the left
        Assert.Equal(leafData[1], proof1.LeafValue);
    }

    [Fact]
    public void GenerateProof_WithThreeLeaves_GeneratesValidProof()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTree(leafData);

        // Act - Generate proof for first leaf
        var proof0 = tree.GenerateProof(0);

        // Assert
        Assert.Equal(0, proof0.LeafIndex);
        Assert.Equal(2, proof0.TreeHeight);
        Assert.Equal(2, proof0.SiblingHashes.Length);
        Assert.Equal(2, proof0.SiblingIsRight.Length);
        Assert.Equal(leafData[0], proof0.LeafValue);

        // Act - Generate proof for second leaf
        var proof1 = tree.GenerateProof(1);

        // Assert
        Assert.Equal(1, proof1.LeafIndex);
        Assert.Equal(2, proof1.TreeHeight);
        Assert.Equal(2, proof1.SiblingHashes.Length);
        Assert.Equal(2, proof1.SiblingIsRight.Length);
        Assert.Equal(leafData[1], proof1.LeafValue);

        // Act - Generate proof for third leaf
        var proof2 = tree.GenerateProof(2);

        // Assert
        Assert.Equal(2, proof2.LeafIndex);
        Assert.Equal(2, proof2.TreeHeight);
        Assert.Equal(2, proof2.SiblingHashes.Length);
        Assert.Equal(2, proof2.SiblingIsRight.Length);
        Assert.Equal(leafData[2], proof2.LeafValue);
    }

    [Fact]
    public void GenerateProof_WithFourLeaves_GeneratesValidProof()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf4");
        var tree = new MerkleTree(leafData);

        // Act & Assert - Generate proofs for all leaves
        for (int i = 0; i < 4; i++)
        {
            var proof = tree.GenerateProof(i);
            Assert.Equal(i, proof.LeafIndex);
            Assert.Equal(2, proof.TreeHeight);
            Assert.Equal(2, proof.SiblingHashes.Length);
            Assert.Equal(2, proof.SiblingIsRight.Length);
            Assert.Equal(leafData[i], proof.LeafValue);
        }
    }

    [Fact]
    public void GenerateProof_WithSevenLeaves_GeneratesValidProof()
    {
        // Arrange
        var leafData = CreateLeafData("l1", "l2", "l3", "l4", "l5", "l6", "l7");
        var tree = new MerkleTree(leafData);

        // Act & Assert - Generate proofs for all leaves
        for (int i = 0; i < 7; i++)
        {
            var proof = tree.GenerateProof(i);
            Assert.Equal(i, proof.LeafIndex);
            Assert.Equal(3, proof.TreeHeight);
            Assert.Equal(3, proof.SiblingHashes.Length);
            Assert.Equal(3, proof.SiblingIsRight.Length);
            Assert.Equal(leafData[i], proof.LeafValue);
        }
    }

    [Fact]
    public void GenerateProof_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTree(leafData);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.GenerateProof(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.GenerateProof(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.GenerateProof(100));
    }

    [Fact]
    public void Verify_WithValidProof_ReturnsTrue()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTree(leafData);
        var rootHash = tree.GetRootHash();
        var hashFunction = new Sha256HashFunction();

        // Act - Generate and verify proof for each leaf
        for (int i = 0; i < 3; i++)
        {
            var proof = tree.GenerateProof(i);
            var isValid = proof.Verify(rootHash, hashFunction);

            // Assert
            Assert.True(isValid, $"Proof for leaf {i} should be valid");
        }
    }

    [Fact]
    public void Verify_WithModifiedLeafValue_ReturnsFalse()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTree(leafData);
        var rootHash = tree.GetRootHash();
        var hashFunction = new Sha256HashFunction();

        // Act - Generate proof
        var proof = tree.GenerateProof(1);

        // Create modified proof with different leaf value
        var modifiedProof = new MerkleProof(
            Encoding.UTF8.GetBytes("modified"),
            proof.LeafIndex,
            proof.TreeHeight,
            proof.SiblingHashes,
            proof.SiblingIsRight);

        var isValid = modifiedProof.Verify(rootHash, hashFunction);

        // Assert
        Assert.False(isValid, "Proof with modified leaf value should be invalid");
    }

    [Fact]
    public void Verify_WithDifferentRootHash_ReturnsFalse()
    {
        // Arrange
        var leafData1 = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree1 = new MerkleTree(leafData1);
        
        var leafData2 = CreateLeafData("leaf4", "leaf5", "leaf6");
        var tree2 = new MerkleTree(leafData2);
        
        var hashFunction = new Sha256HashFunction();

        // Act - Generate proof from tree1 and verify against tree2's root
        var proof = tree1.GenerateProof(0);
        var isValid = proof.Verify(tree2.GetRootHash(), hashFunction);

        // Assert
        Assert.False(isValid, "Proof should be invalid against different tree's root hash");
    }

    [Fact]
    public void Verify_WithDifferentHashFunction_MayProduceDifferentResults()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var treeSHA256 = new MerkleTree(leafData, new Sha256HashFunction());
        var treeSHA512 = new MerkleTree(leafData, new Sha512HashFunction());

        // Act - Generate proof from SHA256 tree
        var proof = treeSHA256.GenerateProof(1);

        // Verify with matching hash function
        var validWithSHA256 = proof.Verify(treeSHA256.GetRootHash(), new Sha256HashFunction());

        // Verify with different hash function (expected to fail)
        var validWithSHA512 = proof.Verify(treeSHA512.GetRootHash(), new Sha512HashFunction());

        // Assert
        Assert.True(validWithSHA256, "Proof should be valid with matching hash function");
        Assert.False(validWithSHA512, "Proof should be invalid with different hash function");
    }

    [Fact]
    public void GenerateProof_WithLargeTree_GeneratesValidProof()
    {
        // Arrange - Create a larger tree with 100 leaves
        var leafData = Enumerable.Range(0, 100)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();
        var tree = new MerkleTree(leafData);
        var rootHash = tree.GetRootHash();
        var hashFunction = new Sha256HashFunction();

        // Act & Assert - Test a few representative leaves
        var testIndices = new[] { 0, 1, 50, 99 };
        foreach (var index in testIndices)
        {
            var proof = tree.GenerateProof(index);
            var isValid = proof.Verify(rootHash, hashFunction);
            
            Assert.True(isValid, $"Proof for leaf {index} should be valid");
            Assert.Equal(leafData[index], proof.LeafValue);
        }
    }

    [Fact]
    public void MerkleProof_Constructor_WithNullLeafValue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MerkleProof(
            null!,
            0,
            1,
            new byte[][] { new byte[] { 1, 2, 3 } },
            new bool[] { true }));
    }

    [Fact]
    public void MerkleProof_Constructor_WithNullSiblingHashes_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            1,
            null!,
            new bool[] { true }));
    }

    [Fact]
    public void MerkleProof_Constructor_WithNullSiblingIsRight_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            1,
            new byte[][] { new byte[] { 1, 2, 3 } },
            null!));
    }

    [Fact]
    public void MerkleProof_Constructor_WithNegativeLeafIndex_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            -1,
            1,
            new byte[][] { new byte[] { 1, 2, 3 } },
            new bool[] { true }));
    }

    [Fact]
    public void MerkleProof_Constructor_WithMismatchedArrayLengths_ThrowsArgumentException()
    {
        // Act & Assert - More sibling hashes than orientation bits
        Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            2,
            new byte[][] { new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 } },
            new bool[] { true }));

        // Act & Assert - More orientation bits than sibling hashes
        Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            2,
            new byte[][] { new byte[] { 1, 2, 3 } },
            new bool[] { true, false }));
    }

    [Fact]
    public void MerkleProof_Constructor_WithArrayLengthNotMatchingHeight_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            3, // Height is 3
            new byte[][] { new byte[] { 1, 2, 3 } }, // But only 1 sibling hash
            new bool[] { true }));
    }

    [Fact]
    public void Verify_WithNullRootHash_ThrowsArgumentNullException()
    {
        // Arrange
        var proof = new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            1,
            new byte[][] { new byte[] { 4, 5, 6 } },
            new bool[] { true });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => proof.Verify(null!, new Sha256HashFunction()));
    }

    [Fact]
    public void Verify_WithNullHashFunction_ThrowsArgumentNullException()
    {
        // Arrange
        var proof = new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            1,
            new byte[][] { new byte[] { 4, 5, 6 } },
            new bool[] { true });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => proof.Verify(new byte[] { 1, 2 }, null!));
    }

    [Fact]
    public void GenerateProof_OrientationBits_CorrectForLeftAndRightPositions()
    {
        // Arrange - Tree with 4 leaves has predictable structure
        var leafData = CreateLeafData("leaf0", "leaf1", "leaf2", "leaf3");
        var tree = new MerkleTree(leafData);

        // Act - Generate proofs
        var proof0 = tree.GenerateProof(0); // Left child at both levels
        var proof1 = tree.GenerateProof(1); // Right at level 0, left at level 1
        var proof2 = tree.GenerateProof(2); // Left at level 0, right at level 1
        var proof3 = tree.GenerateProof(3); // Right child at both levels

        // Assert - Check orientation bits
        // Leaf 0 (index 0): left child, so sibling is on right at level 0
        Assert.True(proof0.SiblingIsRight[0], "Leaf 0: sibling should be on right at level 0");
        // At level 1, it's still left child, so sibling is on right
        Assert.True(proof0.SiblingIsRight[1], "Leaf 0: sibling should be on right at level 1");

        // Leaf 1 (index 1): right child, so sibling is on left at level 0
        Assert.False(proof1.SiblingIsRight[0], "Leaf 1: sibling should be on left at level 0");
        // At level 1, it's left child, so sibling is on right
        Assert.True(proof1.SiblingIsRight[1], "Leaf 1: sibling should be on right at level 1");

        // Leaf 2 (index 2): left child, so sibling is on right at level 0
        Assert.True(proof2.SiblingIsRight[0], "Leaf 2: sibling should be on right at level 0");
        // At level 1, it's right child, so sibling is on left
        Assert.False(proof2.SiblingIsRight[1], "Leaf 2: sibling should be on left at level 1");

        // Leaf 3 (index 3): right child, so sibling is on left at level 0
        Assert.False(proof3.SiblingIsRight[0], "Leaf 3: sibling should be on left at level 0");
        // At level 1, it's right child, so sibling is on left
        Assert.False(proof3.SiblingIsRight[1], "Leaf 3: sibling should be on left at level 1");
    }
}
