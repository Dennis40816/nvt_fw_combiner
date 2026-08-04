using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Application/Bootstrap runtime evidence for the function-open AB profiles.</summary>
public sealed partial class AbMergeRuntimeAdmissionTests
{
    private const int DpLength = 0x80000;
    private const int TpLength = 0x40000;

    /// <summary>Function-open AB profiles are exposed even while 950/951 certification evidence remains pending.</summary>
    [Fact]
    public void RuntimeCatalogContainsOnlyTheApprovedPilot()
    {
        Assert.Equal(
            ["NT51919", "NT51929", "NT51932", "NT51950", "NT51951"],
            WorkbenchCompositionService.GetAbMergeProfileSummaries().Select(static profile => profile.IcId));
        Assert.All(
            WorkbenchCompositionService.GetAbMergeProfileSummaries(),
            static profile => Assert.True(profile.CompileSucceeded, string.Join(',', profile.IssueCodes)));
        Assert.True(AbMergeWorkbenchCompositionService.IsAbMergeSupported("51929"));
        Assert.True(AbMergeWorkbenchCompositionService.IsAbMergeSupported("NT51950"));
        Assert.True(AbMergeWorkbenchCompositionService.IsAbMergeSupported("NT51951"));
    }

    /// <summary>The desktop adapter exposes compiler-owned exact-container and source-view authority.</summary>
    [Theory]
    [InlineData("NT51919", DpLength, TpLength)]
    [InlineData("NT51929", DpLength, TpLength)]
    [InlineData("NT51932", DpLength, TpLength)]
    [InlineData("NT51950", 0x80000, 0x37000)]
    [InlineData("NT51951", 0x100000, 0x37000)]
    public void WorkbenchInputLayoutComesFromTheCompiledProfile(
        string icId,
        int expectedDpLength,
        int expectedTpPrefixLength)
    {
        IReadOnlyList<WorkbenchAbMergeInputSlot> slots =
            WorkbenchCompositionService.GetAbMergeInputSlots(
                icId,
                abMergeTopologyToken: icId == "NT51950" ? "single" : null);
        Assert.Collection(
            slots,
            slot => AssertAbSlot(
                slot,
                CompositionAddressSpaceIds.DpAbInput,
                WorkbenchAbMergeInputRole.DpAb,
                expectedDpLength,
                [expectedDpLength]),
            slot => AssertAbSlot(
                slot,
                CompositionAddressSpaceIds.TpAInput,
                WorkbenchAbMergeInputRole.TpA,
                expectedTpPrefixLength,
                []),
            slot => AssertAbSlot(
                slot,
                CompositionAddressSpaceIds.TpBInput,
                WorkbenchAbMergeInputRole.TpB,
                expectedTpPrefixLength,
                []));
    }

    /// <summary>Only NT51950's AB profile exposes symbolic map topology selection.</summary>
    [Fact]
    public void TopologyChoicesAreProfileMapOwned()
    {
        Assert.Equal(
            ["single", "cascade"],
            AbMergeWorkbenchCompositionService.GetTopologyChoices("NT51950")
                .Select(static choice => choice.Token));
        Assert.Empty(AbMergeWorkbenchCompositionService.GetTopologyChoices("NT51951"));
        Assert.Empty(AbMergeWorkbenchCompositionService.GetTopologyChoices("NT51929"));
    }

