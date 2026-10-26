using System.Globalization;

namespace SpanPerformance;

/// <summary>
/// Deterministic log lines shared by tests and benchmarks.
/// </summary>
public static class SampleData
{
    public const int BenchmarkBatchSize = 128;

    public const string SettledUsdLine =
        "2026-10-26T12:34:56.789Z|INFO|PaymentGateway|TXN-8F3A1C2E|ACC-004821|1299.50|USD|SETTLED|ACME CORP|A1B2C3D4E5F67890";

    public static readonly DateTimeOffset SettledUsdTimestamp =
        new(2026, 10, 26, 12, 34, 56, 789, TimeSpan.Zero);

    public static string CreateLine(
        DateTimeOffset timestamp,
        string level,
        string transactionId,
        int accountNumber,
        decimal amount,
        string currency,
        string status,
        string merchant,
        ulong trace)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(transactionId);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(merchant);

        return string.Join(
            LogFormat.Delimiter,
            timestamp.UtcDateTime.ToString(LogFormat.TimestampPattern, CultureInfo.InvariantCulture),
            level,
            LogFormat.ServiceName,
            transactionId,
            LogFormat.AccountPrefix + accountNumber.ToString(CultureInfo.InvariantCulture),
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            currency,
            status,
            merchant,
            trace.ToString("X16", CultureInfo.InvariantCulture));
    }

    public static string[] CreateBatch(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        string[] levels = ["INFO", "WARN", "ERROR"];
        string[] currencies = ["USD", "EUR", "GBP"];
        string[] statuses = ["AUTHORIZED", "SETTLED", "DECLINED", "REVERSED"];
        string[] merchants = ["ACME CORP", "NORTHWIND", "CONTOSO", "FABRIKAM", "ACME CAFE"];
        var lines = new string[count];

        for (int i = 0; i < count; i++)
        {
            var timestamp = new DateTimeOffset(2026, 10, 26, 12, 34, 56, i % 1000, TimeSpan.Zero);
            decimal amount = (i % 50_000) / 100m;
            int account = (i % 900_000) + 1;
            ulong trace = 0xA1B2C3D4E5F67890UL ^ (uint)i;

            lines[i] = string.Join(
                LogFormat.Delimiter,
                timestamp.UtcDateTime.ToString(LogFormat.TimestampPattern, CultureInfo.InvariantCulture),
                levels[i % levels.Length],
                LogFormat.ServiceName,
                "TXN-" + i.ToString("X8", CultureInfo.InvariantCulture),
                LogFormat.AccountPrefix + account.ToString("D6", CultureInfo.InvariantCulture),
                amount.ToString("0.00", CultureInfo.InvariantCulture),
                currencies[i % currencies.Length],
                statuses[i % statuses.Length],
                merchants[i % merchants.Length],
                trace.ToString("X16", CultureInfo.InvariantCulture));
        }

        return lines;
    }
}
