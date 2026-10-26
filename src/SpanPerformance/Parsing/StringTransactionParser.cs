namespace SpanPerformance.Parsing;

internal static class StringTransactionParser
{
    public static bool TryParse(string[] fields, out TransactionSummary summary)
    {
        summary = default;
        if (fields.Length != LogFormat.FieldCount)
        {
            return false;
        }

        if (!FieldParsers.TryParseTimestamp(fields[LogFormat.TimestampIndex], out DateTimeOffset timestamp)
            || !FieldParsers.TryParseLevel(fields[LogFormat.LevelIndex], out LogLevel level)
            || !FieldParsers.IsService(fields[LogFormat.ServiceIndex])
            || !FieldParsers.IsTransactionId(fields[LogFormat.TransactionIdIndex])
            || !FieldParsers.TryParseAccount(fields[LogFormat.AccountIndex], out int accountNumber)
            || !FieldParsers.TryParseAmount(fields[LogFormat.AmountIndex], out decimal amount)
            || !FieldParsers.TryParseCurrency(fields[LogFormat.CurrencyIndex], out Currency currency)
            || !FieldParsers.TryParseStatus(fields[LogFormat.StatusIndex], out TransactionStatus status)
            || fields[LogFormat.MerchantIndex].Length == 0
            || !FieldParsers.TryParseTrace(fields[LogFormat.TraceIndex], out ulong trace))
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
            fields[LogFormat.TransactionIdIndex],
            fields[LogFormat.MerchantIndex]);
        return true;
    }
}
