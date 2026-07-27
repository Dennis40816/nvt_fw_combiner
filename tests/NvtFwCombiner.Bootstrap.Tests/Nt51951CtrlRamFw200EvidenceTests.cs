using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Exact V1/V2 route parity from NT51951 Common FW 2.0.0 single owner evidence.</summary>
public sealed class Nt51951CtrlRamFw200EvidenceTests
{
    private const string CaseId = "nt51951-fw200-single-auto-prj-695-20260718";
    private const string OwnerExpectedSha256 = "c1cd54d93af431727220adc37fec2488765909dc09cb917d1ff69f6087bb6b69";
    private const string CurrentOutputSha256 = "64ffa21a36a3a9560ebe109b9b0c94edcbb37c69a0dcb0aa183da7542694d1ea";
    private const string RegisteredCombinerSha256 = "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf";
    private const int TpPrefixCapacity = 0x37000;
    private const int NfStart = 0x22C00;
    private const int NfMaximumLength = 0x2A10;
    private const int NormalStart = 0x25610;
    private const int NormalLength = 0x5C00;
    private const int VnStart = 0x2B210;
    private const int VnLength = 0x20FC;
    private const int HeaderCopyStart = 0x2D30C;
    private const int HeaderCopyLength = 0x200;

    /// <summary>Locks the exact Standard Merge reconstruction and metadata admission facts.</summary>
    [Fact]
    public async Task StandardMergeReconstructsOwnerExpectedAndExactFirmwareContextAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-fw200-base");
        string outputPath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51951",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = evidence.Dp.Path,
                [CompositionAddressSpaceIds.TpInput] = evidence.Tp.Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal("nt51951-standard-merge-dp-perspective", ReadProfileId(result.ReportJson));
        Assert.Equal(OwnerExpectedSha256, Hash(File.ReadAllBytes(outputPath)));
        Assert.Equal(OwnerExpectedSha256, Hash(evidence.Expected.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(evidence.Expected.Bytes, out FirmwareConfigMetadata metadata));
        Assert.Equal("2.0.0", metadata.CommonFwVersion);
        Assert.Equal(1, metadata.ChipNumber);
        Assert.Equal(0x5901, metadata.ProjectId);

        WorkbenchFirmwareContextSuggestion suggestion = Assert.IsType<WorkbenchFirmwareContextSuggestion>(
            WorkbenchCompositionService.TryReadFirmwareContextSuggestion("NT51951", outputPath));
        Assert.Equal("single", suggestion.NumberToken);
        Assert.Equal(metadata.CommonFwVersion, suggestion.CommonFwVersion);
        Assert.Equal(metadata.ChipNumber, suggestion.ChipNumber);
        Assert.Equal(metadata.ProjectId, suggestion.ProjectId);
    }

