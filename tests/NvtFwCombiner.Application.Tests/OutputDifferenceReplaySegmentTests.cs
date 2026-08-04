using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using System.Security.Cryptography;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Immutable persisted bytes used to replay one report difference viewport.</summary>
public sealed class OutputDifferenceReplaySegmentTests
{
    /// <summary>The report owns copies of both byte planes.</summary>
    [Fact]
    public void ConstructorDefensivelyCopiesReplayBytes()
    {
        byte[] before = [0x10, 0x20];
        byte[] after = [0x10, 0x30];

        var replay = new OutputDifferenceReplaySegment(
            new ByteRange(16, 2),
            before,
            after);

        before[1] = 0xFF;
        after[1] = 0xFF;

        Assert.Equal([0x10, 0x20], replay.BeforeBytes.ToArray());
        Assert.Equal([0x10, 0x30], replay.AfterBytes.ToArray());
    }

    /// <summary>Both planes must exactly cover the declared replay range.</summary>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void ConstructorRejectsBytePlanesThatDoNotMatchRange(
        int beforeLength,
        int afterLength)
    {
        _ = Assert.Throws<ArgumentException>(() => new OutputDifferenceReplaySegment(
            new ByteRange(16, 2),
            new byte[beforeLength],
            new byte[afterLength]));
    }

    /// <summary>The factory retains the complete difference plus exactly two aligned context rows.</summary>
    [Fact]
    public void FactoryCapturesCompleteDifferenceAndTwoAlignedRowsOnEachSide()
    {
        byte[] before = [.. Enumerable.Range(0, 0xA0).Select(static value => (byte)value)];
        byte[] after = (byte[])before.Clone();
        after.AsSpan(0x31, 0x20).Fill(0xEE);

        var replay = OutputDifferenceReplaySegment.CreateWithAlignedContext(
            before,
            after,
            new ByteRange(0x31, 0x20));

        Assert.Equal(new ByteRange(0x10, 0x70), replay.Range);
        Assert.Equal(before.AsSpan(0x10, 0x70).ToArray(), replay.BeforeBytes.ToArray());
        Assert.Equal(after.AsSpan(0x10, 0x70).ToArray(), replay.AfterBytes.ToArray());
    }

    /// <summary>Full context planes and the changed slice retain separate evidence identities.</summary>
    [Fact]
    public void ReplayHashesBindContextAndDifferenceBytes()
    {
        byte[] before = [.. Enumerable.Range(0, 0x80).Select(static value => (byte)value)];
        byte[] after = (byte[])before.Clone();
        var difference = new ByteRange(0x31, 4);
        after.AsSpan(0x31, 4).Fill(0xEE);
        var replay = OutputDifferenceReplaySegment.CreateWithAlignedContext(
            before,
            after,
            difference);

        Assert.Equal(Hash(replay.BeforeBytes.Span), replay.BeforeSha256);
        Assert.Equal(Hash(replay.AfterBytes.Span), replay.AfterSha256);
        Assert.True(replay.MatchesDifferenceEvidence(
            difference,
            4,
            Hash(before.AsSpan(0x31, 4)),
            Hash(after.AsSpan(0x31, 4))));
        Assert.False(replay.MatchesDifferenceEvidence(
            difference,
            3,
            Hash(before.AsSpan(0x31, 4)),
            Hash(after.AsSpan(0x31, 4))));
        Assert.False(replay.MatchesDifferenceEvidence(
            difference,
            4,
            Hash(new byte[4]),
            Hash(after.AsSpan(0x31, 4))));
    }

    /// <summary>Context clips to image bounds without dropping the complete changed range.</summary>
    [Theory]
    [InlineData(0x01, 0x02, 0x00, 0x30)]
    [InlineData(0x91, 0x02, 0x70, 0x30)]
    public void FactoryClipsAlignedContextAtImageBounds(
        int differenceStart,
        int differenceLength,
        int expectedStart,
        int expectedLength)
    {
        byte[] before = new byte[0xA0];
        byte[] after = (byte[])before.Clone();

        var replay = OutputDifferenceReplaySegment.CreateWithAlignedContext(
            before,
            after,
            new ByteRange(differenceStart, differenceLength));

        Assert.Equal(new ByteRange(expectedStart, expectedLength), replay.Range);
        Assert.True(replay.Range.Contains(new ByteRange(differenceStart, differenceLength)));
    }

    /// <summary>Persisted replay accepts only the canonical aligned envelope and never a complete artifact.</summary>
    [Fact]
    public void PersistableEnvelopeMustBeCanonicalAndPartial()
    {
        byte[] before = new byte[0xA0];
        byte[] after = (byte[])before.Clone();
        var difference = new ByteRange(0x31, 0x20);
        var canonical = OutputDifferenceReplaySegment.CreateWithAlignedContext(before, after, difference);
        var shortened = new OutputDifferenceReplaySegment(
            canonical.Range.Start + 0x10,
            canonical.BeforeBytes[0x10..],
            canonical.AfterBytes[0x10..]);
        var complete = OutputDifferenceReplaySegment.CreateWithAlignedContext(
            before.AsMemory(0, 0x40),
            after.AsMemory(0, 0x40),
            new ByteRange(0x10, 0x10));

        Assert.True(canonical.MatchesPersistableAlignedContext(before.Length, difference));
        Assert.False(shortened.MatchesPersistableAlignedContext(before.Length, difference));
        Assert.False(complete.MatchesPersistableAlignedContext(0x40, new ByteRange(0x10, 0x10)));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
