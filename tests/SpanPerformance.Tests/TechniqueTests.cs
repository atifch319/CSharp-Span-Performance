using System.Buffers.Binary;
using System.Globalization;
using SpanPerformance.Techniques;

namespace SpanPerformance.Tests;

public class TechniqueTests
{
    [Fact]
    public void FieldSlicing_BeforeAndAfterReturnTheSameAmount()
    {
        Assert.True(FieldSlicing.Before.TryParseAmount(SampleData.SettledUsdLine, out decimal before));
        Assert.True(FieldSlicing.After.TryParseAmount(SampleData.SettledUsdLine, out decimal after));
        Assert.Equal(1299.50m, before);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("x|x|x|x|x|42.5")]
    [InlineData("x|x|x|x|x|42.5|ignored")]
    public void FieldSlicing_ParsesTheAmountFieldWithoutValidatingTheOthers(string line)
    {
        Assert.True(FieldSlicing.Before.TryParseAmount(line, out decimal before));
        Assert.True(FieldSlicing.After.TryParseAmount(line, out decimal after));
        Assert.Equal(42.5m, before);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("")]
    [InlineData("only-five|fields|are|present|here")]
    [InlineData("x|x|x|x|x|12.345")]
    [InlineData("x|x|x|x|x|")]
    public void FieldSlicing_RejectsTheSameBadLines(string line)
    {
        Assert.False(FieldSlicing.Before.TryParseAmount(line, out decimal before));
        Assert.False(FieldSlicing.After.TryParseAmount(line, out decimal after));
        Assert.Equal(0m, before);
        Assert.Equal(before, after);
    }

    [Fact]
    public void FieldSlicing_NullLineThrowsFromTheStringApi()
    {
        Assert.Throws<ArgumentNullException>(() => FieldSlicing.Before.TryParseAmount(null!, out _));
    }

    [Fact]
    public void LineSplitting_BeforeAndAfterReturnTheSameAmount()
    {
        Assert.True(LineSplitting.Before.TryParseAmount(SampleData.SettledUsdLine, out decimal before));
        Assert.True(LineSplitting.After.TryParseAmount(SampleData.SettledUsdLine, out decimal after));
        Assert.Equal(1299.50m, before);
        Assert.Equal(before, after);
    }

    [Fact]
    public void LineSplitting_RequiresExactlyTenFields()
    {
        const string tenFields = "x|x|x|x|x|42.00|x|x|x|x";
        Assert.True(LineSplitting.Before.TryParseAmount(tenFields, out decimal before));
        Assert.True(LineSplitting.After.TryParseAmount(tenFields, out decimal after));
        Assert.Equal(42.00m, before);
        Assert.Equal(before, after);

        Assert.False(LineSplitting.Before.TryParseAmount(tenFields + "|extra", out _));
        Assert.False(LineSplitting.After.TryParseAmount(tenFields + "|extra", out _));
        Assert.False(LineSplitting.Before.TryParseAmount("x|x|x|x|x|42.00|x|x|x", out _));
        Assert.False(LineSplitting.After.TryParseAmount("x|x|x|x|x|42.00|x|x|x", out _));
        Assert.False(LineSplitting.Before.TryParseAmount("a||c|d|e|f|g|h|i|j", out _));
        Assert.False(LineSplitting.After.TryParseAmount("a||c|d|e|f|g|h|i|j", out _));
    }

    [Fact]
    public void PrimitiveParsing_StringAndSpanOverloadsMatch()
    {
        string[] parts = SampleData.SettledUsdLine.Split('|');
        Assert.True(PrimitiveParsing.Before.TryParse(parts[0], parts[5], parts[4], out ParsedPrimitives before));

        ReadOnlySpan<char> source = SampleData.SettledUsdLine;
        ReadOnlySpan<char> timestamp = default;
        ReadOnlySpan<char> account = default;
        ReadOnlySpan<char> amount = default;
        int index = 0;
        foreach (Range range in source.Split('|'))
        {
            if (index == LogFormat.TimestampIndex)
            {
                timestamp = source[range];
            }
            else if (index == LogFormat.AccountIndex)
            {
                account = source[range];
            }
            else if (index == LogFormat.AmountIndex)
            {
                amount = source[range];
            }

            index++;
        }

        Assert.True(PrimitiveParsing.After.TryParse(timestamp, amount, account, out ParsedPrimitives after));
        Assert.Equal(before, after);
        Assert.Equal(SampleData.SettledUsdTimestamp, after.Timestamp);
        Assert.Equal(1299.50m, after.Amount);
        Assert.Equal(4821, after.AccountNumber);
    }

    [Fact]
    public void PrimitiveParsing_RejectsABadAmountOnBothPaths()
    {
        Assert.False(PrimitiveParsing.Before.TryParse("2026-10-26T12:34:56.789Z", "12.345", "ACC-1", out _));
        Assert.False(PrimitiveParsing.After.TryParse("2026-10-26T12:34:56.789Z", "12.345", "ACC-1", out _));
    }

