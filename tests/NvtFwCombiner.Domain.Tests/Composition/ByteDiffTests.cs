using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed class ByteDiffTests
{
    [Fact]
    public void FindChangedRangesReturnsContiguousRanges()
    {
        byte[] before = [0, 0, 0, 0, 0, 0, 0, 0];
        byte[] after = [0, 1, 2, 0, 0, 3, 4, 0];

        IReadOnlyList<ByteRange> ranges = ByteDiff.FindChangedRanges(before, after);

        Assert.Equal([new ByteRange(1, 2), new ByteRange(5, 2)], ranges);
    }

    [Fact]
    public void FindChangedRangesRejectsLengthChanges()
    {
        byte[] before = [0, 1];
        byte[] after = [0, 1, 2];

        Assert.Throws<ArgumentException>(() => ByteDiff.FindChangedRanges(before, after));
    }

    [Fact]
    public void ChangedRangePolicyAllowsDeclaredChanges()
    {
        ChangedRangePolicy policy = new([new ByteRange(10, 4), new ByteRange(20, 8)]);

        ChangedRangeVerdict verdict = policy.Evaluate([new ByteRange(11, 2), new ByteRange(20, 8)]);

        Assert.True(verdict.IsAllowed);
        Assert.Empty(verdict.ViolatingRanges);
    }

    [Fact]
    public void ChangedRangePolicyRejectsOneByteOutsideDeclaredRange()
    {
        ChangedRangePolicy policy = new([new ByteRange(10, 4)]);

        ChangedRangeVerdict verdict = policy.Evaluate([new ByteRange(13, 2)]);

        Assert.False(verdict.IsAllowed);
        Assert.Equal([new ByteRange(13, 2)], verdict.ViolatingRanges);
    }
}
