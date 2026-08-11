using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Executable-candidate evidence for the routed NT51926 Common FW 1.4.1 cascade CtrlRAM postbuild plan.</summary>
public sealed class Nt51926CtrlRamReplaceCandidateProfileTests
{
    private const int Capacity = 0x3C000;
    private const int FullFlashCapacity = 0x40000;
    private const int FirmwareConfigBackupStart = 0x3B000;
    private const int NvtMarkerStart = FirmwareConfigBackupStart + 0xFFC;
    private static readonly ByteRange NormalCtrlRamRange = new(0x22800, 0x2C00);
    private static readonly ByteRange MpCtrlRamRange = new(0x25400, 0x2400);
    private static readonly ByteRange DiffCtrlRamRange = new(0x27800, 0x2800);
    private static readonly ByteRange NfCtrlRamRange = new(0x2C800, 0x2DD0);
    private static readonly ByteRange VnCtrlRamRange = new(0x315D0, 0x1660);

    private static ReadOnlySpan<byte> NvtMarker => [0x00, 0x4E, 0x56, 0x54];

    /// <summary>Locks V2 staging and write authority to the legacy Common FW 1.4.1 cascade command plan.</summary>
    [Fact]
    public void CandidateProfileCompilesTheLegacyCascadeStagingAndWriteAuthority()
    {
        byte[] referenceBase = ReadOwnerIntakeFile(
            "NT51926",
            "replace",
            "ctrlram",
            "1.4.1",
            "cascade",
            "NT51926TT_TPFW_T06.bin").Bytes;
        Assert.Equal(Capacity, referenceBase.Length);
        CompiledComposition composition = CompileCandidate(referenceBase);
        CompositionOperation processorOperation = Assert.Single(composition.Plan.OrderedOperations);
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            processorOperation.ExternalProcessorInvocation);
        LegacyCombinerPostbuildCommandPlan legacyPlan = LegacyCombinerPostbuildCatalog.Nt51926CommonFw141.ResolvePlan(new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal("nt51926-ctrlram-fw141-tp-work-240k", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
        Assert.Equal(
            ["firmware-owner-review", "runtime-route"],
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
        Assert.Equal(Capacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(new ByteRange(0, Capacity), processorOperation.TargetRange);
        Assert.Equal("nfc.nt51926.ctrlram-postbuild-fw1.4.1", invocation.ProcessorId);
        Assert.Equal("legacy-combiner-1.13.0", invocation.ToolBindingId);
        Assert.Equal([new ByteRange(0, Capacity)], invocation.AllowedReadRanges);
        Assert.Equal(
            [
                new ByteRange(0x18, 4),
                new ByteRange(0x1C, 4),
                new ByteRange(0x3C, 4),
                new ByteRange(0x4C, 4),
                new ByteRange(0x5C, 4),
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
        Assert.Equal(0x3B800, invocation.AllowedWriteRanges.Max(static range => range.EndExclusive));
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

    /// <summary>The shared executor passes only the exact TP work image to the processor and returns that clone.</summary>
    [Fact]
    public async Task CandidateProcessorReceivesAndReturnsOnlyTheExactTpWorkImageAsync()
    {
        byte[] referenceBase = CreateReferenceImage();
        CompiledComposition composition = CompileCandidate(referenceBase);
        bool invoked = false;

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(CreateInputs(referenceBase)),
            (_, inputBytes, _, _, _) =>
            {
                invoked = true;
                Assert.Equal(Capacity, inputBytes.Length);
                Assert.Equal(referenceBase, inputBytes.ToArray());
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes));
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(Capacity, result.OutputBytes.Length);
        Assert.Equal(referenceBase, result.OutputBytes.ToArray());
    }

    /// <summary>The full-Flash form stages the same TP prefix and preserves every byte outside that prefix.</summary>
    [Fact]
    public async Task CandidateFullFlashStagesTpPrefixAndPreservesContainerTailAsync()
    {
        byte[] referenceBase = CreateReferenceImage(FullFlashCapacity);
        referenceBase.AsSpan(Capacity).Fill(0xA5);
        byte[] originalTail = referenceBase[Capacity..];
        CompiledComposition composition = CompileCandidate(referenceBase);
        CompositionOperation operation = Assert.Single(composition.Plan.OrderedOperations);

        Assert.Equal(FullFlashCapacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(new ByteRange(0, Capacity), operation.TargetRange);
        Assert.Equal(
            "nt51926-ctrlram-fw141-full-flash-256k",
            composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(CreateInputs(referenceBase)),
            (_, inputBytes, _, _, _) =>
            {
                Assert.Equal(Capacity, inputBytes.Length);
                Assert.Equal(referenceBase.AsSpan(0, Capacity).ToArray(), inputBytes.ToArray());
                byte[] transformed = inputBytes.ToArray();
                transformed[0x22800] ^= 0xFF;
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(transformed));
            },
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(FullFlashCapacity, result.OutputBytes.Length);
        Assert.Equal(originalTail, result.OutputBytes.Span[Capacity..].ToArray());
        Assert.Equal((byte)(referenceBase[0x22800] ^ 0xFF), result.OutputBytes.Span[0x22800]);
    }

    /// <summary>The exact TP artifact produces stable, reviewable resolved-map and compilation identities.</summary>
    [Fact]
    public void CandidateFingerprintsAreExactAndRepeatable()
    {
        byte[] referenceBase = CreateReferenceImage();
        CompiledComposition first = CompileCandidate(referenceBase);
        CompiledComposition second = CompileCandidate([.. referenceBase]);

        Assert.Equal(
            "f11e8bc970bfebcb803082c9f048b235fd990fba440f5900cbe81e100b3c9cd3",
            first.V2Details.Provenance.ResolvedMap.ResolutionFingerprint);
        Assert.Equal(
            "d4ca898a8324723a104c690bd64a1db3edf61a217bb6a0a092d600f356a1da27",
            first.CompilationFingerprint);
        Assert.Equal(
            first.V2Details.Provenance.ResolvedMap.ResolutionFingerprint,
            second.V2Details.Provenance.ResolvedMap.ResolutionFingerprint);
        Assert.Equal(first.CompilationFingerprint, second.CompilationFingerprint);
    }

    /// <summary>The candidate accepts only the declared TP and full-Flash container shapes.</summary>
    [Theory]
    [InlineData(Capacity - 1)]
    [InlineData(Capacity + 1)]
    [InlineData(FullFlashCapacity - 1)]
    [InlineData(FullFlashCapacity + 1)]
    public void CandidateRejectsEveryUndeclaredReferenceLength(int referenceLength)
    {
        V2CompositionPlanCompileResult compilation = CompileCandidateResult(new byte[referenceLength]);

        Assert.False(compilation.IsCompiled);
        Assert.Null(compilation.CompiledComposition);
        Assert.Contains(
            compilation.Issues,
            static issue => issue.Code == "profile.v2.compile.map-capacity-unavailable");
    }

    /// <summary>Verifies CtrlRAM-only oversize normalization is declared while the candidate remains outside runtime admission.</summary>
    [Fact]
    public async Task CandidatePlanTruncatesOnlyCtrlRamInputsBeforeHostStagingAsync()
    {
        CompiledComposition composition = CompileCandidate(CreateReferenceImage());
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
                Assert.Equal(Capacity, inputBytes.Length);
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

    /// <summary>Proves the routed V2 profile matches the compiled candidate on approved owner inputs.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoutedV2MatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync(bool fullFlashBase)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerIntakeFile baseFile = fullFlashBase
            ? ReadOwnerIntakeFile(
                "NT51926",
                "replace",
                "ctrlram",
                "1.4.1",
                "cascade",
                "expected_output",
                "NT51926TT_FlashCode_CSOT_TOYOTA_D02T06_JIRA0597_20260622.bin")
            : ReadOwnerIntakeFile(
                "NT51926",
                "replace",
                "ctrlram",
                "1.4.1",
                "cascade",
                "NT51926TT_TPFW_T06.bin");
        byte[] referenceBase = baseFile.Bytes;
        byte[] originalReference = [.. referenceBase];
        var inputFileNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["replace-ctrlram-normal"] = "Normal_Ctrlram.bin",
            ["replace-ctrlram-diff"] = "DiffDLM.bin",
            ["replace-ctrlram-mp"] = "MP_Ctrlram.bin",
            ["replace-ctrlram-vn"] = "VN_Ctrlram.bin",
            ["replace-ctrlram-nf"] = "NF_Ctrlram.bin",
        };
        var intakeFiles = inputFileNames.ToDictionary(
            static pair => pair.Key,
            pair => ReadOwnerIntakeFile(
                "NT51926", "replace", "ctrlram", "1.4.1", "cascade", "postbuild_inputs", pair.Value),
            StringComparer.Ordinal);
        var replacementInputs = intakeFiles.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Bytes,
            StringComparer.Ordinal);
        var originalInputs = replacementInputs.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value[..],
            StringComparer.Ordinal);
        var slotPaths = intakeFiles.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Path,
            StringComparer.Ordinal);
        slotPaths[CompositionSlotIds.ReplaceBase] = baseFile.Path;

        using var workspace = TempWorkspace.Create("nfc-nt51926-ctrlram-v2-parity");
        string routedOutputPath = workspace.PathFor("routed-v2-output.bin");
        CompositionRunResult routed = await CtrlRamReplaceTestSupport.RunAsync(BootstrapTestHost.Canonical,
            "NT51926",
            "cascade",
            ExperienceIds.CtrlRamReplace,
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            routedOutputPath);
        Assert.True(routed.Succeeded, CompositionRunReportJson.Serialize(routed));
        using (var routedReport = JsonDocument.Parse(CompositionRunReportJson.Serialize(routed)))
        {
            Assert.Equal(
                "nt51926-ctrlram-replace-fw141-runtime-cascade",
                routedReport.RootElement.GetProperty("ProfileId").GetString());
        }

        byte[] routedOutput = File.ReadAllBytes(routedOutputPath);

        Dictionary<string, byte[]> candidateInputs = new(StringComparer.Ordinal)
        {
            ["reference-base"] = referenceBase,
            ["normal-ctrlram-input"] = replacementInputs["replace-ctrlram-normal"],
            ["diff-ctrlram-input"] = replacementInputs["replace-ctrlram-diff"],
            ["mp-ctrlram-input"] = replacementInputs["replace-ctrlram-mp"],
            ["vn-ctrlram-input"] = replacementInputs["replace-ctrlram-vn"],
            ["nf-ctrlram-input"] = replacementInputs["replace-ctrlram-nf"],
        };
        CompositionExecutionResult v2 = await ExecuteCandidateWithLegacyCombinerAsync(
            referenceBase,
            candidateInputs,
            "nt51926-ctrlram-v2-parity");

        Assert.True(
            v2.Status == CompositionExecutionStatus.Succeeded,
            FormatIssues(v2.Issues));
        Assert.Equal(2, v2.Issues.Count);
        Assert.All(v2.Issues, static issue => Assert.Equal(
            CompositionIssueCodes.InputAddressSpaceTruncated,
            issue.Code));
        Assert.Equal(routedOutput, v2.OutputBytes.ToArray());
        Assert.Equal(routed.OutputSha256, Hash(v2.OutputBytes.Span));
        Assert.Equal(originalReference, referenceBase);
        if (fullFlashBase)
        {
            Assert.Equal(originalReference[Capacity..], v2.OutputBytes.Span[Capacity..].ToArray());
        }

        Assert.All(
            originalInputs,
            pair => Assert.Equal(pair.Value, replacementInputs[pair.Key]));
    }

    /// <summary>Locks the V2 candidate to the archived Legacy Combiner 1.13 TP-base output for one selected VN replacement.</summary>
    [Fact]
    public async Task CandidateMatchesArchivedTpBaseLegacyCombinerGoldenForSelectiveVnReplacementAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerRegressionCase evidence = ReadOwnerRegressionCase();
        byte[] referenceBase = evidence.Base.Bytes;
        byte[] originalReference = [.. referenceBase];
        Dictionary<string, byte[]> candidateInputs = CreateBaseDerivedCandidateInputs(referenceBase);
        candidateInputs["vn-ctrlram-input"] = evidence.Vn.Bytes;
        byte[] originalVn = [.. evidence.Vn.Bytes];

        CompositionExecutionResult v2 = await ExecuteCandidateWithLegacyCombinerAsync(
            referenceBase,
            candidateInputs,
            "nt51926-ctrlram-v2-owner-golden");

        Assert.True(
            v2.Status == CompositionExecutionStatus.Succeeded,
            FormatIssues(v2.Issues));
        Assert.Empty(v2.Issues);
        Assert.Equal(evidence.Expected.Bytes, v2.OutputBytes.ToArray());
        Assert.Equal(originalReference, referenceBase);
        Assert.Equal(originalVn, evidence.Vn.Bytes);
    }

    /// <summary>Locks the runtime-reference candidate to the same archived Legacy Combiner 1.13 TP-base output.</summary>
    [Fact]
    public async Task RuntimeReferenceCandidateMatchesArchivedTpBaseLegacyCombinerGoldenAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerRegressionCase evidence = ReadOwnerRegressionCase();
        byte[] referenceBase = evidence.Base.Bytes;
        byte[] originalReference = [.. referenceBase];
        byte[] originalVn = [.. evidence.Vn.Bytes];
        CompiledComposition candidate = CompileRuntimeCandidate(referenceBase, evidence.Vn.Bytes.Length);
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["reference-base"] = referenceBase,
            ["vn-source"] = evidence.Vn.Bytes,
        };

