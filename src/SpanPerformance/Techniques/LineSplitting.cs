namespace SpanPerformance.Techniques;

/// <summary>
/// Use 2. Separate a line into fields. Both sides require exactly ten fields and return the same amount.
/// <see cref="Before"/> uses <c>string.Split</c>, which allocates the array and every field.
/// <see cref="After"/> uses the span split enumerator and parses only the amount.
/// </summary>
public static class LineSplitting
{
    public static class Before
    {
        public static bool TryParseAmount(string line, out decimal amount)
        {
            ArgumentNullException.ThrowIfNull(line);
            amount = 0;
            string[] fields = line.Split(LogFormat.Delimiter, StringSplitOptions.None);
            if (fields.Length != LogFormat.FieldCount)
            {
                return false;
            }

            return Parsing.FieldParsers.TryParseAmount(fields[LogFormat.AmountIndex], out amount);
        }
    }

    public static class After
    {
        public static bool TryParseAmount(ReadOnlySpan<char> line, out decimal amount)
        {
            amount = 0;
            int index = 0;
            ReadOnlySpan<char> amountText = default;
            bool found = false;

            foreach (Range range in line.Split(LogFormat.Delimiter))
            {
                if (index == LogFormat.AmountIndex)
                {
                    amountText = line[range];
                    found = true;
                }

                index++;
                if (index > LogFormat.FieldCount)
                {
                    return false;
                }
            }

            if (!found || index != LogFormat.FieldCount)
            {
                return false;
            }

            return Parsing.FieldParsers.TryParseAmount(amountText, out amount);
        }
    }
}
