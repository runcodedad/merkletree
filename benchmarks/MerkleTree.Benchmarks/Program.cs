using BenchmarkDotNet.Running;

namespace MerkleTree.Benchmarks;

/// <summary>
/// Entry point for running performance benchmarks.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
