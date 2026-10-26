namespace SpanPerformance.Parsing;

/// <summary>
/// Separates fields with <see cref="string.Split(char, StringSplitOptions)"/>, then parses those strings.
/// Empty fields are preserved so a doubled delimiter does not shift the columns.
/// </summary>
public static class SplitLogParser
{
    public static bool TryParse(string line, out TransactionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(line);
        summary = default;
        string[] fields = line.Split(LogFormat.Delimiter, StringSplitOptions.None);
        return StringTransactionParser.TryParse(fields, out summary);
    }
}
