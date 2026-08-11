namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class ApplicationBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Preparation reuses the Domain map outcome instead of restoring a Profiles status/result mirror.</summary>
    [Fact]
    public void TrustedPreparationUsesDomainMapResolutionOutcome()
    {
        string profiles = ReadProfileSources();
        string bootstrap = ReadBootstrapSources();
        string preparation = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPreparationService.cs");
        string contractLowering = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs");
        string runtimeReplace = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.cs");

        Assert.DoesNotContain("class V2CompositionPreparationResult", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("enum V2CompositionPreparationStatus", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("bool TryPrepare(", profiles, StringComparison.Ordinal);
        Assert.Contains("private PreparedCompilation(", preparation, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(preparation, "new PreparedCompilation("));
        Assert.Equal(1, CountOccurrences(preparation, "internal static bool TryCreate("));
        Assert.DoesNotContain("internal PreparedCompilation(", preparation, StringComparison.Ordinal);
        Assert.Contains(
            "out FirmwareMapResolutionResult? mapResolution",
            profiles,
            StringComparison.Ordinal);
        Assert.DoesNotContain("bool TryCompileAdmitted(", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompileRuntimeReferenceReplaceAdmitted", profiles, StringComparison.Ordinal);
        Assert.Contains(
            "CompilePrepared(\n" +
            "        V2CompositionPreparationService.PreparedCompilation preparation,\n" +
            "        IReadOnlyCollection<string>? selectedInputSlotIds)",
            contractLowering,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompileRuntimeReferenceReplacePrepared(\n" +
            "        V2CompositionPreparationService.PreparedCompilation preparation,\n" +
            "        V2RuntimeReferenceReplaceCompileRequest request)",
            runtimeReplace,
            StringComparison.Ordinal);
        Assert.Contains(
            "private static V2CompositionPlanCompileResult CompileAdmittedCore(",
            profiles,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompileRuntimeReferenceReplaceAdmittedCore", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("V2CompositionPlanCompiler", bootstrap, StringComparison.Ordinal);
    }
}
