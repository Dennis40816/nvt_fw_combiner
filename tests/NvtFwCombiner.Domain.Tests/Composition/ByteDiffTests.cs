using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests byte diff extraction and declared changed-range policy.</summary>
public sealed class ByteDiffTests
{
    /// <summary>Verifies adjacent changed bytes are coalesced into contiguous ranges.</summary>
    [Fact]
    public void FindChangedRangesReturnsContiguousRanges()
    {
        byte[] before = [0, 0, 0, 0, 0, 0, 0, 0];
        byte[] after = [0, 1, 2, 0, 0, 3, 4, 0];

        IReadOnlyList<ByteRange> ranges = ByteDiff.FindChangedRanges(before, after);

        Assert.Equal([new ByteRange(1, 2), new ByteRange(5, 2)], ranges);
    }

    /// <summary>Verifies byte diff rejects before/after buffers with different lengths.</summary>
    [Fact]
    public void FindChangedRangesRejectsLengthChanges()
    {
        byte[] before = [0, 1];
        byte[] after = [0, 1, 2];

        _ = Assert.Throws<ArgumentException>(() => ByteDiff.FindChangedRanges(before, after));
    }

    /// <summary>Verifies all observed changes are accepted when they are inside declared ranges.</summary>
    [Fact]
    public void ChangedRangePolicyAllowsDeclaredChanges()
    {
        ChangedRangePolicy policy = new([new ByteRange(10, 4), new ByteRange(20, 8)]);

        ChangedRangeVerdict verdict = policy.Evaluate([new ByteRange(11, 2), new ByteRange(20, 8)]);

        Assert.True(verdict.IsAllowed);
        Assert.Empty(verdict.ViolatingRanges);
    }

    /// <summary>Verifies a changed byte outside the declared range makes the verdict fail.</summary>
    [Fact]
    public void ChangedRangePolicyRejectsOneByteOutsideDeclaredRange()
    {
        ChangedRangePolicy policy = new([new ByteRange(10, 4)]);

        ChangedRangeVerdict verdict = policy.Evaluate([new ByteRange(13, 2)]);

        Assert.False(verdict.IsAllowed);
        Assert.Equal([new ByteRange(13, 2)], verdict.ViolatingRanges);
    }

    /// <summary>Verifies adjacent declared write ranges authorize an observed range that spans their boundary.</summary>
    [Fact]
    public void ChangedRangePolicyAllowsAdjacentDeclaredRangeUnion()
    {
        ChangedRangePolicy policy = new([new ByteRange(10, 2), new ByteRange(12, 3)]);

        ChangedRangeVerdict verdict = policy.Evaluate([new ByteRange(10, 5)]);

        Assert.True(verdict.IsAllowed);
        Assert.Empty(verdict.ViolatingRanges);
    }

    /// <summary>Verifies separated declared write ranges do not authorize a changed range through the gap.</summary>
    [Fact]
    public void ChangedRangePolicyRejectsRangeAcrossUndeclaredGap()
    {
        ChangedRangePolicy policy = new([new ByteRange(10, 2), new ByteRange(13, 2)]);

        ChangedRangeVerdict verdict = policy.Evaluate([new ByteRange(10, 5)]);

        Assert.False(verdict.IsAllowed);
        Assert.Equal([new ByteRange(10, 5)], verdict.ViolatingRanges);
    }
}
