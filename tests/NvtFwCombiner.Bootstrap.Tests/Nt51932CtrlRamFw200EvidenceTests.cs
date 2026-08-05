using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Exact V1/V2 route parity from NT51932 Common FW 2.0.0 owner evidence.</summary>
public sealed class Nt51932CtrlRamFw200EvidenceTests
{
    private const string CaseId = "nt51932-fw200-cascade3-auto-prj-525-20260718";
    private const string OwnerExpectedSha256 = "3eb556e0a9323dd4fbe4c703be1eb33679df2b1ba839e79ddd7bbffa235008fd";
    private const string CurrentOutputSha256 = "0e59a2fbaab16979745b3543564b18f49c9d4eb7912bdea2e61383e31e662566";
    private const string RegisteredCombinerSha256 = "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf";
    private const string PostbuildBatSha256 = "9b570db204df0849f9962f09f9800e6e442a86d38d6dacd0988b32e18f0a514f";
    private const int Capacity = 0x40000;
    private const int NfStart = 0x1FC00;
    private const int NfMaximumLength = 0x1F90;
    private const int NormalStart = 0x21B90;
    private const int NormalLength = 0x4A00;
    private const int VnStart = 0x26590;
    private const int VnMaximumLength = 0x1960;
    private const int HeaderCopyStart = 0x27EF0;
    private const int HeaderCopyLength = 0x200;
    private const int DiffStart = 0x2D100;
    private const int DiffLength = 0x8C00;

