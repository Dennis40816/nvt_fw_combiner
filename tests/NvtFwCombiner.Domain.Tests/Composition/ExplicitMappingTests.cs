using NvtFwCombiner.Domain.Composition;

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

    /// <summary>Verifies runtime explicit mappings carry report provenance by default.</summary>
    [Fact]
    public void ConstructorAssignsRuntimeGeneralMappingProvenance()
    {
        var mapping = new ExplicitMapping(
            "mapping-1",
            0,
            ExplicitMappingOperationKind.CopyRange,
            "input-1",
            Range(0, 16),
            "output",
            Range(0, 16),
            OverlapPolicy.Reject,
            1,
            "test");

        Assert.Equal("runtime-general-mapping", mapping.Provenance.Kind);
        Assert.Equal("mapping-1", mapping.Provenance.SourceId);
        Assert.Null(mapping.Provenance.SourceVersion);
    }

    /// <summary>Verifies saved-rule provenance records the reviewed rule version.</summary>
    [Fact]
    public void SavedRuleProvenanceRecordsRuleVersion()
    {
        var provenance = OperationProvenance.SavedRule("rule-1", "1.2.3");

        Assert.Equal("saved-rule", provenance.Kind);
        Assert.Equal("rule-1", provenance.SourceId);
        Assert.Equal("1.2.3", provenance.SourceVersion);
    }

    private static ByteRange Range(long start, long length)
    {
        return new(start, length);
    }
}
