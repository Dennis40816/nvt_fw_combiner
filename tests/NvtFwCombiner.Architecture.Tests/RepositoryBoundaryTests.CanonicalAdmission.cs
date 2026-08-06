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
        Assert.Contains("IReadOnlyList<CompiledCapabilityAdmission> CapabilityAdmissions", preparation, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompiledCapabilityAdmission", lowering, StringComparison.Ordinal);
        Assert.Equal(
            1,
            profiles.Split(
                "V2CompositionPreparationResult.Admitted(",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            1,
            profiles.Split(
                "CompositionProfileMapAdmissionValidator.Validate(",
                StringSplitOptions.None).Length - 1);
    }
}
