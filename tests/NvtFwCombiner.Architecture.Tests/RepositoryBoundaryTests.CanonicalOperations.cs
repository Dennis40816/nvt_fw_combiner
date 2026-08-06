namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Normalized operations reuse Domain primitives and retain only unresolved references.</summary>
    [Fact]
    public void NormalizedOperationsUseDomainCanonicalPrimitives()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();

        Assert.DoesNotContain("abstract record CompositionProfileOperation", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyOrReplaceProfileOperation", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("FillRangeProfileOperation", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("PatchScalarProfileOperation", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("TransformScalarProfileOperation", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("RunProcessorProfileOperation", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("enum CompositionProfileOperationKind", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("class CompositionProfileByteValue", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("abstract record TransformAddendSource", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("FixedTransformAddendSource", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("RegionInstanceDeltaTransformAddendSource", profiles, StringComparison.Ordinal);
        Assert.Contains("class CompositionOperationDefinition", domain, StringComparison.Ordinal);
        Assert.Contains("enum CompositionOperationKind", domain, StringComparison.Ordinal);
        Assert.Contains("class CompiledValidationBytes", domain, StringComparison.Ordinal);
        Assert.Contains("record ScalarTransformAddendSource", domain, StringComparison.Ordinal);
    }
}
