using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SpanPerformance.Parsing;

namespace SpanPerformance.Benchmarks;

/// <summary>
/// Full payment-log parse. One operation is <see cref="SampleData.BenchmarkBatchSize"/> lines.
/// The returned checksum keeps every summary field live so the JIT cannot delete the work.
/// </summary>
[Config(typeof(MeasuredRunConfig))]
public class LogParsingBenchmarks
{
    private readonly string[] _lines = SampleData.CreateBatch(SampleData.BenchmarkBatchSize);

    [GlobalSetup]
    public void Setup()
    {
        foreach (string line in _lines)
        {
            bool substring = SubstringLogParser.TryParse(line, out TransactionSummary left);
            bool split = SplitLogParser.TryParse(line, out TransactionSummary middle);
            bool span = SpanLogParser.TryParse(line, out TransactionSummary right);
            if (!substring || !split || !span || left != middle || middle != right)
            {
                throw new InvalidOperationException("Parsers diverged on benchmark input.");
            }
        }
    }

    [Benchmark(Baseline = true, Description = "Substring fields, then parse strings")]
    public int Substring()
    {
        int checksum = 0;
        foreach (string line in _lines)
        {
            if (!SubstringLogParser.TryParse(line, out TransactionSummary summary))
            {
                ThrowInvalid();
            }

            checksum = Consume(checksum, summary);
        }

        return checksum;
    }

    [Benchmark(Description = "Split the line, then parse strings")]
    public int Split()
    {
        int checksum = 0;
        foreach (string line in _lines)
        {
            if (!SplitLogParser.TryParse(line, out TransactionSummary summary))
            {
                ThrowInvalid();
            }

            checksum = Consume(checksum, summary);
        }

        return checksum;
    }

    [Benchmark(Description = "Slice and parse in place; copy two strings")]
    public int Span()
    {
        int checksum = 0;
        foreach (string line in _lines)
        {
            if (!SpanLogParser.TryParse(line, out TransactionSummary summary))
            {
                ThrowInvalid();
            }

            checksum = Consume(checksum, summary);
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(int checksum, TransactionSummary summary)
    {
        checksum = unchecked(checksum + summary.AccountNumber);
        checksum = unchecked(checksum + (int)summary.Amount);
        checksum = unchecked(checksum + summary.Timestamp.Millisecond);
        checksum = unchecked(checksum + (int)summary.Level);
        checksum = unchecked(checksum + (int)summary.Currency);
        checksum = unchecked(checksum + (int)summary.Status);
        checksum = unchecked(checksum ^ (int)summary.Trace);
        checksum = unchecked(checksum + summary.TransactionId.Length + summary.Merchant.Length);
        checksum = unchecked(checksum + summary.TransactionId[0] + summary.Merchant[0]);
        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalid() =>
        throw new InvalidOperationException("Benchmark input must parse.");
}
