using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using MerkleTree.Core;
using MerkleTree.Cache;

namespace MerkleTree.Benchmarks;

/// <summary>
/// Benchmarks for cache hit/miss performance.
/// Measures the performance impact of caching on proof generation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class CachePerformanceBenchmarks
{
    private string? _cacheFile;
    private CacheData? _cache;
    private MerkleTreeStream? _stream;
    private const int LeafCount = 10000;

    [GlobalSetup]
    public async Task Setup()
    {
        _cacheFile = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid():N}.cache");
        _stream = new MerkleTreeStream();
        
        // Build tree with cache
        var cacheConfig = new CacheConfiguration(_cacheFile, topLevelsToCache: 5);
        await _stream.BuildAsync(GenerateLeavesAsync(LeafCount), cacheConfig);
        
        // Load cache for reuse
        _cache = CacheFileManager.LoadCache(_cacheFile);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_cacheFile != null && File.Exists(_cacheFile))
            File.Delete(_cacheFile);
    }

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
    [BenchmarkCategory("Cache", "ProofGeneration", "WithCache")]
    public async Task<MerkleTree.Proofs.MerkleProof> GenerateProof_WithCache_FirstLeaf()
    {
        return await _stream!.GenerateProofAsync(GenerateLeavesAsync(LeafCount), 0, LeafCount, _cache);
    }

    [Benchmark]
    [BenchmarkCategory("Cache", "ProofGeneration", "WithCache")]
    public async Task<MerkleTree.Proofs.MerkleProof> GenerateProof_WithCache_MiddleLeaf()
    {
        return await _stream!.GenerateProofAsync(GenerateLeavesAsync(LeafCount), LeafCount / 2, LeafCount, _cache);
    }

    [Benchmark]
    [BenchmarkCategory("Cache", "ProofGeneration", "WithCache")]
    public async Task<MerkleTree.Proofs.MerkleProof> GenerateProof_WithCache_LastLeaf()
    {
        return await _stream!.GenerateProofAsync(GenerateLeavesAsync(LeafCount), LeafCount - 1, LeafCount, _cache);
    }

    [Benchmark]
    [BenchmarkCategory("Cache", "ProofGeneration", "WithoutCache")]
    public async Task<MerkleTree.Proofs.MerkleProof> GenerateProof_WithoutCache_MiddleLeaf()
    {
        return await _stream!.GenerateProofAsync(GenerateLeavesAsync(LeafCount), LeafCount / 2, LeafCount);
    }

    [Benchmark]
    [BenchmarkCategory("Cache", "Building")]
    public async Task<MerkleTreeMetadata> BuildTree_WithCache()
    {
        var tempCache = Path.Combine(Path.GetTempPath(), $"benchmark_build_{Guid.NewGuid():N}.cache");
        try
        {
            var stream = new MerkleTreeStream();
            var cacheConfig = new CacheConfiguration(tempCache, topLevelsToCache: 5);
            return await stream.BuildAsync(GenerateLeavesAsync(1000), cacheConfig);
        }
        finally
        {
            if (File.Exists(tempCache))
                File.Delete(tempCache);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Cache", "Building")]
    public async Task<MerkleTreeMetadata> BuildTree_WithoutCache()
    {
        var stream = new MerkleTreeStream();
        return await stream.BuildAsync(GenerateLeavesAsync(1000));
    }
}
