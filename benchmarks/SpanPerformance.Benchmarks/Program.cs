using BenchmarkDotNet.Running;

#if DEBUG
Console.Error.WriteLine("Run benchmarks in Release. Debug timings and allocations are not comparable.");
Console.Error.WriteLine("dotnet run -c Release --project benchmarks/SpanPerformance.Benchmarks");
return 1;
#else
// BenchmarkSwitcher waits for console input when no filter is supplied.
// A bare `dotnet run` measures every benchmark. Pass `--filter` to narrow it.
string[] benchmarkArgs = args.Length == 0 ? ["--filter", "*"] : args;
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArgs);
return 0;
#endif
