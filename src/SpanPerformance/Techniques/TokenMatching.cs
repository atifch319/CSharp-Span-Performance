namespace SpanPerformance.Techniques;

/// <summary>
/// Use 4. Decide whether a line should be escalated, without validating the rest of the line.
/// Escalation is level <c>ERROR</c>, or status <c>DECLINED</c> or <c>REVERSED</c>, compared ordinally.
/// <see cref="Before"/> allocates the two tokens with <c>Substring</c>. <see cref="After"/> compares slices.
/// </summary>
public static class TokenMatching
{
    public static class Before
    {
        public static bool ShouldEscalate(string line)
        {
            ArgumentNullException.ThrowIfNull(line);
            if (!TryGetField(line, LogFormat.LevelIndex, out string level)
                || !TryGetField(line, LogFormat.StatusIndex, out string status))
            {
                return false;
            }

            return level.Equals("ERROR", StringComparison.Ordinal)
                || status.Equals("DECLINED", StringComparison.Ordinal)
                || status.Equals("REVERSED", StringComparison.Ordinal);
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
        public static bool ShouldEscalate(ReadOnlySpan<char> line)
        {
            if (!TryGetField(line, LogFormat.LevelIndex, out ReadOnlySpan<char> level)
                || !TryGetField(line, LogFormat.StatusIndex, out ReadOnlySpan<char> status))
            {
                return false;
            }

            return level.SequenceEqual("ERROR")
                || status.SequenceEqual("DECLINED")
                || status.SequenceEqual("REVERSED");
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