    [Theory]
    [InlineData("INFO", "SETTLED", false)]
    [InlineData("ERROR", "SETTLED", true)]
    [InlineData("WARN", "DECLINED", true)]
    [InlineData("INFO", "REVERSED", true)]
    [InlineData("error", "SETTLED", false)]
    [InlineData("INFO", "declined", false)]
    public void TokenMatching_BeforeAndAfterAgree(string level, string status, bool expected)
    {
        string line = SampleData.SettledUsdLine;
        string[] parts = line.Split('|');
        parts[LogFormat.LevelIndex] = level;
        parts[LogFormat.StatusIndex] = status;
        line = string.Join('|', parts);

        Assert.Equal(expected, TokenMatching.Before.ShouldEscalate(line));
        Assert.Equal(expected, TokenMatching.After.ShouldEscalate(line));
    }

    [Fact]
    public void TokenMatching_DoesNotRequireFieldsAfterTheStatus()
    {
        string[] parts = SampleData.SettledUsdLine.Split('|');
        parts[LogFormat.LevelIndex] = "ERROR";
        string line = string.Join('|', parts.Take(LogFormat.StatusIndex + 1));

        Assert.True(TokenMatching.Before.ShouldEscalate(line));
        Assert.True(TokenMatching.After.ShouldEscalate(line));
        Assert.False(TokenMatching.Before.ShouldEscalate("INFO|only"));
        Assert.False(TokenMatching.After.ShouldEscalate("INFO|only"));
    }

    [Theory]
    [InlineData("A1B2C3D4E5F67890")]
    [InlineData("a1b2c3d4e5f67890")]
    [InlineData("ffffffffffffffff")]
    [InlineData("0000000000000000")]
    [InlineData("0123456789aBcDeF")]
    public void TraceBuffer_BeforeAndAfterProduceTheSameBytes(string hex)
    {
        byte[] expected = Convert.FromHexString(hex);
        Assert.True(TraceBuffer.Before.TryDecode(hex, out byte[] allocated));
        Assert.Equal(expected, allocated);

        byte[] written = new byte[LogFormat.TraceByteLength];
        Assert.True(TraceBuffer.After.TryDecode(hex, written));
        Assert.Equal(expected, written);
        Assert.Equal(
            ulong.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
            BinaryPrimitives.ReadUInt64BigEndian(written));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A1B2")]
    [InlineData("A1B2C3D4E5F6789G")]
    [InlineData("A1B2C3D4E5F678900")]
    [InlineData("0xB2C3D4E5F67890")]
    public void TraceBuffer_InvalidHexIsRejectedWithoutThrowing(string hex)
    {
        Assert.False(TraceBuffer.Before.TryDecode(hex, out byte[] allocated));
        Assert.Empty(allocated);

        byte[] destination = [9, 8, 7, 6, 5, 4, 3, 2];
        Assert.False(TraceBuffer.After.TryDecode(hex, destination));
        Assert.Equal(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 }, destination);
    }

    [Fact]
    public void TraceBuffer_ShortDestinationIsRejectedAndLeftUntouched()
    {
        byte[] destination = [1, 2];
        Assert.False(TraceBuffer.After.TryDecode("A1B2C3D4E5F67890", destination));
        Assert.Equal(new byte[] { 1, 2 }, destination);
    }

    [Fact]
    public void TraceBuffer_LongerDestinationKeepsTheTail()
    {
        byte[] destination = new byte[10];
        destination[8] = 4;
        destination[9] = 5;

        Assert.True(TraceBuffer.After.TryDecode("A1B2C3D4E5F67890", destination));
        Assert.Equal(Convert.FromHexString("A1B2C3D4E5F67890"), destination.AsSpan(0, 8).ToArray());
        Assert.Equal(4, destination[8]);
        Assert.Equal(5, destination[9]);
    }

    [Fact]
    public void TraceBuffer_NullHexThrowsFromTheStringApi()
    {
        Assert.Throws<ArgumentNullException>(() => TraceBuffer.Before.TryDecode(null!, out _));
    }

    [Fact]
    public void FieldMaterialization_AllThreePathsFoldTheSameChecksum()
    {
        foreach (string line in new[] { SampleData.SettledUsdLine }.Concat(SampleData.CreateBatch(32)))
        {
            Assert.True(FieldMaterialization.Before.TryChecksumWithSubstring(line, out int substring));
            Assert.True(FieldMaterialization.Before.TryChecksumWithSplit(line, out int split));
            Assert.True(FieldMaterialization.After.TryChecksum(line, out int span));
            Assert.Equal(substring, split);
            Assert.Equal(split, span);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("a|b|c")]
    [InlineData("a|b|c|d|e|f|g|h|i|j|k")]
    public void FieldMaterialization_RejectsTheWrongFieldCount(string line)
    {
        Assert.False(FieldMaterialization.Before.TryChecksumWithSubstring(line, out _));
        Assert.False(FieldMaterialization.Before.TryChecksumWithSplit(line, out _));
        Assert.False(FieldMaterialization.After.TryChecksum(line, out _));
    }
}
