using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Loggers;

namespace MerkleTree.Benchmarks;

/// <summary>
/// Custom BenchmarkDotNet configuration for consistent benchmark execution.
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        // Add standard columns
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.Error);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(StatisticColumn.Median);
        AddColumn(BaselineColumn.Default);
        AddColumn(RankColumn.Arabic);

        // Add exporters for various output formats
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);
        AddExporter(CsvExporter.Default);
        AddExporter(JsonExporter.Brief);

        // Add console logger
        AddLogger(ConsoleLogger.Default);

        // Keep benchmark artifacts for analysis
        WithOptions(ConfigOptions.KeepBenchmarkFiles);
        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}
