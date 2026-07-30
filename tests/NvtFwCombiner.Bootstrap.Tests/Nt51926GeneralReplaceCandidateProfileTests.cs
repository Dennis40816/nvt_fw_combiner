using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>V2 migration evidence for the routed processor-free NT51926 General Replace DP subset.</summary>
public sealed class Nt51926GeneralReplaceCandidateProfileTests
{
    private const int FullFlashCapacity = 0x40000;
    private const int DpStart = 0x3E000;
    private static readonly Lazy<TrustedProfileBundleCatalog> CandidateCatalog = new(LoadCandidateCatalog);

    /// <summary>The built-in candidate compiles only the exact full-Flash DP explicit-range contract.</summary>
    [Fact]
    public void CandidateCompilesTheFullFlashDpSubsetWithoutRuntimePromotion()
    {
        V2CompositionPlanCompileResult result = CompileCandidate(
            FullFlashCapacity,
            sourceLength: 2,
            targetStart: DpStart + 0x10,
            targetLength: 2);

        Assert.True(result.IsCompiled, FormatIssues(result.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        Assert.Equal("nt51926-general-replace-dp-single-candidate", composition.ProfileId);
        Assert.Equal("0.1.0", composition.ProfileVersion);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, composition.V2Details!.Provenance.Promotion.Stage);
        Assert.Equal(2, composition.V2Details.Provenance.Promotion.Blockers.Count);
        Assert.Equal(
            "nt51926-general-replace-full-flash-256k",
            composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        CompositionOperation operation = Assert.Single(composition.Plan.OrderedOperations);
        Assert.Equal(CompositionOperationKind.ReplaceRange, operation.Kind);
        Assert.Equal(new ByteRange(DpStart + 0x10, 2), operation.TargetRange);
        Assert.Null(operation.ExternalProcessorInvocation);
    }

    /// <summary>The candidate fails closed for TP-only bases and TP/CTRLRAM targets.</summary>
    [Theory]
    [InlineData(0x3C000, 0x3E000, "profile.v2.compile.map-selection-invalid")]
    [InlineData(FullFlashCapacity, 0x22800, "profile.v2.plan.region-access-denied")]
    public void CandidateRejectsUndeclaredBaseAndTpTargets(int referenceLength, int targetStart, string issueCode)
    {
        V2CompositionPlanCompileResult result = CompileCandidate(
            referenceLength,
            sourceLength: 1,
            targetStart,
            targetLength: 1);

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    /// <summary>The routed candidate produces the compiled V2 DP-only bytes from immutable inputs.</summary>
    [Fact]
    public async Task RoutedCandidateMatchesCompiledGeneralReplaceDpOnlyOutputBytesAsync()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-replace-v2-parity");
        byte[] baseBytes = CreatePattern(FullFlashCapacity, 0x26);
        byte[] originalBase = [.. baseBytes];
        byte[] replacement = [0xA5, 0x5A];
        string basePath = workspace.Write("base.bin", baseBytes);
        string sourcePath = workspace.Write("dp-source.bin", replacement);
        const int targetStart = DpStart + 0x20;

        V2CompositionPlanCompileResult candidate = CompileCandidate(
            FullFlashCapacity,
            replacement.Length,
            targetStart,
            replacement.Length);
        Assert.True(candidate.IsCompiled, FormatIssues(candidate.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(candidate.CompiledComposition);
        CompositionExecutionResult execution = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["base"] = baseBytes,
                ["source-a"] = replacement,
            }),
            externalProcessor: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        byte[] expected = [.. originalBase];
        replacement.CopyTo(expected, targetStart);
        Assert.Equal(expected, execution.OutputBytes.ToArray());
        Assert.Equal(originalBase, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
        Assert.Equal(originalBase, baseBytes);

        string routedOutputPath = workspace.PathFor("routed.bin");
        var mappingDraft = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "dp-map",
                ExplicitMappingOperationKind.ReplaceRange,
                GeneralMappingSource.File(sourcePath),
                new ByteRange(0, replacement.Length),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(targetStart, replacement.Length),
                OverlapPolicy.Reject,
                alignment: 1,
                "General Replace V2 DP parity mapping."),
        ]);
        WorkbenchRunResult routed = await WorkbenchCompositionService.RunGeneralReplaceEphemeralDraftAsync(
            "NT51926",
            "single",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = basePath,
            },
            mappingDraft,
            build: true,
            TestContext.Current.CancellationToken,
            routedOutputPath);

        Assert.True(routed.Succeeded, routed.ReportJson);
        Assert.Equal(execution.OutputBytes.ToArray(), await File.ReadAllBytesAsync(
            routedOutputPath,
            TestContext.Current.CancellationToken));
        using var routedReport = JsonDocument.Parse(routed.ReportJson);
        Assert.Equal(
            "nt51926-general-replace-dp-single-candidate",
            routedReport.RootElement.GetProperty("ProfileId").GetString());
    }

    /// <summary>Single-selector virtual patches fail closed until their V2 contract is migrated.</summary>
    [Fact]
    public async Task DpVirtualPatchFailsClosedWithoutOutputAsync()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-replace-patch-unsupported");
        string basePath = workspace.Write("base.bin", CreatePattern(FullFlashCapacity, 0x26));

        string outputPath = workspace.PathFor("unsupported-output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            "single",
            "General",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = basePath,
            },
            [],
            [new WorkbenchGeneralReplacePatchInput(
                "dp-patch",
                "0x3E020",
                "0x3E021",
                WorkbenchGeneralReplacePatchKind.Overwrite,
                "A5 5A")],
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.False(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(WorkbenchIssueCodes.ReplaceWorkflowNotSupported, issue.GetProperty("Code").GetString());
        Assert.False(File.Exists(outputPath));
    }

    private static V2CompositionPlanCompileResult CompileCandidate(
        int referenceLength,
        int sourceLength,
        int targetStart,
        int targetLength)
    {
        return TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CandidateCatalog.Value,
            "nt51926-general-replace-dp-single-candidate",
            "0.1.0",
            "NT51926",
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2RuntimeReferenceReplaceInputBinding("base", "reference", referenceLength),
                    new V2RuntimeReferenceReplaceInputBinding("source-a", "source", sourceLength),
                ],
                [new ExplicitMapping(
                    "dp-map",
                    sequence: 100,
                    ExplicitMappingOperationKind.ReplaceRange,
                    "source-a",
                    new ByteRange(0, targetLength),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(targetStart, targetLength),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "General Replace V2 DP parity mapping.")]));
    }

    private static TrustedProfileBundleCatalog LoadCandidateCatalog()
    {
        const string bundleDirectory = "nt51926-ctrlram-replace-candidate";
        return V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            bundleDirectory,
            BuiltInV2BundleRegistry.All[bundleDirectory].ContentHash);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
