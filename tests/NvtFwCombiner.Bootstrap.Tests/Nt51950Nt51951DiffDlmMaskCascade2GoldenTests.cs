using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Owner-approved 950-family exact-2-IC DiffDLM preservation regression.</summary>
public sealed class Nt51950Nt51951DiffDlmMaskCascade2GoldenTests
{
    private const string CaseId = "nt51951-fw200-cascade2-auto-prj-599-20260731";
    private const int TpOverlayStart = 0x0A000;
    private const int TpOverlayEndExclusive = 0x37000;
    private const int DiffStart = 0x33200;
    private const int RecordStride = 0x1400;
    private const int DlmLength = 0x0910;
    private const int DiffNfLength = 0x0AF0;
    private const int BackupStart = 0x36000;
    private const int FirmwareConfigLength = 0x0780;

    /// <summary>The direct fixture independently locks reconstruction, preservation, and golden-only dummy quality.</summary>
    [Fact]
    public void OwnerFixtureLocksExactTwoIcGeometryAndCompleteExpectedBytes()
    {
        OwnerCase evidence = ReadOwnerCase();
        byte[] reference = ReconstructReference(evidence);

        Assert.Equal(evidence.Expected.Bytes, reference);
        Assert.Equal(evidence.Expected.Sha256, Hash(reference));
        Assert.Equal(2 * RecordStride, evidence.DiffDlm.Bytes.Length);
        Assert.False(IsUniformFill(evidence.DiffDlm.Bytes.AsSpan(0, DlmLength), 0x00));
        Assert.False(IsUniformFill(evidence.DiffDlm.Bytes.AsSpan(0, DlmLength), 0xFF));
        Assert.True(IsUniformFill(evidence.DiffDlm.Bytes.AsSpan(RecordStride, RecordStride), 0xFF));
        Assert.Equal(
            evidence.NfDiffActive.Bytes,
            evidence.DiffDlm.Bytes.AsSpan(DlmLength, DiffNfLength).ToArray());
        Assert.Equal(
            evidence.DiffDlm.Bytes.AsSpan(0, DlmLength).ToArray(),
            evidence.Expected.Bytes.AsSpan(DiffStart, DlmLength).ToArray());
        Assert.Equal(
            reference.AsSpan(DiffStart + DlmLength, DiffNfLength).ToArray(),
            evidence.Expected.Bytes.AsSpan(DiffStart + DlmLength, DiffNfLength).ToArray());
        Assert.NotEqual(
            evidence.DiffDlm.Bytes.AsSpan(DlmLength, DiffNfLength).ToArray(),
            evidence.Expected.Bytes.AsSpan(DiffStart + DlmLength, DiffNfLength).ToArray());
        Assert.Equal(
            reference.AsSpan(DiffStart + RecordStride, RecordStride).ToArray(),
            evidence.Expected.Bytes.AsSpan(DiffStart + RecordStride, RecordStride).ToArray());
        Assert.NotEqual(
            evidence.DiffDlm.Bytes.AsSpan(RecordStride, RecordStride).ToArray(),
            evidence.Expected.Bytes.AsSpan(DiffStart + RecordStride, RecordStride).ToArray());
        Assert.Equal(
            evidence.Expected.Bytes.AsSpan(0x22200, FirmwareConfigLength).ToArray(),
            evidence.Expected.Bytes.AsSpan(BackupStart, FirmwareConfigLength).ToArray());
        Assert.Equal([0x00, 0x4E, 0x56, 0x54], evidence.Expected.Bytes.AsSpan(0x36FFC, 4).ToArray());
    }

    /// <summary>The production route reproduces the complete direct expected output without an independent NF input.</summary>
    [Fact]
    public async Task PassThroughPostbuildReproducesCompleteOwnerExpectedAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-owner-golden");
        string referencePath = workspace.Write("reference.bin", ReconstructReference(evidence));
        string outputPath = workspace.PathFor("output.bin");
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await RunAsync(
            evidence,
            referencePath,
            evidence.DiffDlm.Path,
            outputPath,
            processor);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(1, processor.CallCount);
        Assert.Equal(evidence.Expected.Bytes, File.ReadAllBytes(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal("nt51951-ctrlram-replace-fw1x-cascade", report.RootElement.GetProperty("ProfileId").GetString());
    }

    /// <summary>A nonzero active-prefix delta is applied while the adjacent DiffNF and later target record remain immutable.</summary>
    [Fact]
    public async Task NonzeroActivePrefixWritesOnlyDeclaredDlmAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-active-prefix");
        byte[] reference = ReconstructReference(evidence);
        byte[] diff = [.. evidence.DiffDlm.Bytes];
        diff[0x20] ^= 0x01;
        string referencePath = workspace.Write("reference.bin", reference);
        string diffPath = workspace.Write("DiffDLM.bin", diff);
        string outputPath = workspace.PathFor("output.bin");

        WorkbenchRunResult result = await RunAsync(
            evidence,
            referencePath,
            diffPath,
            outputPath,
            new CountingPassThroughProcessor());

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(diff.AsSpan(0, DlmLength).ToArray(), output.AsSpan(DiffStart, DlmLength).ToArray());
        Assert.Equal(
            reference.AsSpan(DiffStart + DlmLength, DiffNfLength).ToArray(),
            output.AsSpan(DiffStart + DlmLength, DiffNfLength).ToArray());
        Assert.Equal(
            reference.AsSpan(DiffStart + RecordStride, RecordStride).ToArray(),
            output.AsSpan(DiffStart + RecordStride, RecordStride).ToArray());
        Assert.Equal(1, CountDifferences(reference, output));
    }

