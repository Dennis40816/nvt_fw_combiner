using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed class ByteRangeTests
{
    [Fact]
    public void ConstructorRejectsNegativeStart()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ByteRange(-1, 1));
    }

    [Fact]
    public void ConstructorRejectsNonPositiveLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ByteRange(0, 0));
    }

    [Fact]
    public void FromStartEndExclusiveUsesHalfOpenSemantics()
    {
        ByteRange range = ByteRange.FromStartEndExclusive(10, 14);

        Assert.Equal(10, range.Start);
        Assert.Equal(4, range.Length);
        Assert.Equal(14, range.EndExclusive);
        Assert.True(range.Contains(10));
        Assert.True(range.Contains(13));
        Assert.False(range.Contains(14));
    }

    [Fact]
    public void IntersectReturnsSharedRange()
    {
        ByteRange left = new(10, 10);
        ByteRange right = new(15, 10);

        Assert.Equal(new ByteRange(15, 5), left.Intersect(right));
    }

    [Fact]
    public void AdjacentRangesDoNotOverlap()
    {
        ByteRange left = new(0, 4);
        ByteRange right = new(4, 2);

        Assert.False(left.Overlaps(right));
        Assert.Null(left.Intersect(right));
    }
}
