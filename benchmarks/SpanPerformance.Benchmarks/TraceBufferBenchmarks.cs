using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SpanPerformance.Techniques;

namespace SpanPerformance.Benchmarks;

/// <summary>
/// Decodes one 16-character trace per line. The string path returns a new byte array.
/// The span path refills one stack buffer. One operation is <see cref="SampleData.BenchmarkBatchSize"/> traces.
/// </summary>
[Config(typeof(MeasuredRunConfig))]
public class TraceBufferBenchmarks
{
    private readonly string[] _traces = new string[SampleData.BenchmarkBatchSize];

    [GlobalSetup]
    public void Setup()
    {
        Span<byte> decoded = stackalloc byte[LogFormat.TraceByteLength];
        for (int i = 0; i < _traces.Length; i++)
        {
            ulong trace = 0xA1B2C3D4E5F67890UL ^ (uint)i;
            _traces[i] = trace.ToString("X16", CultureInfo.InvariantCulture);
            if (!TraceBuffer.Before.TryDecode(_traces[i], out byte[] allocated)
                || !TraceBuffer.After.TryDecode(_traces[i], decoded)
                || !allocated.AsSpan().SequenceEqual(decoded))
            {
                throw new InvalidOperationException("Trace decoders diverged on benchmark input.");
            }
        }
    }

    [Benchmark(Baseline = true, Description = "Convert.FromHexString")]
    public int FromHexString()
    {
        int checksum = 0;
        foreach (string hex in _traces)
        {
            if (!TraceBuffer.Before.TryDecode(hex, out byte[] bytes))
            {
                ThrowInvalid();
            }

            checksum = Consume(checksum, bytes);
        }

        return checksum;
    }

    [Benchmark(Description = "Decode into one stackalloc Span<byte>")]
    public int StackAllocSpan()
    {
        Span<byte> buffer = stackalloc byte[LogFormat.TraceByteLength];
        int checksum = 0;
        foreach (string hex in _traces)
        {
            if (!TraceBuffer.After.TryDecode(hex, buffer))
            {
                ThrowInvalid();
            }

            checksum = Consume(checksum, buffer);
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(int checksum, byte[] bytes) =>
        unchecked(checksum + bytes[0] + bytes[^1]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(int checksum, ReadOnlySpan<byte> bytes) =>
        unchecked(checksum + bytes[0] + bytes[^1]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalid() =>
        throw new InvalidOperationException("Benchmark input must decode.");
}
