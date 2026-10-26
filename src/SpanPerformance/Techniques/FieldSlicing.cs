namespace SpanPerformance.Techniques;

/// <summary>
/// Use 1. Read one field from a delimited line. Both sides return the same amount.
/// <see cref="Before"/> allocates a string for that field. <see cref="After"/> parses the slice directly.
/// </summary>
public static class FieldSlicing
{
    public static class Before
    {
        public static bool TryParseAmount(string line, out decimal amount)
        {
            ArgumentNullException.ThrowIfNull(line);
            amount = 0;
            if (!TryGetField(line, LogFormat.AmountIndex, out string field))
            {
                return false;
            }

            return Parsing.FieldParsers.TryParseAmount(field, out amount);
        }

        private static bool TryGetField(string line, int index, out string field)
        {
            int start = 0;
            for (int i = 0; i < index; i++)
            {
                int delimiter = line.IndexOf(LogFormat.Delimiter, start);
                if (delimiter < 0)
                {
                    field = string.Empty;
                    return false;
                }

                start = delimiter + 1;
            }

            int end = line.IndexOf(LogFormat.Delimiter, start);
            field = end < 0
                ? line.Substring(start)
                : line.Substring(start, end - start);
            return true;
        }
    }

    public static class After
    {
        public static bool TryParseAmount(ReadOnlySpan<char> line, out decimal amount)
        {
            amount = 0;
            if (!TryGetField(line, LogFormat.AmountIndex, out ReadOnlySpan<char> field))
            {
                return false;
            }

            return Parsing.FieldParsers.TryParseAmount(field, out amount);
        }

        private static bool TryGetField(ReadOnlySpan<char> line, int index, out ReadOnlySpan<char> field)
        {
            int start = 0;
            for (int i = 0; i < index; i++)
            {
                int relative = line[start..].IndexOf(LogFormat.Delimiter);
                if (relative < 0)
                {
                    field = default;
                    return false;
                }

                start += relative + 1;
            }

            int end = line[start..].IndexOf(LogFormat.Delimiter);
            field = end < 0 ? line[start..] : line.Slice(start, end);
            return true;
        }
    }
}
