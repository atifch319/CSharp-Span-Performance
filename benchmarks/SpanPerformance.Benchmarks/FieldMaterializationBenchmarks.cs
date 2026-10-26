using BenchmarkDotNet.Attributes;
using SpanPerformance.Techniques;

namespace SpanPerformance.Benchmarks;

/// <summary>
/// Copies all ten fields into strings. This is the case where a span still has to allocate the result.
/// One operation is <see cref="SampleData.BenchmarkBatchSize"/> lines.
/// </summary>
[Config(typeof(MeasuredRunConfig))]
public class FieldMaterializationBenchmarks
{
    private readonly string[] _lines = SampleData.CreateBatch(SampleData.BenchmarkBatchSize);

    [GlobalSetup]
    public void Setup()
    {
        foreach (string line in _lines)
        {
            bool substring = FieldMaterialization.Before.TryChecksumWithSubstring(line, out int left);
            bool split = FieldMaterialization.Before.TryChecksumWithSplit(line, out int middle);
            bool span = FieldMaterialization.After.TryChecksum(line, out int right);
            if (!substring || !split || !span || left != middle || middle != right)
            {
                throw new InvalidOperationException("Field checksums diverged on benchmark input.");
            }
        }
    }

    [Benchmark(Baseline = true, Description = "Substring every field")]
    public int Substring()
    {
        int checksum = 0;
        foreach (string line in _lines)
        {
            if (!FieldMaterialization.Before.TryChecksumWithSubstring(line, out int folded))
            {
                throw new InvalidOperationException("Benchmark input must have 10 fields.");
            }

            checksum = unchecked(checksum + folded);
        }

        return checksum;
    }

    [Benchmark(Description = "Split, then keep every string")]
    public int Split()
    {
        int checksum = 0;
        foreach (string line in _lines)
        {
            if (!FieldMaterialization.Before.TryChecksumWithSplit(line, out int folded))
            {
                throw new InvalidOperationException("Benchmark input must have 10 fields.");
            }

            checksum = unchecked(checksum + folded);
        }

        return checksum;
    }

    [Benchmark(Description = "Span split, then ToString every slice")]
    public int SpanToString()
    {
        int checksum = 0;
        foreach (string line in _lines)
        {
            if (!FieldMaterialization.After.TryChecksum(line, out int folded))
            {
                throw new InvalidOperationException("Benchmark input must have 10 fields.");
            }

            checksum = unchecked(checksum + folded);
        }

        return checksum;
    }
}
