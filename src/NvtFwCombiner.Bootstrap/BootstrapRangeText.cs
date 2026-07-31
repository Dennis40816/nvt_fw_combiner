using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap;

internal static class BootstrapRangeText
{
    internal static bool TryParseNonNegativeLong(string text, out long value)
    {
        return AuthoringByteRangeCodec.TryParseNonNegativeLong(text, out value);
    }

    internal static string FormatHex(long value)
    {
        return AuthoringByteRangeCodec.FormatHex(value);
    }
}
