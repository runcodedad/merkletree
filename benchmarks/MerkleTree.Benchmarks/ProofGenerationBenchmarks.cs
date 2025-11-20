using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Text;
using MerkleTree.Core;
using MerkleTree.Proofs;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Benchmarks;

/// <summary>
/// Benchmarks for Merkle proof generation performance.
/// Measures how proof generation time scales with tree size.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class ProofGenerationBenchmarks
{
    private MerkleTreeClass? _tree100;
    private MerkleTreeClass? _tree1000;
    private MerkleTreeClass? _tree10000;

    [GlobalSetup]
    public void Setup()
    {
        _tree100 = new MerkleTreeClass(GenerateLeaves(100));
        _tree1000 = new MerkleTreeClass(GenerateLeaves(1000));
        _tree10000 = new MerkleTreeClass(GenerateLeaves(10000));
    }

    private static List<byte[]> GenerateLeaves(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => Encoding.UTF8.GetBytes($"leaf_{i}"))
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Small")]
    public MerkleProof GenerateProof_Tree100_Index0()
    {
        return _tree100!.GenerateProof(0);
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Small")]
    public MerkleProof GenerateProof_Tree100_IndexMiddle()
    {
        return _tree100!.GenerateProof(50);
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Small")]
    public MerkleProof GenerateProof_Tree100_IndexLast()
    {
        return _tree100!.GenerateProof(99);
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Medium")]
    public MerkleProof GenerateProof_Tree1000_Index0()
    {
        return _tree1000!.GenerateProof(0);
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Medium")]
    public MerkleProof GenerateProof_Tree1000_IndexMiddle()
    {
        return _tree1000!.GenerateProof(500);
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Medium")]
    public MerkleProof GenerateProof_Tree1000_IndexLast()
    {
        return _tree1000!.GenerateProof(999);
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Large")]
    public MerkleProof GenerateProof_Tree10000_Index0()
    {
        return _tree10000!.GenerateProof(0);
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Large")]
    public MerkleProof GenerateProof_Tree10000_IndexMiddle()
    {
        return _tree10000!.GenerateProof(5000);
    }

    [Benchmark]
    [BenchmarkCategory("ProofGeneration", "Large")]
    public MerkleProof GenerateProof_Tree10000_IndexLast()
    {
        return _tree10000!.GenerateProof(9999);
    }
}
