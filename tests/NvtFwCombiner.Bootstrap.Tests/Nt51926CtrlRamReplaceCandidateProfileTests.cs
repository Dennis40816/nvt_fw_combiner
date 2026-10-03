using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Supported-route evidence for the NT51926 Common FW 1.4.1 cascade CtrlRAM postbuild plan.</summary>
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
        CompiledComposition composition = CompileRegisteredCascade(referenceBase);
        Assert.Equal(6, composition.Plan.OrderedOperations.Count);
        (CompositionOperationKind Kind, string? SourceSpaceId, ByteRange? SourceRange,
            string TargetSpaceId, ByteRange TargetRange, string? ProcessorId, string? ToolBindingId)[] expectedOperations =
        [
            (CompositionOperationKind.ReplaceRange, "normal-ctrlram-input", new ByteRange(0, 0x2C00),
                "output-image", new ByteRange(0x22800, 0x2C00), null, null),
            (CompositionOperationKind.ReplaceRange, "mp-ctrlram-input", new ByteRange(0, 0x2400),
                "output-image", new ByteRange(0x25400, 0x2400), null, null),
            (CompositionOperationKind.ReplaceRange, "diff-ctrlram-input", new ByteRange(0, 0x2800),
                "output-image", new ByteRange(0x27800, 0x2800), null, null),
            (CompositionOperationKind.ReplaceRange, "nf-ctrlram-input", new ByteRange(0, 0x2DD0),
                "output-image", new ByteRange(0x2C800, 0x2DD0), null, null),
            (CompositionOperationKind.ReplaceRange, "vn-ctrlram-input", new ByteRange(0, 0x1660),
                "output-image", new ByteRange(0x315D0, 0x1660), null, null),
            (CompositionOperationKind.RunExternalProcessor, null, null,
                "output-image", new ByteRange(0, 0x3C000),
                "nfc.nt51926.ctrlram-postbuild-fw1.4.1", "legacy-combiner-1.13.0"),
        ];
        Assert.Equal(
            expectedOperations,
            composition.Plan.OrderedOperations.Select(static operation =>
                (operation.Kind, operation.SourceSpaceId, operation.SourceRange,
                    operation.TargetSpaceId, operation.TargetRange,
                    operation.ExternalProcessorInvocation?.ProcessorId,
                    operation.ExternalProcessorInvocation?.ToolBindingId)));
        CompositionOperation processorOperation = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor);
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            processorOperation.ExternalProcessorInvocation);
        LegacyCombinerPostbuildCommandPlan legacyPlan = LegacyCombinerPostbuildCatalog.Nt51926CommonFw141.ResolvePlan(new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal("nt51926-ctrlram-replace-fw141-runtime-cascade", details.ProfileId);
        Assert.Equal("0.4.0", details.ProfileVersion);
        Assert.Equal("nt51926-ctrlram-fw141-tp-work-240k", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.Supported, details.Provenance.Promotion.Stage);
        Assert.Empty(details.Provenance.Promotion.Blockers);
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
            composition.Plan.OrderedOperations
                .Where(static operation => operation.Kind == CompositionOperationKind.ReplaceRange)
                .Select(static operation =>
                    (operation.SourceSpaceId, operation.SourceRange!.Value, operation.TargetRange)));
        Assert.Empty(invocation.StagedSourceBindings);
        ExternalProcessorProtocolPlan protocol = Assert.IsType<ExternalProcessorProtocolPlan>(invocation.ProtocolPlan);
        Assert.Equal(legacyPlan.ProtocolPlan.ProtocolId, protocol.ProtocolId);
        Assert.Equal(legacyPlan.ProtocolPlan.TargetFileName, protocol.TargetFileName);
        Assert.Equal(legacyPlan.ProtocolPlan.Commands.Select(static command => command.CommandId),
            protocol.Commands.Select(static command => command.CommandId));
        Assert.All(protocol.Commands.Zip(legacyPlan.ProtocolPlan.Commands), pair =>
        {
            Assert.Equal(pair.Second.Arguments, pair.First.Arguments);
            Assert.Equal(pair.Second.Blocks.Select(static block =>
                (block.BlockId, block.SourceKind, block.SourceFileName, block.SourceOffset, block.FirmwareRange)),
                pair.First.Blocks.Select(static block =>
                    (block.BlockId, block.SourceKind, block.SourceFileName, block.SourceOffset, block.FirmwareRange)));
        });
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
        CompiledComposition composition = CompileRegisteredCascade(referenceBase);
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
        CompiledComposition composition = CompileRegisteredCascade(referenceBase);
        Assert.Equal(6, composition.Plan.OrderedOperations.Count);
        (CompositionOperationKind Kind, string? SourceSpaceId, ByteRange? SourceRange,
            string TargetSpaceId, ByteRange TargetRange, string? ProcessorId, string? ToolBindingId)[] expectedOperations =
        [
            (CompositionOperationKind.ReplaceRange, "normal-ctrlram-input", new ByteRange(0, 0x2C00),
                "output-image", new ByteRange(0x22800, 0x2C00), null, null),
            (CompositionOperationKind.ReplaceRange, "mp-ctrlram-input", new ByteRange(0, 0x2400),
                "output-image", new ByteRange(0x25400, 0x2400), null, null),
            (CompositionOperationKind.ReplaceRange, "diff-ctrlram-input", new ByteRange(0, 0x2800),
                "output-image", new ByteRange(0x27800, 0x2800), null, null),
            (CompositionOperationKind.ReplaceRange, "nf-ctrlram-input", new ByteRange(0, 0x2DD0),
                "output-image", new ByteRange(0x2C800, 0x2DD0), null, null),
            (CompositionOperationKind.ReplaceRange, "vn-ctrlram-input", new ByteRange(0, 0x1660),
                "output-image", new ByteRange(0x315D0, 0x1660), null, null),
            (CompositionOperationKind.RunExternalProcessor, null, null,
                "output-image", new ByteRange(0, 0x3C000),
                "nfc.nt51926.ctrlram-postbuild-fw1.4.1", "legacy-combiner-1.13.0"),
        ];
        Assert.Equal(
            expectedOperations,
            composition.Plan.OrderedOperations.Select(static operation =>
                (operation.Kind, operation.SourceSpaceId, operation.SourceRange,
                    operation.TargetSpaceId, operation.TargetRange,
                    operation.ExternalProcessorInvocation?.ProcessorId,
                    operation.ExternalProcessorInvocation?.ToolBindingId)));
        CompositionOperation operation = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor);
        bool invoked = false;
        byte[] originalReference = [.. referenceBase];

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
                invoked = true;
                Assert.Equal(Capacity, inputBytes.Length);
                Assert.Equal(referenceBase.AsSpan(0, Capacity).ToArray(), inputBytes.ToArray());
                byte[] transformed = inputBytes.ToArray();
                transformed[0x22800] ^= 0xFF;
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(transformed));
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(FullFlashCapacity, result.OutputBytes.Length);
        Assert.Equal(originalTail, result.OutputBytes.Span[Capacity..].ToArray());
        Assert.Equal((byte)(referenceBase[0x22800] ^ 0xFF), result.OutputBytes.Span[0x22800]);
        Assert.Equal(originalReference, referenceBase);
    }

    /// <summary>The exact TP artifact produces stable, reviewable resolved-map and compilation identities.</summary>
    [Fact]
    public void CandidateFingerprintsAreExactAndRepeatable()
    {
        byte[] referenceBase = CreateReferenceImage();
        CompiledComposition first = CompileRegisteredCascade(referenceBase);
        CompiledComposition second = CompileRegisteredCascade([.. referenceBase]);

        Assert.Equal(
            "9b3c01ce065cbbc97ac78c0d7044d98916ba9d66b49a9e6eeed388390f66b087",
            first.V2Details.Provenance.ResolvedMap.ResolutionFingerprint);
        Assert.Equal(
            "d58278d6b1dd32289532c77462984e6fe85031b21ed0600e6127422fbe265a78",
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
        V2CompositionPlanCompileResult compilation = CompileRegisteredCascadeResult(new byte[referenceLength]);

        Assert.False(compilation.IsCompiled);
        Assert.Null(compilation.CompiledComposition);
        Assert.Contains(
            compilation.Issues,
            static issue => issue.Code == "profile.v2.compile.map-selection-invalid");
    }

    /// <summary>Verifies the supported route still applies only its declared CtrlRAM oversize normalization.</summary>
    [Fact]
    public async Task CandidatePlanTruncatesOnlyCtrlRamInputsBeforeHostStagingAsync()
    {
        CompiledComposition composition = CompileRegisteredCascade(CreateReferenceImage());
        Dictionary<string, byte[]> inputs = CreateInputs();
        byte[] normal = [.. inputs["normal-ctrlram-input"]];
        normal.AsSpan().Fill(0x5A);
        inputs["normal-ctrlram-input"] = [.. normal, 0xCC];
        var originals = inputs.ToDictionary(static pair => pair.Key, static pair => pair.Value[..], StringComparer.Ordinal);
        bool invoked = false;

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(inputs),
            (_, inputBytes, stagedSources, _, _) =>
            {
                invoked = true;
                Assert.Equal(Capacity, inputBytes.Length);
                Assert.Empty(stagedSources);
                Assert.Equal(normal, Slice(inputBytes.ToArray(), NormalCtrlRamRange));
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes));
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(normal.Length + 1, inputs["normal-ctrlram-input"].Length);
        Assert.Contains(result.Issues, static issue => StringComparer.Ordinal.Equals(
            issue.Code,
            CompositionIssueCodes.InputAddressSpaceTruncated));
        CompositionIssue truncation = Assert.Single(result.Issues);
        Assert.Equal("normal-ctrlram-input", truncation.OperationId);
        Assert.Equal(CompositionIssueSeverity.Warning, truncation.Severity);
        Assert.All(originals, pair => Assert.Equal(pair.Value, inputs[pair.Key]));

        // CtrlRAM normalization never grants permission to truncate the immutable Reference.
        var oversizedReferenceInputs = inputs.ToDictionary(
            static pair => pair.Key, static pair => pair.Value[..], StringComparer.Ordinal);
        oversizedReferenceInputs["reference-base"] = [.. inputs["reference-base"], 0xCC];
        var oversizedOriginals = oversizedReferenceInputs.ToDictionary(
            static pair => pair.Key, static pair => pair.Value[..], StringComparer.Ordinal);
        invoked = false;
        CompositionExecutionResult refused = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(oversizedReferenceInputs),
            (_, inputBytes, _, _, _) =>
            {
                invoked = true;
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes));
            },
            CancellationToken.None);
        Assert.False(invoked);
        Assert.Equal(CompositionExecutionStatus.Failed, refused.Status);
        CompositionIssue referenceRejection = Assert.Single(refused.Issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, referenceRejection.Code);
        Assert.True(refused.OutputBytes.IsEmpty);
        Assert.DoesNotContain(refused.Issues, static issue =>
            issue.Code == CompositionIssueCodes.InputAddressSpaceTruncated &&
            issue.OperationId == "reference-base");
        Assert.All(oversizedOriginals, pair => Assert.Equal(pair.Value, oversizedReferenceInputs[pair.Key]));
    }

    /// <summary>Proves the routed V2 profile matches the compiled candidate on approved owner inputs.</summary>
    [Fact]
    public Task RoutedV2MatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync()
    {
        return VerifyRoutedSelfReplacementAsync(fullFlashBase: true);
    }

    private static async Task VerifyRoutedSelfReplacementAsync(bool fullFlashBase)
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
            JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
                "ctrlram-replace",
                "nt51926-fw141-cascade2-auto-prj-597-20260717");
            CanonicalGoldenDifferenceResult differences =
                CanonicalGoldenTestData.AssertAllowedByteDifferences(
                    goldenCase,
                    originalReference,
                    routedOutput);
            Assert.Equal(16, differences.DifferenceCount);
            Assert.All(differences.DifferenceCountByAllowedRange, count => Assert.Equal(4, count));
        }

        Assert.All(
            originalInputs,
            pair => Assert.Equal(pair.Value, replacementInputs[pair.Key]));
    }

    /// <summary>Retains TP-only compiled/routed parity separately from the full-Flash Golden case.</summary>
    [Fact]
    public Task RoutedTpBaseMatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync()
    {
        return VerifyRoutedSelfReplacementAsync(fullFlashBase: false);
    }

    /// <summary>Builds the registered full-Flash route against the owner-approved 16-byte CRC difference bound.</summary>
    [Fact]
    public async Task RegisteredCascadeFullFlashMatchesOwnerApprovedSelfReplacementGoldenAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerIntakeFile baseFile = ReadOwnerIntakeFile(
            "NT51926", "replace", "ctrlram", "1.4.1", "cascade", "expected_output",
            "NT51926TT_FlashCode_CSOT_TOYOTA_D02T06_JIRA0597_20260622.bin");
        byte[] originalReference = [.. baseFile.Bytes];
        Assert.Equal(FullFlashCapacity, originalReference.Length);
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
        var originalInputs = intakeFiles.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Bytes[..],
            StringComparer.Ordinal);
        var slotPaths = intakeFiles.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Path,
            StringComparer.Ordinal);
        slotPaths[CompositionSlotIds.ReplaceBase] = baseFile.Path;

        using var workspace = TempWorkspace.Create("nfc-nt51926-registered-full-flash-golden");
        string outputPath = workspace.PathFor("registered-output.bin");
        CompositionRunResult routed = await CtrlRamReplaceTestSupport.RunAsync(BootstrapTestHost.Canonical,
            "NT51926",
            "cascade",
            ExperienceIds.CtrlRamReplace,
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);
        Assert.True(routed.Succeeded, CompositionRunReportJson.Serialize(routed));
        using (var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(routed)))
        {
            Assert.Equal("nt51926-ctrlram-replace-fw141-runtime-cascade",
                report.RootElement.GetProperty("ProfileId").GetString());
        }

        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(FullFlashCapacity, output.Length);
        Assert.Equal(routed.OutputSha256, Hash(output));
        // SHA-256 and all four output-image CRC byte vectors were captured from both paths in
        // RoutedV2MatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync (temporary probe, 2026-10-03):
        // the registered cascade route and CompileCandidate's independently compiled unregistered
        // nt51926-ctrlram-replace-fw141-cascade definition; complete output bytes matched.
        // The manifest/case.json hash pins the owner artifact, not this self-replacement output.
        // R54 retirement plan step 4 moves those Golden evidence references to this test before
        // deleting the comparator; the fixed expectations and approved range bounds remain here.
        Assert.Equal("a2169f9fc908207dae0820ebff2b88879fa68795a311337a87c0a4e8a30c8222", Hash(output));
        Assert.Equal<byte>([0x3E, 0x6F, 0x40, 0x88], output[0x1C..0x20]);
        Assert.Equal<byte>([0x00, 0xEA, 0x10, 0x60], output[0xFC..0x100]);
        Assert.Equal<byte>([0x51, 0x0F, 0xF4, 0x42], output[0x32F6C..0x32F70]);
        Assert.Equal<byte>([0xD2, 0xB1, 0xB2, 0xF6], output[0x3304C..0x33050]);
        Assert.Equal(originalReference[Capacity..], output[Capacity..]);
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace", "nt51926-fw141-cascade2-auto-prj-597-20260717");
        CanonicalGoldenDifferenceResult differences = CanonicalGoldenTestData.AssertAllowedByteDifferences(
            goldenCase, originalReference, output);
        Assert.Equal(16, differences.DifferenceCount);
        Assert.All(differences.DifferenceCountByAllowedRange, static count => Assert.Equal(4, count));
        Assert.Equal(originalReference, baseFile.Bytes);
        Assert.Equal(originalReference, File.ReadAllBytes(baseFile.Path));
        Assert.All(originalInputs, pair =>
        {
            Assert.Equal(pair.Value, intakeFiles[pair.Key].Bytes);
            Assert.Equal(pair.Value, File.ReadAllBytes(intakeFiles[pair.Key].Path));
        });
    }

    /// <summary>Consolidates selective-VN archived full-output and immutable-source evidence on the registered cascade profile.</summary>
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
        Assert.Equal(Hash(evidence.Expected.Bytes), Hash(result.OutputBytes.Span));
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

        V2CompositionPlanCompileResult compilation = CompileRegisteredCascadeResult(referenceBase);

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
        ExternalProcessorEnvironmentLease processorLease =
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent();
        IExternalProcessor processor = Assert.IsType<IExternalProcessor>(
            processorLease.Processor,
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
                "0.4.0",
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
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        return composition;
    }

    private static CompiledComposition CompileRegisteredCascade(byte[] referenceBase)
    {
        V2CompositionPlanCompileResult compilation = CompileRegisteredCascadeResult(referenceBase);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        return composition;
    }

    private static V2CompositionPlanCompileResult CompileRegisteredCascadeResult(byte[] referenceBase)
    {
        // Explicit test vectors replace the retired declaration's fixed input slots.
        (string SpaceId, ByteRange Range)[] sources =
        [
            ("normal-ctrlram-input", NormalCtrlRamRange),
            ("mp-ctrlram-input", MpCtrlRamRange),
            ("diff-ctrlram-input", DiffCtrlRamRange),
            ("nf-ctrlram-input", NfCtrlRamRange),
            ("vn-ctrlram-input", VnCtrlRamRange),
        ];
        LegacyCombinerPostbuildCommandPlan commandPlan = LegacyCombinerPostbuildCatalog.Nt51926CommonFw141
            .ResolvePlan(new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));
        ByteRange[] stagedRanges = [.. LegacyCombinerPostbuildPlanCompiler.GetStagedFileBlocks(commandPlan)
            .Select(static block => block.FirmwareRange)];
        var request = new V2RuntimeReferenceReplaceCompileRequest(
            [
                new V2ExplicitMappingInputBinding("reference-base", "reference-base", referenceBase.Length),
                .. sources.Select(static source => new V2ExplicitMappingInputBinding(
                    source.SpaceId, "ctrlram-source", source.Range.Length)),
            ],
            sources.Select((source, index) => new ExplicitMapping(
                $"replace-{source.SpaceId}",
                sequence: 100 + index,
                ExplicitMappingOperationKind.ReplaceRange,
                source.SpaceId,
                new ByteRange(0, source.Range.Length),
                CompositionAddressSpaceIds.OutputImage,
                source.Range,
                OverlapPolicy.Reject,
                alignment: 1,
                reason: "Registered cascade CtrlRAM coverage vector.")),
            postbuildWriteRangeSections: LegacyCombinerPostbuildPlanCompiler.GetAllowedWriteRangeSectionsForStagedSources(
                commandPlan, Capacity, stagedRanges, stagedRanges),
            processorProtocolPlan: commandPlan.ProtocolPlan);
        return BuiltInV2BundleRegistry.All["nt51926-ctrlram-replace-candidate"].CompileRuntimeReferenceReplace(
            "nt51926-ctrlram-replace-fw141-runtime-cascade",
            "0.4.0",
            "NT51926",
            ExperienceIds.CtrlRamReplace,
            new TopologySelection(2, "cascade", TopologySelectionSource.Requested, "ic-number"),
            [new FirmwareArtifactPayload("reference-base", referenceBase)],
            request);
    }

    private static byte[] Slice(byte[] source, ByteRange range)
    {
        return source.AsSpan(checked((int)range.Start), checked((int)range.Length)).ToArray();
    }

    private static CompiledComposition CompileCandidate(byte[] referenceBase)
    {
        V2CompositionPlanCompileResult compilation = CompileCandidateResult(referenceBase);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        return composition;
    }

    private static V2CompositionPlanCompileResult CompileCandidateResult(byte[] referenceBase)
    {
        var payload = new FirmwareArtifactPayload(
            CompositionAddressSpaceIds.ReferenceBase,
            referenceBase);
        return BuiltInV2BundleRegistry.All["nt51926-ctrlram-replace-candidate"].Compile(
            "nt51926-ctrlram-replace-fw141-cascade",
            "0.7.0",
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
        JsonElement standardMergeCase = CanonicalGoldenTestData.LoadDirectCase(
            "standard-merge",
            "nt51926-gen-flash");
        JsonElement ctrlRamCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw141-cascade2-auto-prj-597-20260717");
        JsonElement baseArtifact = standardMergeCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.GetProperty("artifactId").GetString(),
                "tp-input"));
        JsonElement expectedArtifact = ctrlRamCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.GetProperty("artifactId").GetString(),
                "selective-vn-regression-output"));
        JsonElement vnArtifact = ctrlRamCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.GetProperty("artifactId").GetString(),
                "postbuild-vn-ctrlram"));
        return new OwnerRegressionCase(
            ReadManifestArtifact(CanonicalGoldenTestData.Root, baseArtifact),
            ReadManifestArtifact(CanonicalGoldenTestData.Root, expectedArtifact),
            ReadManifestArtifact(CanonicalGoldenTestData.Root, vnArtifact));
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