    /// <summary>Mixed inactive source bytes are ignored by production; uniform fill is a golden-fixture rule only.</summary>
    [Fact]
    public async Task MixedInactiveSourceRecordDoesNotGainRuntimeAuthorityAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-inactive-source");
        byte[] diff = [.. evidence.DiffDlm.Bytes];
        diff[RecordStride + 7] = 0x00;
        string referencePath = workspace.Write("reference.bin", ReconstructReference(evidence));
        string diffPath = workspace.Write("DiffDLM.bin", diff);
        string outputPath = workspace.PathFor("output.bin");

        WorkbenchRunResult result = await RunAsync(
            evidence,
            referencePath,
            diffPath,
            outputPath,
            new CountingPassThroughProcessor());

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(evidence.Expected.Bytes, File.ReadAllBytes(outputPath));
    }

    /// <summary>A complete active source record is mandatory even though only its DLM prefix is writable.</summary>
    [Fact]
    public async Task TruncatedActiveRecordBlocksBeforeProcessorInvocationAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-truncated-active");
        string referencePath = workspace.Write("reference.bin", ReconstructReference(evidence));
        string diffPath = workspace.Write(
            "DiffDLM.bin",
            evidence.DiffDlm.Bytes.AsSpan(0, RecordStride - 1).ToArray());
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await RunAsync(
            evidence,
            referencePath,
            diffPath,
            workspace.PathFor("must-not-exist.bin"),
            processor);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
    }

    /// <summary>A uniform active DLM slice is a blocking placeholder, independent of inactive-record fill.</summary>
    [Fact]
    public async Task UniformActiveDlmBlocksBeforeProcessorInvocationAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-uniform-active");
        byte[] diff = [.. evidence.DiffDlm.Bytes];
        diff.AsSpan(0, DlmLength).Fill(0xFF);
        string referencePath = workspace.Write("reference.bin", ReconstructReference(evidence));
        string diffPath = workspace.Write("DiffDLM.bin", diff);
        var processor = new CountingPassThroughProcessor();

        WorkbenchRunResult result = await RunAsync(
            evidence,
            referencePath,
            diffPath,
            workspace.PathFor("must-not-exist.bin"),
            processor);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmPlaceholder);
    }

    /// <summary>A stale independent NF binding is rejected by the shared Application/CLI contract.</summary>
    [Fact]
    public async Task StaleIndependentNfBindingBlocksBeforeProcessorInvocationAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-stale-nf");
        string referencePath = workspace.Write("reference.bin", ReconstructReference(evidence));
        var processor = new CountingPassThroughProcessor();
        Dictionary<string, string> slots = CreateSlotPaths(evidence, referencePath, evidence.DiffDlm.Path);
        slots[WorkbenchSlotIds.CreateReplaceCtrlRam("nf")] = evidence.IndependentNf.Path;

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51951", "cascade", slots, true, workspace.PathFor("must-not-exist.bin"), null,
            processor, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
    }

    /// <summary>Processor mutation of the first inactive target record fails the exact staged-diff audit.</summary>
    [Fact]
    public async Task InactiveTargetMutationFailsClosedAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-inactive-target");
        string referencePath = workspace.Write("reference.bin", ReconstructReference(evidence));

        WorkbenchRunResult result = await RunAsync(
            evidence,
            referencePath,
            evidence.DiffDlm.Path,
            workspace.PathFor("must-not-exist.bin"),
            new UnauthorizedMutationProcessor(DiffStart + RecordStride));

        Assert.False(result.Succeeded, result.ReportJson);
    }

    /// <summary>Processor mutation of the active record's reference-owned DiffNF tail fails closed.</summary>
    [Fact]
    public async Task ActiveDiffNfTailMutationFailsClosedAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-active-diffnf-tail");
        string referencePath = workspace.Write("reference.bin", ReconstructReference(evidence));

        WorkbenchRunResult result = await RunAsync(
            evidence,
            referencePath,
            evidence.DiffDlm.Path,
            workspace.PathFor("must-not-exist.bin"),
            new UnauthorizedMutationProcessor(DiffStart + DlmLength));

        Assert.False(result.Succeeded, result.ReportJson);
    }

    /// <summary>A coherent marker-derived Backup at any address other than fixed 0x36000 fails closed.</summary>
    [Fact]
    public async Task WrongFixedFirmwareConfigBackupPlacementFailsClosedAsync()
    {
        const int wrongBackupStart = BackupStart - 0x1000;
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-wrong-fixed-backup");
        byte[] reference = ReconstructReference(evidence);
        reference.AsSpan(BackupStart, 0x1000).CopyTo(reference.AsSpan(wrongBackupStart, 0x1000));
        reference.AsSpan(BackupStart + 0x0FFC, 4).Clear();
        string referencePath = workspace.Write("reference.bin", reference);

        WorkbenchRunResult result = await RunAsync(
            evidence,
            referencePath,
            evidence.DiffDlm.Path,
            workspace.PathFor("must-not-exist.bin"),
            new CountingPassThroughProcessor());

        Assert.False(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() ==
                     WorkbenchIssueCodes.ReplaceCtrlRamFirmwareConfigBackupPlacementInvalid);
    }

    /// <summary>The registered, hash-pinned Combiner differs from the owner tool only in the four approved CRC words.</summary>
    [Fact]
    public async Task RegisteredCombinerDiffersOnlyInApprovedCrcWordsAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51951-cascade2-real-combiner");
        string referencePath = workspace.Write("reference.bin", ReconstructReference(evidence));
        string outputPath = workspace.PathFor("output.bin");

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunReplaceAsync(
            "NT51951", "cascade", WorkbenchReplaceModes.CtrlRam,
            CreateSlotPaths(evidence, referencePath, evidence.DiffDlm.Path),
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = File.ReadAllBytes(outputPath);
        (long differenceCount, ByteRange[] differenceRanges) = FindDifferences(
            evidence.Expected.Bytes,
            output);
        Assert.Equal(16, differenceCount);
        Assert.Equal(
            [
                new ByteRange(0xA11C, 4),
                new ByteRange(0xA130, 4),
                new ByteRange(0x2D428, 4),
                new ByteRange(0x2D43C, 4),
            ],
            differenceRanges);
        Assert.Equal("1536d344af83aafd29e5884d9d2d904f1efa03c8fdcc4e913832253814644ebd", Hash(output));
    }

    private static async Task<WorkbenchRunResult> RunAsync(
        OwnerCase evidence,
        string referencePath,
        string diffPath,
        string outputPath,
        IExternalProcessor processor)
    {
        return await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51951", "cascade", CreateSlotPaths(evidence, referencePath, diffPath),
            true, outputPath, null, processor, TestContext.Current.CancellationToken);
    }

    private static Dictionary<string, string> CreateSlotPaths(
        OwnerCase evidence,
        string referencePath,
        string diffPath)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath,
            [WorkbenchSlotIds.CreateReplaceCtrlRam("normal")] = evidence.Normal.Path,
            [WorkbenchSlotIds.CreateReplaceCtrlRam("vn")] = evidence.Vn.Path,
            [WorkbenchSlotIds.CreateReplaceCtrlRam("diff")] = diffPath,
        };
    }

    private static byte[] ReconstructReference(OwnerCase evidence)
    {
        byte[] reference = [.. evidence.Initial.Bytes];
        evidence.TpFirmware.Bytes.AsSpan(TpOverlayStart, TpOverlayEndExclusive - TpOverlayStart)
            .CopyTo(reference.AsSpan(TpOverlayStart, TpOverlayEndExclusive - TpOverlayStart));
        return reference;
    }

    private static OwnerCase ReadOwnerCase()
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId);
        OwnerArtifact[] artifacts =
        [
            .. goldenCase.GetProperty("artifacts").EnumerateArray().Select(ReadArtifact),
        ];
        OwnerArtifact Require(string artifactId)
        {
            return artifacts.Single(artifact => artifact.ArtifactId == artifactId);
        }
        return new OwnerCase(
            Require("initial-code-input"),
            Require("tp-firmware-input"),
            Require("normal-ctrlram-input"),
            Require("vn-ctrlram-input"),
            Require("diffdlm-input"),
            Require("independent-nf-excluded-reference"),
            Require("nf-diff-active-reference"),
            Require("expected-output"));
    }

    private static OwnerArtifact ReadArtifact(JsonElement entry)
    {
        string path = CanonicalGoldenTestData.ArtifactPath(entry);
        return new OwnerArtifact(
            entry.GetProperty("artifactId").GetString()!,
            path,
            File.ReadAllBytes(path),
            entry.GetProperty("sha256").GetString()!);
    }

    private static bool IsUniformFill(ReadOnlySpan<byte> bytes, byte fill)
    {
        return bytes.IndexOfAnyExcept(fill) < 0;
    }

    private static int CountDifferences(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        Assert.Equal(left.Length, right.Length);
        int count = 0;
        for (int index = 0; index < left.Length; index++)
        {
            count += left[index] == right[index] ? 0 : 1;
        }

        return count;
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
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

    private sealed record OwnerArtifact(string ArtifactId, string Path, byte[] Bytes, string Sha256);

    private sealed record OwnerCase(
        OwnerArtifact Initial,
        OwnerArtifact TpFirmware,
        OwnerArtifact Normal,
        OwnerArtifact Vn,
        OwnerArtifact DiffDlm,
        OwnerArtifact IndependentNf,
        OwnerArtifact NfDiffActive,
        OwnerArtifact Expected);

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
