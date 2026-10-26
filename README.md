# CSharp-Span-Performance

**Stop writing slow C# — use `Span<T>` on the hot path.**

A payment gateway ingests pipe-delimited transaction logs continuously. The hot path classifies each line and keeps two strings: the transaction id and the merchant. Everything else is a timestamp, an enum, a decimal, or an integer. Copying those fields into strings is work the program throws away.

This solution shows five practical uses of `Span<T>` and `ReadOnlySpan<T>` on that workload, with equivalent `Substring` and `Split` implementations, xUnit tests, and BenchmarkDotNet measurements of time and allocated bytes. `Span<T>` removes allocations when the caller can consume a slice in place. It does not make every string operation faster. The [control benchmark](#when-every-field-must-be-a-string) copies every field into a string; the allocation gap collapses there. Re-run the benchmarks on your hardware before treating any ratio as a property of `Span<T>`.

## Log line

```text
2026-10-26T12:34:56.789Z|INFO|PaymentGateway|TXN-8F3A1C2E|ACC-004821|1299.50|USD|SETTLED|ACME CORP|A1B2C3D4E5F67890
```

| Index | Field | Rule |
| ---: | --- | --- |
| 0 | Timestamp | `yyyy-MM-ddTHH:mm:ss.fffZ`, read as UTC |
| 1 | Level | `INFO`, `WARN`, `ERROR` |
| 2 | Service | `PaymentGateway` |
| 3 | Transaction id | `TXN-` plus 8 hex digits. Retained as a string |
| 4 | Account | `ACC-` plus 1–10 digits. Stored as `int` (`ACC-004821` is `4821`) |
| 5 | Amount | Invariant decimal, optional `.`, at most two decimal places, zero or positive |
| 6 | Currency | `USD`, `EUR`, `GBP` |
| 7 | Status | `AUTHORIZED`, `SETTLED`, `DECLINED`, `REVERSED` |
| 8 | Merchant | Non-empty, not trimmed. May contain spaces. Retained as a string |
| 9 | Trace | Exactly 16 hex digits. Stored as `ulong` |

Fields are separated by `|`. The delimiter is not escaped, so a field cannot contain `|`. Token comparisons are ordinal and case-sensitive. Invalid lines return `false` and leave the `out` value at `default`. A null `string` argument throws `ArgumentNullException`. The span overloads take a `ReadOnlySpan<char>`, so they also parse a slice of a larger buffer.

`SubstringLogParser`, `SplitLogParser`, and `SpanLogParser` implement those rules. The tests require them to agree on valid lines, rejected lines, and a 128-line batch.

## Five uses

### 1. Slice a field instead of `Substring`

`Techniques/FieldSlicing.cs` reads the amount (field 5). Both sides return the same `decimal`.

`Before` copies the field, then parses the string:

```csharp
int end = line.IndexOf('|', start);
string field = end < 0 ? line.Substring(start) : line.Substring(start, end - start);
return FieldParsers.TryParseAmount(field, out amount);
```

`After` slices and calls `decimal.TryParse(ReadOnlySpan<char>, ...)`:

```csharp
int end = line[start..].IndexOf('|');
ReadOnlySpan<char> field = end < 0 ? line[start..] : line.Slice(start, end);
return FieldParsers.TryParseAmount(field, out amount);
```

`Slice` is O(1) and does not allocate. The win shows up because the amount is parsed from the slice. `field.ToString()` would allocate a string of the same size as `Substring`, and the two would be much closer.

### 2. Tokenize with a span split instead of `string.Split`

`Techniques/LineSplitting.cs` requires exactly ten fields and returns the same amount.

```csharp
string[] fields = line.Split('|', StringSplitOptions.None);
return FieldParsers.TryParseAmount(fields[5], out amount);
```

`string.Split` allocates the array and a string per field, including fields this caller never reads. `StringSplitOptions.None` keeps empty entries, so a doubled pipe does not shift later columns. The span enumerator yields `Range` values over the same line:

```csharp
foreach (Range range in line.Split('|'))
{
    if (index == LogFormat.AmountIndex)
        amountText = line[range];
    index++;
}
```

`IndexOf` for a single character is already vectorized. Replacing the scan with a span does not change that. The extra cost of `Split`, once every field is retained as a string, is the array. The [materialization benchmark](#when-every-field-must-be-a-string) measured that array at 13 KB per 128-line batch (104 bytes per line) on the run below.

### 3. Parse primitives with the span `TryParse` overloads

`Techniques/PrimitiveParsing.cs` turns a timestamp, an amount, and an account into a `ParsedPrimitives` value. The string overloads are what the `Substring` and `Split` parsers call. The span overloads parse the same characters.

The account prefix is the sharp example. The string path drops `ACC-` with `Substring`, which allocates a second string, then calls `int.TryParse(string, ...)`:

```csharp
string digits = text.Substring(LogFormat.AccountPrefix.Length);
return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out accountNumber);
```

The span path slices the prefix off without copying the digits:

```csharp
ReadOnlySpan<char> digits = text[LogFormat.AccountPrefix.Length..];
return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out accountNumber);
```

`DateTimeOffset.TryParseExact`, `decimal.TryParse`, and `ulong.TryParse` have the same pair of overloads. The full span parser uses them for every value field.

### 4. Match tokens on `ReadOnlySpan<char>`

`Techniques/TokenMatching.cs` escalates a line when the level is `ERROR` or the status is `DECLINED` or `REVERSED`. Both sides return the same `bool`. Comparisons are ordinal, which is the right choice for log tokens.

```csharp
level.Equals("ERROR", StringComparison.Ordinal)
    || status.Equals("DECLINED", StringComparison.Ordinal)
    || status.Equals("REVERSED", StringComparison.Ordinal);
```

```csharp
level.SequenceEqual("ERROR")
    || status.SequenceEqual("DECLINED")
    || status.SequenceEqual("REVERSED");
```

`SequenceEqual` is an ordinal, case-sensitive compare. `"ERROR"` converts to `ReadOnlySpan<char>` without allocating. The `Before` path pays for two `Substring` calls so it has something to compare. A culture-aware compare, or `ToUpperInvariant()`, would add both cost and incorrect matches.

### 5. Fill a `Span<byte>` instead of allocating a `byte[]`

`Techniques/TraceBuffer.cs` decodes the 16 hex digits into 8 bytes. The first pair is the first byte, so a big-endian read matches `ulong.TryParse` of the same text.

`Before` calls `Convert.FromHexString`, which returns a new array:

```csharp
bytes = Convert.FromHexString(hex);
```

`After` writes into memory the caller already owns. On invalid input it leaves that memory unchanged:

```csharp
public static bool TryDecode(ReadOnlySpan<char> hex, Span<byte> destination)
{
    Span<byte> decoded = stackalloc byte[8];
    // validate and write decoded[i], then copy only if every nibble was valid
    decoded.CopyTo(destination);
    return true;
}
```

The caller can pass `stackalloc byte[8]`, an array, or a rented buffer, and can reuse that buffer across lines. `stackalloc` here is a fixed 8 bytes. It is not sized from the log line. A `stackalloc` as large as the input can overflow the stack.

The full log parser stores the trace as a `ulong` and calls `ulong.TryParse(ReadOnlySpan<char>, NumberStyles.AllowHexSpecifier, ...)`. That is the better tool when the next stage wants a number. The byte buffer is the tool when the next stage wants bytes. On the run below, the scalar decoder used no heap memory and took longer than `Convert.FromHexString`.

## Full parsers

| Parser | How fields are separated | What gets allocated on a valid line |
| --- | --- | --- |
| `SubstringLogParser` | `IndexOf` + `Substring` for every field | One string per field, the `string[10]`, and a second string for the account digits |
| `SplitLogParser` | `string.Split('|', StringSplitOptions.None)` | The `string[]`, one string per field, and the account-digit string |
| `SpanLogParser` | `IndexOf` into a `stackalloc Range[10]` | The transaction id and the merchant, plus any allocations inside the BCL parse methods. The measured total is in the results |

`SpanLogParser.TryParse(ReadOnlySpan<char>)` can run on a slice of a chunk that was read from a socket or a file. The two retained strings outlive that chunk. The string parsers need a `string` per line before they can start.

```csharp
Span<Range> fields = stackalloc Range[LogFormat.FieldCount];
if (!TryReadFields(line, fields))
    return false;

// validate every span first
summary = new TransactionSummary(
    timestamp, level, amount, currency, status, accountNumber, trace,
    transactionId.ToString(),
    merchant.ToString());
```

## `Span<T>` and `ReadOnlySpan<T>`

Both are `ref struct` views of contiguous memory. They store a pointer and a length. They do not own that memory, and slicing one is creating another view.

`Span<T>` can write through the view. `ReadOnlySpan<T>` cannot. `Span<T>` converts implicitly to `ReadOnlySpan<T>`, so a method that only reads should take `ReadOnlySpan<T>`. Callers can then pass a `Span<T>`, a `ReadOnlySpan<T>`, a `string` (`AsSpan()` or the implicit conversion), an array, or a stack buffer.

`string` is immutable, so `string.AsSpan()` returns `ReadOnlySpan<char>`. Writing log bytes into a buffer, or decoding hex into `destination[i]`, needs `Span<T>`. Parsing a log line needs `ReadOnlySpan<char>`.

`ReadOnlySpan<T>` means "this view cannot write." It does not mean the underlying memory is immutable. A `ReadOnlySpan<byte>` over a shared array still races if another thread writes that array.

## Limitations

- A span cannot be boxed, cannot be an element of an array, and cannot be a field of a class or a normal struct. It can be a field of another `ref struct`. In C# 13 a `ref struct` can implement an interface, and a generic parameter can allow one with `allows ref struct`, but boxing is still illegal. `List<Span<char>>` does not compile.
- A span cannot live across an `await`, and it cannot be used in an iterator. Parse synchronously into a `TransactionSummary` (or into strings and numbers), then await.
- Do not capture a span in a lambda that escapes the method.
- The compiler rejects returning a span that points at a local `stackalloc`. Returning a span derived from a parameter is legal. The caller has to keep the owner alive for as long as the span is used.
- While a span sits on the stack, the GC treats it as an interior pointer and keeps a heap owner alive. That protection ends when the method returns. Store the data you need before returning: a parsed `decimal`, or an explicit `ToString()` copy.
- `ToString()` on a `ReadOnlySpan<char>` allocates a new string. Slicing does not.
- `stackalloc` comes out of the thread stack (typically 1 MB). This sample stack-allocates 10 ranges or 8 bytes. Do not stack-allocate a buffer sized to an untrusted line.

## Tradeoffs

- The allocation win is largest when most fields are parsed or compared and then discarded. A pipeline that must retain every field as a `string` still allocates those strings. See the materialization numbers below.
- `string.Split` in modern .NET is heavily optimized. It still has to return a `string[]` and a `string` per field, because that is its contract. A span splitter that then calls `ToString()` on every range accepts the same contract and pays a similar cost.
- One `Substring` and one `Slice().ToString()` for a single field you need to keep are usually a tie. Both copy those characters into a new string.
- The span parsers are longer and the lifetimes are part of the API. For code that runs once, or is dominated by I/O, `Split` is the clearer implementation and the allocation is noise.
- Small time gaps move around with the machine, the GC mode, and the input mix. Benchmarks below use a fixed 128-line batch of valid lines, in Release, with BenchmarkDotNet's memory diagnoser. Debug builds are not comparable. A workload of mostly invalid lines, or lines where every field is retained, is a different measurement.
- Single-character `IndexOf` is already vectorized. The reason to reach for a span here is the avoided copy, not a faster search.
- Prefer the span `TryParse` overload when you want a number. Prefer a caller-supplied `Span<byte>` when you want bytes. A hand-rolled decoder is not automatically faster than the BCL; it is the way to avoid the `byte[]` that `Convert.FromHexString(string)` returns.

## Project structure

```text
CSharp-Span-Performance.slnx
Directory.Build.props
global.json
src/SpanPerformance/
  LogFormat.cs                  field indexes and grammar constants
  TransactionSummary.cs         retained result
  SampleData.cs                 deterministic lines shared by tests and benchmarks
  Parsing/
    SubstringLogParser.cs       IndexOf + Substring
    SplitLogParser.cs           string.Split
    SpanLogParser.cs            span slices and span TryParse
    FieldParsers.cs             string and span overloads of the field rules
  Techniques/
    FieldSlicing.cs             use 1
    LineSplitting.cs            use 2
    PrimitiveParsing.cs         use 3
    TokenMatching.cs            use 4
    TraceBuffer.cs              use 5
    FieldMaterialization.cs     control: copy every field to a string
tests/SpanPerformance.Tests/
benchmarks/SpanPerformance.Benchmarks/
```

## Build and test

Requires the .NET 10 SDK (`global.json` asks for `10.0.100` and rolls forward within the 10.0 feature band).

```bash
dotnet build CSharp-Span-Performance.slnx
dotnet test CSharp-Span-Performance.slnx
```

## Benchmarks

Benchmarks run in Release. The Debug host refuses to start them.

```bash
dotnet run -c Release --project benchmarks/SpanPerformance.Benchmarks
```

A run with no arguments measures every benchmark class. Run one class:

```bash
dotnet run -c Release --project benchmarks/SpanPerformance.Benchmarks -- --filter *LogParsing*
dotnet run -c Release --project benchmarks/SpanPerformance.Benchmarks -- --filter *FieldMaterialization*
dotnet run -c Release --project benchmarks/SpanPerformance.Benchmarks -- --filter *TraceBuffer*
```

`MeasuredRunConfig` uses one launch, 3 warmup iterations, 6 measured iterations, and a 150 ms iteration time. `MemoryDiagnoser` records allocated bytes. Each benchmark method processes `SampleData.BenchmarkBatchSize` lines (128). The **Mean** column is the time for that whole batch. Dividing **Allocated** by 128 is arithmetic on the measured batch total, reported below as "per line" only as a reading aid.

The log-parsing benchmarks return a checksum that reads every field of `TransactionSummary`, including both retained strings, so the JIT cannot delete the parse. A `[GlobalSetup]` method throws if the three parsers disagree. The trace benchmark passes each `byte[]` into a non-inlined method so the array from `Convert.FromHexString` stays a real heap allocation, and it reuses one `stackalloc byte[8]` for the span path.

GitHub-flavored tables are written under `BenchmarkDotNet.Artifacts/` (gitignored). Re-run with BenchmarkDotNet's default job when you want tighter error bars than this short job.

## Measured results

These tables are copied from BenchmarkDotNet 0.15.8 on this machine. One operation is one batch of 128 lines. **Mean** is the time for that batch. **Error** is half of the 99.9% confidence interval. **Allocated** follows BenchmarkDotNet's rule that 1 KB = 1024 bytes. **Ratio** is the mean of the per-iteration ratios to the baseline, so the baseline row can print as 1.01. **Gen0** is collections per 1000 batches, not per 1000 lines.

```text
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Intel Core i7-8550U CPU 1.80GHz (Max: 1.99GHz) (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Measured : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

GC=Concurrent Workstation
Job=Measured  IterationCount=6  IterationTime=150ms  LaunchCount=1  WarmupCount=3
```

BenchmarkDotNet switched the OS power plan to High performance for the run and restored Balanced afterward. Global total time was 38.89 seconds for all 8 benchmarks.

### Full log parse

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Substring fields, then parse strings | 130.31 μs | 25.593 μs | 9.127 μs | 1.00 | 0.09 | 16.8269 | 75.25 KB | 1.00 |
| Split the line, then parse strings | 96.14 μs | 10.243 μs | 2.660 μs | 0.74 | 0.05 | 18.3081 | 75.25 KB | 1.00 |
| Slice and parse in place; copy two strings | 82.20 μs | 2.747 μs | 0.980 μs | 0.63 | 0.04 | 2.1930 | 11 KB | 0.15 |

`75.25 KB` is 77,056 bytes, which is 602 bytes per line. `11 KB` is 11,264 bytes, which is 88 bytes per line. Both string parsers landed on the same total: each builds a 10-string array, a string per field, and a second string for the account digits. The span parser's 11 KB is the measured remainder after those field copies are gone. The code asks for two strings per line. Allocations inside `TryParse` are included in that 11 KB; this run did not split them out.

The span parser's detailed interval was [79.450 μs, 84.945 μs]. Split's was [85.900 μs, 106.385 μs] after BenchmarkDotNet dropped one iteration at 104.64 μs (N = 5). Substring's was [104.720 μs, 155.906 μs] (N = 6). The span interval sits below the other two. The Substring and Split intervals meet at the edge, and Substring's interval is wide.

BenchmarkDotNet warned that the shortest Substring iteration lasted 49.685 ms. The pilot had seen roughly 360 μs/op and scheduled 416 calls, aiming at the 150 ms iteration time. The measured iterations ran at about 120–140 μs/op, so that iteration finished under the 100 ms BenchmarkDotNet prefers. The mean above is still the mean of the measured iterations. A longer iteration time would tighten the Substring interval. It would not change the fact that both string parsers allocated 75.25 KB and the span parser allocated 11 KB.

### When every field must be a string

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Substring every field | 26.08 μs | 6.659 μs | 2.375 μs | 1.01 | 0.12 | 13.9423 | 57.25 KB | 1.00 |
| Split, then keep every string | 40.55 μs | 14.660 μs | 5.228 μs | 1.56 | 0.22 | 17.1162 | 70.25 KB | 1.23 |
| Span split, then ToString every slice | 29.05 μs | 1.358 μs | 0.353 μs | 1.12 | 0.09 | 13.8436 | 57.25 KB | 1.00 |

`Substring` and `Slice().ToString()` allocated the same 57.25 KB (58,624 bytes, 458 bytes per line). `Split` allocated 70.25 KB (71,936 bytes, 562 bytes per line). The 13 KB gap is 104 bytes per line. On this 64-bit runtime that is the size of one `string[10]` (header, length, ten references, and padding), and `Split` returns one of those arrays per line while the `Substring` path never builds it. The span row's detailed interval was [27.688 μs, 30.404 μs] after one iteration at 31.04 μs was removed (N = 5). That interval sits inside both string intervals ([19.424 μs, 32.742 μs] for `Substring`, [25.889 μs, 55.210 μs] for `Split`). This job does not separate those three times. It does separate the array allocation.

### Trace bytes

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Convert.FromHexString | 2.865 μs | 0.7031 μs | 0.2507 μs | 1.01 | 0.11 | 0.9642 | 4096 B | 1.00 |
| Decode into one stackalloc `Span<byte>` | 6.147 μs | 0.3956 μs | 0.1411 μs | 2.16 | 0.18 | - | - | 0.00 |

`4096 B / 128` is 32 bytes per trace, a `byte[8]` plus its object header. The stackalloc decoder's allocated column is `-`, which is what BenchmarkDotNet prints when it records no managed allocations. Its detailed interval was [5.751 μs, 6.542 μs]. `Convert.FromHexString`'s was [2.161 μs, 3.568 μs]. The intervals do not overlap: the scalar decoder was slower on this CPU, and it did not allocate. `SpanLogParser` does not use that decoder. It parses the trace with `ulong.TryParse` on a span, because the summary stores a `ulong`.
