using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Exact V1/V2 route parity from NT51930 Common FW 1.3.0 owner evidence.</summary>
public sealed class Nt51930CtrlRamFw130EvidenceTests
{
    private const string CaseId = "nt51930-fw130-cascade3-auto-prj-302-inx-20260718";
    private const string StandardMergeSha256 = "f831e6348af02d9cb8ad833433b165764c495c17b385b996a6fb270dbcddb08d";
    private const string OwnerExpectedSha256 = "676a4b3fb1a302b9bee4b2cea795e17189d70b6d4dd20a45b3fef603afabb1a8";
    private const string CurrentOutputSha256 = "6725c501f66a064c200612f2a1569f13f76f71cab51f4366b4c4f6e7e73ff48f";
    private const string RegisteredCombinerSha256 = "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf";
    private const string SuppliedCombinerSha256 = "291c2c1cc5b75c59680818497ddb863718ff1930b1f000c61a27e1c4eac9dec3";
    private const string PostbuildBatSha256 = "7641ef3b25442d31048d1831714f822a225d9f40875ab059c5b6cb669ead2b08";

    private const int FullFlashCapacity = 0x40000;
    private const int NfStart = 0x1FC00;
    private const int NfMaximumLength = 0x1A50;
    private const int NormalStart = 0x21650;
    private const int NormalLength = 0x2C00;
    private const int MpStart = 0x24250;
    private const int MpLength = 0x3400;
    private const int VnStart = 0x27650;
    private const int VnLength = 0x195E;
    private const int HeaderCopyStart = 0x28FB0;
    private const int HeaderCopyLength = 0x100;
    private const int DiffStart = 0x2F200;
    private const int DiffLength = 0xFE00;

    /// <summary>Locks the exact standard-merge reconstruction and its FWConfig admission facts.</summary>
    [Fact]
    public async Task StandardMergeBaseSelectsExactOwnerFirmwareContextAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        var immutableInputHashes = evidence.Artifacts.ToDictionary(
            static artifact => artifact.FileName,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51930-fw130-base");
        string referencePath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult standardMerge = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51930",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = evidence.Dp.Path,
                [CompositionAddressSpaceIds.TpInput] = evidence.Tp.Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            referencePath);

        Assert.True(standardMerge.Succeeded, standardMerge.ReportJson);
        Assert.Equal("nt51930-standard-merge-flashmap", ReadProfileId(standardMerge.ReportJson));
        byte[] reference = File.ReadAllBytes(referencePath);
        Assert.Equal(StandardMergeSha256, Hash(reference));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        Assert.Equal("1.3.0", metadata.CommonFwVersion);
        Assert.Equal(3, metadata.ChipNumber);
        Assert.Equal(0x110D, metadata.ProjectId);

        (long differenceCount, ByteRange[] differenceRanges) = FindDifferences(evidence.Expected.Bytes, reference);
        Assert.Equal(4097, differenceCount);
        Assert.Equal([new ByteRange(0x6000, 0x1000), new ByteRange(0x3FFFF, 1)], differenceRanges);

        WorkbenchFirmwareContextSuggestion suggestion = Assert.IsType<WorkbenchFirmwareContextSuggestion>(
            WorkbenchCompositionService.TryReadFirmwareContextSuggestion("NT51930", referencePath));
        Assert.Equal(WorkbenchIcNumberTokens.CascadeTwoToThirteen, suggestion.NumberToken);
        Assert.Equal(metadata.CommonFwVersion, suggestion.CommonFwVersion);
        Assert.Equal(metadata.ChipNumber, suggestion.ChipNumber);
        Assert.Equal(metadata.ProjectId, suggestion.ProjectId);
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableInputHashes[artifact.FileName], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Proves the exact V2 route produces the locked full output from owner evidence.</summary>
    [Fact]
    public async Task V2MatchesLockedOutputFromOwnerExpectedReferenceAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase();
        Assert.Equal(OwnerExpectedSha256, Hash(evidence.Expected.Bytes));
        Assert.Equal(PostbuildBatSha256, Hash(evidence.PostbuildBat.Bytes));
        Assert.Equal(SuppliedCombinerSha256, evidence.SuppliedTool.Sha256);
        Assert.False(evidence.SuppliedTool.ExecutionAuthorized);
        Assert.Equal("none", evidence.SuppliedTool.RepositoryRegistration);
        AssertBatProvenance(evidence.PostbuildBat.Path);

