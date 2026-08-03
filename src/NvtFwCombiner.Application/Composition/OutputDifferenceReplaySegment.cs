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

    /// <summary>Checks the changed-byte count and evidence hashes for one complete changed range.</summary>
    public bool MatchesDifferenceEvidence(
        long differenceStart,
        long differenceLength,
        long changedByteCount,
        string beforeSha256,
        string afterSha256)
    {
        return MatchesDifferenceEvidence(
            new ByteRange(differenceStart, differenceLength),
            changedByteCount,
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

    /// <summary>Checks the changed-byte count and evidence hashes for one complete changed range.</summary>
    public bool MatchesDifferenceEvidence(
        ByteRange differenceRange,
        long changedByteCount,
        string beforeSha256,
        string afterSha256)
    {
        if (changedByteCount <= 0 || changedByteCount > differenceRange.Length ||
            !Range.Contains(differenceRange))
        {
            return false;
        }

        int offset = checked((int)(differenceRange.Start - Range.Start));
        int length = checked((int)differenceRange.Length);
        ReadOnlySpan<byte> before = _beforeBytes.AsSpan(offset, length);
        ReadOnlySpan<byte> after = _afterBytes.AsSpan(offset, length);
        long observedChangedByteCount = 0;
        for (int index = 0; index < length; index++)
        {
            if (before[index] != after[index])
            {
                observedChangedByteCount++;
            }
        }

        return observedChangedByteCount == changedByteCount &&
            MatchesDifferenceEvidence(differenceRange, beforeSha256, afterSha256);
    }

    /// <summary>
    /// Checks that persisted replay uses the unique aligned context envelope and does not retain the complete artifact.
    /// </summary>
    public bool MatchesPersistableAlignedContext(
        long artifactLength,
        long differenceStart,
        long differenceLength)
    {
        try
        {
            return MatchesPersistableAlignedContext(
                artifactLength,
                new ByteRange(differenceStart, differenceLength));
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks that persisted replay uses the unique aligned context envelope and does not retain the complete artifact.
    /// </summary>
    public bool MatchesPersistableAlignedContext(long artifactLength, ByteRange differenceRange)
    {
        try
        {
            ByteRange expectedRange = CalculateAlignedContextRange(artifactLength, differenceRange);
            return (expectedRange.Start != 0 || expectedRange.EndExclusive != artifactLength) &&
                Range == expectedRange;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return false;
        }
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
        return CreateWithAlignedContextCore(
            beforeBytes,
            afterBytes,
            differenceRange,
            suppressCompleteArtifact: false)!;
    }

    /// <summary>
    /// Captures aligned context for report persistence, or returns null rather than retaining a complete artifact.
    /// </summary>
    public static OutputDifferenceReplaySegment? CreatePersistableWithAlignedContext(
        ReadOnlyMemory<byte> beforeBytes,
        ReadOnlyMemory<byte> afterBytes,
        ByteRange differenceRange)
    {
        return CreateWithAlignedContextCore(
            beforeBytes,
            afterBytes,
            differenceRange,
            suppressCompleteArtifact: true);
    }

    private static OutputDifferenceReplaySegment? CreateWithAlignedContextCore(
        ReadOnlyMemory<byte> beforeBytes,
        ReadOnlyMemory<byte> afterBytes,
        ByteRange differenceRange,
        bool suppressCompleteArtifact)
    {
        if (beforeBytes.Length != afterBytes.Length)
        {
            throw new ArgumentException("Output-difference replay byte planes must have equal lengths.");
        }

        ByteRange replayRange = CalculateAlignedContextRange(beforeBytes.Length, differenceRange);
        if (suppressCompleteArtifact &&
            replayRange.Start == 0 && replayRange.EndExclusive == beforeBytes.Length)
        {
            return null;
        }

        int start = checked((int)replayRange.Start);
        int length = checked((int)replayRange.Length);
        return new OutputDifferenceReplaySegment(
            replayRange,
            beforeBytes.Slice(start, length),
            afterBytes.Slice(start, length));
    }

    private static ByteRange CalculateAlignedContextRange(
        long artifactLength,
        ByteRange differenceRange)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(artifactLength);
        if (differenceRange.EndExclusive > artifactLength)
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
            artifactLength,
            checked(alignedEnd + (BytesPerRow * ContextRowCount)));
        return ByteRange.FromStartEndExclusive(replayStart, replayEnd);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
