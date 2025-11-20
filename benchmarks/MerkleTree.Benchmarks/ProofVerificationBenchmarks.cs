using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Text;
using MerkleTree.Core;
using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Benchmarks;

/// <summary>
/// Benchmarks for Merkle proof verification performance.
/// Measures the time taken to verify proofs of different sizes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class ProofVerificationBenchmarks
{
    private MerkleProof? _proof100;
    private MerkleProof? _proof1000;
    private MerkleProof? _proof10000;
    private byte[]? _rootHash100;
    private byte[]? _rootHash1000;
    private byte[]? _rootHash10000;
    private IHashFunction? _hashFunction;

    [GlobalSetup]
    public void Setup()
    {
        _hashFunction = new Sha256HashFunction();
        
        var tree100 = new MerkleTreeClass(GenerateLeaves(100));
        _proof100 = tree100.GenerateProof(50);
        _rootHash100 = tree100.GetRootHash();

        var tree1000 = new MerkleTreeClass(GenerateLeaves(1000));
        _proof1000 = tree1000.GenerateProof(500);
        _rootHash1000 = tree1000.GetRootHash();

        var tree10000 = new MerkleTreeClass(GenerateLeaves(10000));
        _proof10000 = tree10000.GenerateProof(5000);
        _rootHash10000 = tree10000.GetRootHash();
    }

    private static List<byte[]> GenerateLeaves(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => Encoding.UTF8.GetBytes($"leaf_{i}"))
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory("ProofVerification", "Small")]
    public bool VerifyProof_Tree100()
    {
        return _proof100!.Verify(_rootHash100!, _hashFunction!);
    }

    [Benchmark]
    [BenchmarkCategory("ProofVerification", "Medium")]
    public bool VerifyProof_Tree1000()
    {
        return _proof1000!.Verify(_rootHash1000!, _hashFunction!);
    }

    [Benchmark]
    [BenchmarkCategory("ProofVerification", "Large")]
    public bool VerifyProof_Tree10000()
    {
        return _proof10000!.Verify(_rootHash10000!, _hashFunction!);
    }

    [Benchmark]
    [BenchmarkCategory("ProofVerification", "HashFunction", "SHA256")]
    public bool VerifyProof_SHA256()
    {
        var hashFunc = new Sha256HashFunction();
        return _proof1000!.Verify(_rootHash1000!, hashFunc);
    }

    [Benchmark]
    [BenchmarkCategory("ProofVerification", "HashFunction", "SHA512")]
    public bool VerifyProof_SHA512()
    {
        var tree = new MerkleTreeClass(GenerateLeaves(1000), new Sha512HashFunction());
        var proof = tree.GenerateProof(500);
        var rootHash = tree.GetRootHash();
        var hashFunc = new Sha512HashFunction();
        return proof.Verify(rootHash, hashFunc);
    }

    [Benchmark]
    [BenchmarkCategory("ProofVerification", "HashFunction", "BLAKE3")]
    public bool VerifyProof_BLAKE3()
    {
        var tree = new MerkleTreeClass(GenerateLeaves(1000), new Blake3HashFunction());
        var proof = tree.GenerateProof(500);
        var rootHash = tree.GetRootHash();
        var hashFunc = new Blake3HashFunction();
        return proof.Verify(rootHash, hashFunc);
    }
}
