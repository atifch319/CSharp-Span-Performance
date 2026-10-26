using System.Globalization;
using SpanPerformance.Parsing;

namespace SpanPerformance.Tests;

public class ParserTests
{
    private static readonly TransactionSummary SettledUsd = new(
        SampleData.SettledUsdTimestamp,
        LogLevel.Info,
        1299.50m,
        Currency.Usd,
        TransactionStatus.Settled,
        4821,
        ulong.Parse("A1B2C3D4E5F67890", NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
        "TXN-8F3A1C2E",
        "ACME CORP");

    [Fact]
    public void SampleLine_AllParsersReturnTheSameSummary()
    {
        AssertParsed(SampleData.SettledUsdLine, SettledUsd);
    }

    [Fact]
    public void SampleLine_SpanParserAcceptsASliceOfALargerBuffer()
    {
        const string prefix = "batch-header\n";
        string blob = prefix + SampleData.SettledUsdLine + "\nnext";
        ReadOnlySpan<char> slice = blob.AsSpan(prefix.Length, SampleData.SettledUsdLine.Length);

        Assert.True(SpanLogParser.TryParse(slice, out TransactionSummary summary));
        Assert.Equal(SettledUsd, summary);
    }

    [Fact]
    public void FirstGeneratedLine_MatchesTheKnownTextAndAllParsers()
    {
        const string expected =
            "2026-10-26T12:34:56.000Z|INFO|PaymentGateway|TXN-00000000|ACC-000001|0.00|USD|AUTHORIZED|ACME CORP|A1B2C3D4E5F67890";

        string line = SampleData.CreateBatch(1)[0];
        Assert.Equal(expected, line);

        var summary = new TransactionSummary(
            new DateTimeOffset(2026, 10, 26, 12, 34, 56, 0, TimeSpan.Zero),
            LogLevel.Info,
            0m,
            Currency.Usd,
            TransactionStatus.Authorized,
            1,
            ulong.Parse("A1B2C3D4E5F67890", NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
            "TXN-00000000",
            "ACME CORP");
        AssertParsed(line, summary);
    }

    [Fact]
    public void BenchmarkBatch_AllParsersAgreeOnEveryLine()
    {
        foreach (string line in SampleData.CreateBatch(SampleData.BenchmarkBatchSize))
        {
            Assert.True(SubstringLogParser.TryParse(line, out TransactionSummary substring));
            Assert.True(SplitLogParser.TryParse(line, out TransactionSummary split));
            Assert.True(SpanLogParser.TryParse(line, out TransactionSummary span));
            Assert.Equal(substring, split);
            Assert.Equal(split, span);
            Assert.False(string.IsNullOrEmpty(span.TransactionId));
            Assert.False(string.IsNullOrEmpty(span.Merchant));
        }
    }

    [Theory]
    [MemberData(nameof(ValidLines))]
    public void ValidLine_AllParsersReturnTheExpectedSummary(string line, TransactionSummary expected)
    {
        AssertParsed(line, expected);
    }

    [Theory]
    [MemberData(nameof(InvalidLines))]
    public void InvalidLine_AllParsersRejectAndLeaveTheResultDefault(string line)
    {
        Assert.False(SubstringLogParser.TryParse(line, out TransactionSummary substring));
        Assert.False(SplitLogParser.TryParse(line, out TransactionSummary split));
        Assert.False(SpanLogParser.TryParse(line, out TransactionSummary span));
        Assert.Equal(default, substring);
        Assert.Equal(substring, split);
        Assert.Equal(split, span);
    }

    [Fact]
    public void NullLine_StringParsersThrow()
    {
        Assert.Throws<ArgumentNullException>(() => SubstringLogParser.TryParse(null!, out _));
        Assert.Throws<ArgumentNullException>(() => SplitLogParser.TryParse(null!, out _));
        Assert.Throws<ArgumentNullException>(() => SpanLogParser.TryParse(null!, out _));
    }

    [Fact]
    public void EmptySpan_IsRejected()
    {
        Assert.False(SpanLogParser.TryParse(ReadOnlySpan<char>.Empty, out TransactionSummary summary));
        Assert.Equal(default, summary);
    }

    public static TheoryData<string, TransactionSummary> ValidLines()
    {
        ulong sampleTrace = ulong.Parse("A1B2C3D4E5F67890", NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var data = new TheoryData<string, TransactionSummary>();

        Add(data, SampleData.CreateLine(
            SampleData.SettledUsdTimestamp,
            "ERROR",
            "TXN-000000ab",
            0,
            0m,
            "EUR",
            "DECLINED",
            "東京商事",
            0UL),
            LogLevel.Error,
            0m,
            Currency.Eur,
            TransactionStatus.Declined,
            0,
            0UL,
            "TXN-000000ab",
            "東京商事");

        Add(data, WithFields(
            (LogFormat.LevelIndex, "WARN"),
            (LogFormat.AmountIndex, "10"),
            (LogFormat.CurrencyIndex, "GBP"),
            (LogFormat.StatusIndex, "REVERSED"),
            (LogFormat.AccountIndex, "ACC-2147483647"),
            (LogFormat.MerchantIndex, " ACME"),
            (LogFormat.TraceIndex, "a1b2c3d4e5f67890")),
            LogLevel.Warn,
            10m,
            Currency.Gbp,
            TransactionStatus.Reversed,
            int.MaxValue,
            sampleTrace,
            "TXN-8F3A1C2E",
            " ACME");

        Add(data, WithFields((LogFormat.AmountIndex, "1299.5")),
            LogLevel.Info, 1299.5m, Currency.Usd, TransactionStatus.Settled, 4821, sampleTrace, "TXN-8F3A1C2E", "ACME CORP");
        Add(data, WithFields((LogFormat.AmountIndex, "1299")),
            LogLevel.Info, 1299m, Currency.Usd, TransactionStatus.Settled, 4821, sampleTrace, "TXN-8F3A1C2E", "ACME CORP");
        Add(data, WithFields((LogFormat.AmountIndex, "0.01")),
            LogLevel.Info, 0.01m, Currency.Usd, TransactionStatus.Settled, 4821, sampleTrace, "TXN-8F3A1C2E", "ACME CORP");
        Add(data, WithFields((LogFormat.MerchantIndex, "   ")),
            LogLevel.Info, 1299.50m, Currency.Usd, TransactionStatus.Settled, 4821, sampleTrace, "TXN-8F3A1C2E", "   ");
        Add(data, WithFields((LogFormat.AccountIndex, "ACC-0000000001")),
            LogLevel.Info, 1299.50m, Currency.Usd, TransactionStatus.Settled, 1, sampleTrace, "TXN-8F3A1C2E", "ACME CORP");

        string longMerchant = new('M', 4_000);
        Add(data, WithFields((LogFormat.MerchantIndex, longMerchant)),
            LogLevel.Info, 1299.50m, Currency.Usd, TransactionStatus.Settled, 4821, sampleTrace, "TXN-8F3A1C2E", longMerchant);

        return data;
    }

    public static TheoryData<string> InvalidLines()
    {
        string[] parts = SampleData.SettledUsdLine.Split('|');
        var data = new TheoryData<string>
        {
            string.Empty,
            " ",
            "not-a-log-line",
            new string('|', LogFormat.FieldCount - 1),
            SampleData.SettledUsdLine + "|EXTRA",
            string.Join('|', parts.Take(LogFormat.FieldCount - 1)),
            SampleData.SettledUsdLine + "|",
            "|" + SampleData.SettledUsdLine,
            SampleData.SettledUsdLine.Replace("|INFO|", "||", StringComparison.Ordinal),
        };

        data.Add(WithFields((LogFormat.TimestampIndex, "2026-10-26T12:34:56Z")));
        data.Add(WithFields((LogFormat.TimestampIndex, "2026-10-26T12:34:56.789+00:00")));
        data.Add(WithFields((LogFormat.TimestampIndex, "2026-10-26T12:34:56.789z")));
        data.Add(WithFields((LogFormat.TimestampIndex, "2026-13-26T12:34:56.789Z")));
        data.Add(WithFields((LogFormat.LevelIndex, "error")));
        data.Add(WithFields((LogFormat.LevelIndex, "INFO ")));
        data.Add(WithFields((LogFormat.LevelIndex, "DEBUG")));
        data.Add(WithFields((LogFormat.ServiceIndex, "paymentgateway")));
        data.Add(WithFields((LogFormat.TransactionIdIndex, "txn-8F3A1C2E")));
        data.Add(WithFields((LogFormat.TransactionIdIndex, "TXN-8F3A1C2")));
        data.Add(WithFields((LogFormat.TransactionIdIndex, "TXN-8F3A1C2G")));
        data.Add(WithFields((LogFormat.AccountIndex, "ACC-")));
        data.Add(WithFields((LogFormat.AccountIndex, "acc-4821")));
        data.Add(WithFields((LogFormat.AccountIndex, "ACC-2147483648")));
        data.Add(WithFields((LogFormat.AccountIndex, "ACC-00000000000")));
        data.Add(WithFields((LogFormat.AccountIndex, "ACC-12.0")));
        data.Add(WithFields((LogFormat.AmountIndex, "12.345")));
        data.Add(WithFields((LogFormat.AmountIndex, "-1.00")));
        data.Add(WithFields((LogFormat.AmountIndex, "+1.00")));
        data.Add(WithFields((LogFormat.AmountIndex, "1,299.00")));
        data.Add(WithFields((LogFormat.AmountIndex, "1e2")));
        data.Add(WithFields((LogFormat.AmountIndex, "50.")));
        data.Add(WithFields((LogFormat.AmountIndex, ".50")));
        data.Add(WithFields((LogFormat.AmountIndex, " 1299.50")));
        data.Add(WithFields((LogFormat.AmountIndex, "")));
        data.Add(WithFields((LogFormat.CurrencyIndex, "usd")));
        data.Add(WithFields((LogFormat.CurrencyIndex, "JPY")));
        data.Add(WithFields((LogFormat.StatusIndex, "settled")));
        data.Add(WithFields((LogFormat.StatusIndex, "PENDING")));
        data.Add(WithFields((LogFormat.MerchantIndex, "")));
        data.Add(WithFields((LogFormat.TraceIndex, "A1B2C3D4E5F6789")));
        data.Add(WithFields((LogFormat.TraceIndex, "A1B2C3D4E5F6789G")));
        data.Add(WithFields((LogFormat.TraceIndex, "0xB2C3D4E5F67890")));
        data.Add(WithFields((LogFormat.TraceIndex, "A1B2C3D4E5F67890 ")));
        return data;
    }

    private static void AssertParsed(string line, TransactionSummary expected)
    {
        Assert.True(SubstringLogParser.TryParse(line, out TransactionSummary substring));
        Assert.True(SplitLogParser.TryParse(line, out TransactionSummary split));
        Assert.True(SpanLogParser.TryParse(line, out TransactionSummary span));
        Assert.Equal(expected, substring);
        Assert.Equal(expected, split);
        Assert.Equal(expected, span);
    }

    private static void Add(
        TheoryData<string, TransactionSummary> data,
        string line,
        LogLevel level,
        decimal amount,
        Currency currency,
        TransactionStatus status,
        int accountNumber,
        ulong trace,
        string transactionId,
        string merchant)
    {
        data.Add(line, new TransactionSummary(
            SampleData.SettledUsdTimestamp,
            level,
            amount,
            currency,
            status,
            accountNumber,
            trace,
            transactionId,
            merchant));
    }

    private static string WithFields(params (int Index, string Value)[] changes)
    {
        string[] parts = SampleData.SettledUsdLine.Split('|');
        foreach ((int index, string value) in changes)
        {
            parts[index] = value;
        }

        return string.Join('|', parts);
    }
}
