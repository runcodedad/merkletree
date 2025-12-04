using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using MerkleTree.Core;
using MerkleTree.Hashing;

namespace MerkleTree.Benchmarks;

/// <summary>
/// Benchmarks for streaming tree building performance.
/// Measures performance of building trees from async enumerable data sources.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class StreamingTreeBenchmarks
{
    private static async IAsyncEnumerable<byte[]> GenerateLeavesAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var data = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(data, i);
            yield return data;
            if (i % 100 == 0)
                await Task.Yield();
        }
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "Streaming", "Small")]
    public async Task<MerkleTreeMetadata> BuildStreamingTree_100Leaves()
    {
        var stream = new MerkleTreeStream();
        return await stream.BuildAsync(GenerateLeavesAsync(100));
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "Streaming", "Medium")]
    public async Task<MerkleTreeMetadata> BuildStreamingTree_1000Leaves()
    {
        var stream = new MerkleTreeStream();
        return await stream.BuildAsync(GenerateLeavesAsync(1000));
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "Streaming", "Large")]
    public async Task<MerkleTreeMetadata> BuildStreamingTree_10000Leaves()
    {
        var stream = new MerkleTreeStream();
        return await stream.BuildAsync(GenerateLeavesAsync(10000));
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "Streaming", "SHA256")]
    public async Task<MerkleTreeMetadata> BuildStreamingTree_SHA256_1000Leaves()
    {
        var stream = new MerkleTreeStream(new Sha256HashFunction());
        return await stream.BuildAsync(GenerateLeavesAsync(1000));
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "Streaming", "SHA512")]
    public async Task<MerkleTreeMetadata> BuildStreamingTree_SHA512_1000Leaves()
    {
        var stream = new MerkleTreeStream(new Sha512HashFunction());
        return await stream.BuildAsync(GenerateLeavesAsync(1000));
    }

    [Benchmark]
    [BenchmarkCategory("TreeBuilding", "Streaming", "BLAKE3")]
    public async Task<MerkleTreeMetadata> BuildStreamingTree_BLAKE3_1000Leaves()
    {
        var stream = new MerkleTreeStream(new Blake3HashFunction());
        return await stream.BuildAsync(GenerateLeavesAsync(1000));
    }
}
