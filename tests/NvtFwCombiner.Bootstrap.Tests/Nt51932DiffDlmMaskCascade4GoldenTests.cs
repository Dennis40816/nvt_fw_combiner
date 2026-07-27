using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Owner-approved NT51932 cascade-4 regression for the active-record DiffDLM NF-preservation hot-fix.</summary>
public sealed class Nt51932DiffDlmMaskCascade4GoldenTests
{
    private const string CaseId = "nt51932-diffdlm-nf-mask-cascade4-20260727";
    private const string ExpectedSha256 = "a75c900c912e2d46ddbbbad199516d75c37adb9deb6ce1a749e1d7ca99e922a8";
    private const int DiffStart = 0x2D100;
    private const int RecordStride = 0x1400;
    private const int DlmLength = 0x0B90;

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
        JsonElement[] diffMappings =
        [
            .. report.RootElement.GetProperty("Operations").EnumerateArray()
                .Where(operation => operation.GetProperty("OperationId").GetString()!
                    .StartsWith("replace-diff-", StringComparison.Ordinal)),
        ];
        Assert.Equal(3, diffMappings.Length);
        for (int record = 0; record < diffMappings.Length; record++)
        {
            JsonElement mapping = diffMappings[record];
            Assert.Equal("ReplaceRange", mapping.GetProperty("Kind").GetString());
            Assert.Equal(record * RecordStride, mapping.GetProperty("SourceRange").GetProperty("Start").GetInt64());
            Assert.Equal(DlmLength, mapping.GetProperty("SourceRange").GetProperty("Length").GetInt64());
            Assert.Equal(DiffStart + (record * RecordStride), mapping.GetProperty("TargetRange").GetProperty("Start").GetInt64());
            Assert.Equal(DlmLength, mapping.GetProperty("TargetRange").GetProperty("Length").GetInt64());
        }

        JsonElement diffInput = Assert.Single(
            report.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => input.GetProperty("AddressSpaceId").GetString() ==
                WorkbenchSlotIds.CreateReplaceCtrlRam("diff"));
        Assert.Equal(evidence.DiffDlm.Bytes.LongLength, diffInput.GetProperty("Size").GetInt64());
        Assert.Equal(Hash(evidence.DiffDlm.Bytes), diffInput.GetProperty("Sha256").GetString());
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
            issue => issue.GetProperty("Code").GetString() == WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid);
    }

    /// <summary>A symbolic Cascade run fails before execution when FWConfig cannot provide an exact IC Count.</summary>
    [Fact]
    public async Task ZeroFwConfigIcCountFailsBeforeProcessorInvocationAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        byte[] reference = [.. evidence.Base.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        reference[checked((int)metadata.StructureStart) + FirmwareConfigLayout.ChipNumberOffset] = 0;
        using var workspace = TempWorkspace.Create("nfc-nt51932-diffdlm-mask-zero-count");
        string referencePath = workspace.Write("zero-count.bin", reference);
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, basePath: referencePath),
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
                WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch);
    }

    /// <summary>Without a selected DiffDLM, an unreadable IC Count retains the pre-hot-fix Cascade fallback.</summary>
    [Fact]
    public async Task ZeroFwConfigIcCountWithoutDiffDlmRetainsExistingCascadeBehaviorAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        byte[] reference = [.. evidence.Base.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        reference[checked((int)metadata.StructureStart) + FirmwareConfigLayout.ChipNumberOffset] = 0;
        using var workspace = TempWorkspace.Create("nfc-nt51932-no-diff-zero-count");
        string referencePath = workspace.Write("zero-count.bin", reference);
        Dictionary<string, string> slots = CreateSlotPaths(evidence, basePath: referencePath);
        Assert.True(slots.Remove(WorkbenchSlotIds.CreateReplaceCtrlRam("diff")));

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            slots,
            build: true,
            workspace.PathFor("normal-only.bin"),
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
    }

    /// <summary>Base and DiffDLM cannot alias the same physical input identity.</summary>
    [Fact]
    public async Task DiffDlmCannotReuseBasePathAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        var processor = new CountingPassThroughProcessor();
        using var workspace = TempWorkspace.Create("nfc-nt51932-diff-base-alias");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51932",
            WorkbenchIcNumberTokens.CascadeTwoToEight,
            CreateSlotPaths(evidence, diffPath: evidence.Base.Path),
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
                WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid);
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
        return new OwnerCase(
            artifacts.Single(artifact => artifact.ArtifactId == "reference-base"),
            artifacts.Single(artifact => artifact.ArtifactId == "normal-ctrlram-input"),
            artifacts.Single(artifact => artifact.ArtifactId == "diffdlm-input"),
            artifacts.Single(artifact => artifact.ArtifactId == "expected-output"));
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

    private sealed record OwnerCase(OwnerArtifact Base, OwnerArtifact Normal, OwnerArtifact DiffDlm, OwnerArtifact Expected);

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
}
