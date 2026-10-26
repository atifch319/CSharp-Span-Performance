namespace SpanPerformance;

public enum LogLevel : byte
{
    Info = 1,
    Warn = 2,
    Error = 3,
}

public enum Currency : byte
{
    Usd = 1,
    Eur = 2,
    Gbp = 3,
}

public enum TransactionStatus : byte
{
    Authorized = 1,
    Settled = 2,
    Declined = 3,
    Reversed = 4,
}

/// <summary>
/// Retained parse result. Value fields live inline. <see cref="TransactionId"/> and <see cref="Merchant"/>
/// are strings because a caller that indexes the event has to keep them. The out value of a failed
/// <c>TryParse</c> is <c>default</c> and must not be read.
/// </summary>
public readonly record struct TransactionSummary(
    DateTimeOffset Timestamp,
    LogLevel Level,
    decimal Amount,
    Currency Currency,
    TransactionStatus Status,
    int AccountNumber,
    ulong Trace,
    string TransactionId,
    string Merchant);
