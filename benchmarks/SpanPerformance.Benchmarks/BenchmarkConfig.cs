using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;

namespace SpanPerformance.Benchmarks;

/// <summary>
/// One launch, three warmup iterations, and six measured iterations of at least 150 ms.
/// The default BenchmarkDotNet config still supplies the console logger, columns, and exporters.
/// This type adds the memory diagnoser and sets the job.
/// Re-run without this config when you want tighter error bars.
/// </summary>
public sealed class MeasuredRunConfig : ManualConfig
{
    public MeasuredRunConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);

        AddJob(Job.Default
            .WithLaunchCount(1)
            .WithWarmupCount(3)
            .WithIterationCount(6)
            .WithIterationTime(TimeInterval.FromMilliseconds(150))
            .WithId("Measured"));
    }
}
