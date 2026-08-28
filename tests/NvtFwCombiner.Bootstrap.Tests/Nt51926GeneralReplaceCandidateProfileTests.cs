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
        Assert.Equal("nt51926-general-replace-dp-single-candidate", composition.V2Details.ProfileId);
        Assert.Equal("0.1.0", composition.V2Details.ProfileVersion);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, composition.V2Details.Provenance.Promotion.Stage);
        Assert.Equal(
            CompiledOutputNameRendererKind.Static,
            composition.V2Details.OutputNamingRequirement.RendererKind);
        Assert.Null(composition.V2Details.OutputNamingRequirement.RuleId);
        Assert.Equal(
            CompiledOutputArtifactType.Unspecified,
            composition.V2Details.OutputNamingRequirement.OutputArtifactType);
        Assert.Empty(composition.V2Details.OutputNamingRequirement.TokenRequirements);
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
        CompositionRunResult routed = await GeneralWorkflowTestSupport.BuildGeneralReplaceAsync(BootstrapTestHost.Canonical,
            "NT51926",
            "single",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionSlotIds.ReplaceBase] = basePath,
            },
            mappingDraft,
            routedOutputPath,
            TestContext.Current.CancellationToken);

        Assert.True(routed.Succeeded, CompositionRunReportJson.Serialize(routed));
        Assert.Equal(execution.OutputBytes.ToArray(), await File.ReadAllBytesAsync(
            routedOutputPath,
            TestContext.Current.CancellationToken));
        using var routedReport = JsonDocument.Parse(CompositionRunReportJson.Serialize(routed));
        Assert.Equal(
            "nt51926-general-replace-dp-single-candidate",
            routedReport.RootElement.GetProperty("ProfileId").GetString());
    }

    /// <summary>The public runner independently rejects forged Saved Rule Parent provenance.</summary>
    [Fact]
    public async Task RoutedCandidateRejectsMismatchedSavedRuleParentBeforeOutputAsync()
    {
        using var workspace = TempWorkspace.Create(
            "nvt-fw-combiner-general-replace-parent-mismatch");
        string basePath = workspace.Write(
            "base.bin",
            CreatePattern(FullFlashCapacity, 0x27));
        string sourcePath = workspace.Write("source.bin", [0xA5]);
        string outputPath = workspace.PathFor("must-not-exist.bin");
        var draft = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "dp-map",
                ExplicitMappingOperationKind.ReplaceRange,
                GeneralMappingSource.File(sourcePath),
                new ByteRange(0, 1),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(DpStart, 1),
                OverlapPolicy.Reject,
                alignment: 1,
                "Mismatched Parent regression."),
        ]);
        GeneralResourceLimits limits = new(
            1,
            1,
            1,
            1,
            [new GeneralSlotLengthLimits("dp-map", 1, 1)]);
        var forgedIdentity = new SavedRuleExecutionIdentity(
            "forged",
            "1.0.0",
            new string('a', 64),
            new SavedRuleParentIdentity(
                "forged-bundle",
                "1.0.0",
                new string('b', 64),
                "forged-profile",
                "1.0.0",
                new string('c', 64),
                "forged-family",
                "1.0.0",
                new string('d', 64),
                "forged-map"));

        var savedRulePolicy = new GeneralSavedRuleResourcePolicy(
            new SavedRuleLifecycleSnapshot(
                forgedIdentity,
                SavedRuleStorageKind.TrustedCatalog,
                SavedRuleLifecycleState.Published,
                hasApproval: true,
                hasEvidence: true,
                isTrusted: true),
            limits);
        GeneralAuthoringSessionPreparation prepared =
            await GeneralWorkflowTestSupport.PrepareGeneralReplaceAsync(
                BootstrapTestHost.Canonical,
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [CompositionSlotIds.ReplaceBase] = basePath,
                },
                draft,
                savedRulePolicy,
                TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(
            prepared.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.SavedRuleParentMismatch);
        Assert.Null(prepared.Admission?.SavedRule);
    }

    /// <summary>Single-selector virtual patches fail closed until their V2 contract is migrated.</summary>
    [Fact]
    public async Task DpVirtualPatchFailsClosedWithoutOutputAsync()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-replace-patch-unsupported");
        string basePath = workspace.Write("base.bin", CreatePattern(FullFlashCapacity, 0x26));

        string outputPath = workspace.PathFor("unsupported-output.bin");
        GeneralAuthoringSessionPreparation prepared =
            await GeneralWorkflowTestSupport.PrepareGeneralReplaceAsync(
            BootstrapTestHost.Canonical,
            "NT51926",
            "single",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionSlotIds.ReplaceBase] = basePath,
            },
            GeneralTestDraftFactory.CreateReplaceDraft([
                GeneralTestDraftFactory.ReplacePatch(
                "dp-patch",
                "0x3E020",
                "0x2",
                GeneralMappingSourceKind.HexOverwrite,
                "A5 5A"),
            ]),
            savedRulePolicy: null,
            TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Equal(
            CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported,
            Assert.Single(prepared.Issues).Code);
        Assert.False(File.Exists(outputPath));
    }

    private static V2CompositionPlanCompileResult CompileCandidate(
        int referenceLength,
        int sourceLength,
        int targetStart,
        int targetLength)
    {
        return CandidateCatalog.Value.CompileRuntimeReferenceReplace(
            "nt51926-general-replace-dp-single-candidate",
            "0.1.0",
            "NT51926",
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2ExplicitMappingInputBinding("base", "reference", referenceLength),
                    new V2ExplicitMappingInputBinding("source-a", "source", sourceLength),
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
