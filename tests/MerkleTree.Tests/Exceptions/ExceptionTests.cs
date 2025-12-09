using MerkleTree.Exceptions;

namespace MerkleTree.Tests.Exceptions;

/// <summary>
/// Tests for custom exception types and their properties.
/// </summary>
public class ExceptionTests
{
    #region MalformedProofException Tests

    [Fact]
    public void MalformedProofException_Constructor_SetsProperties()
    {
        // Arrange
        var message = "Invalid proof structure";
        var errorCode = "INCONSISTENT_HASH_SIZE";
        var details = "Expected 32 bytes, got 16 bytes";

        // Act
        var ex = new MalformedProofException(message, errorCode, details);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(details, ex.Details);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void MalformedProofException_WithInnerException_PreservesChain()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner error");
        var message = "Proof deserialization failed";
        var errorCode = "DESERIALIZATION_ERROR";

        // Act
        var ex = new MalformedProofException(message, errorCode, innerEx);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Same(innerEx, ex.InnerException);
    }

    [Fact]
    public void MalformedProofException_WithoutDetails_AllowsNull()
    {
        // Act
        var ex = new MalformedProofException("Error", "CODE");

        // Assert
        Assert.Null(ex.Details);
    }

    #endregion

    #region ProofVerificationFailedException Tests

    [Fact]
    public void ProofVerificationFailedException_Constructor_SetsAllProperties()
    {
        // Arrange
        var message = "Verification failed";
        var errorCode = "ROOT_HASH_MISMATCH";
        var expectedRoot = new byte[] { 1, 2, 3, 4 };
        var computedRoot = new byte[] { 5, 6, 7, 8 };
        var details = "Mismatch at byte 0";

        // Act
        var ex = new ProofVerificationFailedException(
            message, errorCode, expectedRoot, computedRoot, details);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Same(expectedRoot, ex.ExpectedRoot);
        Assert.Same(computedRoot, ex.ComputedRoot);
        Assert.Equal(details, ex.Details);
    }

    [Fact]
    public void ProofVerificationFailedException_WithInnerException_PreservesChain()
    {
        // Arrange
        var innerEx = new ArgumentException("Hash size invalid");
        var message = "Verification error";
        var errorCode = "INVALID_HASH";

        // Act
        var ex = new ProofVerificationFailedException(
            message, errorCode, innerEx);

        // Assert
        Assert.Same(innerEx, ex.InnerException);
    }

    [Fact]
    public void ProofVerificationFailedException_WithOptionalParameters_AllowsNull()
    {
        // Act
        var ex = new ProofVerificationFailedException("Error", "CODE");

        // Assert
        Assert.Null(ex.ExpectedRoot);
        Assert.Null(ex.ComputedRoot);
        Assert.Null(ex.Details);
    }

    #endregion

    #region DepthMismatchException Tests

    [Fact]
    public void DepthMismatchException_Constructor_SetsAllProperties()
    {
        // Arrange
        var message = "Tree depth mismatch";
        var errorCode = "PROOF_HEIGHT_MISMATCH";
        var expectedDepth = 256;
        var actualDepth = 16;
        var details = "Proof from different tree configuration";

        // Act
        var ex = new DepthMismatchException(
            message, errorCode, expectedDepth, actualDepth, details);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(expectedDepth, ex.ExpectedDepth);
        Assert.Equal(actualDepth, ex.ActualDepth);
        Assert.Equal(details, ex.Details);
    }

    [Fact]
    public void DepthMismatchException_WithInnerException_PreservesChain()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Path too long");
        var message = "Depth validation failed";
        var errorCode = "PATH_LENGTH_INVALID";

        // Act
        var ex = new DepthMismatchException(
            message, errorCode, innerEx, 256, 300);

