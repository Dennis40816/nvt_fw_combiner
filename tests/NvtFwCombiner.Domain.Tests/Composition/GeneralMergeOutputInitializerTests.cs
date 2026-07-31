using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Verifies the canonical typed General Merge blank-output initializer.</summary>
public sealed class GeneralMergeOutputInitializerTests
{
    /// <summary>Omitted fill is the reviewed zero-byte compatibility default.</summary>
    [Fact]
    public void OmittedFillDefaultsToZero()
    {
        var initializer = new GeneralMergeOutputInitializer(0x100);

        Assert.Equal(0x100, initializer.Capacity);
        Assert.Equal(0x00, initializer.FillByte);
    }

    /// <summary>Every byte through FF remains a valid deterministic blank fill.</summary>
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x5A)]
    [InlineData(0xFF)]
    public void ExplicitFillAcceptsCompleteByteDomain(int fillByte)
    {
        var initializer = new GeneralMergeOutputInitializer(1, checked((byte)fillByte));

        Assert.Equal(checked((byte)fillByte), initializer.FillByte);
    }

    /// <summary>The typed contract rejects non-positive or unsupported in-memory capacities.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData((long)int.MaxValue + 1)]
    public void CapacityMustFitThePositiveInMemoryDomain(long capacity)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeneralMergeOutputInitializer(capacity));
    }
}
