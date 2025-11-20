using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Text;
using MerkleTree.Core;
using MerkleTree.Proofs;
using MerkleTree.Hashing;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Benchmarks;

/// <summary>
/// Benchmarks for serialization and deserialization performance.
/// Measures the speed of converting proofs and tree metadata to/from binary format.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class SerializationBenchmarks
{
    private MerkleProof? _proof100;
    private MerkleProof? _proof1000;
    private MerkleProof? _proof10000;
    private byte[]? _serializedProof100;
    private byte[]? _serializedProof1000;
    private byte[]? _serializedProof10000;
    private MerkleTreeMetadata? _metadata100;
    private MerkleTreeMetadata? _metadata1000;
    private MerkleTreeMetadata? _metadata10000;

    [GlobalSetup]
    public void Setup()
    {
        var tree100 = new MerkleTreeClass(GenerateLeaves(100));
        _proof100 = tree100.GenerateProof(50);
        _serializedProof100 = _proof100.Serialize();
        _metadata100 = tree100.GetMetadata();

        var tree1000 = new MerkleTreeClass(GenerateLeaves(1000));
        _proof1000 = tree1000.GenerateProof(500);
        _serializedProof1000 = _proof1000.Serialize();
        _metadata1000 = tree1000.GetMetadata();

        var tree10000 = new MerkleTreeClass(GenerateLeaves(10000));
        _proof10000 = tree10000.GenerateProof(5000);
        _serializedProof10000 = _proof10000.Serialize();
        _metadata10000 = tree10000.GetMetadata();
    }

    private static List<byte[]> GenerateLeaves(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => Encoding.UTF8.GetBytes($"leaf_{i}"))
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "Proof", "Small")]
    public byte[] SerializeProof_Tree100()
    {
        return _proof100!.Serialize();
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "Proof", "Medium")]
    public byte[] SerializeProof_Tree1000()
    {
        return _proof1000!.Serialize();
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "Proof", "Large")]
    public byte[] SerializeProof_Tree10000()
    {
        return _proof10000!.Serialize();
    }

    [Benchmark]
    [BenchmarkCategory("Deserialization", "Proof", "Small")]
    public MerkleProof DeserializeProof_Tree100()
    {
        return MerkleProof.Deserialize(_serializedProof100!);
    }

    [Benchmark]
    [BenchmarkCategory("Deserialization", "Proof", "Medium")]
    public MerkleProof DeserializeProof_Tree1000()
    {
        return MerkleProof.Deserialize(_serializedProof1000!);
    }

    [Benchmark]
    [BenchmarkCategory("Deserialization", "Proof", "Large")]
    public MerkleProof DeserializeProof_Tree10000()
    {
        return MerkleProof.Deserialize(_serializedProof10000!);
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "RootHash", "Small")]
    public byte[] SerializeRootHash_Tree100()
    {
        return _metadata100!.SerializeRoot();
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "RootHash", "Medium")]
    public byte[] SerializeRootHash_Tree1000()
    {
        return _metadata1000!.SerializeRoot();
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "RootHash", "Large")]
    public byte[] SerializeRootHash_Tree10000()
    {
        return _metadata10000!.SerializeRoot();
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "RoundTrip", "Proof")]
    public MerkleProof ProofRoundTrip_Tree1000()
    {
        var serialized = _proof1000!.Serialize();
        return MerkleProof.Deserialize(serialized);
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "HashFunction", "SHA512")]
    public byte[] SerializeProof_SHA512()
    {
        var tree = new MerkleTreeClass(GenerateLeaves(1000), new Sha512HashFunction());
        var proof = tree.GenerateProof(500);
        return proof.Serialize();
    }

    [Benchmark]
    [BenchmarkCategory("Serialization", "HashFunction", "BLAKE3")]
    public byte[] SerializeProof_BLAKE3()
    {
        var tree = new MerkleTreeClass(GenerateLeaves(1000), new Blake3HashFunction());
        var proof = tree.GenerateProof(500);
        return proof.Serialize();
    }
}
