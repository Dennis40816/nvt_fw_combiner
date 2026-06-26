using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Memory;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests for explicit mapping invariants.</summary>
public sealed class ExplicitMappingTests
{
    /// <summary>Verifies that source and target ranges must have identical byte lengths.</summary>
    [Fact]
    public void ConstructorRejectsDifferentSourceAndTargetLengths()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new ExplicitMapping(
                "mapping-1",
                0,
                ExplicitMappingOperationKind.CopyRange,
                "input-1",
                Range(0, 16),
                "output",
                Range(0, 8),
                OverlapPolicy.Reject,
                1,
                "test"));
    }

    /// <summary>Verifies that target starts must satisfy the declared alignment.</summary>
    [Fact]
    public void ConstructorRejectsMisalignedTarget()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new ExplicitMapping(
                "mapping-1",
                0,
                ExplicitMappingOperationKind.ReplaceRange,
                "input-1",
                Range(0, 16),
                "work-buffer",
                Range(3, 16),
                OverlapPolicy.Reject,
                4,
                "test"));
    }

    private static ByteRange Range(long start, long length)
    {
        return new(new ByteOffset(start), new ByteLength(length));
    }
}
