using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Closed parse failures emitted by the invariant authoring range codec.</summary>
public enum AuthoringRangeTextIssueKind
{
    /// <summary>The editable start value is not a non-negative integer.</summary>
    StartInvalid,

    /// <summary>The editable length value is not a positive integer.</summary>
    LengthInvalid,

    /// <summary>The accepted start and length cannot form one checked range.</summary>
    RangeOverflow,
}

/// <summary>Typed failure returned while converting editable Start + Length text.</summary>
public sealed record AuthoringRangeTextIssue(
    AuthoringRangeTextIssueKind Kind,
    string Message);

/// <summary>
/// One culture-invariant authoring codec for hexadecimal or decimal Start +
/// Length text. Domain and compiler consumers receive only checked
/// half-open <see cref="ByteRange"/> values.
/// </summary>
public static class AuthoringByteRangeCodec
{
    /// <summary>Parses one editable Start + Length pair into a checked half-open range.</summary>
    public static bool TryParseStartAndLength(
        string? startText,
        string? lengthText,
        out ByteRange range,
        out AuthoringRangeTextIssue? issue)
    {
        range = default;
        if (!TryParseNonNegativeLong(startText, out long start))
        {
            issue = new AuthoringRangeTextIssue(
                AuthoringRangeTextIssueKind.StartInvalid,
                "Range start must be a non-negative hexadecimal or decimal byte offset.");
            return false;
        }

        if (!TryParseNonNegativeLong(lengthText, out long length) || length <= 0)
        {
            issue = new AuthoringRangeTextIssue(
                AuthoringRangeTextIssueKind.LengthInvalid,
                "Range length must be a positive hexadecimal or decimal byte count.");
            return false;
        }

        try
        {
            range = new ByteRange(start, length);
            issue = null;
            return true;
        }
        catch (OverflowException)
        {
            issue = new AuthoringRangeTextIssue(
                AuthoringRangeTextIssueKind.RangeOverflow,
                "Range start plus length exceeds the supported address size.");
            return false;
        }
    }

    /// <summary>Parses one non-negative hexadecimal or decimal integer.</summary>
    public static bool TryParseNonNegativeLong(string? value, out long result)
    {
        result = 0;
        string text = value?.Trim() ?? string.Empty;
        NumberStyles style = NumberStyles.Integer;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
            style = NumberStyles.AllowHexSpecifier;
        }

        return text.Length > 0 &&
            long.TryParse(text, style, CultureInfo.InvariantCulture, out result) &&
            result >= 0;
    }

    /// <summary>Formats one accepted non-negative value as invariant hexadecimal text.</summary>
    public static string FormatHex(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return string.Create(CultureInfo.InvariantCulture, $"0x{value:X}");
    }

    /// <summary>Derives the read-only inclusive end used by display adapters.</summary>
    public static long GetEndInclusive(ByteRange range)
    {
        return checked(range.EndExclusive - 1);
    }
}
