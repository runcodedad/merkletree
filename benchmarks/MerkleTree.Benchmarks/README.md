# MerkleTree Performance Benchmarks

This project contains comprehensive performance benchmarks for the MerkleTree library using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Overview

The benchmark suite measures and tracks key performance characteristics of the library:

- **Tree Building Performance**: How tree construction time scales with leaf count
- **Streaming Performance**: Performance of async streaming tree building
- **Proof Generation**: Time to generate Merkle proofs for various tree sizes
- **Proof Verification**: Time to verify proofs of different heights
- **Cache Performance**: Impact of caching on proof generation (hits vs. misses)
- **Serialization/Deserialization**: Speed of binary format conversion

## Running Benchmarks

### Run All Benchmarks

```bash
cd benchmarks/MerkleTree.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark Class

```bash
# Run only tree building benchmarks
dotnet run -c Release -- --filter *TreeBuildingBenchmarks*

# Run only proof generation benchmarks
dotnet run -c Release -- --filter *ProofGenerationBenchmarks*

# Run only serialization benchmarks
dotnet run -c Release -- --filter *SerializationBenchmarks*
```

### Run Benchmarks by Category

```bash
# Run all tree building benchmarks
dotnet run -c Release -- --filter *TreeBuilding*

# Run all in-memory benchmarks
dotnet run -c Release -- --filter *InMemory*

# Run all streaming benchmarks
dotnet run -c Release -- --filter *Streaming*

