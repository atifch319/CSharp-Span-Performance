namespace SpanPerformance;

/// <summary>
/// Pipe-delimited payment gateway log line. Fields are not quoted and cannot contain the delimiter.
/// </summary>
public static class LogFormat
{
    public const char Delimiter = '|';
    public const int FieldCount = 10;

    public const int TimestampIndex = 0;
    public const int LevelIndex = 1;
    public const int ServiceIndex = 2;
    public const int TransactionIdIndex = 3;
    public const int AccountIndex = 4;
    public const int AmountIndex = 5;
    public const int CurrencyIndex = 6;
    public const int StatusIndex = 7;
    public const int MerchantIndex = 8;
    public const int TraceIndex = 9;

    public const string TimestampPattern = "yyyy-MM-dd'T'HH:mm:ss'.'fff'Z'";
    public const string ServiceName = "PaymentGateway";
    public const string TransactionPrefix = "TXN-";
    public const string AccountPrefix = "ACC-";

    public const int TransactionIdLength = 12;
    public const int TransactionHexLength = 8;
    public const int TraceHexLength = 16;
    public const int TraceByteLength = 8;
    public const int MaxAccountDigits = 10;
}
