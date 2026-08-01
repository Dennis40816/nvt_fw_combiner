using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Exact V1/V2 route parity from NT51950 Common FW 2.0.0 single owner evidence.</summary>
public sealed class Nt51950CtrlRamFw200EvidenceTests
{
    private const string CaseId = "nt51950-fw200-single-auto-prj-676-20260717";
    private const string OwnerExpectedSha256 = "ccda75d0aa08540e293f9ab4a8058c43c4e39d2dd0238238848a2f13df68e38e";
    private const string CurrentOutputSha256 = "a32e6896b840d44e4933adb8827d66bfe642a1347835a1c5bda848cb33ecd5c4";
    private const string RegisteredCombinerSha256 = "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf";
    private const int Capacity = 0x40000;
    private const int TpPrefixCapacity = 0x37000;
    private const int NfStart = 0x22C00;
    private const int NfMaximumLength = 0x2A10;
    private const int NormalStart = 0x25610;
    private const int NormalLength = 0x5C00;
    private const int VnStart = 0x2B210;
    private const int VnLength = 0x20FC;
    private const int HeaderCopyStart = 0x2D30C;
    private const int HeaderCopyLength = 0x200;

    /// <summary>The full-flash owner golden supplies its own ChipNumber for NT51950 CMI naming.</summary>
    [Fact]
    public void FullFlashInspectionUsesEmbeddedChipNumberForOutputNaming()
    {
        OwnerCase evidence = ReadOwnerCase();

        Assert.Equal(Capacity, evidence.Expected.Bytes.Length);
        WorkbenchFirmwareInspection inspection = WorkbenchCompositionService.InspectFirmware(
            "NT51950",
            evidence.Expected.Path,
            tpPath: null,
            new WorkbenchCtrlRamInspectionRequest(WorkbenchIcNumberTokens.SingleChip));

        WorkbenchFirmwareConfigMetadata firmwareConfig = Assert.IsType<WorkbenchFirmwareConfigMetadata>(
            inspection.FirmwareConfig);
        Assert.Equal(1, firmwareConfig.ChipNumber);
        WorkbenchCmiDpCodeMetadata cmi = Assert.IsType<WorkbenchCmiDpCodeMetadata>(inspection.CmiDpCode);
        Assert.Equal("8600", cmi.VersionToken);

        WorkbenchOutputFileNameSuggestion suggestion =
            WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                "NT51950",
                [new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, inspection)],
                new WorkbenchCtrlRamFirmwareVersionEdit(0x80, 0x00),
                new DateOnly(2026, 7, 22));
        Assert.Equal("NT51950_FlashCode_D8600T8000_20260722.bin", suggestion.FileName);
    }

    /// <summary>Locks the exact Standard Merge reconstruction and metadata admission facts.</summary>
    [Fact]
    public async Task StandardMergeReconstructsOwnerExpectedAndExactFirmwareContextAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51950-fw200-base");
        string outputPath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51950",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = evidence.Dp.Path,
                [CompositionAddressSpaceIds.TpInput] = evidence.Tp.Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal("nt51950-standard-merge-dp-perspective", ReadProfileId(result.ReportJson));
        Assert.Equal(OwnerExpectedSha256, Hash(File.ReadAllBytes(outputPath)));
        Assert.Equal(OwnerExpectedSha256, Hash(evidence.Expected.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(evidence.Expected.Bytes, out FirmwareConfigMetadata metadata));
        Assert.Equal("2.0.0", metadata.CommonFwVersion);
        Assert.Equal(1, metadata.ChipNumber);
        Assert.Equal(0x4A06, metadata.ProjectId);

        WorkbenchFirmwareContextSuggestion suggestion = Assert.IsType<WorkbenchFirmwareContextSuggestion>(
            WorkbenchCompositionService.TryReadFirmwareContextSuggestion("NT51950", outputPath));
        Assert.Equal("single", suggestion.NumberToken);
        Assert.Equal(metadata.CommonFwVersion, suggestion.CommonFwVersion);
        Assert.Equal(metadata.ChipNumber, suggestion.ChipNumber);
        Assert.Equal(metadata.ProjectId, suggestion.ProjectId);
    }

    /// <summary>Proves the exact V2 route retains the locked output with CRC-only owner divergence.</summary>
    [Fact]
    public async Task V2ProducesLockedOutputWithCrcOnlyOwnerDeviationAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase();
        var immutableHashes = evidence.Artifacts.ToDictionary(
            static artifact => artifact.RelativePath,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51950-fw200-parity");
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(evidence, evidence.Expected.Path);
        string v2Path = workspace.PathFor("v2.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950", "single", WorkbenchReplaceModes.CtrlRam, slots, true,
            TestContext.Current.CancellationToken, v2Path);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2Path);
        Assert.Equal(CurrentOutputSha256, Hash(v2Bytes));
        AssertOwnerDifferenceClassification(evidence.Expected.Bytes, v2Bytes);
        AssertPhysicalInputProjection(evidence, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, "nt51950-ctrlram-replace-fw200-single");
        AssertOversizedNormalInputWarning(v2Report.RootElement);
        AssertProcessEvidence(v2Report.RootElement);
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.RelativePath], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>NT51950 edits the primary FWConfig first, then permits only the propagated Backup fields from postbuild.</summary>
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
        using var workspace = TempWorkspace.Create("nfc-nt51950-fw200-version-edit-real");
        string basePath = workspace.Write("base.bin", evidence.Expected.Bytes);
        string outputPath = workspace.PathFor("version-edited.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            WorkbenchReplaceModes.CtrlRam,
            CreateSlotPaths(evidence, basePath),
            build: true,
            TestContext.Current.CancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit: new WorkbenchCtrlRamFirmwareVersionEdit(
                firmwareVersion,
                firmwareSubVersion));

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(evidence.Expected.Bytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.True(BuiltInTpFlashMapCatalog.TryFind("NT51950", out TpFlashMapProfile? flashMap));
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

        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement session = Assert.Single(ReadProcessorSessions(report.RootElement));
        Assert.Contains(new ByteRange(backup.StructureStart, 2), ReadRanges(session, "ProcessorAllowedWriteRanges"));
        Assert.Contains(
            new ByteRange(backup.StructureStart + FirmwareConfigLayout.FirmwareSubVersionOffset, 1),
            ReadRanges(session, "ProcessorAllowedWriteRanges"));
    }

    /// <summary>Requested single routing accepts display-only metadata variations.</summary>
    [Theory]
    [InlineData("single", 2, 0, 0, 1, 0xFFFF)]
    [InlineData("single", 1, 3, 0, 1, 0x4A06)]
    public async Task ProductionRouteAcceptsNonAuthoritativeMetadataVariationsAsync(
        string number,
        byte major,
        byte minor,
        byte additional,
        byte chipCount,
        ushort projectId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51950-fw200-negative-route");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int start = checked((int)metadata.StructureStart);
        reference[start + FirmwareConfigLayout.CommonFwMajorVersionOffset] = major;
        reference[start + FirmwareConfigLayout.CommonFwMinorVersionOffset] = minor;
        reference[start + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = additional;
        reference[start + FirmwareConfigLayout.ChipNumberOffset] = chipCount;
        BinaryPrimitives.WriteUInt16LittleEndian(reference.AsSpan(start + FirmwareConfigLayout.ProjectIdOffset), projectId);
        File.WriteAllBytes(referencePath, reference);

        string outputPath = workspace.PathFor("metadata-variation-output.bin");
        WorkbenchRunResult result = await RunWithPassThroughAsync(evidence, number, referencePath, outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51950-ctrlram-replace-fw200-single");
    }

    /// <summary>Proves production routing accepts a structurally valid base without golden hash admission.</summary>
    [Fact]
    public async Task SameMetadataWithDifferentBaseBytesUsesExactV2RouteAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51950-fw200-base-identity");
        string referencePath = workspace.PathFor("different-variant.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        reference[0x100] ^= 0x01;
        File.WriteAllBytes(referencePath, reference);

        Assert.NotEqual(Hash(evidence.Expected.Bytes), Hash(reference));

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult result = await RunWithPassThroughAsync(evidence, "single", referencePath, outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(reference[0x100], File.ReadAllBytes(outputPath)[0x100]);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51950-ctrlram-replace-fw200-single");
    }

    /// <summary>Proves the exact-2-IC Cascade route maps only active DLM bytes and preserves DiffNF.</summary>
    [Fact]
    public async Task CascadeSelectorBuildsThroughSharedTpLayoutAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51950-fw200-cascade-guard");
        byte[] cascadeReference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(cascadeReference, out FirmwareConfigMetadata metadata));
        cascadeReference[checked((int)metadata.StructureStart + FirmwareConfigLayout.ChipNumberOffset)] = 2;
        string referencePath = workspace.Write("cascade-reference.bin", cascadeReference);
        string diffPath = workspace.PathFor("DiffDLM.bin");
        byte[] diffBytes = [.. Enumerable.Range(0, 0x1400).Select(static index => unchecked((byte)(index * 17)))];
        File.WriteAllBytes(diffPath, diffBytes);
        Dictionary<string, string> slots = CreateSlotPaths(evidence, referencePath);
        _ = slots.Remove(WorkbenchSlotIds.CreateReplaceCtrlRam("nf"));
        slots[WorkbenchSlotIds.CreateReplaceCtrlRam("diff")] = diffPath;

        string outputPath = workspace.PathFor("cascade.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51950", "cascade", slots, true, outputPath, null,
            new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(diffBytes.AsSpan(0, 0x0910).ToArray(), output.AsSpan(0x33200, 0x0910).ToArray());
        Assert.Equal(
            cascadeReference.AsSpan(0x33B10, 0x0AF0).ToArray(),
            output.AsSpan(0x33B10, 0x0AF0).ToArray());
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51950-ctrlram-replace-fw1x-cascade");
    }

    /// <summary>Proves accepted NT51950 identifiers select the same exact V2 route.</summary>
    [Theory]
    [InlineData("51950")]
    [InlineData("nt51950")]
    [InlineData(" NT51950 ")]
    public async Task AcceptedIcAliasesSelectExactV2RouteAsync(string icId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51950-fw200-alias");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            icId, "single", CreateSlotPaths(evidence, evidence.Expected.Path), true,
            workspace.PathFor("alias.bin"), null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51950-ctrlram-replace-fw200-single");
    }

    private static async Task<WorkbenchRunResult> RunWithPassThroughAsync(
        OwnerCase evidence,
        string number,
        string referencePath,
        string outputPath)
    {
        return await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51950", number, CreateSlotPaths(evidence, referencePath), true,
            outputPath, null, new PassThroughProcessor(), TestContext.Current.CancellationToken);
    }

    private static void AssertOwnerDifferenceClassification(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        ByteRange[] crcWords = [new(0xA11C, 4), new(0xA130, 4), new(0x2D428, 4), new(0x2D43C, 4)];
        Assert.Equal(16, CountDifferences(expected, actual));
        foreach (ByteRange range in crcWords)
        {
            Assert.Equal(4, CountDifferences(expected, actual, range));
        }

        Assert.Equal(0, CountDifferencesOutside(expected, actual, crcWords));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(NfStart, NfMaximumLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(NormalStart, NormalLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(VnStart, VnLength)));
        Assert.Equal(8, CountDifferences(expected, actual, new ByteRange(HeaderCopyStart, HeaderCopyLength)));
    }

    private static void AssertPhysicalInputProjection(OwnerCase evidence, byte[] output)
    {
        OwnerArtifact nf = evidence.Require("NF_Ctrlram.bin");
        OwnerArtifact normal = evidence.Require("Normal_Ctrlram.bin");
        OwnerArtifact vn = evidence.Require("VN_Ctrlram.bin");
        Assert.Equal(nf.Bytes, output.AsSpan(NfStart, nf.Bytes.Length).ToArray());
        Assert.Equal(
            evidence.Expected.Bytes.AsSpan(NfStart + nf.Bytes.Length, NfMaximumLength - nf.Bytes.Length).ToArray(),
            output.AsSpan(NfStart + nf.Bytes.Length, NfMaximumLength - nf.Bytes.Length).ToArray());
        Assert.Equal(normal.Bytes.AsSpan(0, NormalLength).ToArray(), output.AsSpan(NormalStart, NormalLength).ToArray());
        Assert.Equal(vn.Bytes, output.AsSpan(VnStart, VnLength).ToArray());
        ReadOnlySpan<byte> header = output.AsSpan(0xA000, HeaderCopyLength);
        ReadOnlySpan<byte> headerCopy = output.AsSpan(HeaderCopyStart, HeaderCopyLength);
        Assert.Equal(8, CountDifferences(header, headerCopy));
        Assert.Equal(0, CountDifferencesOutside(
            header, headerCopy, [new ByteRange(0x11C, 4), new ByteRange(0x130, 4)]));
    }

    private static void AssertProcessEvidence(JsonElement report)
    {
        JsonElement session = Assert.Single(ReadProcessorSessions(report));
        Assert.Equal(ExpectedArguments(), ReadNormalizedArguments(session));
        Assert.Equal(2, session.GetProperty("ExecutedCommands").GetArrayLength());
        AssertProcessorIdentity(session);
        Assert.Equal([new ByteRange(0, TpPrefixCapacity)], ReadRanges(session, "ProcessorAllowedReadRanges"));
        ByteRange[] expectedWrites = [
            new(0xA11C, 4), new(0xA130, 4), new(NfStart, 2816), new(NormalStart, NormalLength),
            new(VnStart, VnLength), new(HeaderCopyStart, HeaderCopyLength),
        ];
        Assert.Equal(expectedWrites, ReadRanges(session, "ProcessorAllowedWriteRanges"));
        string executable = session.GetProperty("ExecutedCommands")[0].GetProperty("ExecutablePath").GetString()!;
        Assert.Equal(RegisteredCombinerSha256, Hash(File.ReadAllBytes(executable)));
    }

    private static string[][] ExpectedArguments()
    {
        return [
            [
                "NT51950BASED_NORMAL_MODE", "CRC8", "output/nt51950_fw.bin", "output/nt51950_fw.bin",
                "BIN/Normal_Ctrlram.bin", "0x0", "0x25610", "23552", "BIN/VN_Ctrlram.bin", "0x0", "0x2B210", "8444",
                "BIN/NF_Ctrlram.bin", "0x0", "0x22C00", "10768", "output/nt51950_fw.bin", "0xA000", "0x2D30C", "512",
            ],
            [
                "NT51950BASED_NORMAL_MODE", "CRC8", "output/nt51950_fw.bin", "output/nt51950_fw.bin",
                "output/nt51950_fw.bin", "0xA000", "0x2D30C", "512",
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

    private static void AssertProcessorIdentity(JsonElement session)
    {
        Assert.Equal("nfc.nt51950.ctrlram-postbuild-v1", session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
    }

    private static void AssertReportIdentity(JsonElement report, string profileId)
    {
        Assert.Equal(profileId, report.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51950", report.GetProperty("IcId").GetString());
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
        Assert.Contains("23552", message, StringComparison.Ordinal);
        Assert.Contains("631808 trailing bytes were discarded", message, StringComparison.Ordinal);
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
            [WorkbenchSlotIds.ReplaceBase] = referencePath,
            ["replace-ctrlram-normal"] = evidence.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = evidence.Require("VN_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = evidence.Require("NF_Ctrlram.bin").Path,
        };
    }

    private static OwnerCase ReadOwnerCase()
    {
        string root = CanonicalGoldenTestData.Root;
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId);
        OwnerArtifact[] artifacts = [
            .. goldenCase.GetProperty("artifacts").EnumerateArray()
                .Select(item => ReadArtifact(root, item)),
        ];
        return new OwnerCase(
            artifacts,
            artifacts.Single(static artifact => artifact.Role == "standard-merge-dp-input"),
            artifacts.Single(static artifact => artifact.Role == "standard-merge-tp-input"),
            artifacts.Single(static artifact => artifact.Role == "expected-final-output"));
    }

    private static OwnerArtifact ReadArtifact(string root, JsonElement entry)
    {
        string path = RepositoryPaths.ManifestPath(root, entry);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        return new(
            entry.GetProperty("originalFileName").GetString()!,
            entry.GetProperty("path").GetString()!,
            entry.GetProperty("sourceRole").GetString()!,
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
        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, []));
        }
    }

    private sealed record OwnerArtifact(string FileName, string RelativePath, string Role, string Path, byte[] Bytes);

    private sealed record OwnerCase(
        IReadOnlyList<OwnerArtifact> Artifacts,
        OwnerArtifact Dp,
        OwnerArtifact Tp,
        OwnerArtifact Expected)
    {
        public OwnerArtifact Require(string fileName)
        {
            return Artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.FileName, fileName));
        }
    }
}