        var immutableInputHashes = evidence.Artifacts.ToDictionary(
            static artifact => artifact.FileName,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51930-fw130-parity");
        string referencePath = evidence.Expected.Path;
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(evidence.Expected.Bytes, out FirmwareConfigMetadata metadata));
        Assert.Equal("1.3.0", metadata.CommonFwVersion);
        Assert.Equal(3, metadata.ChipNumber);
        Assert.Equal(0x110D, metadata.ProjectId);

        IReadOnlyDictionary<string, string> slotPaths = CreateSlotPaths(evidence, referencePath);
        string v2OutputPath = workspace.PathFor("v2-output.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51930",
            WorkbenchIcNumberTokens.CascadeTwoToThirteen,
            WorkbenchReplaceModes.CtrlRam,
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            v2OutputPath);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2OutputPath);
        Assert.Equal(CurrentOutputSha256, Hash(v2Bytes));
        Assert.Equal(CurrentOutputSha256, v2.OutputSha256);

        AssertOwnerDifferenceClassification(evidence, v2Bytes);
        AssertPhysicalInputProjection(evidence, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, "nt51930-ctrlram-replace-fw130-cascade3");
        AssertProcessEvidence(v2Report.RootElement, evidence.SuppliedTool.Sha256);

        Assert.Equal(OwnerExpectedSha256, Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableInputHashes[artifact.FileName], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Locks all 29 DiffNF inputs in numeric order and the direct NF composite separately.</summary>
    [Fact]
    public void DiffNfInventoryAndDirectCompositeStayHashPinned()
    {
        OwnerCase evidence = ReadOwnerCase();
        OwnerArtifact[] diffNf = [
            .. evidence.Artifacts
                .Where(static artifact => StringComparer.Ordinal.Equals(artifact.Role, "diff-nf-source-evidence"))
                .OrderBy(static artifact => ReadDiffNfIndex(artifact.FileName)),
        ];

        Assert.Equal(29, diffNf.Length);
        Assert.Equal(Enumerable.Range(0, 29), diffNf.Select(static artifact => ReadDiffNfIndex(artifact.FileName)));
        AssertArtifact(diffNf[0], 3072, "a54f5ebeb680595df1d8f1eb0a85685733373ccf0b51f59f6dd9a0af3d4e9366");
        AssertArtifact(diffNf[1], 3072, "66d54d8377a48c16822338d88974e56f01b46b575972f794d7da9243a88f22f5");
        AssertArtifact(diffNf[2], 2944, "844773646d691b4e9bd10c968928e9ab52c9b6a801d77e5cc33cd1e1648ee9cb");
        Assert.All(
            diffNf[3..],
            artifact => AssertArtifact(
                artifact,
                2048,
                "811f40f7b32e57269b38ab644810fd3c30321034c9655e3bf72a2e7fd80098d7"));

        OwnerArtifact composite = evidence.Require("NF_Ctrlram.bin");
        AssertArtifact(composite, 577, "2e79b9cdc060442190e31c9e3c3a11f82ee6e76407d3db73d90add907dde148e");
        Assert.DoesNotContain(diffNf, artifact => Hash(artifact.Bytes) == Hash(composite.Bytes));
    }

    /// <summary>Bounded requested topology admits structurally valid bases regardless of display-only metadata.</summary>
    [Theory]
    [InlineData(WorkbenchIcNumberTokens.CascadeTwoToThirteen, 1, 3, 0, 3, 0xFFFF)]
    [InlineData(WorkbenchIcNumberTokens.CascadeTwoToThirteen, 1, 2, 0, 3, 0x110D)]
    [InlineData(WorkbenchIcNumberTokens.CascadeTwoToThirteen, 1, 3, 0, 2, 0x110D)]
    public async Task ProductionRouteAcceptsNonAuthoritativeMetadataVariationsAsync(
        string number,
        byte commonFwMajor,
        byte commonFwMinor,
        byte commonFwAdditional,
        byte chipCount,
        ushort projectId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51930-fw130-negative-route");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int backupStart = checked((int)metadata.StructureStart);
        reference[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = commonFwMajor;
        reference[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = commonFwMinor;
        reference[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = commonFwAdditional;
        reference[backupStart + FirmwareConfigLayout.ChipNumberOffset] = chipCount;
        BinaryPrimitives.WriteUInt16LittleEndian(
            reference.AsSpan(backupStart + FirmwareConfigLayout.ProjectIdOffset),
            projectId);
        File.WriteAllBytes(referencePath, reference);
        string beforeSha256 = Hash(reference);
        var processor = new PassThroughProcessor();

        string outputPath = workspace.PathFor("metadata-variation-output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51930",
            number,
            CreateSlotPaths(evidence, referencePath),
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            processor,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        using (var report = JsonDocument.Parse(result.ReportJson))
        {
            AssertReportIdentity(report.RootElement, "nt51930-ctrlram-replace-fw130-cascade3");
        }
        Assert.Equal(beforeSha256, Hash(File.ReadAllBytes(referencePath)));
    }

    /// <summary>Proves NT51930 single routes through V2 without exposing cascade-only DiffDLM authority.</summary>
    [Fact]
    public async Task SinglePlanBuildsWithoutDiffDlmAuthorityAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51930-fw1x-single");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int backupStart = checked((int)metadata.StructureStart);
        reference[backupStart + FirmwareConfigLayout.ChipNumberOffset] = 1;
        File.WriteAllBytes(referencePath, reference);
        string referenceSha256 = Hash(reference);
        Dictionary<string, string> slotPaths = CreateSlotPaths(evidence, referencePath);
        Assert.True(slotPaths.Remove("replace-ctrlram-diff"));

        string outputPath = workspace.PathFor("single-output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51930",
            WorkbenchIcNumberTokens.SingleChip,
            slotPaths,
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51930-ctrlram-replace-fw1x-runtime-single");
        JsonElement session = Assert.Single(ReadProcessorSessions(report.RootElement));
        ByteRange[] allowedWrites = ReadRanges(session, "ProcessorAllowedWriteRanges");
        Assert.DoesNotContain(allowedWrites, range => range.Overlaps(new ByteRange(DiffStart, DiffLength)));
        Assert.Contains(allowedWrites, range => range == new ByteRange(HeaderCopyStart, HeaderCopyLength));
        Assert.Equal(referenceSha256, Hash(File.ReadAllBytes(referencePath)));
    }

    /// <summary>Proves production admission uses firmware facts rather than a golden-only whole-file hash.</summary>
    [Fact]
    public async Task ExactRouteAcceptsDifferentReferenceHashWhenFirmwareFactsStillMatchAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51930-fw130-production-reference");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        reference[0x100] ^= 0x01;
        File.WriteAllBytes(referencePath, reference);
        string beforeSha256 = Hash(reference);
        Assert.NotEqual(OwnerExpectedSha256, beforeSha256);

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51930",
            WorkbenchIcNumberTokens.CascadeTwoToThirteen,
            CreateSlotPaths(evidence, referencePath),
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(reference[0x100], output[0x100]);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51930-ctrlram-replace-fw130-cascade3");
        Assert.Equal(beforeSha256, Hash(File.ReadAllBytes(referencePath)));
    }

    /// <summary>Proves execution remains bound to the admitted snapshot after the source path disappears.</summary>
    [Fact]
    public async Task ExactRouteExecutesFromCapturedReferenceSnapshotAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51930-fw130-reference-snapshot");
        string referencePath = workspace.Write("reference.bin", evidence.Expected.Bytes);
        string outputPath = workspace.PathFor("snapshot-output.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51930",
            WorkbenchIcNumberTokens.CascadeTwoToThirteen,
            CreateSlotPaths(evidence, referencePath),
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new DeleteReferencePassThroughProcessor(referencePath),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.False(File.Exists(referencePath));
        Assert.True(File.Exists(outputPath));
        Assert.Equal(evidence.Expected.Bytes[0x100], File.ReadAllBytes(outputPath)[0x100]);
    }

    /// <summary>Proves every accepted NT51930 identifier form selects the same exact V2 route.</summary>
    [Theory]
    [InlineData("51930")]
    [InlineData("nt51930")]
    [InlineData(" NT51930 ")]
    public async Task ExactRouteCanonicalizesAcceptedIcIdentifiersAsync(string icId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51930-fw130-canonical-id");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            icId,
            WorkbenchIcNumberTokens.CascadeTwoToThirteen,
            CreateSlotPaths(evidence, evidence.Expected.Path),
            build: true,
            workspace.PathFor("canonical-output.bin"),
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51930-ctrlram-replace-fw130-cascade3");
    }

    private static void AssertOwnerDifferenceClassification(OwnerCase evidence, byte[] output)
    {
        Assert.Equal(4397, CountDifferences(evidence.Expected.Bytes, output));
        Assert.Equal(4, CountDifferences(evidence.Expected.Bytes, output, new ByteRange(0x7100, 4)));
        Assert.Equal(4, CountDifferences(evidence.Expected.Bytes, output, new ByteRange(0x7118, 4)));
        Assert.Equal(0, CountDifferences(evidence.Expected.Bytes, output, new ByteRange(NfStart, NfMaximumLength)));
        Assert.Equal(0, CountDifferences(evidence.Expected.Bytes, output, new ByteRange(NormalStart, NormalLength)));
        Assert.Equal(0, CountDifferences(evidence.Expected.Bytes, output, new ByteRange(MpStart, MpLength)));
        Assert.Equal(0, CountDifferences(evidence.Expected.Bytes, output, new ByteRange(VnStart, VnLength)));
        Assert.Equal(1, CountDifferences(evidence.Expected.Bytes, output, new ByteRange(HeaderCopyStart, HeaderCopyLength)));
        Assert.Equal(4388, CountDifferences(evidence.Expected.Bytes, output, new ByteRange(DiffStart, DiffLength)));
        Assert.Equal(
            0,
            CountDifferencesOutside(
                evidence.Expected.Bytes,
                output,
                [
                    new ByteRange(0x7100, 4),
                    new ByteRange(0x7118, 4),
                    new ByteRange(HeaderCopyStart, HeaderCopyLength),
                    new ByteRange(DiffStart, DiffLength),
                ]));
    }

    private static void AssertPhysicalInputProjection(OwnerCase evidence, byte[] output)
    {
        OwnerArtifact nf = evidence.Require("NF_Ctrlram.bin");
        OwnerArtifact normal = evidence.Require("Normal_Ctrlram.bin");
        OwnerArtifact mp = evidence.Require("MP_Ctrlram.bin");
        OwnerArtifact vn = evidence.Require("VN_Ctrlram.bin");
        OwnerArtifact diff = evidence.Require("DiffDLM.bin");
        Assert.Equal(nf.Bytes, output.AsSpan(NfStart, nf.Bytes.Length).ToArray());
        Assert.Equal(normal.Bytes.AsSpan(0, NormalLength).ToArray(), output.AsSpan(NormalStart, NormalLength).ToArray());
        Assert.Equal(mp.Bytes.AsSpan(0, MpLength).ToArray(), output.AsSpan(MpStart, MpLength).ToArray());
        Assert.Equal(vn.Bytes.AsSpan(0, VnLength).ToArray(), output.AsSpan(VnStart, VnLength).ToArray());
        Assert.Equal(output.AsSpan(0x7000, HeaderCopyLength).ToArray(), output.AsSpan(HeaderCopyStart, HeaderCopyLength).ToArray());
        Assert.Equal(4087, CountDifferences(diff.Bytes.AsSpan(0, DiffLength), output.AsSpan(DiffStart, DiffLength)));
    }

    private static void AssertProcessEvidence(JsonElement report, string suppliedToolSha256)
    {
        JsonElement session = Assert.Single(ReadProcessorSessions(report));
        string executable = Assert.Single(session.GetProperty("ExecutedCommands").EnumerateArray())
            .GetProperty("ExecutablePath").GetString()!;
        Assert.Equal([ExpectedArguments()], ReadNormalizedArguments([session]));
        AssertProcessorIdentity(session);
        Assert.Equal([new ByteRange(0, FullFlashCapacity)], ReadRanges(session, "ProcessorAllowedReadRanges"));
        Assert.Equal(ExpectedV2WriteRanges(), ReadRanges(session, "ProcessorAllowedWriteRanges"));
        Assert.Equal(RegisteredCombinerSha256, Hash(File.ReadAllBytes(executable)));
        Assert.NotEqual(suppliedToolSha256, Hash(File.ReadAllBytes(executable)));
    }

    private static string[] ExpectedArguments()
    {
        return [
            "NT51930BASED_NORMAL_MODE", "CRC8",
            "output/nt51930_fw.bin", "output/nt51930_fw.bin",
            "BIN/NF_Ctrlram.bin", "0x0", "0x1FC00", "6736",
            "BIN/Normal_Ctrlram.bin", "0x0", "0x21650", "11264",
            "BIN/MP_Ctrlram.bin", "0x0", "0x24250", "13312",
            "BIN/VN_Ctrlram.bin", "0x0", "0x27650", "6494",
            "output/nt51930_fw.bin", "0x7000", "0x28FB0", "256",
            "BIN/DiffDLM.bin", "0x0", "0x2F200", "65024",
        ];
    }

    private static ByteRange[] ExpectedV2WriteRanges()
    {
        return [
            new(0x7100, 4),
            new(0x7118, 4),
            new(NfStart, 577),
            new(NormalStart, NormalLength),
            new(MpStart, MpLength),
            new(VnStart, VnLength),
            new(HeaderCopyStart, HeaderCopyLength),
            new(DiffStart, DiffLength),
        ];
    }

    private static JsonElement[] ReadProcessorSessions(JsonElement report)
    {
        return [
            .. report.GetProperty("Operations").EnumerateArray().Where(operation =>
                StringComparer.Ordinal.Equals(operation.GetProperty("Kind").GetString(), "RunExternalProcessor")),
        ];
    }

    private static void AssertProcessorIdentity(JsonElement session)
    {
        Assert.Equal("nfc.nt51930.ctrlram-postbuild-fw1.x", session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
    }

    private static string[][] ReadNormalizedArguments(IEnumerable<JsonElement> sessions)
    {
        return [
            .. sessions.SelectMany(session => session.GetProperty("ExecutedCommands").EnumerateArray())
                .Select(command =>
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
                .Select(range => new ByteRange(
                    range.GetProperty("Start").GetInt64(),
                    range.GetProperty("Length").GetInt64()))
                .OrderBy(static range => range.Start),
        ];
    }

    private static void AssertReportIdentity(JsonElement report, string profileId)
    {
        Assert.Equal(profileId, report.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51930", report.GetProperty("IcId").GetString());
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
            ["replace-ctrlram-mp"] = evidence.Require("MP_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = evidence.Require("VN_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = evidence.Require("NF_Ctrlram.bin").Path,
        };
    }

    private static void AssertBatProvenance(string path)
    {
        string text = File.ReadAllText(path);
        int cascadeLabel = text.IndexOf(":NT51930_Cascade_Postbuild", StringComparison.Ordinal);
        int insertSid = text.IndexOf("@python output\\InsertSID.py output\\nt51930_fw.bin", cascadeLabel, StringComparison.Ordinal);
        int combiner = text.IndexOf(
            "@output\\Combiner.exe NT51930BASED_NORMAL_MODE CRC8 output\\nt51930_fw.bin output\\nt51930_fw.bin " +
            "BIN\\NF_Ctrlram.bin 0x0 0x1FC00 6736 BIN\\Normal_Ctrlram.bin 0x0 0x21650 11264 " +
            "BIN\\MP_Ctrlram.bin 0x0 0x24250 13312 BIN\\VN_Ctrlram.bin 0x0 0x27650 6494 " +
            "output\\nt51930_fw.bin 0x7000 0x28FB0 256 BIN\\DiffDLM.bin 0x0 0x2F200 65024",
            cascadeLabel,
            StringComparison.Ordinal);
        Assert.True(cascadeLabel >= 0);
        Assert.True(insertSid > cascadeLabel);
        Assert.True(combiner > insertSid);
    }

    private static OwnerCase ReadOwnerCase()
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        JsonElement caseElement = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId);
        OwnerArtifact[] artifacts = [
            .. caseElement.GetProperty("artifacts")
                .EnumerateArray()
                .Select(candidate => ReadArtifact(goldenRoot, candidate)),
        ];
        OwnerArtifact expected = artifacts.Single(static artifact =>
            StringComparer.Ordinal.Equals(artifact.Role, "expected-final-output"));
        OwnerArtifact bat = artifacts.Single(static artifact =>
            StringComparer.Ordinal.Equals(artifact.Role, "postbuild-command-evidence"));
        JsonElement toolEntry = Assert.Single(caseElement.GetProperty("externalToolObservations").EnumerateArray());
        return new OwnerCase(
            artifacts,
            artifacts.Single(static artifact => StringComparer.Ordinal.Equals(artifact.Role, "standard-merge-dp-input")),
            artifacts.Single(static artifact => StringComparer.Ordinal.Equals(artifact.Role, "standard-merge-tp-input")),
            expected,
            bat,
            new OwnerToolObservation(
                toolEntry.GetProperty("sha256").GetString()!,
                toolEntry.GetProperty("repositoryRegistration").GetString()!,
                toolEntry.GetProperty("executionAuthorized").GetBoolean()));
    }

    private static OwnerArtifact ReadArtifact(string goldenRoot, JsonElement entry)
    {
        string path = RepositoryPaths.ManifestPath(goldenRoot, entry);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        return new OwnerArtifact(
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
            if (expected[index] != actual[index])
            {
                count++;
            }
        }

        return count;
    }

    private static long CountDifferencesOutside(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        IReadOnlyList<ByteRange> allowedRanges)
    {
        long count = 0;
        for (int index = 0; index < Math.Min(expected.Length, actual.Length); index++)
        {
            if (expected[index] != actual[index] && !allowedRanges.Any(range => range.Contains(index)))
            {
                count++;
            }
        }

        return count + Math.Abs(expected.Length - actual.Length);
    }

    private static (long DifferenceCount, ByteRange[] Ranges) FindDifferences(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual)
    {
        List<ByteRange> ranges = [];
        long differenceCount = 0;
        int index = 0;
        int commonLength = Math.Min(expected.Length, actual.Length);
        while (index < commonLength)
        {
            if (expected[index] == actual[index])
            {
                index++;
                continue;
            }

            int start = index++;
            differenceCount++;
            while (index < commonLength && expected[index] != actual[index])
            {
                index++;
                differenceCount++;
            }

            ranges.Add(new ByteRange(start, index - start));
        }

        differenceCount += Math.Abs(expected.Length - actual.Length);
        if (expected.Length != actual.Length)
        {
            ranges.Add(new ByteRange(commonLength, Math.Abs(expected.Length - actual.Length)));
        }

        return (differenceCount, [.. ranges]);
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

    private sealed class DeleteReferencePassThroughProcessor(string referencePath) : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            File.Delete(referencePath);
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, []));
        }
    }

    private sealed record OwnerArtifact(
        string FileName,
        string RelativePath,
        string Role,
        string Path,
        byte[] Bytes);

    private sealed record OwnerToolObservation(
        string Sha256,
        string RepositoryRegistration,
        bool ExecutionAuthorized);

    private sealed record OwnerCase(
        IReadOnlyList<OwnerArtifact> Artifacts,
        OwnerArtifact Dp,
        OwnerArtifact Tp,
        OwnerArtifact Expected,
        OwnerArtifact PostbuildBat,
        OwnerToolObservation SuppliedTool)
    {
        public OwnerArtifact Require(string fileName)
        {
            return Artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.FileName, fileName));
        }
    }
}