# Run all cache-related benchmarks
dotnet run -c Release -- --filter *Cache*
```

### Run Specific Benchmark Method

```bash
# Run a single benchmark
dotnet run -c Release -- --filter *BuildTree_1000Leaves*
```

## Benchmark Categories

### TreeBuildingBenchmarks

Measures in-memory tree construction performance across different leaf counts and hash functions.

**Categories:**
- `TreeBuilding` - General tree building
- `InMemory` - In-memory trees (vs streaming)
- `Small` - 10 leaves
- `Medium` - 100 leaves
- `Large` - 1,000 leaves
- `VeryLarge` - 10,000 leaves
- `SHA256`, `SHA512`, `BLAKE3` - Hash function specific

**Key Metrics:**
- Mean execution time
- Memory allocation
- Scaling characteristics

### StreamingTreeBenchmarks

Measures streaming tree construction using `IAsyncEnumerable<byte[]>` data sources.

**Categories:**
- `TreeBuilding`, `Streaming`
- `Small` (100 leaves), `Medium` (1,000 leaves), `Large` (10,000 leaves)
- Hash function variants

**Key Metrics:**
- Async operation overhead
- Memory efficiency
- Throughput

### ProofGenerationBenchmarks

Measures Merkle proof generation time for various tree sizes and leaf positions.

**Categories:**
- `ProofGeneration`
- `Small` (100 leaves), `Medium` (1,000 leaves), `Large` (10,000 leaves)

**Key Metrics:**
- Proof generation time vs tree size
- Impact of leaf position (first, middle, last)
- Scalability

### ProofVerificationBenchmarks

Measures proof verification performance across different tree sizes and hash functions.

**Categories:**
- `ProofVerification`
- `Small`, `Medium`, `Large`
- `HashFunction` - SHA256, SHA512, BLAKE3

**Key Metrics:**
- Verification time vs proof height
- Hash function performance comparison

### CachePerformanceBenchmarks

Measures the performance impact of caching on streaming tree operations.

**Categories:**
- `Cache`
- `ProofGeneration` - With and without cache
- `Building` - Cache creation overhead

**Key Metrics:**
- Cache hit performance
- Cache miss overhead
- Memory usage with caching

### SerializationBenchmarks

Measures serialization and deserialization speed for proofs and metadata.

**Categories:**
- `Serialization`, `Deserialization`
- `Proof` - Proof serialization
- `RootHash` - Root hash serialization
- `RoundTrip` - Full serialize/deserialize cycle
- `HashFunction` - Different hash sizes

**Key Metrics:**
- Serialization throughput
- Deserialization speed
- Binary format efficiency

## Understanding Results

### Output Formats

BenchmarkDotNet produces results in multiple formats:

- **Console Output**: Summary table with key metrics
- **Markdown**: `BenchmarkDotNet.Artifacts/results/*.md`
- **HTML**: `BenchmarkDotNet.Artifacts/results/*.html`
- **CSV**: `BenchmarkDotNet.Artifacts/results/*.csv`

### Key Metrics

- **Mean**: Average execution time across iterations
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Median**: 50th percentile
- **Gen0/Gen1/Gen2**: Garbage collection statistics per 1000 operations
- **Allocated**: Total memory allocated per operation

### Interpreting Results

#### Performance Targets

| Operation | Tree Size | Target Time |
|-----------|-----------|-------------|
| Tree Building | 1,000 leaves | < 10 ms |
| Tree Building | 10,000 leaves | < 100 ms |
| Proof Generation | Any size | < 1 ms |
| Proof Verification | Any size | < 1 ms |
| Serialization | Any proof | < 500 μs |

#### Memory Efficiency

- In-memory trees should use O(n) memory where n = leaf count
- Streaming trees should use O(log n) memory
- Cache should provide at least 2x speedup for repeated proof generation

## Performance Regression Detection

### Baseline Results

To track performance over time, establish baseline results:

```bash
# Create baseline
dotnet run -c Release -- --exporters json --filter *

# Store baseline results
cp BenchmarkDotNet.Artifacts/results/*-report.json baseline/
```

### Comparing Results

```bash
# Run benchmarks and compare with baseline
dotnet run -c Release -- --baseline "Baseline" --filter *
```

### CI Integration

The benchmarks can be integrated into CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
- name: Run Benchmarks
  run: |
    cd benchmarks/MerkleTree.Benchmarks
    dotnet run -c Release -- --exporters json

- name: Check Performance Regression
  run: |
    # Compare with previous results
    # Fail if performance degrades > 20%
```

## Advanced Usage

### Custom Configuration

Create a `BenchmarkDotNet.Artifacts/config.json`:

```json
{
  "jobs": [
    {
      "warmupCount": 10,
      "iterationCount": 20
    }
  ]
}
```

### Memory Profiling

```bash
# Enable memory diagnoser
dotnet run -c Release -- --memory --filter *TreeBuilding*
```

### Statistical Analysis

```bash
# Export detailed statistics
dotnet run -c Release -- --statisticalTest 3ms --filter *
```

## Best Practices

1. **Always run in Release mode** - Debug builds are not representative
2. **Close unnecessary applications** - Reduce system noise
3. **Run multiple iterations** - Ensure statistical significance
4. **Monitor system resources** - CPU/memory availability affects results
5. **Use consistent hardware** - Compare results on same machine
6. **Disable CPU throttling** - Ensure consistent clock speeds
7. **Track results over time** - Identify trends and regressions

## Troubleshooting

### Long Running Times

Some benchmarks may take several minutes to complete. To speed up:

```bash
# Reduce iterations
dotnet run -c Release -- --iterationCount 3 --filter *

# Run only fast benchmarks
dotnet run -c Release -- --filter *Small*
```

### High Memory Usage

Large tree benchmarks may require significant memory. Adjust as needed:

- Close memory-intensive applications
- Reduce benchmark scale in code if needed
- Run benchmarks individually

### Inconsistent Results

If results vary significantly between runs:

1. Ensure system is idle
2. Disable background processes
3. Increase warmup iterations
4. Check for thermal throttling

## Contributing

When adding new benchmarks:

1. Follow existing naming conventions
2. Use appropriate `[BenchmarkCategory]` attributes
3. Include XML documentation
4. Add memory diagnostics with `[MemoryDiagnoser]`
5. Use realistic data sizes
6. Document expected performance characteristics

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [.NET Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/core/performance/)
- [Merkle Tree Library Documentation](../../README.md)
