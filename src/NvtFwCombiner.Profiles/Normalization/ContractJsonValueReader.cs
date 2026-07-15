using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace NvtFwCombiner.Profiles.Normalization;

/// <summary>Shared lossless reader for schema-validated contract scalar values.</summary>
internal static class ContractJsonValueReader
{
    internal const int MaximumNormalizedIntegerDigits = 4096;

    internal static BigInteger ReadInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new ArgumentException("Expected a JSON integer.", nameof(value));
        }

        string raw = value.GetRawText();
        int exponentSeparator = raw.IndexOfAny(['e', 'E']);
        ReadOnlySpan<char> mantissa = exponentSeparator >= 0
            ? raw.AsSpan(0, exponentSeparator)
            : raw.AsSpan();
        bool negative = mantissa.StartsWith("-", StringComparison.Ordinal);
        if (negative)
        {
            mantissa = mantissa[1..];
        }

        int decimalPoint = mantissa.IndexOf('.');
        int fractionalDigits = decimalPoint >= 0 ? mantissa.Length - decimalPoint - 1 : 0;
        string digits = decimalPoint >= 0
            ? string.Concat(
                mantissa[..decimalPoint].ToString(),
                mantissa[(decimalPoint + 1)..].ToString())
            : mantissa.ToString();
        digits = digits.TrimStart('0');
        if (digits.Length == 0)
        {
            return BigInteger.Zero;
        }

        int exponent = ReadExponent(raw, exponentSeparator);
        long power = (long)exponent - fractionalDigits;
        if (power < 0)
        {
            long requiredTrailingZeros = -power;
            int availableTrailingZeros = digits.Length - digits.TrimEnd('0').Length;
            if (requiredTrailingZeros > availableTrailingZeros)
            {
                throw new ArgumentException("Expected a mathematically integral JSON number.", nameof(value));
            }

            digits = digits[..^(int)requiredTrailingZeros];
            power = 0;
        }

        long normalizedDigitCount = digits.Length + power;
        if (normalizedDigitCount > MaximumNormalizedIntegerDigits)
        {
            throw new ArgumentException(
                $"Normalized integers cannot exceed {MaximumNormalizedIntegerDigits} decimal digits.",
                nameof(value));
        }

        var integer = BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        if (power > 0)
        {
            integer *= BigInteger.Pow(10, (int)power);
        }

        return negative ? BigInteger.Negate(integer) : integer;
    }

    internal static string ReadString(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String && value.GetString() is { } text
            ? text
            : throw new ArgumentException("Expected a JSON string.", nameof(value));
    }

    internal static byte[] ParseCanonicalHex(string value)
    {
        return string.IsNullOrEmpty(value) ||
            value.Length % 2 != 0 ||
            value.Any(static character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            ? throw new ArgumentException(
                "Expected non-empty canonical lowercase hexadecimal bytes.",
                nameof(value))
            : Convert.FromHexString(value);
    }

    private static int ReadExponent(string raw, int exponentSeparator)
    {
        return exponentSeparator < 0
            ? 0
            : int.TryParse(
            raw.AsSpan(exponentSeparator + 1),
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out int exponent)
            ? exponent
            : throw new ArgumentException("Integer exponent exceeds the supported normalized range.", nameof(raw));
    }
}
