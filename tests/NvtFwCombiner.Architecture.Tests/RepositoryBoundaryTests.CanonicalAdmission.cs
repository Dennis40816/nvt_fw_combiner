namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Map preparation retains Domain capability admission without a Profiles mirror graph.</summary>
    [Fact]
    public void V2PreparationUsesCanonicalMapAndCapabilityAdmission()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();
        string preparation = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPreparationService.cs");
        string lowering = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs");

        Assert.DoesNotContain("class CompositionProfileMapAdmissionResult", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("sealed class CompositionProfileMapAdmission", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("class AdmittedCapabilityEvidence", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileMapAdmissionValidator", profiles, StringComparison.Ordinal);
        Assert.Contains("AdmitRequiredCapabilities", domain, StringComparison.Ordinal);
        const string canonicalCapabilityBindingList =
            "IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>>";
        Assert.Contains(
            $"internal {canonicalCapabilityBindingList} CapabilityAdmissions",
            preparation,
            StringComparison.Ordinal);
        Assert.Contains(
            $"out {canonicalCapabilityBindingList} admittedCapabilities",
            preparation,
            StringComparison.Ordinal);
        Assert.Contains(".AdmitRequiredCapabilities(", preparation, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledCapabilityAdmission", preparation, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledCapabilityAdmission", lowering, StringComparison.Ordinal);
    }

    /// <summary>Static trusted profile/map facts are admitted once by the catalog, not re-derived per compilation.</summary>
    [Fact]
    public void V2RuntimeAdmissionDoesNotRevalidateCatalogOwnedMapIdentityOrStaticFacts()
    {
        string domain = ReadDomainSources();
        string catalog = ReadText(
            "src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogFactory.cs");

        Assert.DoesNotContain("ProfileFamilyIdMismatch", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolvedMapNotOwned", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredRegionMissing", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredMetadataStructureMissing", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataTargetMissing", domain, StringComparison.Ordinal);
        Assert.Contains("ValidateStaticMapContract", catalog, StringComparison.Ordinal);
    }

    /// <summary>Runtime lowering selects the declared context but does not repeat immutable profile-shape validation.</summary>
    [Fact]
    public void V2RuntimeLoweringTrustsCanonicalProfileShape()
    {
        string definition = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompositionProfileDefinition.Graph.cs");
        string logical = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.LogicalOutput.cs");
        string runtimeReplace = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.cs");

        Assert.Contains("ValidateLogicalOutputShape", definition, StringComparison.Ordinal);
        Assert.Contains("ValidateRuntimeReferenceReplaceShape", definition, StringComparison.Ordinal);
        Assert.DoesNotContain("IsLogicalOutputProfile", logical, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRuntimeReferenceReplaceProfile", runtimeReplace, StringComparison.Ordinal);
        Assert.DoesNotContain("expectedSourceClass", runtimeReplace, StringComparison.Ordinal);
    }

    /// <summary>Metadata read envelopes are derived by the canonical family, not revalidated by the compiler.</summary>
    [Fact]
    public void V2InputGeometryUsesCanonicalMetadataReadEnvelope()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();

        Assert.DoesNotContain("TryResolveMetadataReadEnd", profiles, StringComparison.Ordinal);
        Assert.Contains("GetMaximumMetadataReadEnd", domain, StringComparison.Ordinal);
    }

    /// <summary>Canonical graph validation owns cycle traversal for Domain facts and Profiles alias normalization.</summary>
    [Fact]
    public void CanonicalDependencyTraversalHasOneOwner()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();

        Assert.Contains("AcyclicDependencyGraph.Sort", domain, StringComparison.Ordinal);
        Assert.Contains("AcyclicDependencyGraph.Sort", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("DependencyVisitState", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("DependencyFrame", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("CapabilityDirect", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("ParentVisitState", domain, StringComparison.Ordinal);
    }
}
