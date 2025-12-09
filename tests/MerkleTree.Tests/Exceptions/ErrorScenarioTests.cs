using MerkleTree.Exceptions;
using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTree.Smt;

namespace MerkleTree.Tests.Exceptions;

/// <summary>
/// Tests demonstrating how custom exceptions should be used in error scenarios.
/// These tests validate error reporting for common failure modes.
/// </summary>
public class ErrorScenarioTests
{
    #region MalformedProofException Scenarios

    [Fact]
    public void MalformedProofException_ForInconsistentHashSize_ProvidesDetailedContext()
    {
        // This test documents how MalformedProofException should be used
        // when detecting inconsistent hash sizes in a proof structure
        
        // Arrange
        var errorCode = "INCONSISTENT_HASH_SIZE";
        var expectedSize = 32;
        var actualSize = 16;
        var index = 2;
        var details = $"Hash at index {index}: expected {expectedSize} bytes, got {actualSize} bytes";

        // Act
        var ex = new MalformedProofException(
            "Proof has inconsistent sibling hash sizes",
            errorCode,
            details);

        // Assert - Verify exception provides all necessary context
        Assert.Contains("inconsistent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sibling", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Contains("index 2", ex.Details);
        Assert.Contains("32", ex.Details);
        Assert.Contains("16", ex.Details);
    }

    [Fact]
    public void MalformedProofException_ForSiblingCountMismatch_ProvidesDetailedContext()
    {
        // Arrange
        var errorCode = "SIBLING_COUNT_MISMATCH";
        var expectedCount = 8;
        var actualCount = 5;
        var details = $"Tree height {expectedCount} requires {expectedCount} siblings, but proof contains {actualCount}";

        // Act
        var ex = new MalformedProofException(
            "Proof sibling count does not match tree height",
            errorCode,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Contains("8", ex.Details);
        Assert.Contains("5", ex.Details);
    }

    [Fact]
    public void MalformedProofException_ForNullSiblingHash_ProvidesDetailedContext()
    {
        // Arrange
        var errorCode = "NULL_SIBLING_HASH";
        var index = 3;
        var details = $"Sibling hash at index {index} is null";

        // Act
        var ex = new MalformedProofException(
            "Proof contains null sibling hash",
            errorCode,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Contains("index 3", ex.Details);
    }

    #endregion

    #region ProofVerificationFailedException Scenarios

    [Fact]
    public void ProofVerificationFailedException_ForRootHashMismatch_IncludesHashValues()
    {
        // Arrange
        var errorCode = "ROOT_HASH_MISMATCH";
        var expectedRoot = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var computedRoot = new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 };
        var details = "Computed root hash does not match expected root hash";

        // Act
        var ex = new ProofVerificationFailedException(
            "Proof verification failed: root hash mismatch",
            errorCode,
            expectedRoot,
            computedRoot,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.NotNull(ex.ExpectedRoot);
        Assert.NotNull(ex.ComputedRoot);
        Assert.Equal(expectedRoot, ex.ExpectedRoot);
        Assert.Equal(computedRoot, ex.ComputedRoot);
    }

    [Fact]
    public void ProofVerificationFailedException_ForInvalidPath_ProvidesContext()
    {
        // Arrange
        var errorCode = "INVALID_PATH";
        var details = "Proof path does not correspond to a valid leaf position in the tree";

        // Act
        var ex = new ProofVerificationFailedException(
            "Proof path is invalid for tree structure",
            errorCode,
            details: details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Contains("path", ex.Details, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region DepthMismatchException Scenarios

    [Fact]
    public void DepthMismatchException_ForProofHeightMismatch_IncludesDepthValues()
    {
        // Arrange
        var errorCode = "PROOF_HEIGHT_MISMATCH";
        var expectedDepth = 256;
        var actualDepth = 16;
        var details = $"Proof was generated for a tree with depth {actualDepth}, but current tree has depth {expectedDepth}";

        // Act
        var ex = new DepthMismatchException(
            "Proof depth does not match tree configuration",
            errorCode,
            expectedDepth,
            actualDepth,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(expectedDepth, ex.ExpectedDepth);
        Assert.Equal(actualDepth, ex.ActualDepth);
        Assert.Contains("256", ex.Details);
        Assert.Contains("16", ex.Details);
    }

    [Fact]
    public void DepthMismatchException_ForPathLengthInvalid_ProvidesContext()
    {
        // Arrange
        var errorCode = "PATH_LENGTH_INVALID";
        var expectedLength = 256;
        var actualLength = 300;
        var details = $"Bit path has length {actualLength}, which exceeds maximum depth {expectedLength}";

        // Act
        var ex = new DepthMismatchException(
            "Bit path length exceeds tree depth",
            errorCode,
            expectedLength,
            actualLength,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(expectedLength, ex.ExpectedDepth);
        Assert.Equal(actualLength, ex.ActualDepth);
    }

    #endregion

    #region InvalidLeafFormatException Scenarios

    [Fact]
    public void InvalidLeafFormatException_ForNullLeaf_IncludesLeafIndex()
    {
        // Arrange
        var errorCode = "NULL_LEAF_DATA";
        var leafIndex = 42L;
        var expectedFormat = "Non-null byte array";
        var details = $"Leaf data at index {leafIndex} is null";

        // Act
        var ex = new InvalidLeafFormatException(
            "Leaf data cannot be null",
            errorCode,
            leafIndex,
            expectedFormat,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(leafIndex, ex.LeafIndex);
        Assert.Equal(expectedFormat, ex.ExpectedFormat);
        Assert.Contains("42", ex.Details);
    }

    [Fact]
    public void InvalidLeafFormatException_ForInvalidSize_ProvidesExpectedFormat()
    {
        // Arrange
        var errorCode = "INVALID_LEAF_SIZE";
        var leafIndex = 10L;
        var expectedFormat = "32 bytes (SHA-256 hash)";
        var details = "Leaf data has size 16 bytes, expected 32 bytes";

        // Act
        var ex = new InvalidLeafFormatException(
            "Leaf data has incorrect size",
            errorCode,
            leafIndex,
            expectedFormat,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(expectedFormat, ex.ExpectedFormat);
        Assert.Contains("32 bytes", ex.ExpectedFormat);
    }

    #endregion

    #region StorageAdapterException Scenarios

    [Fact]
    public void StorageAdapterException_ForIOError_WrapsInnerException()
    {
        // Arrange - Simulate an I/O error from underlying storage
        var ioException = new System.IO.IOException("Disk full");
        var errorCode = "IO_ERROR";
        var operation = "WriteBatch";
        var details = "Failed to write nodes to disk at /var/merkle/data";

        // Act
        var ex = new StorageAdapterException(
            "Storage I/O operation failed",
            errorCode,
            ioException,
            operation,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(operation, ex.Operation);
        Assert.Same(ioException, ex.InnerException);
        Assert.Contains("disk", ex.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StorageAdapterException_ForDatabaseError_PreservesExceptionChain()
    {
        // Arrange - Simulate a database connection error
        var dbException = new InvalidOperationException("Connection timeout");
        var errorCode = "DATABASE_ERROR";
        var operation = "ReadNodeByHash";
        var details = "Database connection timeout after 30 seconds";

        // Act
        var ex = new StorageAdapterException(
            "Database operation failed",
            errorCode,
            dbException,
            operation,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Same(dbException, ex.InnerException);
        Assert.Equal("ReadNodeByHash", ex.Operation);
        Assert.Contains("timeout", ex.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StorageAdapterException_ForCorruptedData_ProvidesContext()
    {
        // Arrange
        var errorCode = "CORRUPTED_DATA";
        var operation = "ReadNodeByPath";
        var details = "Node data failed checksum validation";

        // Act
        var ex = new StorageAdapterException(
            "Storage data corruption detected",
            errorCode,
            operation,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Contains("corruption", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("checksum", ex.Details);
    }

    #endregion

    #region InvalidMetadataException Scenarios

    [Fact]
    public void InvalidMetadataException_ForUnsupportedVersion_IncludesVersionInfo()
    {
        // Arrange
        var errorCode = "UNSUPPORTED_VERSION";
        var fieldName = "SmtCoreVersion";
        var expectedVersion = 1;
        var actualVersion = 99;
        var details = $"This library supports SMT core version {expectedVersion}, but metadata specifies version {actualVersion}";

        // Act
        var ex = new InvalidMetadataException(
            "Unsupported metadata version",
            errorCode,
            fieldName,
            expectedVersion,
            actualVersion,
            details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(fieldName, ex.FieldName);
        Assert.Equal(expectedVersion, ex.ExpectedVersion);
        Assert.Equal(actualVersion, ex.ActualVersion);
        Assert.Contains("99", ex.Details);
    }

    [Fact]
    public void InvalidMetadataException_ForUnknownHashAlgorithm_ProvidesContext()
    {
        // Arrange
        var errorCode = "UNKNOWN_HASH_ALGORITHM";
        var fieldName = "HashAlgorithmId";
        var details = "Hash algorithm 'UNKNOWN-HASH-512' is not supported";

        // Act
        var ex = new InvalidMetadataException(
            "Unknown hash algorithm in metadata",
            errorCode,
            fieldName,
            details: details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(fieldName, ex.FieldName);
        Assert.Contains("UNKNOWN-HASH-512", ex.Details);
    }

    [Fact]
    public void InvalidMetadataException_ForInvalidDepth_ProvidesRangeInfo()
    {
        // Arrange
        var errorCode = "INVALID_DEPTH";
        var fieldName = "TreeDepth";
        var details = "Tree depth 1024 exceeds maximum supported depth of 512";

        // Act
        var ex = new InvalidMetadataException(
            "Invalid tree depth in metadata",
            errorCode,
            fieldName,
            details: details);

        // Assert
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1024", ex.Details);
        Assert.Contains("512", ex.Details);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void ErrorScenario_MerkleProofDeserialization_ShouldThrowMalformedProofException()
    {
        // This test demonstrates how actual code should throw MalformedProofException
        // when encountering malformed proof data
        
        // Arrange - Create truncated proof data
        var data = new byte[5]; // Too short to be valid
        data[0] = 1; // version

        // Act & Assert - Current implementation throws ArgumentException
        // In future, this should throw MalformedProofException
        Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(data));
        
        // TODO: Update MerkleProof.Deserialize to throw MalformedProofException:
        // try {
        //     return MerkleProof.Deserialize(data);
        // } catch (ArgumentException argEx) {
        //     throw new MalformedProofException(
        //         "Proof deserialization failed: data too short",
        //         "TRUNCATED_DATA",
        //         argEx,
        //         details: $"Expected at least 17 bytes for header, got {data.Length}");
        // }
    }

    [Fact]
    public void ErrorScenario_SmtMetadataDeserialization_ShouldThrowInvalidMetadataException()
    {
        // This test demonstrates how SMT metadata deserialization should
        // throw InvalidMetadataException for version mismatches
        
        // Arrange - Create metadata with invalid version
        var hashFunc = new Sha256HashFunction();
        var zeroHashes = ZeroHashTable.Compute(hashFunc, 256);
        var metadata = new SmtMetadata(hashFunc.Name, 256, zeroHashes);
        var serialized = metadata.Serialize();
        
        // Modify version to be unsupported
        serialized[0] = 99; // Invalid version
        
        // Act & Assert - Current implementation throws InvalidOperationException
        // In future, this should throw InvalidMetadataException
        Assert.Throws<InvalidOperationException>(() => SmtMetadata.Deserialize(serialized));
        
        // TODO: Update SmtMetadata.Deserialize to throw InvalidMetadataException:
        // if (version != SmtMetadata.CurrentSmtCoreVersion) {
        //     throw new InvalidMetadataException(
        //         "Unsupported SMT metadata version",
        //         "UNSUPPORTED_VERSION",
        //         fieldName: "SmtCoreVersion",
        //         expectedVersion: SmtMetadata.CurrentSmtCoreVersion,
        //         actualVersion: version,
        //         details: $"Migration required from version {version} to {SmtMetadata.CurrentSmtCoreVersion}");
        // }
    }

    [Fact]
    public void ErrorScenario_StorageAdapter_ShouldWrapIOException()
    {
        // This test documents how storage adapters should wrap I/O exceptions
        
        // Arrange - Simulate a storage adapter encountering I/O error
        var ioError = new System.IO.IOException("No space left on device");
        
        // Act - Storage adapter wraps the exception
        var storageEx = new StorageAdapterException(
            "Failed to persist tree nodes",
            "IO_ERROR",
            ioError,
            operation: "WriteBatch",
            details: "Disk full at /mnt/merkle-data");
        
        // Assert - Exception chain is preserved
        Assert.Same(ioError, storageEx.InnerException);
        Assert.Equal("WriteBatch", storageEx.Operation);
        Assert.Contains("Disk full", storageEx.Details);
        
        // Verify client code can catch and handle appropriately
        try
        {
            throw storageEx;
        }
        catch (StorageAdapterException ex) when (ex.ErrorCode == "IO_ERROR")
        {
            // Client can handle I/O errors specifically
            Assert.NotNull(ex.InnerException);
            Assert.IsType<System.IO.IOException>(ex.InnerException);
        }
    }

    #endregion
}
