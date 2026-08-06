namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Map preparation retains Domain capability admission without a Profiles mirror graph.</summary>
    [Fact]
    public void V2PreparationUsesCanonicalMapAndCapabilityAdmission()
    {
        string profiles = ReadProfileSources();
        string preparation = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPreparationService.cs");
        string lowering = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs");

        Assert.DoesNotContain("class CompositionProfileMapAdmissionResult", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("sealed class CompositionProfileMapAdmission", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("class AdmittedCapabilityEvidence", profiles, StringComparison.Ordinal);
        Assert.Contains("out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions", preparation, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompiledCapabilityAdmission", lowering, StringComparison.Ordinal);
        Assert.Equal(
            1,
            profiles.Split(
                "CompositionProfileMapAdmissionValidator.Validate(",
                StringSplitOptions.None).Length - 1);
    }

    /// <summary>Static trusted profile/map facts are admitted once by the catalog, not re-derived per compilation.</summary>
    [Fact]
    public void V2RuntimeAdmissionDoesNotRevalidateCatalogOwnedMapIdentityOrStaticFacts()
    {
        string admission = ReadText(
            "src/NvtFwCombiner.Profiles/V2/CompositionProfileMapAdmissionValidator.cs");
        string catalog = ReadText(
            "src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogFactory.cs");

        Assert.DoesNotContain("ProfileFamilyIdMismatch", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolvedMapNotOwned", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredRegionMissing", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredMetadataStructureMissing", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataTargetMissing", admission, StringComparison.Ordinal);
        Assert.Contains("ValidateStaticMapContract", catalog, StringComparison.Ordinal);
    }

    /// <summary>Runtime lowering selects the declared context but does not repeat immutable profile-shape validation.</summary>
    [Fact]
    public void V2RuntimeLoweringTrustsCanonicalProfileShape()
    {
        string definition = ReadText(
            "src/NvtFwCombiner.Profiles/V2/CompositionProfileDefinition.Graph.cs");
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
}
