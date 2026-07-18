using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Non-routed V2 runtime-reference evidence for NT51926 Common FW 1.4.1 CtrlRAM Replace.</summary>
public sealed class Nt51926CtrlRamRuntimeReferenceProfileTests
{
    private const int TpWorkCapacity = 0x3C000;
    private const int FullFlashCapacity = 0x40000;
    private const int VnStart = 0x315D0;
    private const int VnMaximum = 0x1660;

    /// <summary>Verifies the cascade candidate compiles a short-source prefix without full-region authority.</summary>
    [Fact]
    public void CandidateCompilesShortCtrlRamPrefixWithNarrowProcessorAuthority()
    {
        const int sourceLength = 0x120;
        const string profileId = "nt51926-ctrlram-replace-fw141-runtime-cascade";
        V2CompositionPlanCompileResult result = Compile(profileId, chipCount: 2, TpWorkCapacity, sourceLength);

        Assert.True(result.IsCompiled, FormatIssues(result.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        Assert.Equal(profileId, composition.ProfileId);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, composition.V2Details!.Provenance.Promotion.Stage);
        Assert.Equal(
            "nt51926-ctrlram-fw141-tp-work-240k",
            composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(TpWorkCapacity, composition.Plan.OutputInitialization.Capacity);

        CompositionOperation[] operations = [.. composition.Plan.OrderedOperations];
        Assert.Equal(2, operations.Length);
        Assert.Equal(new ByteRange(VnStart, sourceLength), operations[0].TargetRange);
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            operations[1].ExternalProcessorInvocation);
        Assert.Empty(invocation.StagedSourceBindings);
        Assert.Equal(
            [
                new ByteRange(0x1C, 4),
                new ByteRange(0x3C, 4),
                new ByteRange(0xFC, 4),
                new ByteRange(VnStart, sourceLength),
                new ByteRange(0x32F50, 0x100),
                new ByteRange(0x3B000, 0x800),
            ],
            invocation.AllowedWriteRanges);
    }

    /// <summary>Verifies the full Flash reference selects the canonical map while the processor remains confined to the TP prefix.</summary>
    [Fact]
    public void CandidateKeepsFullFlashTailOutsideProcessorAuthority()
    {
        V2CompositionPlanCompileResult result = Compile(
            "nt51926-ctrlram-replace-fw141-runtime-cascade",
            chipCount: 2,
            FullFlashCapacity,
            sourceLength: 1);

        Assert.True(result.IsCompiled, FormatIssues(result.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal(
            "nt51926-ctrlram-fw141-full-flash-256k",
            composition.V2Details!.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(FullFlashCapacity, composition.Plan.OutputInitialization.Capacity);
        CompositionOperation processor = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor);
        Assert.Equal(new ByteRange(0, TpWorkCapacity), processor.TargetRange);
        Assert.Equal([new ByteRange(0, TpWorkCapacity)], processor.ExternalProcessorInvocation!.AllowedReadRanges);
    }

    /// <summary>Verifies a source mapping cannot cross the declared VN maximum into the following physical gap.</summary>
    [Fact]
    public void CandidateRejectsCtrlRamMappingBeyondDeclaredMaximum()
    {
        V2CompositionPlanCompileResult result = Compile(
            "nt51926-ctrlram-replace-fw141-runtime-cascade",
            chipCount: 2,
            TpWorkCapacity,
            sourceLength: VnMaximum + 1);

        Assert.False(result.IsCompiled);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.v2.runtime-reference-replace.ctrlram-target-invalid");
    }

    private static V2CompositionPlanCompileResult Compile(
        string profileId,
        int chipCount,
        int referenceLength,
        int sourceLength)
    {
        byte[] reference = new byte[referenceLength];
        new byte[] { 0x00, 0x4E, 0x56, 0x54 }.CopyTo(reference, 0x3BFFC);
        return BuiltInV2BundleRegistry.All["nt51926-ctrlram-replace-candidate"].CompileRuntimeReferenceReplace(
            profileId,
            "0.1.0",
            "NT51926",
            ExperienceIds.CtrlRamReplace,
            new TopologySelection(
                chipCount,
                chipCount == 1 ? "single" : "cascade",
                TopologySelectionSource.Requested,
                "ic-number"),
            [new FirmwareArtifactPayload("reference-base", reference)],
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2RuntimeReferenceReplaceInputBinding("reference-base", "reference-base", referenceLength),
                    new V2RuntimeReferenceReplaceInputBinding("vn-source", "ctrlram-source", sourceLength),
                ],
                [new ExplicitMapping(
                    "replace-vn",
                    sequence: 100,
                    ExplicitMappingOperationKind.ReplaceRange,
                    "vn-source",
                    new ByteRange(0, sourceLength),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(VnStart, sourceLength),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "NT51926 Common FW 1.4.1 runtime CtrlRAM prefix evidence.")]));
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
