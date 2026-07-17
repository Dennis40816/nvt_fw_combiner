using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Compilable, non-routed evidence for the NT51926 Common FW 1.4.1 cascade CtrlRAM postbuild plan.</summary>
public sealed class Nt51926CtrlRamReplaceCandidateProfileTests
{
    private const int TpWorkImageCapacity = 0x3C000;
    private const int FullFlashCapacity = 0x40000;
    private const int FirmwareConfigBackupStart = 0x3B000;
    private const int NvtMarkerStart = FirmwareConfigBackupStart + 0xFFC;

    private static ReadOnlySpan<byte> NvtMarker => [0x00, 0x4E, 0x56, 0x54];

    /// <summary>Locks V2 staging and write authority to the legacy Common FW 1.4.1 cascade command plan.</summary>
    [Fact]
    public void CandidateProfileCompilesTheLegacyCascadeStagingAndWriteAuthority()
    {
        byte[] referenceBase = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        Assert.Equal(FullFlashCapacity, referenceBase.Length);
        CompiledComposition composition = CompileCandidate(referenceBase);
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            Assert.Single(composition.Plan.OrderedOperations).ExternalProcessorInvocation);
        LegacyCombinerPostbuildCommandPlan legacyPlan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51926CommonFw141,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal("nt51926-ctrlram-fw141-full-flash-256k", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
        Assert.Equal(
            ["direct-golden-evidence", "firmware-owner-review", "runtime-route"],
            details.Provenance.Promotion.Blockers.Select(static blocker => blocker.BlockerId));
        FirmwareResolvedMetadataStructure backup = Assert.Single(
            details.Provenance.ResolvedMap.ResolvedMetadataStructures);
        Assert.Equal("nt51926-fwconfig-backup-envelope", backup.DecodedStructure.MetadataStructureId);
        Assert.Equal("reference-base", backup.ArtifactIdentity.ArtifactId);
        Assert.Equal(FirmwareMetadataLocatorKind.MarkerRelative, backup.LocatorOutcome.LocatorKind);
        Assert.Equal(new ByteRange(FirmwareConfigBackupStart, 0x1000), backup.LocatorOutcome.ResolvedRange.Range);
        Assert.Equal(1, backup.LocatorOutcome.MarkerMatchCount);
        Assert.Equal(NvtMarkerStart, backup.LocatorOutcome.SelectedMarkerStart);
        Assert.Equal(
            FirmwareConfigBackupStart,
            backup.LocatorOutcome.SelectedMarkerStart!.Value + 3 - 0xFFF);
        Assert.Equal(FullFlashCapacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal("nfc.nt51926.ctrlram-postbuild-fw1.4.1", invocation.ProcessorId);
        Assert.Equal("legacy-combiner-1.13.0", invocation.ToolBindingId);
        Assert.Equal([new ByteRange(0, TpWorkImageCapacity)], invocation.AllowedReadRanges);
        Assert.Equal(
            [
                new ByteRange(0x1C, 4),
                new ByteRange(0x3C, 4),
                new ByteRange(0xFC, 4),
                new ByteRange(0x22800, 0x2C00),
                new ByteRange(0x25400, 0x2400),
                new ByteRange(0x27800, 0x2800),
                new ByteRange(0x2C800, 0x2DD0),
                new ByteRange(0x315D0, 0x1660),
                new ByteRange(0x32F50, 0x100),
                new ByteRange(0x3B000, 0x800),
            ],
            invocation.AllowedWriteRanges);
        Assert.Equal(
            [
                ("normal-ctrlram-input", new ByteRange(0, 0x2C00), new ByteRange(0x22800, 0x2C00)),
                ("mp-ctrlram-input", new ByteRange(0, 0x2400), new ByteRange(0x25400, 0x2400)),
                ("diff-ctrlram-input", new ByteRange(0, 0x2800), new ByteRange(0x27800, 0x2800)),
                ("nf-ctrlram-input", new ByteRange(0, 0x2DD0), new ByteRange(0x2C800, 0x2DD0)),
                ("vn-ctrlram-input", new ByteRange(0, 0x1660), new ByteRange(0x315D0, 0x1660)),
            ],
            invocation.StagedSourceBindings.Select(static binding =>
                (binding.SourceSpaceId, binding.SourceRange, binding.FirmwareRange)));
        Assert.Equal(
            ["nt51926-fw141-cascade-merge-crc", "nt51926-fw141-cascade-header-crc"],
            legacyPlan.Commands.Select(static command => command.CommandId));
        Assert.Contains(
            legacyPlan.Commands.SelectMany(static command => command.Blocks),
            static block => block.BlockId == "fw-config-backup" &&
                            block.FirmwareRange == new ByteRange(0x3B000, 0x800) &&
                            block.SourceOffset == 0x22000);
        Assert.Contains(
            legacyPlan.Commands.SelectMany(static command => command.Blocks),
            static block => block.BlockId == "header-copy" &&
                            block.FirmwareRange == new ByteRange(0x32F50, 0x100) &&
                            block.SourceOffset == 0);
    }

    /// <summary>Exact reference length selects one canonical map and clone capacity without touching the full-flash tail.</summary>
    [Theory]
    [InlineData(TpWorkImageCapacity, "nt51926-ctrlram-fw141-tp-work-240k")]
    [InlineData(FullFlashCapacity, "nt51926-ctrlram-fw141-full-flash-256k")]
    public async Task CandidateSelectsExactReferenceShapeAndPreservesTheClonedImageAsync(
        int capacity,
        string expectedMapId)
    {
        byte[] referenceBase = CreateReferenceImage(capacity);
        if (capacity == FullFlashCapacity)
        {
            referenceBase.AsSpan(TpWorkImageCapacity).Fill(0xA5);
        }

        CompiledComposition composition = CompileCandidate(referenceBase);
        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(CreateInputs(referenceBase)),
            (_, inputBytes, _, _, _) =>
                ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes)),
            CancellationToken.None);

        Assert.Equal(expectedMapId, composition.V2Details!.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(capacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(referenceBase, result.OutputBytes.ToArray());
    }

    /// <summary>Both artifact shapes expose only the shared TP prefix and retain the reviewed write authority.</summary>
    [Fact]
    public void ArtifactShapeMapsKeepIdenticalProcessorAuthorityAndDistinctFingerprints()
    {
        CompiledComposition tpWork = CompileCandidate(CreateReferenceImage(TpWorkImageCapacity));
        CompiledComposition fullFlash = CompileCandidate(CreateReferenceImage(FullFlashCapacity));
        ExternalProcessorInvocation tpInvocation = Assert.IsType<ExternalProcessorInvocation>(
            Assert.Single(tpWork.Plan.OrderedOperations).ExternalProcessorInvocation);
        ExternalProcessorInvocation fullInvocation = Assert.IsType<ExternalProcessorInvocation>(
            Assert.Single(fullFlash.Plan.OrderedOperations).ExternalProcessorInvocation);

        Assert.Equal([new ByteRange(0, TpWorkImageCapacity)], tpInvocation.AllowedReadRanges);
        Assert.Equal(tpInvocation.AllowedReadRanges, fullInvocation.AllowedReadRanges);
        Assert.Equal(tpInvocation.AllowedWriteRanges, fullInvocation.AllowedWriteRanges);
        Assert.Equal(0x3B800, tpInvocation.AllowedWriteRanges.Max(static range => range.EndExclusive));
        Assert.Equal(tpInvocation.ProcessorId, fullInvocation.ProcessorId);
        Assert.Equal(tpInvocation.ToolBindingId, fullInvocation.ToolBindingId);
        Assert.NotEqual(
            tpWork.V2Details!.Provenance.ResolvedMap.ResolutionFingerprint,
            fullFlash.V2Details!.Provenance.ResolvedMap.ResolutionFingerprint);
        Assert.NotEqual(tpWork.CompilationFingerprint, fullFlash.CompilationFingerprint);
    }

    /// <summary>Only the two owner-approved exact reference capacities can select a canonical map.</summary>
    [Theory]
    [InlineData(TpWorkImageCapacity - 1)]
    [InlineData(TpWorkImageCapacity + 1)]
    [InlineData(FullFlashCapacity - 1)]
    [InlineData(FullFlashCapacity + 1)]
    public void CandidateRejectsUndeclaredReferenceLengths(int capacity)
    {
        V2CompositionPlanCompileResult compilation = CompileCandidateResult(new byte[capacity]);

        Assert.False(compilation.IsCompiled);
        Assert.Null(compilation.CompiledComposition);
        Assert.Contains(
            compilation.Issues,
            static issue => issue.Code == "profile.v2.compile.map-capacity-unavailable");
    }

    /// <summary>A marker in the full-flash-only tail is outside the metadata search authority.</summary>
    [Fact]
    public void FullFlashTailNvtMarkerIsIgnored()
    {
        byte[] referenceBase = CreateReferenceImage(FullFlashCapacity);
        WriteNvtMarker(referenceBase, FullFlashCapacity - NvtMarker.Length);

        CompiledComposition composition = CompileCandidate(referenceBase);
        FirmwareResolvedMetadataStructure backup = Assert.Single(
            composition.V2Details!.Provenance.ResolvedMap.ResolvedMetadataStructures);

        Assert.Equal(1, backup.LocatorOutcome.MarkerMatchCount);
        Assert.Equal(NvtMarkerStart, backup.LocatorOutcome.SelectedMarkerStart);
    }

    /// <summary>Verifies CtrlRAM-only oversize normalization is declared while the candidate remains outside runtime admission.</summary>
    [Fact]
    public async Task CandidatePlanTruncatesOnlyCtrlRamInputsBeforeHostStagingAsync()
    {
        CompiledComposition composition = CompileCandidate(CreateReferenceImage(FullFlashCapacity));
        Dictionary<string, byte[]> inputs = CreateInputs();
        byte[] normal = [.. inputs["normal-ctrlram-input"]];
        inputs["normal-ctrlram-input"] = [.. normal, 0xCC];
        bool invoked = false;

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(inputs),
            (_, inputBytes, stagedSources, _, _) =>
            {
                invoked = true;
                ExternalProcessorStagedSource normalBinding = Assert.Single(
                    stagedSources,
                    static binding => binding.FirmwareRange == new ByteRange(0x22800, 0x2C00));
                Assert.Equal(new ByteRange(0x22800, 0x2C00), normalBinding.FirmwareRange);
                Assert.Equal(normal, normalBinding.Bytes.ToArray());
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes));
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(normal.Length + 1, inputs["normal-ctrlram-input"].Length);
        Assert.Contains(result.Issues, static issue => StringComparer.Ordinal.Equals(
            issue.Code,
            CompositionIssueCodes.InputAddressSpaceTruncated));
    }

    /// <summary>Verifies zero or multiple universal markers reject the candidate before a plan can be minted.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void CandidateCompilationRejectsMissingOrAmbiguousNvtBackupMarker(int markerCount)
    {
        byte[] referenceBase = new byte[FullFlashCapacity];
        if (markerCount >= 1)
        {
            WriteNvtMarker(referenceBase, NvtMarkerStart);
        }

        if (markerCount == 2)
        {
            WriteNvtMarker(referenceBase, 0x34FFC);
        }

        V2CompositionPlanCompileResult compilation = CompileCandidateResult(referenceBase);

        Assert.False(compilation.IsCompiled);
        Assert.Null(compilation.CompiledComposition);
        Assert.Contains(
            compilation.Issues,
            static issue => issue.Code == "profile.v2.compile.preparation-not-admitted");
    }

    private static CompiledComposition CompileCandidate(byte[] referenceBase)
    {
        V2CompositionPlanCompileResult compilation = CompileCandidateResult(referenceBase);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static V2CompositionPlanCompileResult CompileCandidateResult(byte[] referenceBase)
    {
        return WorkbenchCompositionService.CompileNt51926CtrlRamReplaceV2Candidate(referenceBase);
    }

    private static Dictionary<string, byte[]> CreateInputs(byte[]? referenceBase = null)
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["reference-base"] = referenceBase ?? CreateReferenceImage(FullFlashCapacity),
            ["normal-ctrlram-input"] = new byte[0x2C00],
            ["diff-ctrlram-input"] = new byte[0x2800],
            ["mp-ctrlram-input"] = new byte[0x2400],
            ["vn-ctrlram-input"] = new byte[0x1660],
            ["nf-ctrlram-input"] = new byte[0x2DD0],
        };
    }

    private static byte[] CreateReferenceImage(int capacity)
    {
        byte[] referenceBase = new byte[capacity];
        WriteNvtMarker(referenceBase, NvtMarkerStart);
        return referenceBase;
    }

    private static void WriteNvtMarker(byte[] target, int start)
    {
        NvtMarker.CopyTo(target.AsSpan(start));
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
