// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.IcFiles;

namespace NvtFwCombiner.ProfileContract.Tests.IcFiles;

/// <summary>Verifies hex Range Parser Tests.</summary>
public sealed class HexRangeParserTests
{
    private static readonly SourceMark Mark = new(3, 7);

    /// <summary>Verifies unsigned Hex Preserves The Full UInt64 Range.</summary>
    [Theory]
    [InlineData("0x0", 0UL)]
    [InlineData("0xAbCd", 43981UL)]
    [InlineData("0xFFFFFFFFFFFFFFFF", ulong.MaxValue)]
    public void UnsignedHexPreservesTheFullUInt64Range(string text, ulong expected)
    {
        Assert.Equal(expected, IcFileAssertions.Success(HexRangeParser.ParseUnsigned(text, Mark)));
    }

    /// <summary>Verifies invalid Unsigned Hex Has ATyped Error.</summary>
    [Theory]
    [InlineData("", (int)IcFileErrorCode.InvalidHex)]
    [InlineData("0x", (int)IcFileErrorCode.InvalidHex)]
    [InlineData("0X10", (int)IcFileErrorCode.InvalidHex)]
    [InlineData("-0x1", (int)IcFileErrorCode.InvalidHex)]
    [InlineData("0x+1", (int)IcFileErrorCode.InvalidHex)]
    [InlineData("0xG0", (int)IcFileErrorCode.InvalidHex)]
    [InlineData("0x1 ", (int)IcFileErrorCode.InvalidHex)]
    [InlineData("0x10000000000000000", (int)IcFileErrorCode.UnsignedOverflow)]
    public void InvalidUnsignedHexHasATypedError(string text, int expectedCode)
    {
        IcFileError error = IcFileAssertions.Failure(HexRangeParser.ParseUnsigned(text, Mark), (IcFileErrorCode)expectedCode);

        Assert.Equal(Mark, error.Mark);
    }

    /// <summary>Provides positive Ranges.</summary>
    public static TheoryData<long, long> PositiveRanges
    {
        get
        {
            long[] starts = [0, 1, 0x80, long.MaxValue - 0x100];
            long[] lengths = [1, 2, 0x100];
            TheoryData<long, long> data = [];
            foreach (long start in starts)
            {
                foreach (long length in lengths)
                {
                    data.Add(start, length);
                }
            }

            return data;
        }
    }

    /// <summary>Verifies start Length And Start End Agree Across Boundaries.</summary>
    [Theory]
    [MemberData(nameof(PositiveRanges))]
    public void StartLengthAndStartEndAgreeAcrossBoundaries(long start, long length)
    {
        string startHex = Hex(start);
        string lengthHex = Hex(length);
        string endHex = Hex(checked(start + length));
        ByteRange expected = new(start, length);

        Assert.Equal(expected, IcFileAssertions.Success(HexRangeParser.FromStartLength("synthetic-space", startHex, lengthHex, Mark)));
        Assert.Equal(expected, IcFileAssertions.Success(HexRangeParser.FromStartEnd("synthetic-space", startHex, endHex, Mark)));
        Assert.False(expected.Contains(expected.EndExclusive));
    }

    /// <summary>Verifies invalid Start Length Does Not Escape As An Exception.</summary>
    [Theory]
    [InlineData("0xFFFFFFFFFFFFFFFF", "0x1", (int)IcFileErrorCode.RangeOverflow)]
    [InlineData("0x7FFFFFFFFFFFFFFF", "0x1", (int)IcFileErrorCode.RangeOverflow)]
    [InlineData("0x1", "0xFFFFFFFFFFFFFFFF", (int)IcFileErrorCode.RangeOverflow)]
    [InlineData("0x0", "0x0", (int)IcFileErrorCode.EmptyRange)]
    [InlineData("bad", "0x1", (int)IcFileErrorCode.InvalidHex)]
    [InlineData("0x0", "bad", (int)IcFileErrorCode.InvalidHex)]
    public void InvalidStartLengthDoesNotEscapeAsAnException(string start, string length, int expectedCode)
    {
        IcFileError error = IcFileAssertions.Failure(
            HexRangeParser.FromStartLength("synthetic-space", start, length, Mark), (IcFileErrorCode)expectedCode);

        Assert.Equal("synthetic-space", error.Subject);
    }

    /// <summary>Verifies invalid Exclusive Ends Are Rejected.</summary>
    [Theory]
    [InlineData("0x2", "0x1", (int)IcFileErrorCode.EndBeforeStart)]
    [InlineData("0x1", "0x1", (int)IcFileErrorCode.EmptyRange)]
    [InlineData("0x0", "0x8000000000000000", (int)IcFileErrorCode.RangeOverflow)]
    public void InvalidExclusiveEndsAreRejected(string start, string end, int expectedCode)
    {
        _ = IcFileAssertions.Failure(HexRangeParser.FromStartEnd("synthetic-space", start, end, Mark), (IcFileErrorCode)expectedCode);
    }

    /// <summary>Verifies range Requires An Address Space.</summary>
    [Fact]
    public void RangeRequiresAnAddressSpace()
    {
        _ = IcFileAssertions.Failure(HexRangeParser.FromStartLength(" ", "0x0", "0x1", Mark), IcFileErrorCode.MissingAddressSpace);
    }

    private static string Hex(long value)
    {
        return "0x" + value.ToString("X", CultureInfo.InvariantCulture);
    }
}
