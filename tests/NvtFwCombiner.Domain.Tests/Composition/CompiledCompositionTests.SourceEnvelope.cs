using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Compilation retains distinct template and actual lengths with a frozen advisory snapshot.</summary>
    [Fact]
    public void SourceEnvelopeExtentSeparatesTemplateCapacityFromCapturedOutputLength()
    {
        long[] expected = [0x40000, 0x80000, 0x100000];
        var extent = new SourceEnvelopeExtent(
            "dp-input", "dp-container", "nt51950-standard-merge-256k",
            0x40000, 0x40001, expected, "DP_NONSTANDARD_SIZE_WARNING");
        expected[0] = 1;

        Assert.Equal(0x40000, extent.LayoutTemplateCapacity);
        Assert.Equal(0x40001, extent.ActualOutputLength);
        Assert.Equal([0x40000, 0x80000, 0x100000], extent.ExpectedOuterLengths);
        Assert.Equal("nt51950-standard-merge-256k", extent.LayoutTemplateMapId);
    }
}
