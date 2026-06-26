using NvtFwCombiner.Domain.Memory;

namespace NvtFwCombiner.Domain.Tests.Memory;

/// <summary>Tests for half-open byte range behavior.</summary>
public sealed class ByteRangeTests
{
    /// <summary>Verifies checked half-open arithmetic at the exclusive end.</summary>
    [Fact]
    public void EndExclusiveUsesCheckedHalfOpenArithmetic()
    {
        var range = new ByteRange(new ByteOffset(0x100), new ByteLength(0x20));

        Assert.Equal(0x120, range.EndExclusive);
        Assert.True(range.Contains(new ByteOffset(0x11F)));
        Assert.False(range.Contains(new ByteOffset(0x120)));
    }

    /// <summary>Verifies that adjacent ranges do not overlap.</summary>
    [Fact]
    public void OverlapsTreatsTouchingRangesAsDisjoint()
    {
        var left = new ByteRange(new ByteOffset(0), new ByteLength(16));
        var right = new ByteRange(new ByteOffset(16), new ByteLength(16));

        Assert.False(left.Overlaps(right));
        Assert.Null(left.Intersect(right));
    }

    /// <summary>Verifies that range construction rejects checked arithmetic overflow.</summary>
    [Fact]
    public void ConstructorRejectsOverflow()
    {
        _ = Assert.Throws<OverflowException>(() =>
            new ByteRange(new ByteOffset(long.MaxValue), new ByteLength(1)));
    }
}
