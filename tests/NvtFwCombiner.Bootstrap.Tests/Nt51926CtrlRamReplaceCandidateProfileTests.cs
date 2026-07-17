using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Compilable, non-routed evidence for the NT51926 Common FW 1.4.1 cascade CtrlRAM postbuild plan.</summary>
public sealed class Nt51926CtrlRamReplaceCandidateProfileTests
{
    private const int Capacity = 0x3C000;
    private const int FirmwareConfigBackupStart = 0x3B000;
    private const int NvtMarkerStart = FirmwareConfigBackupStart + 0xFFC;

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
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            Assert.Single(composition.Plan.OrderedOperations).ExternalProcessorInvocation);
        LegacyCombinerPostbuildCommandPlan legacyPlan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51926CommonFw141,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal("nt51926-ctrlram-fw141-tp-240k", details.Provenance.ResolvedMap.ImageMap.MapId);
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
        Assert.Equal(Capacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal("nfc.nt51926.ctrlram-postbuild-fw1.4.1", invocation.ProcessorId);
        Assert.Equal("legacy-combiner-1.13.0", invocation.ToolBindingId);
        Assert.Equal([new ByteRange(0, Capacity)], invocation.AllowedReadRanges);
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

    /// <summary>Proves the V2 candidate and current Workbench route produce identical bytes from the approved owner inputs.</summary>
    [Fact]
    public async Task CandidateMatchesLegacyWorkbenchBytesForOwnerApprovedSelfReplacementAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerIntakeFile baseFile = ReadOwnerIntakeFile(
            "NT51926", "replace", "ctrlram", "1.4.1", "cascade", "NT51926TT_TPFW_T06.bin");
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
        Dictionary<string, OwnerIntakeFile> intakeFiles = inputFileNames.ToDictionary(
            static pair => pair.Key,
            pair => ReadOwnerIntakeFile(
                "NT51926", "replace", "ctrlram", "1.4.1", "cascade", "postbuild_inputs", pair.Value),
            StringComparer.Ordinal);
        Dictionary<string, byte[]> replacementInputs = intakeFiles.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Bytes,
            StringComparer.Ordinal);
        var originalInputs = replacementInputs.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value[..],
            StringComparer.Ordinal);
        var legacySlotPaths = intakeFiles.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Path,
            StringComparer.Ordinal);
        legacySlotPaths[WorkbenchSlotIds.ReplaceBase] = baseFile.Path;

        using var workspace = TempWorkspace.Create("nfc-nt51926-ctrlram-v2-parity");
        string legacyOutputPath = workspace.PathFor("legacy-workbench-output.bin");
        WorkbenchRunResult legacy = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            "cascade",
            WorkbenchReplaceModes.CtrlRam,
            legacySlotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            legacyOutputPath);
        Assert.True(legacy.Succeeded, legacy.ReportJson);
        byte[] legacyOutput = File.ReadAllBytes(legacyOutputPath);

        CompiledComposition candidate = CompileCandidate(referenceBase);
        Dictionary<string, byte[]> candidateInputs = new(StringComparer.Ordinal)
        {
            ["reference-base"] = referenceBase,
            ["normal-ctrlram-input"] = replacementInputs["replace-ctrlram-normal"],
            ["diff-ctrlram-input"] = replacementInputs["replace-ctrlram-diff"],
            ["mp-ctrlram-input"] = replacementInputs["replace-ctrlram-mp"],
            ["vn-ctrlram-input"] = replacementInputs["replace-ctrlram-vn"],
            ["nf-ctrlram-input"] = replacementInputs["replace-ctrlram-nf"],
        };
        IExternalProcessor processor = Assert.IsType<IExternalProcessor>(
            ExternalProcessorFactory.CreateOrNull(),
            exactMatch: false);
        var selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]);

        CompositionExecutionResult v2 = await CompositionEngine.ExecuteAsync(
            candidate.Plan,
            new CompositionExecutionInput(candidateInputs),
            async (operation, inputBytes, stagedSources, stagedArtifacts, cancellationToken) =>
            {
                ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
                    operation.ExternalProcessorInvocation);
                ExternalProcessorResult result = await processor.TransformAsync(
                    new ExternalProcessorRequest(
                        "nt51926-ctrlram-v2-parity",
                        invocation.ProcessorId,
                        invocation.ToolBindingId,
                        inputBytes,
                        invocation.AllowedWriteRanges,
                        selection,
                        stagedSources,
                        stagedArtifacts),
                    cancellationToken);
                return result.Succeeded
                    ? CompositionExternalProcessorResult.Success(result.OutputBytes)
                    : CompositionExternalProcessorResult.Failed(result.Issues);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(CompositionExecutionStatus.Succeeded, v2.Status);
        Assert.Equal(2, v2.Issues.Count);
        Assert.All(v2.Issues, static issue => Assert.Equal(
            CompositionIssueCodes.InputAddressSpaceTruncated,
            issue.Code));
        Assert.Equal(legacyOutput, v2.OutputBytes.ToArray());
        Assert.Equal(legacy.OutputSha256, Hash(v2.OutputBytes.Span));
        Assert.Equal(originalReference, referenceBase);
        Assert.All(
            originalInputs,
            pair => Assert.Equal(pair.Value, replacementInputs[pair.Key]));
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
            "0.3.0",
            "NT51926",
            ExperienceIds.CtrlRamReplace,
            Capacity,
            [payload]);
    }

    private static Dictionary<string, byte[]> CreateInputs()
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["reference-base"] = CreateReferenceImage(),
            ["normal-ctrlram-input"] = new byte[0x2C00],
            ["diff-ctrlram-input"] = new byte[0x2800],
            ["mp-ctrlram-input"] = new byte[0x2400],
            ["vn-ctrlram-input"] = new byte[0x1660],
            ["nf-ctrlram-input"] = new byte[0x2DD0],
        };
    }

    private static byte[] CreateReferenceImage()
    {
        byte[] referenceBase = new byte[Capacity];
        WriteNvtMarker(referenceBase, NvtMarkerStart);
        return referenceBase;
    }

    private static void WriteNvtMarker(byte[] target, int start)
    {
        NvtMarker.CopyTo(target.AsSpan(start));
    }

    private static OwnerIntakeFile ReadOwnerIntakeFile(params string[] parts)
    {
        string goldenRoot = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace");
        string relativePath = Path.Combine("fixtures", "20260717", Path.Combine(parts)).Replace('\\', '/');
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            goldenRoot,
            "manifest.20260717.json")));
        JsonElement entry = manifest.RootElement.GetProperty("payloads")
            .EnumerateArray()
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.GetProperty("path").GetString(),
                relativePath));
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

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
