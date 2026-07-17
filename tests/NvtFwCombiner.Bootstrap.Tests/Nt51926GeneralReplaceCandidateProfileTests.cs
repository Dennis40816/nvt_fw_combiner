using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Non-routed V2 migration evidence for the processor-free NT51926 General Replace DP subset.</summary>
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
        Assert.Equal(3, composition.V2Details.Provenance.Promotion.Blockers.Count);
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

    /// <summary>The candidate and current V1 compiler produce identical DP-only output bytes from the same immutable inputs.</summary>
    [Fact]
    public async Task CandidateMatchesCurrentGeneralReplaceDpOnlyOutputBytesAsync()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-replace-v2-parity");
        byte[] baseBytes = CreatePattern(FullFlashCapacity, 0x26);
        byte[] originalBase = [.. baseBytes];
        byte[] replacement = [0xA5, 0x5A];
        string basePath = workspace.Write("base.bin", baseBytes);
        string sourcePath = workspace.Write("dp-source.bin", replacement);
        string legacyOutputPath = workspace.PathFor("legacy.bin");
        const int targetStart = DpStart + 0x20;

        WorkbenchRunResult legacy = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            "single",
            "General",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["replace-base"] = basePath,
            },
            [new WorkbenchGeneralReplaceMappingInput(
                "dp-map",
                sourcePath,
                $"0x{targetStart:X}",
                $"0x{targetStart + replacement.Length - 1:X}")],
            build: true,
            TestContext.Current.CancellationToken,
            legacyOutputPath);

        Assert.True(legacy.Succeeded, legacy.ReportJson);
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
        Assert.Equal(
            await File.ReadAllBytesAsync(legacyOutputPath, TestContext.Current.CancellationToken),
            execution.OutputBytes.ToArray());
        Assert.Equal(originalBase, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
        Assert.Equal(originalBase, baseBytes);
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
