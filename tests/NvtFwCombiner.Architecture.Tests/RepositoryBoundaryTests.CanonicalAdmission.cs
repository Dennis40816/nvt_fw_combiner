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

        AssertDoesNotContainAny(profiles, "class CompositionProfileMapAdmissionResult",
            "sealed class CompositionProfileMapAdmission", "class AdmittedCapabilityEvidence",
            "CompositionProfileMapAdmissionValidator");
        AssertContainsAll(domain, "AdmitRequiredCapabilities");
        const string canonicalCapabilityBindingList =
            "IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>>";
        AssertContainsAll(preparation, $"internal {canonicalCapabilityBindingList} CapabilityAdmissions",
            $"out {canonicalCapabilityBindingList} admittedCapabilities", ".AdmitRequiredCapabilities(");
        AssertDoesNotContainAny(preparation, "CompiledCapabilityAdmission");
        AssertDoesNotContainAny(lowering, "CompiledCapabilityAdmission");
    }

    /// <summary>Static trusted profile/map facts are admitted once by the catalog, not re-derived per compilation.</summary>
    [Fact]
    public void V2RuntimeAdmissionDoesNotRevalidateCatalogOwnedMapIdentityOrStaticFacts()
    {
        string domain = ReadDomainSources();
        string catalog = ReadText(
            "src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogFactory.cs");

        AssertDoesNotContainAny(domain, "ProfileFamilyIdMismatch", "ResolvedMapNotOwned",
            "RequiredRegionMissing", "RequiredMetadataStructureMissing", "MetadataTargetMissing");
        AssertContainsAll(catalog, "ValidateStaticMapContract");
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

        AssertContainsAll(definition, "ValidateLogicalOutputShape", "ValidateRuntimeReferenceReplaceShape");
        AssertDoesNotContainAny(logical, "IsLogicalOutputProfile");
        AssertDoesNotContainAny(runtimeReplace, "IsRuntimeReferenceReplaceProfile", "expectedSourceClass");
    }

    /// <summary>Metadata read envelopes are derived by the canonical family, not revalidated by the compiler.</summary>
    [Fact]
    public void V2InputGeometryUsesCanonicalMetadataReadEnvelope()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();

        AssertDoesNotContainAny(profiles, "TryResolveMetadataReadEnd");
        AssertContainsAll(domain, "GetMaximumMetadataReadEnd");
    }

    /// <summary>Canonical graph validation owns cycle traversal for Domain facts and Profiles alias normalization.</summary>
    [Fact]
    public void CanonicalDependencyTraversalHasOneOwner()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();

        AssertContainsAll(domain, "AcyclicDependencyGraph.Sort");
        AssertContainsAll(profiles, "AcyclicDependencyGraph.Sort");
        AssertDoesNotContainAny(profiles, "DependencyVisitState", "DependencyFrame", "CapabilityDirect");
        AssertDoesNotContainAny(domain, "ParentVisitState");
    }
}
