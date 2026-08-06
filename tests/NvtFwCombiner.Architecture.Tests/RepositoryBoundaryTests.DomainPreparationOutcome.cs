namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Preparation reuses the Domain map outcome instead of restoring a Profiles status/result mirror.</summary>
    [Fact]
    public void TrustedPreparationUsesDomainMapResolutionOutcome()
    {
        string profiles = ReadProfileSources();
        string bootstrap = ReadBootstrapSources();

        Assert.DoesNotContain("class V2CompositionPreparationResult", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("enum V2CompositionPreparationStatus", profiles, StringComparison.Ordinal);
        Assert.Contains("bool TryPrepare(", profiles, StringComparison.Ordinal);
        Assert.Contains(
            "out FirmwareMapResolutionResult? mapResolution",
            profiles,
            StringComparison.Ordinal);
        Assert.Contains("bool TryCompileAdmitted(", profiles, StringComparison.Ordinal);
        Assert.Contains(
            "private static V2CompositionPlanCompileResult CompileAdmittedCore(",
            profiles,
            StringComparison.Ordinal);
        Assert.Contains(
            "private static V2CompositionPlanCompileResult CompileRuntimeReferenceReplaceAdmittedCore(",
            profiles,
            StringComparison.Ordinal);
        Assert.DoesNotContain("V2CompositionPlanCompiler", bootstrap, StringComparison.Ordinal);
    }
}
