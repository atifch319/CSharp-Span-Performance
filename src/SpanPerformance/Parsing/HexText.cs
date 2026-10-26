namespace SpanPerformance.Parsing;

internal static class HexText
{
    public static bool IsHex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return IsHex(text, 0, text.Length);
    }

    public static bool IsHex(string text, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start < 0 || length <= 0 || (uint)start + (uint)length > (uint)text.Length)
        {
            return false;
        }

        for (int i = start; i < start + length; i++)
        {
            if (!IsNibble(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsHex(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        foreach (char c in text)
        {
            if (!IsNibble(c))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsNibble(char c) =>
        c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

    public static int NibbleValue(char c)
    {
        if (c <= '9')
        {
            return c - '0';
        }

        if (c <= 'F')
        {
            return c - 'A' + 10;
        }

        return c - 'a' + 10;
    }
}
