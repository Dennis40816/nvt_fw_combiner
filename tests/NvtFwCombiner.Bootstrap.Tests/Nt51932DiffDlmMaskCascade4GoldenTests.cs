using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Owner-approved NT51932 cascade-4 regression for the full-stride DiffDLM NF-preservation hot-fix.</summary>
public sealed class Nt51932DiffDlmMaskCascade4GoldenTests
{
    private const string CaseId = "nt51932-diffdlm-nf-mask-cascade4-20260727";
    private const string ExpectedSha256 = "a75c900c912e2d46ddbbbad199516d75c37adb9deb6ce1a749e1d7ca99e922a8";
    private const int DiffStart = 0x2D100;
    private const int RecordStride = 0x1400;
    private const int DlmLength = 0x0B90;
    private const int ExpectedBackupStart = 0x31000;

    /// <summary>Combiner output matches the owner image outside the already-authorized CRC/header words.</summary>
    [Fact]
    public async Task RealCombinerBuildDiffersFromOwnerExpectedOnlyAtPostbuildCrcWordsAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-mask-cascade4-real");
        string outputPath = workspace.PathFor("nt51932_fw_0723.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            WorkbenchReplaceModes.CtrlRam,
            CreateSlotPaths(evidence),
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(ExpectedSha256, Hash(evidence.Expected.Bytes));
        Assert.InRange(AssertOnlyPostbuildCrcDiffers(evidence.Expected.Bytes, output), 0, 40);
    }

    /// <summary>Mapping only DLM preserves March DiffNF bytes and differs from Andes output only at postbuild CRC/header bytes.</summary>
    [Fact]
    public async Task MappingCopiesThreeDlmRecordsAndPreservesEachNfTailAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-mask-cascade4-contract");
        string outputPath = workspace.PathFor("pass-through.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence),
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(40, AssertOnlyPostbuildCrcDiffers(evidence.Expected.Bytes, output));
        AssertDlmMapsAndNfMatchesBase(evidence, evidence.Base.Bytes, output);
        for (int record = 0; record < 3; record++)
        {
            int targetStart = DiffStart + (record * RecordStride);
            Assert.Equal(
                evidence.Base.Bytes.AsSpan(targetStart + DlmLength, RecordStride - DlmLength).ToArray(),
                evidence.Expected.Bytes.AsSpan(targetStart + DlmLength, RecordStride - DlmLength).ToArray());
        }

        using var report = JsonDocument.Parse(result.ReportJson);
        ByteRange[] allowedWrites = [
            .. report.RootElement.GetProperty("Operations").EnumerateArray()
                .Single(operation => operation.GetProperty("Kind").GetString() == "RunExternalProcessor")
                .GetProperty("ProcessorAllowedWriteRanges").EnumerateArray()
                .Select(range => new ByteRange(range.GetProperty("Start").GetInt64(), range.GetProperty("Length").GetInt64())),
        ];
        for (int record = 0; record < 3; record++)
        {
            Assert.DoesNotContain(
                allowedWrites,
                range => range.Overlaps(new ByteRange(DiffStart + (record * RecordStride) + DlmLength, RecordStride - DlmLength)));
        }
    }

    /// <summary>Using Andes 0723 as the immutable base leaves its three DiffNF tails unchanged.</summary>
    [Fact]
    public async Task Andes0723BaseRetainsAndesDiffNfTailsAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-mask-cascade4-andes-base");
        string outputPath = workspace.PathFor("pass-through-andes-base.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, basePath: evidence.Expected.Path),
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(evidence.Expected.Bytes, output);
        AssertDlmMapsAndNfMatchesBase(evidence, evidence.Expected.Bytes, output);
    }

