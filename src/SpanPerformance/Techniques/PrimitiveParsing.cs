namespace SpanPerformance.Techniques;

/// <summary>
/// Value parsed from the timestamp, amount, and account fields.
/// </summary>
public readonly record struct ParsedPrimitives(DateTimeOffset Timestamp, decimal Amount, int AccountNumber);

/// <summary>
/// Use 3. Parse primitives from fields that are already isolated.
/// <see cref="Before"/> calls the string <c>TryParse</c> overloads, so the account suffix is also a new string.
/// <see cref="After"/> calls the <see cref="ReadOnlySpan{Char}"/> overloads.
/// </summary>
public static class PrimitiveParsing
{
    public static class Before
    {
        public static bool TryParse(
            string timestamp,
            string amount,
            string account,
            out ParsedPrimitives value)
        {
            ArgumentNullException.ThrowIfNull(timestamp);
            ArgumentNullException.ThrowIfNull(amount);
            ArgumentNullException.ThrowIfNull(account);
            value = default;

            if (!Parsing.FieldParsers.TryParseTimestamp(timestamp, out DateTimeOffset parsedTimestamp)
                || !Parsing.FieldParsers.TryParseAmount(amount, out decimal parsedAmount)
                || !Parsing.FieldParsers.TryParseAccount(account, out int accountNumber))
            {
                return false;
            }

            value = new ParsedPrimitives(parsedTimestamp, parsedAmount, accountNumber);
            return true;
        }
    }

    public static class After
    {
        public static bool TryParse(
            ReadOnlySpan<char> timestamp,
            ReadOnlySpan<char> amount,
            ReadOnlySpan<char> account,
            out ParsedPrimitives value)
        {
            value = default;
            if (!Parsing.FieldParsers.TryParseTimestamp(timestamp, out DateTimeOffset parsedTimestamp)
                || !Parsing.FieldParsers.TryParseAmount(amount, out decimal parsedAmount)
                || !Parsing.FieldParsers.TryParseAccount(account, out int accountNumber))
            {
                return false;
            }

            value = new ParsedPrimitives(parsedTimestamp, parsedAmount, accountNumber);
            return true;
        }
    }
}
