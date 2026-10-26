namespace SpanPerformance.Techniques;

/// <summary>
/// Use 5. Turn the 16-character trace into 8 bytes.
/// <see cref="Before"/> calls <see cref="Convert.FromHexString(string)"/>, which returns a new array.
/// <see cref="After"/> writes the bytes into a caller-owned <see cref="Span{Byte}"/>, so the caller can pass
/// <c>stackalloc</c>, an array, or a pooled buffer. On failure the destination is left unchanged.
/// Prefer <c>ulong.TryParse(ReadOnlySpan&lt;char&gt;)</c> when the caller only needs the numeric value.
/// The full log parser does that. This type is for the case where the next stage needs the bytes.
/// </summary>
public static class TraceBuffer
{
    public static class Before
    {
        public static bool TryDecode(string hex, out byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(hex);
            bytes = [];
            if (hex.Length != LogFormat.TraceHexLength)
            {
                return false;
            }

            try
            {
                bytes = Convert.FromHexString(hex);
            }
            catch (FormatException)
            {
                bytes = [];
                return false;
            }

            return bytes.Length == LogFormat.TraceByteLength;
        }
    }

    public static class After
    {
        public static bool TryDecode(ReadOnlySpan<char> hex, Span<byte> destination)
        {
            if (hex.Length != LogFormat.TraceHexLength || destination.Length < LogFormat.TraceByteLength)
            {
                return false;
            }

            Span<byte> decoded = stackalloc byte[LogFormat.TraceByteLength];
            for (int i = 0; i < LogFormat.TraceByteLength; i++)
            {
                char high = hex[i * 2];
                char low = hex[(i * 2) + 1];
                if (!Parsing.HexText.IsNibble(high) || !Parsing.HexText.IsNibble(low))
                {
                    return false;
                }

                decoded[i] = (byte)((Parsing.HexText.NibbleValue(high) << 4) | Parsing.HexText.NibbleValue(low));
            }

            decoded.CopyTo(destination);
            return true;
        }
    }
}
