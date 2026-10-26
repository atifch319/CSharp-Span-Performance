using System.Globalization;

namespace SpanPerformance.Parsing;

/// <summary>
/// Field rules used by every parser. String overloads are what <c>Substring</c> and <c>Split</c> call.
/// Span overloads parse the same characters without copying them first.
/// </summary>
internal static class FieldParsers
{
    private const NumberStyles AmountStyle = NumberStyles.AllowDecimalPoint;
    private const NumberStyles AccountStyle = NumberStyles.None;
    private const NumberStyles TraceStyle = NumberStyles.AllowHexSpecifier;
    private const DateTimeStyles TimestampStyle =
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public static bool TryParseTimestamp(string text, out DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(text);
        return DateTimeOffset.TryParseExact(
            text,
            LogFormat.TimestampPattern,
            CultureInfo.InvariantCulture,
            TimestampStyle,
            out timestamp);
    }

    public static bool TryParseTimestamp(ReadOnlySpan<char> text, out DateTimeOffset timestamp)
    {
        return DateTimeOffset.TryParseExact(
            text,
            LogFormat.TimestampPattern,
            CultureInfo.InvariantCulture,
            TimestampStyle,
            out timestamp);
    }

    public static bool TryParseLevel(string text, out LogLevel level)
    {
        ArgumentNullException.ThrowIfNull(text);
        switch (text)
        {
            case "INFO":
                level = LogLevel.Info;
                return true;
            case "WARN":
                level = LogLevel.Warn;
                return true;
            case "ERROR":
                level = LogLevel.Error;
                return true;
            default:
                level = default;
                return false;
        }
    }

    public static bool TryParseLevel(ReadOnlySpan<char> text, out LogLevel level)
    {
        if (text.SequenceEqual("INFO"))
        {
            level = LogLevel.Info;
            return true;
        }

        if (text.SequenceEqual("WARN"))
        {
            level = LogLevel.Warn;
            return true;
        }

        if (text.SequenceEqual("ERROR"))
        {
            level = LogLevel.Error;
            return true;
        }

        level = default;
        return false;
    }

    public static bool IsService(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Equals(LogFormat.ServiceName, StringComparison.Ordinal);
    }

    public static bool IsService(ReadOnlySpan<char> text) =>
        text.SequenceEqual(LogFormat.ServiceName);

    public static bool IsTransactionId(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == LogFormat.TransactionIdLength
            && text.StartsWith(LogFormat.TransactionPrefix, StringComparison.Ordinal)
            && HexText.IsHex(text, LogFormat.TransactionPrefix.Length, LogFormat.TransactionHexLength);
    }

    public static bool IsTransactionId(ReadOnlySpan<char> text)
    {
        return text.Length == LogFormat.TransactionIdLength
            && text.StartsWith(LogFormat.TransactionPrefix)
            && HexText.IsHex(text[LogFormat.TransactionPrefix.Length..]);
    }