    /// <summary>Proves the exact V2 route retains the locked output with CRC-only Combiner-version divergence.</summary>
    [Fact]
    public async Task V2ProducesLockedOutputWithCrcOnlyCombinerVersionDeviationAsync()
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
        using var workspace = TempWorkspace.Create("nfc-nt51951-fw200-parity");
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(evidence, evidence.Expected.Path);
        string v2Path = workspace.PathFor("v2.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51951", "single", WorkbenchReplaceModes.CtrlRam, slots, true,
            TestContext.Current.CancellationToken, v2Path);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2Path);
        Assert.Equal(CurrentOutputSha256, Hash(v2Bytes));
        AssertOwnerDifferenceClassification(evidence.Expected.Bytes, v2Bytes);
        AssertPhysicalInputProjection(evidence, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, "nt51951-ctrlram-replace-fw200-single");
        AssertProcessEvidence(v2Report.RootElement);
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.RelativePath], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Requested single routing accepts display-only metadata variations.</summary>
    [Theory]
    [InlineData("single", 2, 0, 0, 1, 0xFFFF)]
    [InlineData("single", 1, 3, 0, 1, 0x5901)]
    public async Task ProductionRouteAcceptsNonAuthoritativeMetadataVariationsAsync(
        string number,
        byte major,
        byte minor,
        byte additional,
        byte chipCount,
        ushort projectId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-fw200-negative-route");
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
        AssertReportIdentity(report.RootElement, "nt51951-ctrlram-replace-fw200-single");
    }

    /// <summary>Proves production routing accepts a structurally valid base without golden hash admission.</summary>
    [Fact]
    public async Task SameMetadataWithDifferentBaseBytesUsesExactV2RouteAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-fw200-base-identity");
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
        AssertReportIdentity(report.RootElement, "nt51951-ctrlram-replace-fw200-single");
    }

    /// <summary>Proves the owner-approved cascade TP layout builds while preserving the larger image tail.</summary>
    [Fact]
    public async Task CascadeSelectorBuildsThroughSharedTpLayoutAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-fw200-cascade");
        byte[] cascadeReference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(cascadeReference, out FirmwareConfigMetadata metadata));
        cascadeReference[checked((int)metadata.StructureStart + FirmwareConfigLayout.ChipNumberOffset)] = 2;
        string referencePath = workspace.Write("cascade-reference.bin", cascadeReference);
        byte[] diffBytes = [.. Enumerable.Range(0, 0x1400).Select(static index => unchecked((byte)(index * 19)))];
        string diffPath = workspace.Write("DiffDLM.bin", diffBytes);
        Dictionary<string, string> slots = CreateSlotPaths(evidence, referencePath);
        slots[WorkbenchSlotIds.CreateReplaceCtrlRam("diff")] = diffPath;
        Assert.True(slots.Remove(WorkbenchSlotIds.CreateReplaceCtrlRam("nf")));

        string outputPath = workspace.PathFor("cascade.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51951", "cascade", slots, true, outputPath, null,
            new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(
            diffBytes.AsSpan(0, 0x910).ToArray(),
            output.AsSpan(0x33200, 0x910).ToArray());
        Assert.Equal(
            cascadeReference.AsSpan(0x33B10, 0xAF0).ToArray(),
            output.AsSpan(0x33B10, 0xAF0).ToArray());
        Assert.Equal(cascadeReference.AsSpan(0x34600).ToArray(), output.AsSpan(0x34600).ToArray());
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51951-ctrlram-replace-fw1x-cascade");
    }

    /// <summary>Proves accepted NT51951 identifiers select the same exact V2 route.</summary>
    [Theory]
    [InlineData("51951")]
    [InlineData("nt51951")]
    [InlineData(" NT51951 ")]
    public async Task AcceptedIcAliasesSelectExactV2RouteAsync(string icId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-fw200-alias");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            icId, "single", CreateSlotPaths(evidence, evidence.Expected.Path), true,
            workspace.PathFor("alias.bin"), null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51951-ctrlram-replace-fw200-single");
    }

    private static async Task<WorkbenchRunResult> RunWithPassThroughAsync(
        OwnerCase evidence,
        string number,
        string referencePath,
        string outputPath)
    {
        return await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51951", number, CreateSlotPaths(evidence, referencePath), true,
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
            new(0xA11C, 4), new(0xA130, 4), new(NfStart, 2284), new(NormalStart, NormalLength),
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
                "NT51950BASED_NORMAL_MODE", "CRC8", "output/nt51951_fw.bin", "output/nt51951_fw.bin",
                "BIN/Normal_Ctrlram.bin", "0x0", "0x25610", "23552", "BIN/VN_Ctrlram.bin", "0x0", "0x2B210", "8444",
                "BIN/NF_Ctrlram.bin", "0x0", "0x22C00", "10768", "output/nt51951_fw.bin", "0xA000", "0x2D30C", "512",
            ],
            [
                "NT51950BASED_NORMAL_MODE", "CRC8", "output/nt51951_fw.bin", "output/nt51951_fw.bin",
                "output/nt51951_fw.bin", "0xA000", "0x2D30C", "512",
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
        Assert.Equal("nfc.nt51951.ctrlram-postbuild-v1", session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
    }

    private static void AssertReportIdentity(JsonElement report, string profileId)
    {
        Assert.Equal(profileId, report.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51951", report.GetProperty("IcId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ModeId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ExperienceId").GetString());
        Assert.Equal("Replace", report.GetProperty("CompositionKind").GetString());
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
        JsonElement caseElement = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId);
        OwnerArtifact[] artifacts = [
            .. caseElement.GetProperty("artifacts").EnumerateArray()
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
