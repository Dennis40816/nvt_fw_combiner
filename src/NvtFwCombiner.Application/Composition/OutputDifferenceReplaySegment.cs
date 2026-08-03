using NvtFwCombiner.Domain.Composition;
using System.Security.Cryptography;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Persisted immutable byte planes sufficient to replay one output-difference viewport.</summary>
public sealed class OutputDifferenceReplaySegment
{
    private const int BytesPerRow = 16;
    private const int ContextRowCount = 2;
    private readonly byte[] _beforeBytes;
    private readonly byte[] _afterBytes;

    /// <summary>Creates one exact replay segment.</summary>
    public OutputDifferenceReplaySegment(
        long start,
        ReadOnlyMemory<byte> beforeBytes,
        ReadOnlyMemory<byte> afterBytes)
        : this(new ByteRange(start, beforeBytes.Length), beforeBytes, afterBytes)
    {
    }

    /// <summary>Creates one exact replay segment from an existing checked range.</summary>
    public OutputDifferenceReplaySegment(
        ByteRange range,
        ReadOnlyMemory<byte> beforeBytes,
        ReadOnlyMemory<byte> afterBytes)
    {
        if (range.Length != beforeBytes.Length)
        {
            throw new ArgumentException(
                "Before bytes must exactly cover the replay range.",
                nameof(beforeBytes));
        }

        if (range.Length != afterBytes.Length)
        {
            throw new ArgumentException(
                "After bytes must exactly cover the replay range.",
                nameof(afterBytes));
        }

        Range = range;
        _beforeBytes = beforeBytes.ToArray();
        _afterBytes = afterBytes.ToArray();
        BeforeSha256 = Hash(_beforeBytes);
        AfterSha256 = Hash(_afterBytes);
    }

    /// <summary>Output-space range covered by both byte planes.</summary>
    public ByteRange Range { get; }

    /// <summary>Exact immutable reference bytes for <see cref="Range" />.</summary>
    public ReadOnlyMemory<byte> BeforeBytes => _beforeBytes;

    /// <summary>Exact immutable final-output bytes for <see cref="Range" />.</summary>
    public ReadOnlyMemory<byte> AfterBytes => _afterBytes;

    /// <summary>SHA-256 of the complete persisted reference plane.</summary>
    public string BeforeSha256 { get; }

    /// <summary>SHA-256 of the complete persisted output plane.</summary>
    public string AfterSha256 { get; }

    /// <summary>Checks that the complete changed range still matches its report evidence hashes.</summary>
    public bool MatchesDifferenceEvidence(
        long differenceStart,
        long differenceLength,
        string beforeSha256,
        string afterSha256)
    {
        return MatchesDifferenceEvidence(
            new ByteRange(differenceStart, differenceLength),
            beforeSha256,
            afterSha256);
    }

    /// <summary>Checks that the complete changed range still matches its report evidence hashes.</summary>
    public bool MatchesDifferenceEvidence(
        ByteRange differenceRange,
        string beforeSha256,
        string afterSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(afterSha256);
        if (!Range.Contains(differenceRange))
        {
            return false;
        }

        int offset = checked((int)(differenceRange.Start - Range.Start));
        int length = checked((int)differenceRange.Length);
        return string.Equals(
                Hash(_beforeBytes.AsSpan(offset, length)),
                beforeSha256,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                Hash(_afterBytes.AsSpan(offset, length)),
                afterSha256,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Captures a complete difference plus two aligned 16-byte context rows on each side.</summary>
    public static OutputDifferenceReplaySegment CreateWithAlignedContext(
        ReadOnlyMemory<byte> beforeBytes,
        ReadOnlyMemory<byte> afterBytes,
        long differenceStart,
        long differenceLength)
    {
        return CreateWithAlignedContext(
            beforeBytes,
            afterBytes,
            new ByteRange(differenceStart, differenceLength));
    }

    /// <summary>Captures a checked difference plus two aligned 16-byte context rows on each side.</summary>
    public static OutputDifferenceReplaySegment CreateWithAlignedContext(
        ReadOnlyMemory<byte> beforeBytes,
        ReadOnlyMemory<byte> afterBytes,
        ByteRange differenceRange)
    {
        if (beforeBytes.Length != afterBytes.Length)
        {
            throw new ArgumentException("Output-difference replay byte planes must have equal lengths.");
        }

        if (differenceRange.EndExclusive > beforeBytes.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(differenceRange),
                differenceRange,
                "The output difference must fit both byte planes.");
        }

        long alignedStart = differenceRange.Start - (differenceRange.Start % BytesPerRow);
        long replayStart = Math.Max(0, alignedStart - (BytesPerRow * ContextRowCount));
        long alignedEnd = checked(
            (differenceRange.EndExclusive + BytesPerRow - 1) / BytesPerRow * BytesPerRow);
        long replayEnd = Math.Min(
            beforeBytes.Length,
            alignedEnd + (BytesPerRow * ContextRowCount));
        var replayRange = ByteRange.FromStartEndExclusive(replayStart, replayEnd);
        int start = checked((int)replayRange.Start);
        int length = checked((int)replayRange.Length);
        return new OutputDifferenceReplaySegment(
            replayRange,
            beforeBytes.Slice(start, length),
            afterBytes.Slice(start, length));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