        // Assert
        Assert.Same(innerEx, ex.InnerException);
        Assert.Equal(256, ex.ExpectedDepth);
        Assert.Equal(300, ex.ActualDepth);
    }

    [Fact]
    public void DepthMismatchException_WithOptionalParameters_AllowsNull()
    {
        // Act
        var ex = new DepthMismatchException("Error", "CODE");

        // Assert
        Assert.Null(ex.ExpectedDepth);
        Assert.Null(ex.ActualDepth);
        Assert.Null(ex.Details);
    }

    #endregion

    #region InvalidLeafFormatException Tests

    [Fact]
    public void InvalidLeafFormatException_Constructor_SetsAllProperties()
    {
        // Arrange
        var message = "Invalid leaf data";
        var errorCode = "NULL_LEAF_DATA";
        var leafIndex = 42L;
        var expectedFormat = "Non-null byte array";
        var details = "Leaf at index 42 is null";

        // Act
        var ex = new InvalidLeafFormatException(
            message, errorCode, leafIndex, expectedFormat, details);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(leafIndex, ex.LeafIndex);
        Assert.Equal(expectedFormat, ex.ExpectedFormat);
        Assert.Equal(details, ex.Details);
    }

    [Fact]
    public void InvalidLeafFormatException_WithInnerException_PreservesChain()
    {
        // Arrange
        var innerEx = new FormatException("Encoding error");
        var message = "Leaf encoding invalid";
        var errorCode = "MALFORMED_ENCODING";

        // Act
        var ex = new InvalidLeafFormatException(
            message, errorCode, innerEx, 10L);

        // Assert
        Assert.Same(innerEx, ex.InnerException);
        Assert.Equal(10L, ex.LeafIndex);
    }

    [Fact]
    public void InvalidLeafFormatException_WithOptionalParameters_AllowsNull()
    {
        // Act
        var ex = new InvalidLeafFormatException("Error", "CODE");

        // Assert
        Assert.Null(ex.LeafIndex);
        Assert.Null(ex.ExpectedFormat);
        Assert.Null(ex.Details);
    }

    #endregion

    #region StorageAdapterException Tests

    [Fact]
    public void StorageAdapterException_Constructor_SetsAllProperties()
    {
        // Arrange
        var message = "Storage I/O failure";
        var errorCode = "IO_ERROR";
        var operation = "ReadNodeByHash";
        var details = "Disk full at /var/data";

        // Act
        var ex = new StorageAdapterException(
            message, errorCode, operation, details);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(operation, ex.Operation);
        Assert.Equal(details, ex.Details);
    }

    [Fact]
    public void StorageAdapterException_WithInnerException_PreservesChain()
    {
        // Arrange
        var innerEx = new System.IO.IOException("Disk error");
        var message = "Failed to write nodes";
        var errorCode = "IO_ERROR";
        var operation = "WriteBatch";

        // Act
        var ex = new StorageAdapterException(
            message, errorCode, innerEx, operation);

        // Assert
        Assert.Same(innerEx, ex.InnerException);
        Assert.Equal(operation, ex.Operation);
    }

    [Fact]
    public void StorageAdapterException_WithOptionalParameters_AllowsNull()
    {
        // Act
        var ex = new StorageAdapterException("Error", "CODE");

        // Assert
        Assert.Null(ex.Operation);
        Assert.Null(ex.Details);
    }

    #endregion

    #region InvalidMetadataException Tests

    [Fact]
    public void InvalidMetadataException_Constructor_SetsAllProperties()
    {
        // Arrange
        var message = "Unsupported metadata version";
        var errorCode = "UNSUPPORTED_VERSION";
        var fieldName = "SmtCoreVersion";
        var expectedVersion = 1;
        var actualVersion = 99;
        var details = "Upgrade required";

        // Act
        var ex = new InvalidMetadataException(
            message, errorCode, fieldName, expectedVersion, actualVersion, details);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal(fieldName, ex.FieldName);
        Assert.Equal(expectedVersion, ex.ExpectedVersion);
        Assert.Equal(actualVersion, ex.ActualVersion);
        Assert.Equal(details, ex.Details);
    }

    [Fact]
    public void InvalidMetadataException_WithInnerException_PreservesChain()
    {
        // Arrange
        var innerEx = new FormatException("Parse error");
        var message = "Metadata deserialization failed";
        var errorCode = "DESERIALIZATION_ERROR";

        // Act
        var ex = new InvalidMetadataException(
            message, errorCode, innerEx);

        // Assert
        Assert.Same(innerEx, ex.InnerException);
    }

    [Fact]
    public void InvalidMetadataException_WithOptionalParameters_AllowsNull()
    {
        // Act
        var ex = new InvalidMetadataException("Error", "CODE");

        // Assert
        Assert.Null(ex.FieldName);
        Assert.Null(ex.ExpectedVersion);
        Assert.Null(ex.ActualVersion);
        Assert.Null(ex.Details);
    }

    #endregion

    #region Exception Hierarchy Tests

    [Fact]
    public void AllCustomExceptions_InheritFromException()
    {
        // Assert
        Assert.IsAssignableFrom<Exception>(new MalformedProofException("", ""));
        Assert.IsAssignableFrom<Exception>(new ProofVerificationFailedException("", ""));
        Assert.IsAssignableFrom<Exception>(new DepthMismatchException("", ""));
        Assert.IsAssignableFrom<Exception>(new InvalidLeafFormatException("", ""));
        Assert.IsAssignableFrom<Exception>(new StorageAdapterException("", ""));
        Assert.IsAssignableFrom<Exception>(new InvalidMetadataException("", ""));
    }

    [Fact]
    public void AllCustomExceptions_HaveErrorCodeProperty()
    {
        // Arrange & Act
        var exceptions = new Exception[]
        {
            new MalformedProofException("msg", "CODE1"),
            new ProofVerificationFailedException("msg", "CODE2"),
            new DepthMismatchException("msg", "CODE3"),
            new InvalidLeafFormatException("msg", "CODE4"),
            new StorageAdapterException("msg", "CODE5"),
            new InvalidMetadataException("msg", "CODE6")
        };

        // Assert - All exceptions should have ErrorCode property via reflection
        foreach (var ex in exceptions)
        {
            var errorCodeProperty = ex.GetType().GetProperty("ErrorCode");
            Assert.NotNull(errorCodeProperty);
            var errorCode = errorCodeProperty.GetValue(ex) as string;
            Assert.NotNull(errorCode);
            Assert.StartsWith("CODE", errorCode);
        }
    }

    #endregion
}
