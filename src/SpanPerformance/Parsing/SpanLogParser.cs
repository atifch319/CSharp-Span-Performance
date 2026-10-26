namespace SpanPerformance.Parsing;

/// <summary>
/// Parses a payment log line from a character span. Timestamps, tokens, and numbers are read in place.
/// Only the transaction id and merchant are copied to strings, because those are the fields a caller keeps.
/// </summary>
public static class SpanLogParser
{
    public static bool TryParse(string line, out TransactionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(line);
        return TryParse(line.AsSpan(), out summary);
    }

    public static bool TryParse(ReadOnlySpan<char> line, out TransactionSummary summary)
    {
        summary = default;
        Span<Range> fields = stackalloc Range[LogFormat.FieldCount];
        if (!TryReadFields(line, fields))
        {
            return false;
        }

        ReadOnlySpan<char> transactionId = line[fields[LogFormat.TransactionIdIndex]];
        ReadOnlySpan<char> merchant = line[fields[LogFormat.MerchantIndex]];

        if (!FieldParsers.TryParseTimestamp(line[fields[LogFormat.TimestampIndex]], out DateTimeOffset timestamp)
            || !FieldParsers.TryParseLevel(line[fields[LogFormat.LevelIndex]], out LogLevel level)
            || !FieldParsers.IsService(line[fields[LogFormat.ServiceIndex]])
            || !FieldParsers.IsTransactionId(transactionId)
            || !FieldParsers.TryParseAccount(line[fields[LogFormat.AccountIndex]], out int accountNumber)
            || !FieldParsers.TryParseAmount(line[fields[LogFormat.AmountIndex]], out decimal amount)
            || !FieldParsers.TryParseCurrency(line[fields[LogFormat.CurrencyIndex]], out Currency currency)
            || !FieldParsers.TryParseStatus(line[fields[LogFormat.StatusIndex]], out TransactionStatus status)
            || merchant.IsEmpty
            || !FieldParsers.TryParseTrace(line[fields[LogFormat.TraceIndex]], out ulong trace))
        {
            return false;
        }

        summary = new TransactionSummary(
            timestamp,
            level,
            amount,
            currency,
            status,
            accountNumber,
            trace,
            transactionId.ToString(),
            merchant.ToString());
        return true;
    }

    private static bool TryReadFields(ReadOnlySpan<char> line, Span<Range> fields)
    {
        int start = 0;
        for (int i = 0; i < LogFormat.FieldCount - 1; i++)
        {
            int relative = line[start..].IndexOf(LogFormat.Delimiter);
            if (relative < 0)
            {
                return false;
            }

            int end = start + relative;
            fields[i] = start..end;
            start = end + 1;
        }

        if (start > line.Length || line[start..].IndexOf(LogFormat.Delimiter) >= 0)
        {
            return false;
        }

        fields[LogFormat.FieldCount - 1] = start..line.Length;
        return true;
    }
}