    public static bool TryParseAccount(string text, out int accountNumber)
    {
        ArgumentNullException.ThrowIfNull(text);
        accountNumber = 0;
        if (!text.StartsWith(LogFormat.AccountPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string digits = text.Substring(LogFormat.AccountPrefix.Length);
        if (digits.Length == 0 || digits.Length > LogFormat.MaxAccountDigits)
        {
            return false;
        }

        return int.TryParse(digits, AccountStyle, CultureInfo.InvariantCulture, out accountNumber);
    }

    public static bool TryParseAccount(ReadOnlySpan<char> text, out int accountNumber)
    {
        accountNumber = 0;
        if (!text.StartsWith(LogFormat.AccountPrefix))
        {
            return false;
        }

        ReadOnlySpan<char> digits = text[LogFormat.AccountPrefix.Length..];
        if (digits.IsEmpty || digits.Length > LogFormat.MaxAccountDigits)
        {
            return false;
        }

        return int.TryParse(digits, AccountStyle, CultureInfo.InvariantCulture, out accountNumber);
    }

    public static bool TryParseAmount(string text, out decimal amount)
    {
        ArgumentNullException.ThrowIfNull(text);
        amount = 0;
        if (!HasAtMostTwoDecimalPlaces(text))
        {
            return false;
        }

        return decimal.TryParse(text, AmountStyle, CultureInfo.InvariantCulture, out amount)
            && amount >= 0;
    }

    public static bool TryParseAmount(ReadOnlySpan<char> text, out decimal amount)
    {
        amount = 0;
        if (!HasAtMostTwoDecimalPlaces(text))
        {
            return false;
        }

        return decimal.TryParse(text, AmountStyle, CultureInfo.InvariantCulture, out amount)
            && amount >= 0;
    }

    public static bool TryParseCurrency(string text, out Currency currency)
    {
        ArgumentNullException.ThrowIfNull(text);
        switch (text)
        {
            case "USD":
                currency = Currency.Usd;
                return true;
            case "EUR":
                currency = Currency.Eur;
                return true;
            case "GBP":
                currency = Currency.Gbp;
                return true;
            default:
                currency = default;
                return false;
        }
    }

    public static bool TryParseCurrency(ReadOnlySpan<char> text, out Currency currency)
    {
        if (text.SequenceEqual("USD"))
        {
            currency = Currency.Usd;
            return true;
        }

        if (text.SequenceEqual("EUR"))
        {
            currency = Currency.Eur;
            return true;
        }

        if (text.SequenceEqual("GBP"))
        {
            currency = Currency.Gbp;
            return true;
        }

        currency = default;
        return false;
    }

    public static bool TryParseStatus(string text, out TransactionStatus status)
    {
        ArgumentNullException.ThrowIfNull(text);
        switch (text)
        {
            case "AUTHORIZED":
                status = TransactionStatus.Authorized;
                return true;
            case "SETTLED":
                status = TransactionStatus.Settled;
                return true;
            case "DECLINED":
                status = TransactionStatus.Declined;
                return true;
            case "REVERSED":
                status = TransactionStatus.Reversed;
                return true;
            default:
                status = default;
                return false;
        }
    }

    public static bool TryParseStatus(ReadOnlySpan<char> text, out TransactionStatus status)
    {
        if (text.SequenceEqual("AUTHORIZED"))
        {
            status = TransactionStatus.Authorized;
            return true;
        }

        if (text.SequenceEqual("SETTLED"))
        {
            status = TransactionStatus.Settled;
            return true;
        }

        if (text.SequenceEqual("DECLINED"))
        {
            status = TransactionStatus.Declined;
            return true;
        }

        if (text.SequenceEqual("REVERSED"))
        {
            status = TransactionStatus.Reversed;
            return true;
        }

        status = default;
        return false;
    }

    public static bool TryParseTrace(string text, out ulong trace)
    {
        ArgumentNullException.ThrowIfNull(text);
        trace = 0;
        if (text.Length != LogFormat.TraceHexLength || !HexText.IsHex(text))
        {
            return false;
        }

        return ulong.TryParse(text, TraceStyle, CultureInfo.InvariantCulture, out trace);
    }

    public static bool TryParseTrace(ReadOnlySpan<char> text, out ulong trace)
    {
        trace = 0;
        if (text.Length != LogFormat.TraceHexLength || !HexText.IsHex(text))
        {
            return false;
        }

        return ulong.TryParse(text, TraceStyle, CultureInfo.InvariantCulture, out trace);
    }

    private static bool HasAtMostTwoDecimalPlaces(string text)
    {
        int dot = text.IndexOf('.');
        if (dot < 0)
        {
            return true;
        }

        if (dot == 0 || dot == text.Length - 1)
        {
            return false;
        }

        if (text.IndexOf('.', dot + 1) >= 0)
        {
            return false;
        }

        return text.Length - dot - 1 <= 2;
    }

    private static bool HasAtMostTwoDecimalPlaces(ReadOnlySpan<char> text)
    {
        int dot = text.IndexOf('.');
        if (dot < 0)
        {
            return true;
        }

        if (dot == 0 || dot == text.Length - 1)
        {
            return false;
        }

        if (text[(dot + 1)..].IndexOf('.') >= 0)
        {
            return false;
        }

        return text.Length - dot - 1 <= 2;
    }
}
