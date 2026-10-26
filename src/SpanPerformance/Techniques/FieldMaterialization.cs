using System.Runtime.CompilerServices;

namespace SpanPerformance.Techniques;

/// <summary>
/// Control case. Every field is copied into a string, then folded into a checksum.
/// When the product needs every field as a string, span slicing still has to pay for those copies.
/// </summary>
public static class FieldMaterialization
{
    public static class Before
    {
        public static bool TryChecksumWithSubstring(string line, out int checksum)
        {
            ArgumentNullException.ThrowIfNull(line);
            checksum = 0;
            int start = 0;

            for (int i = 0; i < LogFormat.FieldCount - 1; i++)
            {
                int delimiter = line.IndexOf(LogFormat.Delimiter, start);
                if (delimiter < 0)
                {
                    return false;
                }

                checksum = Fold(checksum, line.Substring(start, delimiter - start));
                start = delimiter + 1;
            }

            if (line.IndexOf(LogFormat.Delimiter, start) >= 0)
            {
                return false;
            }

            checksum = Fold(checksum, line.Substring(start));
            return true;
        }

        public static bool TryChecksumWithSplit(string line, out int checksum)
        {
            ArgumentNullException.ThrowIfNull(line);
            checksum = 0;
            string[] fields = line.Split(LogFormat.Delimiter, StringSplitOptions.None);
            if (fields.Length != LogFormat.FieldCount)
            {
                return false;
            }

            foreach (string field in fields)
            {
                checksum = Fold(checksum, field);
            }

            return true;
        }
    }

    public static class After
    {
        public static bool TryChecksum(ReadOnlySpan<char> line, out int checksum)
        {
            checksum = 0;
            int count = 0;
            foreach (Range range in line.Split(LogFormat.Delimiter))
            {
                count++;
                if (count > LogFormat.FieldCount)
                {
                    return false;
                }

                checksum = Fold(checksum, line[range].ToString());
            }

            return count == LogFormat.FieldCount;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Fold(int checksum, string field)
    {
        int hash = unchecked((checksum * 33) + field.Length);
        for (int i = 0; i < field.Length; i++)
        {
            hash = unchecked((hash * 33) + field[i]);
        }

        return hash;
    }
}
