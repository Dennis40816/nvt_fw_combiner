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
        var range = ByteRange.FromStartEndExclusive(10, 14);

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

    /// <summary>Disjoint and adjacent removals leave the source range unchanged.</summary>
    [Fact]
    public void SubtractPreservesSourceWithoutOverlap()
    {
        ByteRange source = new(10, 10);

        Assert.Equal([source], source.Subtract([new ByteRange(0, 10), new ByteRange(20, 2)]));
    }

    /// <summary>A covering removal leaves no remaining byte range.</summary>
    [Fact]
    public void SubtractReturnsEmptyWhenSourceIsCovered()
    {
        ByteRange source = new(10, 10);

        Assert.Empty(source.Subtract([new ByteRange(0, 30)]));
    }

    /// <summary>Unordered and overlapping removals produce minimal ordered remaining segments.</summary>
    [Fact]
    public void SubtractNormalizesOverlappingRemovals()
    {
        ByteRange source = new(10, 10);

        Assert.Equal(
            [new ByteRange(10, 1), new ByteRange(15, 2)],
            source.Subtract([new ByteRange(17, 5), new ByteRange(13, 2), new ByteRange(11, 3)]));
    }

    /// <summary>Subtraction remains checked at the largest representable exclusive end.</summary>
    [Fact]
    public void SubtractSupportsLongMaxExclusiveEnd()
    {
        ByteRange source = new(long.MaxValue - 4, 4);

        Assert.Equal(
            [new ByteRange(long.MaxValue - 4, 2)],
            source.Subtract([new ByteRange(long.MaxValue - 2, 2)]));
    }

    /// <summary>Every small-range byte remains exactly when neither removal contains it.</summary>
    [Fact]
    public void SubtractPreservesExactlyUnremovedBytes()
    {
        ByteRange source = new(2, 6);
        ByteRange[] removals =
        [
            .. Enumerable.Range(0, 10)
                .SelectMany(start => Enumerable.Range(1, 10 - start).Select(length => new ByteRange(start, length))),
        ];

        foreach (ByteRange first in removals)
        {
            foreach (ByteRange second in removals)
            {
                IReadOnlyList<ByteRange> remaining = source.Subtract([first, second]);
                Assert.All(remaining, range => Assert.True(source.Contains(range)));
                for (long offset = 0; offset < 10; offset++)
                {
                    bool expected = source.Contains(offset) && !first.Contains(offset) && !second.Contains(offset);
                    Assert.Equal(expected, remaining.Any(range => range.Contains(offset)));
                }
            }
        }
    }
}