    /// <summary>NT51950 rejects a selected topology that disagrees with both canonical TP FWConfig Backups before postbuild.</summary>
    [Fact]
    public async Task Nt51950BlocksTopologySelectionThatDisagreesWithTpMetadataAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-topology-selection");
        Dictionary<string, string> paths = WriteNt51950Inputs(workspace, tpAChipCount: 2, tpBChipCount: 2);

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51950",
            paths,
            build: false,
            TestContext.Current.CancellationToken,
            abMergeTopologySelection: RequestedTopology("single"));

        Assert.False(result.Succeeded);
        Assert.Contains("AB_TP_TOPOLOGY_SELECTION_MISMATCH", result.ReportJson, StringComparison.Ordinal);
    }

    /// <summary>NT51950 rejects TPA and TPB that declare different canonical FWConfig Backup topologies before postbuild.</summary>
    [Fact]
    public async Task Nt51950BlocksMismatchedTpTopologiesAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-topology-pair");
        Dictionary<string, string> paths = WriteNt51950Inputs(workspace, tpAChipCount: 1, tpBChipCount: 2);

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51950",
            paths,
            build: false,
            TestContext.Current.CancellationToken,
            abMergeTopologySelection: RequestedTopology("single"));

        Assert.False(result.Succeeded);
        Assert.Contains("AB_TP_TOPOLOGY_MISMATCH", result.ReportJson, StringComparison.Ordinal);
    }

    /// <summary>NT51950 rejects a TP source whose accepted prefix does not contain a valid canonical FWConfig Backup.</summary>
    [Fact]
    public async Task Nt51950BlocksMissingCanonicalTpFirmwareConfigBackupAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-topology-metadata");
        Dictionary<string, string> paths = WriteNt51950Inputs(workspace, tpAChipCount: 1, tpBChipCount: 1);
        paths[CompositionAddressSpaceIds.TpBInput] = workspace.Write("inputs/tp-b-invalid.bin", new byte[0x37000]);

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51950",
            paths,
            build: false,
            TestContext.Current.CancellationToken,
            abMergeTopologySelection: RequestedTopology("single"));

        Assert.False(result.Succeeded);
        Assert.Contains("AB_TP_FIRMWARE_CONFIG_BACKUP_INVALID", result.ReportJson, StringComparison.Ordinal);
    }

    /// <summary>Each selected source that ends one byte early blocks under its canonical input geometry.</summary>
    [Theory]
    [InlineData(CompositionAddressSpaceIds.DpAbInput, DpLength - 1, CompositionIssueCodes.InputAddressSpaceLengthMismatch)]
    [InlineData(CompositionAddressSpaceIds.TpAInput, TpLength - 1, CompositionIssueCodes.InputSourceViewIncomplete)]
    [InlineData(CompositionAddressSpaceIds.TpBInput, TpLength - 1, CompositionIssueCodes.InputSourceViewIncomplete)]
    public async Task OneByteShortInputBlocksWithoutOutputAsync(
        string shortAddressSpaceId,
        int shortLength,
        string expectedIssueCode)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-short");
        Dictionary<string, string> paths = WriteInputs(workspace);
        byte[] shortBytes = CreatePattern(shortLength, 0xA1);
        paths[shortAddressSpaceId] = workspace.Write($"short/{shortAddressSpaceId}.bin", shortBytes);
        string outputPath = workspace.PathFor($"output/{shortAddressSpaceId}.bin");

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.OutputSize);
        Assert.False(File.Exists(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == expectedIssueCode);
        Assert.Equal(shortAddressSpaceId, issue.GetProperty("OperationId").GetString());
        Assert.Equal("error", issue.GetProperty("Severity").GetString());
        Assert.Equal(shortBytes, await File.ReadAllBytesAsync(
            paths[shortAddressSpaceId],
            TestContext.Current.CancellationToken));
    }

    /// <summary>Each accepted TP source view preserves full identity and ignores its non-authoritative tail.</summary>
    [Theory]
    [InlineData(CompositionAddressSpaceIds.TpAInput)]
    [InlineData(CompositionAddressSpaceIds.TpBInput)]
    public async Task TpSourceViewIgnoresTrailingBytesWithoutChangingOutputAsync(
        string oversizedAddressSpaceId)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-oversize");
        Dictionary<string, string> paths = WriteInputs(workspace);
        WorkbenchRunResult exact = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: false,
            TestContext.Current.CancellationToken);
        Assert.True(exact.Succeeded, exact.ReportJson);

        byte[] original = await File.ReadAllBytesAsync(
            paths[oversizedAddressSpaceId],
            TestContext.Current.CancellationToken);
        byte[] oversized = [.. original, 0xD7];
        paths[oversizedAddressSpaceId] = workspace.Write($"oversized/{oversizedAddressSpaceId}.bin", oversized);
        WorkbenchRunResult tailed = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(tailed.Succeeded, tailed.ReportJson);
        Assert.Equal(exact.OutputSha256, tailed.OutputSha256);
        using var report = JsonDocument.Parse(tailed.ReportJson);
        Assert.Empty(report.RootElement.GetProperty("Issues").EnumerateArray());
        JsonElement input = Assert.Single(
            report.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => input.GetProperty("AddressSpaceId").GetString() == oversizedAddressSpaceId);
        Assert.Equal(oversized.LongLength, input.GetProperty("Size").GetInt64());
        Assert.Equal(Sha256(oversized), input.GetProperty("Sha256").GetString());
        JsonElement snapshot = input.GetProperty("ExecutionSnapshot");
        Assert.Equal(original.LongLength, snapshot.GetProperty("AcceptedSize").GetInt64());
        Assert.Equal(Sha256(original), snapshot.GetProperty("AcceptedSha256").GetString());
        Assert.Equal(0, snapshot.GetProperty("AcceptedRange").GetProperty("Start").GetInt64());
        Assert.Equal(original.LongLength, snapshot.GetProperty("AcceptedRange").GetProperty("EndExclusive").GetInt64());
        Assert.Equal(1, snapshot.GetProperty("IgnoredTrailingBytes").GetInt64());
        Assert.Equal(original.LongLength, snapshot.GetProperty("IgnoredTrailingRange").GetProperty("Start").GetInt64());
        Assert.Equal(oversized.LongLength, snapshot.GetProperty("IgnoredTrailingRange").GetProperty("EndExclusive").GetInt64());
        Assert.Equal(oversized, await File.ReadAllBytesAsync(
            paths[oversizedAddressSpaceId],
            TestContext.Current.CancellationToken));
    }

    /// <summary>Build commits the exact supported output while preserving every selected source.</summary>
    [Fact]
    public async Task ExactBuildCommitsOutputAndPreservesSourcesAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-build");
        Dictionary<string, string> paths = WriteInputs(workspace);
        var originals = paths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        string outputPath = workspace.PathFor("output/nt51929-ab.bin");
        string aFlashCodePath = workspace.PathFor("output/nt51929-a-flashcode.bin");

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: true,
            cancellationToken: TestContext.Current.CancellationToken,
            outputPath: outputPath,
            aFlashCodeOutputPath: aFlashCodePath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(result.IsDeliveryComplete, result.ReportJson);
        Assert.Equal(DpLength, result.OutputSize);
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(result.OutputSha256, Sha256(output));

        WorkbenchDeliveryArtifact aFlashCodeDelivery = Assert.Single(result.DeliveryArtifacts);
        Assert.Equal(AbMergeAFlashCodeExportService.AFlashCodeDeliveryKind, aFlashCodeDelivery.DeliveryKind);
        Assert.Equal(aFlashCodePath, aFlashCodeDelivery.OutputPath);
        Assert.Equal(0, aFlashCodeDelivery.SourceRange.Start);
        Assert.Equal(DpLength / 2, aFlashCodeDelivery.SourceRange.EndExclusive);
        OutputNamingSummary outputNaming = Assert.IsType<OutputNamingSummary>(result.OutputNaming);
        Assert.True(outputNaming.IsExplicitOverride);
        var namingTokens = outputNaming.Tokens.ToDictionary(
            static token => token.TokenId,
            static token => token.Value,
            StringComparer.Ordinal);
        Assert.Equal("T8100", namingTokens["tp-a"]);
        Assert.Equal(Path.GetFileName(outputPath), outputNaming.ActualFileName);
        Assert.NotEqual(outputNaming.ActualFileName, outputNaming.AutomaticFileName);
        Assert.Matches(
            "^NT51929_FlashCode_A_D[0-9A-F]{4}T8100_B_D[0-9A-F]{4}T8203_[0-9]{8}\\.bin$",
            outputNaming.AutomaticFileName);

        byte[] aFlashCode = await File.ReadAllBytesAsync(aFlashCodePath, TestContext.Current.CancellationToken);
        Assert.Equal(DpLength / 2, aFlashCodeDelivery.OutputSize);
        Assert.Equal(output[..(DpLength / 2)], aFlashCode);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal(Path.GetFileName(outputPath), report.RootElement.GetProperty("Output").GetProperty("FileName").GetString());
        JsonElement reportDelivery = Assert.Single(report.RootElement.GetProperty("DeliveryArtifacts").EnumerateArray());
        Assert.Equal(Path.GetFileName(aFlashCodePath), reportDelivery.GetProperty("FileName").GetString());
        Assert.True(reportDelivery.GetProperty("Committed").GetBoolean());

        foreach ((string addressSpaceId, string path) in paths)
        {
            Assert.Equal(originals[addressSpaceId], await File.ReadAllBytesAsync(
                path,
                TestContext.Current.CancellationToken));
        }
    }

    /// <summary>A requested A FlashCode target is rejected before the primary AB Build can overwrite any selected input or its own output.</summary>
    [Fact]
    public async Task AFlashCodeAliasBlocksBeforePrimaryBuildAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-a-alias");
        Dictionary<string, string> paths = WriteInputs(workspace);
        string outputPath = workspace.PathFor("output/nt51929-ab.bin");

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            AbMergeWorkbenchCompositionService.RunAbMergeAsync(
                "NT51929",
                paths,
                build: true,
                cancellationToken: TestContext.Current.CancellationToken,
                outputPath: outputPath,
                aFlashCodeOutputPath: paths[CompositionAddressSpaceIds.DpAbInput]).AsTask());

        Assert.Contains("AB input artifact", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>
    /// The rendered automatic AB filename is checked after accepted-input naming but before composition,
    /// so a dynamic name can never stage or overwrite an input that differs from the static template.
    /// </summary>
    [Fact]
    public async Task RenderedAutomaticOutputNameCannotAliasInputAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-rendered-output-alias");
        Dictionary<string, string> paths = WriteInputs(workspace);
        string automaticFileName = await AbMergeWorkbenchCompositionService
            .ResolveAutomaticOutputFileNameAsync(
                "NT51929",
                paths,
                TestContext.Current.CancellationToken);
        string originalDpPath = paths[CompositionAddressSpaceIds.DpAbInput];
        byte[] originalDpBytes = await File.ReadAllBytesAsync(
            originalDpPath,
            TestContext.Current.CancellationToken);
        string aliasedDpPath = Path.Combine(Path.GetDirectoryName(originalDpPath)!, automaticFileName);
        File.Move(originalDpPath, aliasedDpPath);
        paths[CompositionAddressSpaceIds.DpAbInput] = aliasedDpPath;

        var progress = new CompositionRunProgressFeed();
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            AbMergeWorkbenchCompositionService.RunAbMergeWithProgressAsync(
                "NT51929",
                paths,
                build: true,
                progress: progress,
                cancellationToken: TestContext.Current.CancellationToken,
                abMergeTopologySelection: null).AsTask());

        Assert.Contains("Output path must not overwrite input artifact", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            originalDpBytes,
            await File.ReadAllBytesAsync(aliasedDpPath, TestContext.Current.CancellationToken));
        var phases = new List<CompositionRunPhase>();
        await foreach (CompositionRunProgressSnapshot snapshot in progress.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            phases.Add(snapshot.CurrentPhase);
        }

        Assert.Equal([CompositionRunPhase.Preparing, CompositionRunPhase.ReadingInputs], phases);
    }

    /// <summary>A post-primary I/O failure is reported as partial delivery and never pretends that both requested outputs were delivered.</summary>
    [Fact]
    public async Task AFlashCodeDeliveryFailureRetainsPrimaryAndReportsIncompleteArtifactAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-a-partial");
        Dictionary<string, string> paths = WriteInputs(workspace);
        string outputPath = workspace.PathFor("output/nt51929-ab.bin");
        string directoryPath = workspace.PathFor("output");

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: true,
            cancellationToken: TestContext.Current.CancellationToken,
            outputPath: outputPath,
            aFlashCodeOutputPath: directoryPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.False(result.IsDeliveryComplete);
        Assert.True(File.Exists(outputPath));
        Assert.False(string.IsNullOrWhiteSpace(result.DeliveryFailureMessage));
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement delivery = Assert.Single(report.RootElement.GetProperty("DeliveryArtifacts").EnumerateArray());
        Assert.False(delivery.GetProperty("Committed").GetBoolean());
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == "delivery.ab-a-flashcode.failed");
    }

    /// <summary>Only the contiguous perfect-family A-bank declarations opt into the optional pre-Build A FlashCode delivery.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public async Task PerfectFamilyAbMapsDeclareTheAFlashCodeDeliveryPlanAsync(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        Assert.True(AbMergeWorkbenchCompositionService.TryCompileAbMerge(
            icId,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues),
            string.Join(',', issues.Select(static issue => issue.Code)));
        CompiledComposition compiledComposition = Assert.IsType<CompiledComposition>(composition);
        OutputNamingSummary outputNaming = CreateCompletedAbResult(icId, DpLength).OutputNaming!;
        WorkbenchAbAFlashCodeDeliveryPlan? export = await AbMergeAFlashCodeExportService.TryCreatePlanAsync(
            compiledComposition,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new CompositionOutputNamePreview(outputNaming.ActualFileName, outputNaming, []),
            TestContext.Current.CancellationToken);

        WorkbenchAbAFlashCodeDeliveryPlan plan = Assert.IsType<WorkbenchAbAFlashCodeDeliveryPlan>(export);
        Assert.Equal(0, plan.SourceRange.Start);
        Assert.Equal(DpLength / 2, plan.SourceRange.EndExclusive);
        Assert.Equal($"NT{icId[2..]}_FlashCode_D0605T8100_20260724.bin", plan.SuggestedFileName);
    }

    /// <summary>NT51950's distinct A-bank and DP layout cannot accidentally inherit the 929-family A FlashCode export.</summary>
    [Fact]
    public void Nt51950AbMapDoesNotDeclareTheAFlashCodeDelivery()
    {
        Assert.True(AbMergeWorkbenchCompositionService.TryCompileAbMerge(
            "NT51950",
            RequestedTopology("single"),
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues),
            string.Join(',', issues.Select(static issue => issue.Code)));
        CompiledComposition compiledComposition = Assert.IsType<CompiledComposition>(composition);

        Assert.False(AbMergeAFlashCodeExportService.TryResolveAFlashCodeRange(compiledComposition, out _));
    }

    /// <summary>NT51951's distinct selector-free AB layout likewise remains outside the perfect-family A-only delivery rule.</summary>
    [Fact]
    public void Nt51951AbMapDoesNotDeclareTheAFlashCodeDelivery()
    {
        Assert.True(AbMergeWorkbenchCompositionService.TryCompileAbMerge(
            "NT51951",
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues),
            string.Join(',', issues.Select(static issue => issue.Code)));
        CompiledComposition compiledComposition = Assert.IsType<CompiledComposition>(composition);

        Assert.False(AbMergeAFlashCodeExportService.TryResolveAFlashCodeRange(compiledComposition, out _));
    }

    private static WorkbenchRunResult CreateCompletedAbResult(string icId, int outputLength)
    {
        string icNumber = icId[2..];
        string fileName = $"NT{icNumber}_FlashCode_A_D0605T8100_B_D0708T8203_20260724.bin";
        return new WorkbenchRunResult(
            true,
            "Succeeded",
            $"{icId.ToLowerInvariant()}-ab-merge",
            outputLength,
            "source-output-sha256",
            fileName,
            Path.Combine(Path.GetTempPath(), fileName),
            "{}")
        {
            OutputBytes = new byte[outputLength],
            OutputNaming = new OutputNamingSummary(
                "ab-code-v1",
                "NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin",
                fileName,
                fileName,
                isExplicitOverride: false,
                "utc",
                new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero),
                [
                    new OutputNamingTokenSummary("ic", icNumber, true, null, null, "compiled-profile"),
                    new OutputNamingTokenSummary("dp-a", "D0605", true, "dp-ab-input", null, "profile-cmi-reg16-18"),
                    new OutputNamingTokenSummary("tp-a", "T8100", true, "tp-a-input", null, "fwconfig-backup"),
                    new OutputNamingTokenSummary("dp-b", "D0708", true, "dp-ab-input", null, "profile-cmi-reg16-18"),
                    new OutputNamingTokenSummary("tp-b", "T8203", true, "tp-b-input", null, "fwconfig-backup"),
                    new OutputNamingTokenSummary("date", "20260724", true, null, null, "utc-clock"),
                ]),
        };
    }

    private static Dictionary<string, string> WriteInputs(TempWorkspace workspace)
    {
        byte[] tpA = CreateTpImage(version: 0x81, subVersion: 0x00);
        byte[] tpB = CreateTpImage(version: 0x82, subVersion: 0x03);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0x7164, sizeof(uint)), 0x00123456);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0x7168, sizeof(uint)), 0x00ABCDEF);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0x716C, sizeof(uint)), 0x0000C0DE);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("inputs/dp-ab.bin", CreatePattern(DpLength, 0x31)),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write("inputs/tp-a.bin", tpA),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write("inputs/tp-b.bin", tpB),
        };
    }

    private static Dictionary<string, string> WriteNt51950Inputs(
        TempWorkspace workspace,
        byte tpAChipCount,
        byte tpBChipCount)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("inputs/dp-ab.bin", new byte[0x80000]),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write(
                "inputs/tp-a.bin",
                CreateTpImage(0x81, 0x00, tpAChipCount, 0x37000)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write(
                "inputs/tp-b.bin",
                CreateTpImage(0x82, 0x01, tpBChipCount, 0x37000)),
        };
    }

    private static WorkbenchFirmwareInspection InspectAbInput(
        string icId,
        string addressSpaceId,
        string path,
        string? topologyToken = null)
    {
        WorkbenchFirmwareInspectionResult result = Assert.Single(
            WorkbenchCompositionService.InspectFirmwareBatch(
                icId,
                [new WorkbenchFirmwareInspectionInput(
                    addressSpaceId,
                    path,
                    AbMergeAddressSpaceId: addressSpaceId,
                    AbMergeTopologyToken: topologyToken)]));
        return result.Inspection;
    }

    private static Domain.Firmware.TopologySelection RequestedTopology(string token)
    {
        Assert.True(AbMergeWorkbenchCompositionService.TryCreateTopologySelection(
            token,
            out Domain.Firmware.TopologySelection? topology));
        return Assert.IsType<Domain.Firmware.TopologySelection>(topology);
    }

    private static byte[] CreatePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < length; index++)
        {
            bytes[index] = unchecked((byte)(salt + (index * 37)));
        }

        return bytes;
    }

    private static void AssertAbSlot(
        WorkbenchAbMergeInputSlot slot,
        string addressSpaceId,
        WorkbenchAbMergeInputRole role,
        int requiredLength,
        IReadOnlyList<long> expectedOuterLengths)
    {
        Assert.Equal(addressSpaceId, slot.SlotId);
        Assert.Equal(addressSpaceId, slot.AddressSpaceId);
        Assert.Equal(role, slot.Role);
        Assert.Equal(requiredLength, slot.RequiredEndExclusive);
        Assert.Equal(expectedOuterLengths, slot.ExpectedOuterLengths);
    }

    private static void WriteCmi(
        byte[] image,
        int bankStart,
        byte major,
        byte minor,
        ushort jira)
    {
        const int register16Offset = 0x401A;
        WriteCmiAt(image, checked(bankStart + register16Offset), major, minor, jira);
    }

    private static void WriteCmiAt(
        byte[] image,
        int register16Offset,
        byte major,
        byte minor,
        ushort jira)
    {
        int start = register16Offset;
        image[start] = checked((byte)(jira & 0xFF));
        image[start + 1] = major;
        image[start + 2] = checked((byte)((minor << 4) | ((jira >> 8) & 0x0F)));
    }

    private static byte[] CreateTpImage(
        byte version,
        byte subVersion,
        byte chipCount = 1,
        int length = TpLength)
    {
        const int backupStart = 0x1000;
        const int markerStart = backupStart + 0xFFC;
        byte[] image = new byte[length];
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = version;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~version);
        image[backupStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = subVersion;
        image[backupStart + FirmwareConfigLayout.ChipNumberOffset] = chipCount;
        image[markerStart] = 0x00;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }

    private static string Sha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