    /// <summary>Locks the exact Standard Merge reconstruction and metadata admission facts.</summary>
    [Fact]
    public async Task StandardMergeReconstructsOwnerExpectedAndExactFirmwareContextAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-fw200-base");
        string outputPath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunStandardMergeAsync(
            "NT51932",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = evidence.Dp.Path,
                [CompositionAddressSpaceIds.TpInput] = evidence.Tp.Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal("nt51932-standard-merge-gen-flash", ReadProfileId(result.ReportJson));
        Assert.Equal(OwnerExpectedSha256, Hash(File.ReadAllBytes(outputPath)));
        Assert.Equal(OwnerExpectedSha256, Hash(evidence.Expected.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(evidence.Expected.Bytes, out FirmwareConfigMetadata metadata));
        Assert.Equal("2.0.0", metadata.CommonFwVersion);
        Assert.Equal(3, metadata.ChipNumber);
        Assert.Equal(0x5601, metadata.ProjectId);

        WorkbenchFirmwareContextSuggestion suggestion = Assert.IsType<WorkbenchFirmwareContextSuggestion>(
            FirmwareInspectionAdapter.TryReadFirmwareContextSuggestion("NT51932", outputPath));
        Assert.Equal(WorkbenchIcNumberTokens.CascadeTwoToEight, suggestion.NumberToken);
        Assert.Equal(metadata.CommonFwVersion, suggestion.CommonFwVersion);
        Assert.Equal(metadata.ChipNumber, suggestion.ChipNumber);
        Assert.Equal(metadata.ProjectId, suggestion.ProjectId);
    }

    /// <summary>Proves the exact V2 route produces the locked full output.</summary>
    [Fact]
    public async Task V2ProducesLockedExactOutputWithCrcOnlyOwnerDeviationAsync()
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
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(
            evidence.Expected.Bytes,
            out FirmwareConfigMetadata referenceMetadata));
        Assert.Equal(3, referenceMetadata.ChipNumber);
        WorkbenchFirmwareConfigMetadata workbenchReferenceMetadata = Assert.IsType<WorkbenchFirmwareConfigMetadata>(
            FirmwareInspectionAdapter.TryReadFirmwareConfigMetadata("NT51932", evidence.Expected.Path));
        Assert.Equal(3, workbenchReferenceMetadata.ChipNumber);
        using var workspace = TempWorkspace.Create("nfc-nt51932-fw200-parity");
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(evidence, evidence.Expected.Path);
        string v2Path = workspace.PathFor("v2.bin");
        WorkbenchRunResult v2 = await CompositionExecutionAdapter.RunReplaceAsync(
            "NT51932", WorkbenchIcNumberTokens.CascadeTwoToEight, WorkbenchReplaceModes.CtrlRam, slots, true,
            TestContext.Current.CancellationToken, v2Path);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2Path);
        Assert.Equal(CurrentOutputSha256, Hash(v2Bytes));
        AssertOwnerDifferenceClassification(evidence.Expected.Bytes, v2Bytes);
        AssertPhysicalInputProjection(evidence, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, "nt51932-ctrlram-replace-fw200-cascade");
        AssertProcessEvidence(v2Report.RootElement);
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.RelativePath], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Locks ordered DiffNF evidence separately from the direct composite and unregistered tool.</summary>
    [Fact]
    public void DiffNfAndDirectCompositeRemainSeparateUnprovenEvidence()
    {
        OwnerCase evidence = ReadOwnerCase();
        OwnerArtifact[] diffNf = [
            .. evidence.Artifacts
                .Where(static artifact => StringComparer.Ordinal.Equals(artifact.Role, "diff-nf-source-evidence"))
                .OrderBy(static artifact => ReadDiffNfIndex(artifact.FileName)),
        ];
        Assert.Equal(16, diffNf.Length);
        Assert.Equal(Enumerable.Range(0, 16), diffNf.Select(static artifact => ReadDiffNfIndex(artifact.FileName)));
        AssertArtifact(diffNf[0], 1758, "a76cc832e496b3866fa6fc73c749098b72c1549ce8ccf7bb0d2675b8e519b99b");
        AssertArtifact(diffNf[1], 3072, "23e74315e3892f27b5a684d0e8d8f0430396f1c26a272905a2a3b8b562a42da2");
        AssertArtifact(diffNf[2], 3072, "3abf1c2c2cefde16d5e8dea2a24260e2b7d1bba39c5c9241dc17435f90da9b5d");
        Assert.All(diffNf[3..], artifact => AssertArtifact(
            artifact, 2160, "687feb4ef613b43ca1a112419a104d250abe687100af0b4141a7ffafa7629c16"));

        OwnerArtifact composite = evidence.Require("NF_Ctrlram.bin");
        AssertArtifact(composite, 1758, "a76cc832e496b3866fa6fc73c749098b72c1549ce8ccf7bb0d2675b8e519b99b");
        Assert.Equal(diffNf[0].Bytes, composite.Bytes);

        string toolRoot = RepositoryPaths.FromRepositoryRoot("external-tools", "diff-nf-merge", "1.0.0");
        Assert.Equal(
            "f611af7e315d46341e15cd7140eb3962f6ac05d337121e5554022ef5e69a2bbe",
            Hash(File.ReadAllBytes(Path.Combine(toolRoot, "DiffNFMerge.exe"))));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(toolRoot, "package-manifest.json")));
        Assert.Equal("not-registered", manifest.RootElement.GetProperty("runtimeIntegrationStatus").GetString());
        Assert.Equal("unverified; deferred to the v0.12.x integration", manifest.RootElement.GetProperty("inputContractStatus").GetString());

        string batPath = RepositoryPaths.FromRepositoryRoot(
            "docs", "references", "ic-flashmap", "postbuild", "PostbuildSetup_51932_2.0.0.bat");
        Assert.Equal(PostbuildBatSha256, Hash(File.ReadAllBytes(batPath)));
        AssertBatCommandOrder(File.ReadAllText(batPath));
    }

    /// <summary>Requested generic-cascade routing accepts display-only metadata variations.</summary>
    [Theory]
    [InlineData(WorkbenchIcNumberTokens.CascadeTwoToEight, 2, 0, 0, 3, 0xFFFF)]
    [InlineData(WorkbenchIcNumberTokens.CascadeTwoToEight, 1, 3, 0, 3, 0x5601)]
    [InlineData(WorkbenchIcNumberTokens.CascadeTwoToEight, 2, 0, 0, 2, 0x5601)]
    public async Task ProductionRouteAcceptsNonAuthoritativeMetadataVariationsAsync(
        string number,
        byte major,
        byte minor,
        byte additional,
        byte chipCount,
        ushort projectId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-fw200-negative-route");
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
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932", number, CreateSlotPaths(evidence, referencePath), true,
            outputPath, null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51932-ctrlram-replace-fw200-cascade");
    }

    /// <summary>Proves production routing accepts a structurally valid base without golden hash admission.</summary>
    [Fact]
    public async Task SameMetadataWithDifferentBaseBytesUsesExactV2RouteAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-fw200-base-identity");
        string referencePath = workspace.PathFor("different-variant.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        reference[0x100] ^= 0x01;
        File.WriteAllBytes(referencePath, reference);

        Assert.NotEqual(Hash(evidence.Expected.Bytes), Hash(reference));

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932", WorkbenchIcNumberTokens.CascadeTwoToEight, CreateSlotPaths(evidence, referencePath), true,
            outputPath, null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(reference[0x100], File.ReadAllBytes(outputPath)[0x100]);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51932-ctrlram-replace-fw200-cascade");
    }

    /// <summary>The single plan routes through the shared TP layout without exposing DiffDLM.</summary>
    [Fact]
    public async Task SinglePlanBuildsWithoutDiffDlmAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-fw1x-single-route");
        string referencePath = workspace.PathFor("single-reference.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        reference[checked((int)metadata.StructureStart) + FirmwareConfigLayout.ChipNumberOffset] = 1;
        File.WriteAllBytes(referencePath, reference);
        Dictionary<string, string> slotPaths = CreateSlotPaths(evidence, referencePath);
        Assert.True(slotPaths.Remove("replace-ctrlram-diff"));
        string outputPath = workspace.PathFor("single-output.bin");

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            "single",
            slotPaths,
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        OwnerArtifact nf = evidence.Require("NF_Ctrlram.bin");
        Assert.Equal(nf.Bytes, File.ReadAllBytes(outputPath).AsSpan(NfStart, nf.Bytes.Length).ToArray());
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51932-ctrlram-replace-fw1x-single");
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
    }

    /// <summary>Proves accepted NT51932 identifiers select the same exact V2 route.</summary>
    [Theory]
    [InlineData("51932")]
    [InlineData("nt51932")]
    [InlineData(" NT51932 ")]
    public async Task AcceptedIcAliasesSelectExactV2RouteAsync(string icId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-fw200-alias");
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            icId, WorkbenchIcNumberTokens.CascadeTwoToEight, CreateSlotPaths(evidence, evidence.Expected.Path), true,
            workspace.PathFor("alias.bin"), null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51932-ctrlram-replace-fw200-cascade");
    }

    private static void AssertOwnerDifferenceClassification(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        ByteRange[] crcWords = [new(0x7100, 4), new(0x7118, 4), new(0x27FF0, 4), new(0x28008, 4)];
        Assert.Equal(16, CountDifferences(expected, actual));
        foreach (ByteRange range in crcWords)
        {
            Assert.Equal(4, CountDifferences(expected, actual, range));
        }
        Assert.Equal(0, CountDifferencesOutside(expected, actual, crcWords));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(NfStart, NfMaximumLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(NormalStart, NormalLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(VnStart, VnMaximumLength)));
        Assert.Equal(8, CountDifferences(expected, actual, new ByteRange(HeaderCopyStart, HeaderCopyLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(DiffStart, DiffLength)));
    }

    private static void AssertPhysicalInputProjection(OwnerCase evidence, byte[] output)
    {
        OwnerArtifact nf = evidence.Require("NF_Ctrlram.bin");
        OwnerArtifact normal = evidence.Require("Normal_Ctrlram.bin");
        OwnerArtifact vn = evidence.Require("VN_Ctrlram.bin");
        OwnerArtifact diff = evidence.Require("DiffDLM.bin");
        Assert.Equal(nf.Bytes, output.AsSpan(NfStart, nf.Bytes.Length).ToArray());
        Assert.Equal(normal.Bytes.AsSpan(0, NormalLength).ToArray(), output.AsSpan(NormalStart, NormalLength).ToArray());
        Assert.Equal(vn.Bytes, output.AsSpan(VnStart, vn.Bytes.Length).ToArray());
        ReadOnlySpan<byte> header = output.AsSpan(0x7000, HeaderCopyLength);
        ReadOnlySpan<byte> headerCopy = output.AsSpan(HeaderCopyStart, HeaderCopyLength);
        Assert.Equal(8, CountDifferences(header, headerCopy));
        Assert.Equal(
            0,
            CountDifferencesOutside(
                header,
                headerCopy,
                [new ByteRange(0x100, 4), new ByteRange(0x118, 4)]));
        Assert.Equal(4094, CountDifferences(diff.Bytes.AsSpan(0, DiffLength), output.AsSpan(DiffStart, DiffLength)));
        for (int index = 0; index < DiffLength; index++)
        {
            if (diff.Bytes[index] != output[DiffStart + index])
            {
                Assert.InRange(index, 0x2F00, 0x3EFF);
                Assert.Equal(0xFF, diff.Bytes[index]);
            }
        }
    }

    private static void AssertProcessEvidence(JsonElement report)
    {
        JsonElement session = Assert.Single(ReadProcessorSessions(report));
        string[][] expectedArguments = ExpectedArguments();
        string[][] actualArguments = ReadNormalizedArguments(session);
        Assert.True(
            expectedArguments.Length == actualArguments.Length &&
            expectedArguments.Zip(actualArguments).All(static pair =>
                pair.First.SequenceEqual(pair.Second, StringComparer.Ordinal)),
            $"Expected arguments: {JsonSerializer.Serialize(expectedArguments)}{Environment.NewLine}" +
            $"Actual arguments: {JsonSerializer.Serialize(actualArguments)}");
        Assert.Equal(2, session.GetProperty("ExecutedCommands").GetArrayLength());
        AssertProcessorIdentity(session);
        Assert.Equal([new ByteRange(0, Capacity)], ReadRanges(session, "ProcessorAllowedReadRanges"));
        ByteRange[] expectedWrites = [
            new(0x7100, 4), new(0x7118, 4), new(0x7128, 0x1C),
            new(NormalStart, NormalLength), new(VnStart, 4120),
            new(HeaderCopyStart, HeaderCopyLength),
            new(DiffStart, 0xB90), new(DiffStart + 0x1400, 0xB90),
            new(0x2F900, 0x7700),
        ];
        IReadOnlyList<ByteRange> actualWrites = ReadRanges(session, "ProcessorAllowedWriteRanges");
        Assert.True(
            expectedWrites.SequenceEqual(actualWrites),
            $"Expected writes: {JsonSerializer.Serialize(expectedWrites)}{Environment.NewLine}" +
            $"Actual writes: {JsonSerializer.Serialize(actualWrites)}");
        string executable = session.GetProperty("ExecutedCommands")[0].GetProperty("ExecutablePath").GetString()!;
        Assert.Equal(RegisteredCombinerSha256, Hash(File.ReadAllBytes(executable)));
        Assert.DoesNotContain("DiffNFMerge", session.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static string[][] ExpectedArguments()
    {
        return [
            [
                "NT51932BASED_NORMAL_MODE", "CRC8", "output/nt51932_fw.bin", "output/nt51932_fw.bin",
                "BIN/Normal_Ctrlram.bin", "0x0", "0x21B90", "18944",
                "BIN/DiffDLM.bin", "0x0", "0x2D100", "2960",
                "BIN/DiffDLM.bin", "0x1400", "0x2E500", "2960",
                "BIN/VN_Ctrlram.bin", "0x0", "0x26590", "6496", "BIN/NF_Ctrlram.bin", "0x0", "0x1FC00", "8080",
                "output/nt51932_fw.bin", "0x7000", "0x27EF0", "512",
            ],
            [
                "NT51932BASED_NORMAL_MODE", "CRC8", "output/nt51932_fw.bin", "output/nt51932_fw.bin",
                "output/nt51932_fw.bin", "0x7000", "0x27EF0", "512",
            ],
        ];
    }

    private static void AssertBatCommandOrder(string text)
    {
        int insertSid = text.IndexOf("@python output\\InsertSID.py output\\nt51932_fw.bin", StringComparison.Ordinal);
        int label = text.IndexOf(":NT51932_Cascade_Postbuild", insertSid, StringComparison.Ordinal);
        int firstCombiner = text.IndexOf("@output\\Combiner.exe NT51932BASED_NORMAL_MODE CRC8", label, StringComparison.Ordinal);
        int secondCombiner = text.IndexOf("@output\\Combiner.exe NT51932BASED_NORMAL_MODE CRC8", firstCombiner + 1, StringComparison.Ordinal);
        Assert.True(label >= 0);
        Assert.True(insertSid >= 0);
        Assert.True(label > insertSid);
        Assert.True(firstCombiner > label);
        Assert.True(secondCombiner > firstCombiner);
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
        Assert.Equal("nfc.nt51932.ctrlram-postbuild-v1", session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
    }

    private static void AssertReportIdentity(JsonElement report, string profileId)
    {
        Assert.Equal(profileId, report.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51932", report.GetProperty("IcId").GetString());
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
            ["replace-ctrlram-diff"] = evidence.Require("DiffDLM.bin").Path,
            ["replace-ctrlram-vn"] = evidence.Require("VN_Ctrlram.bin").Path,
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

    private static void AssertArtifact(OwnerArtifact artifact, long size, string sha256)
    {
        Assert.Equal(size, artifact.Bytes.LongLength);
        Assert.Equal(sha256, Hash(artifact.Bytes));
    }

    private static int ReadDiffNfIndex(string fileName)
    {
        return int.Parse(fileName["NF_Diff_".Length..^".bin".Length], System.Globalization.CultureInfo.InvariantCulture);
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
