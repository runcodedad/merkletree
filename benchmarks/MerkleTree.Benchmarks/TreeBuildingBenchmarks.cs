using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Text;
using MerkleTree.Core;
using MerkleTree.Hashing;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Benchmarks;

/// <summary>
/// Benchmarks for tree building performance with various leaf counts.
/// Measures how tree building time scales with the number of leaves.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class TreeBuildingBenchmarks
{
    private List<byte[]>? _leaves10;
    private List<byte[]>? _leaves100;
    private List<byte[]>? _leaves1000;
    private List<byte[]>? _leaves10000;

    [GlobalSetup]
    public void Setup()
    {
        _leaves10 = GenerateLeaves(10);
        _leaves100 = GenerateLeaves(100);
        _leaves1000 = GenerateLeaves(1000);
        _leaves10000 = GenerateLeaves(10000);
    }

    private static List<byte[]> GenerateLeaves(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => Encoding.UTF8.GetBytes($"leaf_{i}"))
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "InMemory", "Small")]
    public MerkleTreeClass BuildTree_10Leaves()
    {
        return new MerkleTreeClass(_leaves10!);
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "InMemory", "Medium")]
    public MerkleTreeClass BuildTree_100Leaves()
    {
        return new MerkleTreeClass(_leaves100!);
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "InMemory", "Large")]
    public MerkleTreeClass BuildTree_1000Leaves()
    {
        return new MerkleTreeClass(_leaves1000!);
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "InMemory", "VeryLarge")]
    public MerkleTreeClass BuildTree_10000Leaves()
    {
        return new MerkleTreeClass(_leaves10000!);
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "InMemory", "SHA256")]
    public MerkleTreeClass BuildTree_SHA256_1000Leaves()
    {
        return new MerkleTreeClass(_leaves1000!, new Sha256HashFunction());
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "InMemory", "SHA512")]
    public MerkleTreeClass BuildTree_SHA512_1000Leaves()
    {
        return new MerkleTreeClass(_leaves1000!, new Sha512HashFunction());
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "InMemory", "BLAKE3")]
    public MerkleTreeClass BuildTree_BLAKE3_1000Leaves()
    {
        return new MerkleTreeClass(_leaves1000!, new Blake3HashFunction());
    }
}
