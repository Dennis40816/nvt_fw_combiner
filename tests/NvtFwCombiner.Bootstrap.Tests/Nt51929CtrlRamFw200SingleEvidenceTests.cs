using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Exact V2 route evidence from NT51929 Common FW 2.0.0 owner artifacts.</summary>
public sealed partial class Nt51929CtrlRamFw200SingleEvidenceTests
{
    private const string OwnerExpectedSha256 = "d3c958d2aac1e29bd1f88b8ac62dc74c36810ab11e707770199d4b34f5ce3910";
    private const string CurrentOutputSha256 = "d23f53a13db3c6fc0009ed547e8cfa5f1b54033145825828416c7458b14f198f";
    private const string RegisteredCombinerSha256 = "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf";
    private const string CaseId = "nt51929-fw200-single-auto-prj-594-20260717";
    private const int Capacity = 0x40000;
    private const int NfStart = 0x1FC00;
    private const int NfMaximumLength = 0x1F90;
    private const int NormalStart = 0x21B90;
    private const int NormalLength = 0x4A00;
    private const int VnStart = 0x26590;
    private const int VnMaximumLength = 0x1960;
    private const int HeaderCopyStart = 0x27EF0;
    private const int HeaderCopyLength = 0x200;

    /// <summary>Locks the true non-AB single golden, Standard Merge reconstruction, and admission metadata.</summary>
    [Theory]
    [InlineData("NT51929", "nt51929-standard-merge-gen-flash")]
    [InlineData("NT51919", "nt51919-standard-merge-gen-flash-alias")]
    public async Task StandardMergeReconstructsTrueSingleGoldenAndFirmwareContextAsync(
        string icId,
        string expectedProfileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-base");
        string outputPath = workspace.PathFor("standard-merge-base.bin");
        CompositionRunResult result = await StandardMergeTestSupport.RunAsync(BootstrapTestHost.Services,
            icId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = evidence.Dp.Path,
                [CompositionAddressSpaceIds.TpInput] = evidence.Tp.Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal(expectedProfileId, ReadProfileId(CompositionRunReportJson.Serialize(result)));
        Assert.Equal(OwnerExpectedSha256, Hash(File.ReadAllBytes(outputPath)));
        Assert.Equal(OwnerExpectedSha256, Hash(evidence.Expected.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(evidence.Expected.Bytes, out FirmwareConfigMetadata metadata));
        Assert.Equal("2.0.0", metadata.CommonFwVersion);
        Assert.Equal(1, metadata.ChipNumber);
        Assert.Equal(0x4703, metadata.ProjectId);

        FirmwareContextSuggestion suggestion = Assert.IsType<FirmwareContextSuggestion>(
            FirmwareInspectionTestSupport.TryReadFirmwareContextSuggestion(icId, outputPath));
        Assert.Equal("single", suggestion.NumberToken);
        Assert.Equal(metadata.CommonFwVersion, suggestion.CommonFwVersion);
        Assert.Equal(metadata.ChipNumber, suggestion.ChipNumber);
        Assert.Equal(metadata.ProjectId, suggestion.ProjectId);
        Assert.Equal(
            "c7e1e263ac8ca70f83a6f66fa268da4aa9be37c2c822a39d58fa9c153d66abe2",
            Hash(evidence.AbExpected.Bytes));
        Assert.Equal(0x80000, evidence.AbExpected.Bytes.Length);
    }

    /// <summary>Proves the exact V2 route produces the locked full output and process evidence.</summary>
    [Theory]
    [InlineData("NT51929", "nt51929-ctrlram-replace-fw200-single", "nfc.nt51929.ctrlram-postbuild-v1")]
    [InlineData("NT51919", "nt51919-ctrlram-replace-fw200-single", "nfc.nt51919.ctrlram-postbuild-v1")]
    public async Task V2ProducesLockedExactOutputWithCrcOnlyGoldenDeviationAsync(
        string icId,
        string expectedProfileId,
        string expectedProcessorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase();
        var immutableHashes = evidence.Artifacts.ToDictionary(
            static artifact => artifact.RelativePath,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-v2");
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(evidence, evidence.Expected.Path);
        string v2Path = workspace.PathFor("v2.bin");
        CompositionRunResult v2 = await CtrlRamReplaceTestSupport.RunAsync(BootstrapTestHost.Canonical,
            icId, "single", ExperienceIds.CtrlRamReplace, slots, true,
            TestContext.Current.CancellationToken, v2Path);

        Assert.True(v2.Succeeded, CompositionRunReportJson.Serialize(v2));
        byte[] v2Bytes = File.ReadAllBytes(v2Path);
        Assert.Equal(CurrentOutputSha256, Hash(v2Bytes));
        AssertGoldenDifferenceClassification(evidence.Expected.Bytes, v2Bytes);
        AssertPhysicalInputProjection(evidence, v2Bytes);

        using var v2Report = JsonDocument.Parse(CompositionRunReportJson.Serialize(v2));
        AssertReportIdentity(v2Report.RootElement, expectedProfileId, icId);
        AssertOversizedNormalInputWarning(v2Report.RootElement);
        AssertProcessEvidence(v2Report.RootElement, icId, expectedProcessorId);
        Assert.All(
            v2Report.RootElement.GetProperty("OutputDifferences").EnumerateArray(),
            difference =>
            {
                Assert.True(difference.GetProperty("IsAccepted").GetBoolean());
                Assert.Equal("PostbuildCrcHeader", difference.GetProperty("Classification").GetString());
            });
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.RelativePath], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>A declared source route still fails closed when the injected processor does not propagate to Backup.</summary>
    [Fact]
    public async Task FirmwareVersionEditRejectsProcessorThatDoesNotPropagateToBackupAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-version-edit");
        string basePath = workspace.Write("base.bin", evidence.Expected.Bytes);
        string outputPath = workspace.PathFor("version-edited.bin");
        var processor = new PassThroughProcessor();

        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunWithProcessorAsync(BootstrapTestHost.Canonical,
            "NT51929",
            "single",
            CreateSlotPaths(evidence, basePath),
            build: true,
            outputPath: outputPath,
            firmwareVersionEdit: new CtrlRamFirmwareVersionDraftState(0x27, 0x04),
            externalProcessor: processor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Null(result.CommittedOutputId);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(1, processor.CallCount);
        Assert.Equal(evidence.Expected.Bytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));

        using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            candidate => candidate.GetProperty("Code").GetString() ==
                CompositionPlanningIssueCodes.ReplaceCtrlRamFirmwareVersionOutputMismatch);
        Assert.Equal("verify-nvt-fwconfig-backup-version", issue.GetProperty("OperationId").GetString());
        Assert.False(report.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }

    /// <summary>The production NT51929 mode edits Primary, propagates Backup, and commits a full output.</summary>
    [Fact]
    public async Task FirmwareVersionEditBuildPropagatesPrimaryToBackupAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const byte firmwareVersion = 0x27;
        const byte firmwareSubVersion = 0x04;
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-version-edit-real");
        string basePath = workspace.Write("base.bin", evidence.Expected.Bytes);
        string outputPath = workspace.PathFor("version-edited.bin");

        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunAsync(BootstrapTestHost.Canonical,
            "NT51929",
            "single",
            ExperienceIds.CtrlRamReplace,
            CreateSlotPaths(evidence, basePath),
            build: true,
            TestContext.Current.CancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit: new CtrlRamFirmwareVersionDraftState(firmwareVersion, firmwareSubVersion));

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal(outputPath, result.CommittedOutputId);
        Assert.Equal(evidence.Expected.Bytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(Capacity, output.Length);
        Assert.True(BuiltInTpFlashMapCatalog.TryFind("NT51929", out TpFlashMapProfile? flashMap));
        Assert.True(FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress(
            output,
            flashMap!.FirmwareConfigPrimaryStart,
            out FirmwareConfigMetadata primary));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(output, out FirmwareConfigMetadata backup));
        Assert.Equal(firmwareVersion, primary.FirmwareVersion);
        Assert.Equal(unchecked((byte)~firmwareVersion), primary.FirmwareVersionBar);
        Assert.Equal(firmwareSubVersion, primary.FirmwareSubVersion);
        Assert.Equal(firmwareVersion, backup.FirmwareVersion);
        Assert.Equal(unchecked((byte)~firmwareVersion), backup.FirmwareVersionBar);
        Assert.Equal(firmwareSubVersion, backup.FirmwareSubVersion);
    }

    /// <summary>NT51919/NT51929 aliases retain the requested single route across non-authoritative metadata.</summary>
    [Theory]
    [InlineData("NT51929", "pid")]
    [InlineData("NT51929", "version")]
    [InlineData("NT51919", "pid")]
    [InlineData("NT51919", "version")]
    public async Task ProductionRouteAcceptsNonAuthoritativeMetadataVariationsAsync(string icId, string mutation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        OwnerCase evidence = ReadOwnerCase();
        var immutableHashes = evidence.Artifacts.ToDictionary(
            static artifact => artifact.RelativePath,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-negative-route");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int start = checked((int)metadata.StructureStart);
        switch (mutation)
        {
            case "pid":
                BinaryPrimitives.WriteUInt16LittleEndian(
                    reference.AsSpan(start + FirmwareConfigLayout.ProjectIdOffset),
                    0xFFFF);
                break;
            case "version":
                reference[start + FirmwareConfigLayout.CommonFwAdditionalVersionOffset]++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown route mutation.");
        }

        File.WriteAllBytes(referencePath, reference);
        string outputPath = workspace.PathFor("metadata-variation-output.bin");
        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunWithProcessorAsync(BootstrapTestHost.Canonical,
            icId, "single", CreateSlotPaths(evidence, referencePath), true,
            outputPath, null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.True(File.Exists(outputPath));
        using (var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result)))
        {
            string canonicalIcId = icId.ToUpperInvariant();
            AssertReportIdentity(
                report.RootElement,
                $"{canonicalIcId.ToLowerInvariant()}-ctrlram-replace-fw200-single",
                canonicalIcId);
        }
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.RelativePath], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>NT51919 and NT51929 bounded cascade routes consume the owner-declared DiffDLM range.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ctrlram-replace-fw1x-cascade")]
    [InlineData("NT51929", "nt51929-ctrlram-replace-fw1x-cascade")]
    public async Task CascadeFamilyRoutesBuildWithDiffDlmAsync(
        string icId,
        string expectedProfileId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw1x-cascade-route");
        string referencePath = workspace.PathFor("cascade-reference.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        reference[checked((int)metadata.StructureStart) + FirmwareConfigLayout.ChipNumberOffset] = 2;
        int existingBackupStart = checked((int)metadata.StructureStart);
        const int expectedTwoIcBackupStart = 0x2F000;
        reference.AsSpan(existingBackupStart, 0x1000)
            .CopyTo(reference.AsSpan(expectedTwoIcBackupStart, 0x1000));
        reference.AsSpan(existingBackupStart + 0x0FFC, 4).Clear();
        File.WriteAllBytes(referencePath, reference);

        byte[] diff = [.. Enumerable.Range(0, 0x8C00).Select(static index => (byte)((index * 17) + 3))];
        string diffPath = workspace.Write("DiffDLM.bin", diff);
        Dictionary<string, string> slotPaths = CreateSlotPaths(evidence, referencePath);
        _ = slotPaths.Remove("replace-ctrlram-nf");
        slotPaths["replace-ctrlram-diff"] = diffPath;
        string outputPath = workspace.PathFor("cascade-output.bin");

        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunWithProcessorAsync(BootstrapTestHost.Canonical,
            icId,
            IcNumberSelectionTokens.CascadeTwoToEight,
            slotPaths,
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(diff.AsSpan(0, 0x0B90).ToArray(), output.AsSpan(0x2D100, 0x0B90).ToArray());
        Assert.Equal(
            reference.AsSpan(0x2DC90, 0x0870).ToArray(),
            output.AsSpan(0x2DC90, 0x0870).ToArray());
        Assert.Equal(
            reference.AsSpan(0x2E500, 0x7800).ToArray(),
            output.AsSpan(0x2E500, 0x7800).ToArray());
        using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        AssertReportIdentity(report.RootElement, expectedProfileId, icId);
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
    }

    /// <summary>Proves accepted NT51929 identifiers select the same exact V2 route.</summary>
    [Theory]
    [InlineData("51929", "NT51929", "nt51929-ctrlram-replace-fw200-single")]
    [InlineData("nt51929", "NT51929", "nt51929-ctrlram-replace-fw200-single")]
    [InlineData(" NT51929 ", "NT51929", "nt51929-ctrlram-replace-fw200-single")]
    [InlineData("51919", "NT51919", "nt51919-ctrlram-replace-fw200-single")]
    [InlineData("nt51919", "NT51919", "nt51919-ctrlram-replace-fw200-single")]
    [InlineData(" NT51919 ", "NT51919", "nt51919-ctrlram-replace-fw200-single")]
    public async Task AcceptedIcAliasesSelectExactV2RouteAsync(
        string icId,
        string canonicalIcId,
        string expectedProfileId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-alias");
        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunWithProcessorAsync(BootstrapTestHost.Canonical,
            icId, "single", CreateSlotPaths(evidence, evidence.Expected.Path), true,
            workspace.PathFor("alias.bin"), null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        AssertReportIdentity(report.RootElement, expectedProfileId, canonicalIcId);
    }

    private static void AssertGoldenDifferenceClassification(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        ByteRange[] crcWords = [new(0x7100, 4), new(0x7118, 4), new(0x27FF0, 4), new(0x28008, 4)];
        Assert.Equal(15, CountDifferences(expected, actual));
        Assert.Equal(4, CountDifferences(expected, actual, crcWords[0]));
        Assert.Equal(3, CountDifferences(expected, actual, crcWords[1]));
        Assert.Equal(4, CountDifferences(expected, actual, crcWords[2]));
        Assert.Equal(4, CountDifferences(expected, actual, crcWords[3]));
        Assert.Equal(0, CountDifferencesOutside(expected, actual, crcWords));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(NfStart, NfMaximumLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(NormalStart, NormalLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(VnStart, VnMaximumLength)));
    }

    private static void AssertPhysicalInputProjection(OwnerCase evidence, byte[] output)
    {
        OwnerArtifact nf = evidence.Require("NF_Ctrlram.bin");
        OwnerArtifact normal = evidence.Require("Normal_Ctrlram.bin");
        OwnerArtifact vn = evidence.Require("VN_Ctrlram.bin");
        Assert.Equal(nf.Bytes, output.AsSpan(NfStart, nf.Bytes.Length).ToArray());
        Assert.Equal(normal.Bytes.AsSpan(0, NormalLength).ToArray(), output.AsSpan(NormalStart, NormalLength).ToArray());
        Assert.Equal(vn.Bytes, output.AsSpan(VnStart, vn.Bytes.Length).ToArray());
    }

    private static void AssertProcessEvidence(
        JsonElement report,
        string icId,
        string expectedProcessorId)
    {
        JsonElement session = Assert.Single(ReadProcessorSessions(report));
        Assert.Equal(ExpectedArguments(icId), ReadNormalizedArguments(session));
        Assert.Equal(2, session.GetProperty("ExecutedCommands").GetArrayLength());
        AssertProcessorIdentity(session, expectedProcessorId);
        Assert.Equal([new ByteRange(0, Capacity)], ReadRanges(session, "ProcessorAllowedReadRanges"));
        ByteRange[] expectedWrites = [
            new(0x7100, 4), new(0x7118, 4), new(NfStart, 1624), new(NormalStart, NormalLength),
            new(VnStart, VnMaximumLength), new(HeaderCopyStart, HeaderCopyLength),
            new(0x2E000, FirmwareConfigLayout.RequiredLength),
        ];
        Assert.Equal(expectedWrites, ReadRanges(session, "ProcessorAllowedWriteRanges"));
        string executable = session.GetProperty("ExecutedCommands")[0].GetProperty("ExecutablePath").GetString()!;
        Assert.Equal(RegisteredCombinerSha256, Hash(File.ReadAllBytes(executable)));
    }

    private static string[][] ExpectedArguments(string icId)
    {
        string firmware = $"output/{icId.ToLowerInvariant()}_fw.bin";
        return [
            [
                "NT51932BASED_NORMAL_MODE", "CRC8", firmware, firmware,
                "BIN/Normal_Ctrlram.bin", "0x0", "0x21B90", "18944",
                "BIN/VN_Ctrlram.bin", "0x0", "0x26590", "6496",
                "BIN/NF_Ctrlram.bin", "0x0", "0x1FC00", "8080",
                firmware, "0x7000", "0x27EF0", "512",
            ],
            [
                "NT51932BASED_NORMAL_MODE", "CRC8", firmware, firmware,
                firmware, "0x7000", "0x27EF0", "512",
            ],
        ];
    }

    private static JsonElement[] ReadProcessorSessions(JsonElement report)
    {
        return [
            .. report.GetProperty("Operations").EnumerateArray().Where(operation =>
                StringComparer.Ordinal.Equals(operation.GetProperty("Kind").GetString(), "RunExternalProcessor")),
        ];
    }

    private static string[][] ReadNormalizedArguments(JsonElement session)
    {
        return [
            .. session.GetProperty("ExecutedCommands").EnumerateArray().Select(command =>
            {
                string workingDirectory = command.GetProperty("WorkingDirectory").GetString()!;
                return command.GetProperty("Arguments").EnumerateArray()
                    .Select(argument => argument.GetString()!)
                    .Select(argument => Path.IsPathRooted(argument)
                        ? Path.GetRelativePath(workingDirectory, argument).Replace('\\', '/')
                        : argument.Replace('\\', '/'))
                    .ToArray();
            }),
        ];
    }

    private static ByteRange[] ReadRanges(JsonElement operation, string propertyName)
    {
        return [
            .. operation.GetProperty(propertyName).EnumerateArray()
                .Select(range => new ByteRange(range.GetProperty("Start").GetInt64(), range.GetProperty("Length").GetInt64()))
                .OrderBy(static range => range.Start),
        ];
    }

    private static void AssertProcessorIdentity(JsonElement session, string expectedProcessorId)
    {
        Assert.Equal(expectedProcessorId, session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
    }

    private static void AssertReportIdentity(JsonElement report, string profileId, string icId)
    {
        Assert.Equal(profileId, report.GetProperty("ProfileId").GetString());
        Assert.Equal(icId, report.GetProperty("IcId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ModeId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ExperienceId").GetString());
        Assert.Equal("Replace", report.GetProperty("CompositionKind").GetString());
    }

    private static void AssertOversizedNormalInputWarning(JsonElement report)
    {
        JsonElement issue = Assert.Single(report.GetProperty("Issues").EnumerateArray());
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceTruncated, issue.GetProperty("Code").GetString());
        Assert.Equal(CompositionIssueSeverity.Warning, issue.GetProperty("Severity").GetString());
        Assert.Equal("replace-ctrlram-normal", issue.GetProperty("OperationId").GetString());
        string message = issue.GetProperty("Message").GetString()!;
        Assert.Contains("655360", message, StringComparison.Ordinal);
        Assert.Contains("18944", message, StringComparison.Ordinal);
        Assert.Contains("636416 trailing bytes were discarded", message, StringComparison.Ordinal);
    }

    private static string ReadProfileId(string reportJson)
    {
        using var report = JsonDocument.Parse(reportJson);
        return report.RootElement.GetProperty("ProfileId").GetString()!;
    }

    private static Dictionary<string, string> CreateSlotPaths(OwnerCase evidence, string referencePath)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = referencePath,
            ["replace-ctrlram-normal"] = evidence.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = evidence.Require("VN_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = evidence.Require("NF_Ctrlram.bin").Path,
        };
    }

    private static OwnerCase ReadOwnerCase()
    {
        string root = CanonicalGoldenTestData.Root;
        JsonElement ctrlRamCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId);
        JsonElement abCase = CanonicalGoldenTestData.LoadDirectCase(
            "ab-merge",
            "nt51929-ab-t05-d06");
        OwnerArtifact[] artifacts = [
            .. ctrlRamCase.GetProperty("artifacts").EnumerateArray()
                .Select(item => ReadArtifact(root, item)),
        ];
        OwnerArtifact abExpected = abCase.GetProperty("artifacts")
            .EnumerateArray()
            .Where(static item => StringComparer.Ordinal.Equals(
                item.GetProperty("role").GetString(),
                "expected"))
            .Select(item => ReadArtifact(root, item, "expected-final-output-ab"))
            .Single();
        return new OwnerCase(
            artifacts,
            artifacts.Single(static artifact => artifact.Role == "standard-merge-dp-input"),
            artifacts.Single(static artifact => artifact.Role == "standard-merge-tp-input"),
            artifacts.Single(static artifact => artifact.Role == "expected-final-output-single"),
            abExpected);
    }

    private static OwnerArtifact ReadArtifact(
        string root,
        JsonElement entry,
        string? sourceRole = null)
    {
        string path = RepositoryPaths.ManifestPath(root, entry);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        return new(
            entry.GetProperty("originalFileName").GetString()!,
            entry.GetProperty("path").GetString()!,
            sourceRole ?? entry.GetProperty("sourceRole").GetString()!,
            path,
            bytes);
    }

    private static long CountDifferences(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        return CountDifferences(expected, actual, new ByteRange(0, Math.Min(expected.Length, actual.Length))) +
            Math.Abs(expected.Length - actual.Length);
    }

    private static long CountDifferences(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, ByteRange range)
    {
        long count = 0;
        for (int index = checked((int)range.Start); index < checked((int)range.EndExclusive); index++)
        {
            count += expected[index] == actual[index] ? 0 : 1;
        }

        return count;
    }

    private static long CountDifferencesOutside(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        IReadOnlyList<ByteRange> allowed)
    {
        long count = 0;
        for (int index = 0; index < Math.Min(expected.Length, actual.Length); index++)
        {
            count += expected[index] != actual[index] && !allowed.Any(range => range.Contains(index)) ? 1 : 0;
        }

        return count + Math.Abs(expected.Length - actual.Length);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class PassThroughProcessor : IExternalProcessor
    {
        public int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, [], []));
        }
    }

    private sealed record OwnerArtifact(string FileName, string RelativePath, string Role, string Path, byte[] Bytes);

    private sealed record OwnerCase(
        IReadOnlyList<OwnerArtifact> Artifacts,
        OwnerArtifact Dp,
        OwnerArtifact Tp,
        OwnerArtifact Expected,
        OwnerArtifact AbExpected)
    {
        public OwnerArtifact Require(string fileName)
        {
            return Artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.FileName, fileName));
        }
    }
}
