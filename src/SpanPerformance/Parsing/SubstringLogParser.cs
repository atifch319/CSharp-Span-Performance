namespace SpanPerformance.Parsing;

/// <summary>
/// Copies every field with <see cref="string.Substring(int, int)"/>, then parses those strings.
/// </summary>
public static class SubstringLogParser
{
    public static bool TryParse(string line, out TransactionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(line);
        summary = default;
        if (!TryReadFields(line, out string[] fields))
        {
            return false;
        }

        return StringTransactionParser.TryParse(fields, out summary);
    }

    private static bool TryReadFields(string line, out string[] fields)
    {
        var buffer = new string[LogFormat.FieldCount];
        int start = 0;

        for (int i = 0; i < LogFormat.FieldCount - 1; i++)
        {
            int delimiter = line.IndexOf(LogFormat.Delimiter, start);
            if (delimiter < 0)
            {
                fields = [];
                return false;
            }

            buffer[i] = line.Substring(start, delimiter - start);
            start = delimiter + 1;
        }

        if (line.IndexOf(LogFormat.Delimiter, start) >= 0)
        {
            fields = [];
            return false;
        }

        buffer[LogFormat.FieldCount - 1] = line.Substring(start);
        fields = buffer;
        return true;
    }
}