    /// <summary>The third active DLM range is independently validated and cannot be a uniform placeholder.</summary>
    [Fact]
    public async Task UniformThirdDlmRecordFailsBeforeProcessorInvocationAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        byte[] diff = [.. evidence.DiffDlm.Bytes];
        diff.AsSpan(2 * RecordStride, DlmLength).Fill(0xFF);
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-mask-cascade4-placeholder");
        string diffPath = workspace.Write("DiffDLM.bin", diff);
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, diffPath),
            build: true,
            workspace.PathFor("must-not-exist.bin"),
            firmwareVersionEdit: null,
            processor,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() ==
                WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmPlaceholder);
    }

    /// <summary>All three active AE records must be complete 0x1400-byte records, not only long enough for the last DLM prefix.</summary>
    [Theory]
    [InlineData(0x338F)]
    [InlineData(0x3390)]
    [InlineData(0x3BFF)]
    public async Task TruncatedActiveRecordPrefixFailsBeforeProcessorInvocationAsync(int sourceLength)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-truncated-active-record");
        string diffPath = workspace.Write(
            "DiffDLM.bin",
            evidence.DiffDlm.Bytes.AsSpan(0, sourceLength).ToArray());
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, diffPath),
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            processor,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue =>
                issue.GetProperty("Code").GetString() ==
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch &&
                issue.GetProperty("Message").GetString()!.Contains(
                    "complete active records require 0x3C00 bytes",
                    StringComparison.Ordinal));
    }

    /// <summary>The exact three-record active prefix is sufficient; bytes after it have no replacement authority.</summary>
    [Fact]
    public async Task ExactActiveRecordPrefixIsAcceptedAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-exact-active-records");
        string diffPath = workspace.Write(
            "DiffDLM.bin",
            evidence.DiffDlm.Bytes.AsSpan(0, 3 * RecordStride).ToArray());
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, diffPath),
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            processor,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(1, processor.CallCount);
    }

    /// <summary>Dynamic DiffDLM cannot invent a minimum topology when FWConfig Chip_Num is zero.</summary>
    [Fact]
    public async Task ZeroFirmwareConfigChipCountBlocksBeforeProcessorInvocationAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        byte[] baseBytes = [.. evidence.Base.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(baseBytes, out FirmwareConfigMetadata metadata));
        baseBytes[checked((int)metadata.StructureStart + FirmwareConfigLayout.ChipNumberOffset)] = 0;
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-zero-chip-count");
        string basePath = workspace.Write("zero-chip-count.bin", baseBytes);
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, basePath: basePath),
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            processor,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            candidate => candidate.GetProperty("Code").GetString() ==
                FirmwareConfigChipCountDiagnostics.RequiredIssueCode);
        Assert.Equal("error", issue.GetProperty("Severity").GetString());
        Assert.Contains("offset 0x17 is 0", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
    }

    /// <summary>An explicit 4-IC selection cannot silently adopt a different count merely because both fit the 2–8 family range.</summary>
    [Fact]
    public async Task ExplicitCountMismatchFailsBeforeProcessorInvocationAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        byte[] baseBytes = [.. evidence.Base.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(baseBytes, out FirmwareConfigMetadata metadata));
        baseBytes[checked((int)metadata.StructureStart + FirmwareConfigLayout.ChipNumberOffset)] = 5;
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-count-mismatch");
        string basePath = workspace.Write("five-chip-count.bin", baseBytes);
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            "4",
            CreateSlotPaths(evidence, basePath: basePath),
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            processor,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            candidate => candidate.GetProperty("Code").GetString() ==
                WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch);
        Assert.Contains("Selected Number is 4 IC", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
        Assert.Contains("reports 5 IC", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
    }

    /// <summary>A stale hidden NF selection fails closed and never feeds the postbuild-owned NF stage.</summary>
    [Fact]
    public async Task StaleIndependentNfSelectionFailsBeforeProcessorInvocationAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        Dictionary<string, string> slotPaths = CreateSlotPaths(evidence);
        slotPaths[WorkbenchSlotIds.CreateReplaceCtrlRam("nf")] = evidence.Normal.Path;
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-stale-nf");
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            slotPaths,
            build: true,
            workspace.PathFor("must-not-exist.bin"),
            firmwareVersionEdit: null,
            processor,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() ==
                     WorkbenchIssueCodes.ReplaceCtrlRamSourceUnavailable);
    }

    /// <summary>An authorized postbuild placement mismatch remains visible but does not block the build.</summary>
    [Fact]
    public async Task InAuthorityFirmwareConfigBackupPlacementMismatchIsWarningOnlyAsync()
    {
        WorkbenchRunResult result = await RunWithMarkerPlacementAsync(
            replacementBackupStart: 0x32000,
            preserveExistingMarker: false);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() ==
                     WorkbenchIssueCodes.ReplaceCtrlRamFirmwareConfigBackupPlacementUnexpected);
        Assert.Equal("warning", issue.GetProperty("Severity").GetString());
    }

    /// <summary>A marker-derived Backup envelope outside count-resolved authority fails closed.</summary>
    [Fact]
    public async Task OutOfAuthorityFirmwareConfigBackupPlacementFailsClosedAsync()
    {
        WorkbenchRunResult result = await RunWithMarkerPlacementAsync(
            replacementBackupStart: 0x30000,
            preserveExistingMarker: false);

        Assert.False(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() ==
                     WorkbenchIssueCodes.ReplaceCtrlRamFirmwareConfigBackupPlacementInvalid);
    }

    /// <summary>A second readable NVT marker is ambiguous and fails the same placement authority contract.</summary>
    [Fact]
    public async Task AmbiguousFirmwareConfigBackupPlacementFailsClosedAsync()
    {
        WorkbenchRunResult result = await RunWithMarkerPlacementAsync(
            replacementBackupStart: 0x32000,
            preserveExistingMarker: true);

        Assert.False(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() ==
                     WorkbenchIssueCodes.ReplaceCtrlRamFirmwareConfigBackupPlacementInvalid);
    }

    /// <summary>A missing canonical NVT marker fails the Dynamic DiffDLM placement postcondition.</summary>
    [Fact]
    public async Task MissingFirmwareConfigBackupPlacementFailsClosedAsync()
    {
        WorkbenchRunResult result = await RunWithMarkerPlacementAsync(
            replacementBackupStart: null,
            preserveExistingMarker: false);

        Assert.False(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() ==
                     WorkbenchIssueCodes.ReplaceCtrlRamFirmwareConfigBackupPlacementInvalid);
    }

    /// <summary>Broad placement-candidate authority never permits unrelated inactive-record or extension writes.</summary>
    [Theory]
    [InlineData(0x30D00)] // inactive DLM before the current Backup envelope
    [InlineData(0x32000)] // inactive DiffNF immediately after the current Backup envelope
    [InlineData(0x32100)] // next inactive DLM
    [InlineData(0x35D00)] // upper placement-extension padding
    public async Task UnrelatedInactiveMutationInsideCandidateAuthorityFailsClosedAsync(int mutationOffset)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-dynamic-fwconfig-inactive-mutation");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, basePath: evidence.Expected.Path),
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            new UnauthorizedMutationProcessor(mutationOffset),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            candidate => candidate.GetProperty("Code").GetString() ==
                WorkbenchIssueCodes.ReplaceCtrlRamDynamicDiffDlmInactiveMutation);
        Assert.Contains($"0x{mutationOffset:X}", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
    }

    /// <summary>The pre-fix contiguous full-record route is executable only as a rejected corruption-class oracle.</summary>
    [Fact]
    public async Task HistoricalUnmaskedRouteReproducesDocumented3145ByteCorruptionClassAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-known-bad-oracle");
        string outputPath = workspace.PathFor("historical-unmasked-output.bin");
        byte[] prePostbuild = [.. evidence.Base.Bytes];
        evidence.DiffDlm.Bytes.AsSpan(0, 3 * RecordStride).CopyTo(
            prePostbuild.AsSpan(DiffStart, 3 * RecordStride));
        string prePostbuildPath = workspace.Write("historical-unmasked-base.bin", prePostbuild);

        WorkbenchRunResult historical = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            WorkbenchReplaceModes.CtrlRam,
            CreateSlotPaths(evidence, basePath: prePostbuildPath),
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(historical.Succeeded, historical.ReportJson);
        byte[] rejected = File.ReadAllBytes(outputPath);

        Assert.Equal(evidence.KnownBadSize, rejected.LongLength);
        Assert.Equal(evidence.KnownBadSha256, Hash(rejected));
        Assert.Equal(
            evidence.KnownBadDifferenceBytes,
            rejected.Where((value, index) => value != evidence.Expected.Bytes[index]).Count());
        for (int record = 0; record < 3; record++)
        {
            int nfStart = DiffStart + (record * RecordStride) + DlmLength;
            Assert.NotEqual(
                evidence.Base.Bytes.AsSpan(nfStart, RecordStride - DlmLength).ToArray(),
                rejected.AsSpan(nfStart, RecordStride - DlmLength).ToArray());
        }
    }

    private static async Task<WorkbenchRunResult> RunWithMarkerPlacementAsync(
        int? replacementBackupStart,
        bool preserveExistingMarker)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51932-dynamic-fwconfig-placement");
        return await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, basePath: evidence.Expected.Path),
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            new MarkerPlacementProcessor(
                ExpectedBackupStart,
                replacementBackupStart,
                preserveExistingMarker),
            TestContext.Current.CancellationToken);
    }

    private static Dictionary<string, string> CreateSlotPaths(
        OwnerCase evidence,
        string? diffPath = null,
        string? basePath = null)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = basePath ?? evidence.Base.Path,
            [WorkbenchSlotIds.CreateReplaceCtrlRam("normal")] = evidence.Normal.Path,
            [WorkbenchSlotIds.CreateReplaceCtrlRam("diff")] = diffPath ?? evidence.DiffDlm.Path,
        };
    }

    private static void AssertDlmMapsAndNfMatchesBase(
        OwnerCase evidence,
        ReadOnlySpan<byte> expectedNfBase,
        ReadOnlySpan<byte> output)
    {
        for (int record = 0; record < 3; record++)
        {
            int sourceOffset = record * RecordStride;
            int targetStart = DiffStart + sourceOffset;
            Assert.Equal(
                evidence.DiffDlm.Bytes.AsSpan(sourceOffset, DlmLength).ToArray(),
                output.Slice(targetStart, DlmLength).ToArray());
            Assert.Equal(
                expectedNfBase.Slice(targetStart + DlmLength, RecordStride - DlmLength).ToArray(),
                output.Slice(targetStart + DlmLength, RecordStride - DlmLength).ToArray());
        }
    }

    private static int AssertOnlyPostbuildCrcDiffers(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        ByteRange[] postbuildRanges = [
            new(0x7100, 4), new(0x7118, 4), new(0x7128, 0x0C),
            new(0x27FF0, 4), new(0x28008, 4), new(0x28018, 0x0C),
        ];
        Assert.Equal(expected.Length, actual.Length);
        int differenceCount = 0;
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
            {
                differenceCount++;
                Assert.Contains(postbuildRanges, range => range.Contains(index));
            }
        }

        return differenceCount;
    }

    private static OwnerCase ReadOwnerCase()
    {
        string root = CanonicalGoldenTestData.Root;
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId);
        OwnerArtifact[] artifacts = [
            .. goldenCase.GetProperty("artifacts").EnumerateArray().Select(entry => ReadArtifact(root, entry)),
        ];
        JsonElement knownBad = goldenCase
            .GetProperty("hotfixContract")
            .GetProperty("knownBadUnmaskedOutput");
        return new OwnerCase(
            artifacts.Single(artifact => artifact.ArtifactId == "reference-base"),
            artifacts.Single(artifact => artifact.ArtifactId == "normal-ctrlram-input"),
            artifacts.Single(artifact => artifact.ArtifactId == "diffdlm-input"),
            artifacts.Single(artifact => artifact.ArtifactId == "expected-output"),
            knownBad.GetProperty("sha256").GetString()!,
            knownBad.GetProperty("size").GetInt64(),
            knownBad.GetProperty("differenceBytesFromExpected").GetInt32());
    }

    private static OwnerArtifact ReadArtifact(string root, JsonElement entry)
    {
        string path = RepositoryPaths.ManifestPath(root, entry);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        return new(entry.GetProperty("artifactId").GetString()!, path, bytes);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private sealed record OwnerArtifact(string ArtifactId, string Path, byte[] Bytes);

    private sealed record OwnerCase(
        OwnerArtifact Base,
        OwnerArtifact Normal,
        OwnerArtifact DiffDlm,
        OwnerArtifact Expected,
        string KnownBadSha256,
        long KnownBadSize,
        int KnownBadDifferenceBytes);

    private sealed class PassThroughProcessor : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, []));
        }
    }

    private sealed class CountingPassThroughProcessor : IExternalProcessor
    {
        public int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, []));
        }
    }

    private sealed class MarkerPlacementProcessor : IExternalProcessor
    {
        private static readonly byte[] NvtMarker = [0x00, (byte)'N', (byte)'V', (byte)'T'];
        private readonly int _existingBackupStart;
        private readonly int? _replacementBackupStart;
        private readonly bool _preserveExistingMarker;

        internal MarkerPlacementProcessor(
            int existingBackupStart,
            int? replacementBackupStart,
            bool preserveExistingMarker)
        {
            _existingBackupStart = existingBackupStart;
            _replacementBackupStart = replacementBackupStart;
            _preserveExistingMarker = preserveExistingMarker;
        }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            byte[] output = request.InputBytes.ToArray();
            var changedRanges = new List<ByteRange>();
            if (!_preserveExistingMarker)
            {
                int existingMarkerStart = checked(_existingBackupStart + 0x0FFC);
                output.AsSpan(existingMarkerStart, NvtMarker.Length).Clear();
                changedRanges.Add(new ByteRange(existingMarkerStart, NvtMarker.Length));
            }

            if (_replacementBackupStart is { } replacementBackupStart)
            {
                int replacementMarkerStart = checked(replacementBackupStart + 0x0FFC);
                NvtMarker.CopyTo(output.AsSpan(replacementMarkerStart, NvtMarker.Length));
                changedRanges.Add(new ByteRange(replacementMarkerStart, NvtMarker.Length));
            }

            return ValueTask.FromResult(ExternalProcessorResult.Success(output, changedRanges));
        }
    }

    private sealed class UnauthorizedMutationProcessor(int mutationOffset) : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            byte[] output = request.InputBytes.ToArray();
            output[mutationOffset] ^= 0x01;
            return ValueTask.FromResult(ExternalProcessorResult.Success(
                output,
                [new ByteRange(mutationOffset, 1)]));
        }
    }
}
