using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests composition-layer byte range invariants.</summary>
public sealed class ByteRangeTests
{
    /// <summary>Verifies negative starts are rejected.</summary>
    [Fact]
    public void ConstructorRejectsNegativeStart()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ByteRange(-1, 1));
    }

    /// <summary>Verifies zero and negative lengths are rejected.</summary>
    [Fact]
    public void ConstructorRejectsNonPositiveLength()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ByteRange(0, 0));
    }

    /// <summary>Verifies start/end construction uses half-open range semantics.</summary>
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

    /// <summary>Verifies intersect returns the shared half-open range.</summary>
    [Fact]
    public void IntersectReturnsSharedRange()
    {
        ByteRange left = new(10, 10);
        ByteRange right = new(15, 10);

        Assert.Equal(new ByteRange(15, 5), left.Intersect(right));
    }

    /// <summary>Verifies adjacent ranges do not overlap.</summary>
    [Fact]
    public void AdjacentRangesDoNotOverlap()
    {
        ByteRange left = new(0, 4);
        ByteRange right = new(4, 2);

        Assert.False(left.Overlaps(right));
        Assert.Null(left.Intersect(right));
    }
}
