// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.IcFiles;

internal static class HexRangeParser
{
    internal static IcFileResult<ulong> ParseUnsigned(string text, SourceMark mark)
    {
        return string.IsNullOrEmpty(text) || !text.StartsWith("0x", StringComparison.Ordinal) || text.Length == 2 ||
            !text.Skip(2).All(char.IsAsciiHexDigit)
            ? new IcFileResult<ulong>.Failure(new IcFileError(IcFileErrorCode.InvalidHex, mark))
            : ulong.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong value)
                ? new IcFileResult<ulong>.Success(value)
                : new IcFileResult<ulong>.Failure(new IcFileError(IcFileErrorCode.UnsignedOverflow, mark));
    }

    internal static IcFileResult<ByteRange> FromStartLength(string spaceId, string start, string length, SourceMark mark)
    {
        return ParseRange(spaceId, start, length, false, mark);
    }

    internal static IcFileResult<ByteRange> FromStartEnd(string spaceId, string start, string endExclusive, SourceMark mark)
    {
        return ParseRange(spaceId, start, endExclusive, true, mark);
    }

    private static IcFileResult<ByteRange> ParseRange(
        string spaceId, string startText, string extentText, bool extentIsEnd, SourceMark mark)
    {
        if (string.IsNullOrWhiteSpace(spaceId))
        {
            return Failure(IcFileErrorCode.MissingAddressSpace, mark, spaceId);
        }

        IcFileResult<ulong> parsedStart = ParseUnsigned(startText, mark);
        IcFileResult<ulong> parsedExtent = ParseUnsigned(extentText, mark);
        if (parsedStart is IcFileResult<ulong>.Failure startFailure)
        {
            return new IcFileResult<ByteRange>.Failure(startFailure.Error with { Subject = spaceId });
        }

        if (parsedExtent is IcFileResult<ulong>.Failure extentFailure)
        {
            return new IcFileResult<ByteRange>.Failure(extentFailure.Error with { Subject = spaceId });
        }

        ulong start = ((IcFileResult<ulong>.Success)parsedStart).Value;
        ulong extent = ((IcFileResult<ulong>.Success)parsedExtent).Value;
        if (extentIsEnd && extent < start)
        {
            return Failure(IcFileErrorCode.EndBeforeStart, mark, spaceId);
        }

        ulong length = extentIsEnd ? extent - start : extent;
        // ByteRange represents positive ranges only; an empty slice has no ByteRange value.
        return length == 0
            ? Failure(IcFileErrorCode.EmptyRange, mark, spaceId)
            : start > long.MaxValue || length > long.MaxValue || length > long.MaxValue - start
                ? Failure(IcFileErrorCode.RangeOverflow, mark, spaceId)
                : new IcFileResult<ByteRange>.Success(new ByteRange((long)start, (long)length));
    }

    private static IcFileResult<ByteRange>.Failure Failure(IcFileErrorCode code, SourceMark mark, string spaceId)
    {
        return new(new IcFileError(code, mark, spaceId));
    }
}