        CompositionExecutionResult result = await ExecuteCompiledCandidateWithLegacyCombinerAsync(
            candidate,
            inputs,
            "nt51926-ctrlram-runtime-v2-owner-golden");

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(evidence.Expected.Bytes, result.OutputBytes.ToArray());
        Assert.Equal(originalReference, referenceBase);
        Assert.Equal(originalVn, evidence.Vn.Bytes);
    }

    /// <summary>Verifies zero or multiple universal markers reject the candidate before a plan can be minted.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void CandidateCompilationRejectsMissingOrAmbiguousNvtBackupMarker(int markerCount)
    {
        byte[] referenceBase = new byte[Capacity];
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

    private static async Task<CompositionExecutionResult> ExecuteCandidateWithLegacyCombinerAsync(
        byte[] referenceBase,
        Dictionary<string, byte[]> candidateInputs,
        string runId)
    {
        CompiledComposition candidate = CompileCandidate(referenceBase);
        return await ExecuteCompiledCandidateWithLegacyCombinerAsync(candidate, candidateInputs, runId);
    }

    private static async Task<CompositionExecutionResult> ExecuteCompiledCandidateWithLegacyCombinerAsync(
        CompiledComposition candidate,
        Dictionary<string, byte[]> candidateInputs,
        string runId)
    {
        IExternalProcessor processor = Assert.IsType<IExternalProcessor>(
            ExternalProcessorFactory.GetOrCreateOrNull(),
            exactMatch: false);
        var selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]);
        ExternalProcessorProtocolPlan protocolPlan =
            LegacyCombinerPostbuildCatalog.Nt51926CommonFw141.ResolvePlan(selection).ProtocolPlan;
        return await CompositionEngine.ExecuteAsync(
            candidate.Plan,
            new CompositionExecutionInput(candidateInputs),
            async (operation, inputBytes, stagedSources, stagedArtifacts, cancellationToken) =>
            {
                ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
                    operation.ExternalProcessorInvocation);
                ExternalProcessorResult result = await processor.TransformAsync(
                    new ExternalProcessorRequest(
                        runId,
                        invocation.ProcessorId,
                        invocation.ToolBindingId,
                        inputBytes,
                        invocation.AllowedWriteRanges,
                        selection,
                        stagedSources,
                        stagedArtifacts,
                        protocolPlan: protocolPlan),
                    cancellationToken);
                return result.Succeeded
                    ? CompositionExternalProcessorResult.Success(result.OutputBytes)
                    : CompositionExternalProcessorResult.Failed(result.Issues);
            },
            TestContext.Current.CancellationToken);
    }

    private static CompiledComposition CompileRuntimeCandidate(byte[] referenceBase, int sourceLength)
    {
        V2CompositionPlanCompileResult compilation = BuiltInV2BundleRegistry.All[
            "nt51926-ctrlram-replace-candidate"].CompileRuntimeReferenceReplace(
                "nt51926-ctrlram-replace-fw141-runtime-cascade",
                "0.3.0",
                "NT51926",
                ExperienceIds.CtrlRamReplace,
                new TopologySelection(
                    2,
                    "cascade",
                    TopologySelectionSource.Requested,
                    "ic-number"),
                [new FirmwareArtifactPayload("reference-base", referenceBase)],
                new V2RuntimeReferenceReplaceCompileRequest(
                    [
                        new V2ExplicitMappingInputBinding(
                            "reference-base",
                            "reference-base",
                            referenceBase.Length),
                        new V2ExplicitMappingInputBinding("vn-source", "ctrlram-source", sourceLength),
                    ],
                    [new ExplicitMapping(
                        "replace-vn",
                        sequence: 100,
                        ExplicitMappingOperationKind.ReplaceRange,
                        "vn-source",
                        new ByteRange(0, sourceLength),
                        CompositionAddressSpaceIds.OutputImage,
                        new ByteRange(VnCtrlRamRange.Start, sourceLength),
                        OverlapPolicy.Reject,
                        alignment: 1,
                        reason: "NT51926 Common FW 1.4.1 runtime CtrlRAM golden mapping.")]));
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static Dictionary<string, byte[]> CreateBaseDerivedCandidateInputs(byte[] referenceBase)
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["reference-base"] = referenceBase,
            ["normal-ctrlram-input"] = Slice(referenceBase, NormalCtrlRamRange),
            ["diff-ctrlram-input"] = Slice(referenceBase, DiffCtrlRamRange),
            ["mp-ctrlram-input"] = Slice(referenceBase, MpCtrlRamRange),
            ["vn-ctrlram-input"] = Slice(referenceBase, VnCtrlRamRange),
            ["nf-ctrlram-input"] = Slice(referenceBase, NfCtrlRamRange),
        };
    }

    private static byte[] Slice(byte[] source, ByteRange range)
    {
        return source.AsSpan(checked((int)range.Start), checked((int)range.Length)).ToArray();
    }

    private static CompiledComposition CompileCandidate(byte[] referenceBase)
    {
        V2CompositionPlanCompileResult compilation = CompileCandidateResult(referenceBase);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static V2CompositionPlanCompileResult CompileCandidateResult(byte[] referenceBase)
    {
        var payload = new FirmwareArtifactPayload(
            CompositionAddressSpaceIds.ReferenceBase,
            referenceBase);
        return BuiltInV2BundleRegistry.All["nt51926-ctrlram-replace-candidate"].Compile(
            "nt51926-ctrlram-replace-fw141-cascade",
            "0.6.0",
            "NT51926",
            ExperienceIds.CtrlRamReplace,
            referenceBase.Length,
            [payload]);
    }

    private static Dictionary<string, byte[]> CreateInputs(byte[]? referenceBase = null)
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["reference-base"] = referenceBase ?? CreateReferenceImage(),
            ["normal-ctrlram-input"] = new byte[0x2C00],
            ["diff-ctrlram-input"] = new byte[0x2800],
            ["mp-ctrlram-input"] = new byte[0x2400],
            ["vn-ctrlram-input"] = new byte[0x1660],
            ["nf-ctrlram-input"] = new byte[0x2DD0],
        };
    }

    private static byte[] CreateReferenceImage(int length = Capacity)
    {
        byte[] referenceBase = new byte[length];
        referenceBase[FirmwareConfigBackupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = 1;
        referenceBase[FirmwareConfigBackupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = 4;
        referenceBase[FirmwareConfigBackupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = 1;
        WriteNvtMarker(referenceBase, NvtMarkerStart);
        return referenceBase;
    }

    private static void WriteNvtMarker(byte[] target, int start)
    {
        NvtMarker.CopyTo(target.AsSpan(start));
    }

    private static OwnerIntakeFile ReadOwnerIntakeFile(params string[] parts)
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw141-cascade2-auto-prj-597-20260717");
        JsonElement entry = goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.GetProperty("originalFileName").GetString(),
                parts[^1]));
        return ReadManifestArtifact(goldenRoot, entry);
    }

    private static OwnerRegressionCase ReadOwnerRegressionCase()
    {
        string goldenRoot = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement evidenceCase = manifest.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.GetProperty("id").GetString(),
                "nt51926-cascade-tp-base-self-regression-20260717"));
        JsonElement vn = evidenceCase.GetProperty("replacementInputs")
            .EnumerateArray()
            .Single(input => StringComparer.Ordinal.Equals(
                input.GetProperty("slotId").GetString(),
                "replace-ctrlram-vn"));
        return new OwnerRegressionCase(
            ReadManifestArtifact(goldenRoot, evidenceCase.GetProperty("base")),
            ReadManifestArtifact(goldenRoot, evidenceCase.GetProperty("expectedOutput")),
            ReadManifestArtifact(goldenRoot, vn.GetProperty("file")));
    }

    private static OwnerIntakeFile ReadManifestArtifact(string goldenRoot, JsonElement entry)
    {
        string path = RepositoryPaths.ManifestPath(goldenRoot, entry);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        return new OwnerIntakeFile(path, bytes);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record OwnerIntakeFile(string Path, byte[] Bytes);

    private sealed record OwnerRegressionCase(
        OwnerIntakeFile Base,
        OwnerIntakeFile Expected,
        OwnerIntakeFile Vn);

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
