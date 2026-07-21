using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Exact owner-golden parity for NT51926 Common FW 2.0.0 CtrlRAM Replace.</summary>
public sealed class Nt51926CtrlRamFw200GoldenTests
{
    private const string SingleCaseId = "nt51926-fw200-single-auto-prj-597-20260718";
    private const string CascadeCaseId = "nt51926-fw200-cascade3-auto-prj-597-20260718";

    private const int TpWorkCapacity = 0x3C000;

    /// <summary>Proves the exact V2 routes differ from the owner output only at approved CRC words.</summary>
    [Theory]
    [InlineData(
        SingleCaseId,
        "single",
        1,
        "bf4221635b58a33bff6875aacfb29636aa140354cb5ec5256bf2b0c09e9cc81c",
        "5f8913e48784bf0cdb15e64f3f6376dfe741f261ca6465242ec2956bbfe6c450",
        "nt51926-ctrlram-replace-fw200-runtime-single")]
    [InlineData(
        CascadeCaseId,
        "cascade",
        3,
        "2521192e6a846c8beeb49395e98977d243053efd292b094b31272fff70825825",
        "b4336e3d935466feb98695eb9f5fe8b10c91c632fc835ffcfa1ed7ffefe0495a",
        "nt51926-ctrlram-replace-fw200-runtime-cascade")]
    public async Task V2MatchesLockedOutputWithOwnerApprovedHeaderCrcDiffAsync(
        string caseId,
        string topology,
        byte expectedChipCount,
        string ownerExpectedSha256,
        string currentOutputSha256,
        string v2ProfileId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase(caseId);
        Assert.Equal(ownerExpectedSha256, Hash(evidence.Expected.Bytes));
        var immutableInputHashes = evidence.Artifacts.ToDictionary(
            static pair => pair.Key,
            static pair => Hash(pair.Value.Bytes),
            StringComparer.Ordinal);

        using var workspace = TempWorkspace.Create($"nfc-nt51926-fw200-{topology}");
        string referencePath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult standardMerge = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51926",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = evidence.Dp.Path,
                [CompositionAddressSpaceIds.TpInput] = evidence.Tp.Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            referencePath);

        Assert.True(standardMerge.Succeeded, standardMerge.ReportJson);
        Assert.Equal("nt51926-standard-merge-gen-flash", ReadProfileId(standardMerge.ReportJson));
        byte[] reference = File.ReadAllBytes(referencePath);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        Assert.Equal("2.0.0", metadata.CommonFwVersion);
        Assert.Equal(expectedChipCount, metadata.ChipNumber);
        Assert.Equal(0x1309, metadata.ProjectId);
        Assert.Equal(ownerExpectedSha256, Hash(reference));

        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath,
            ["replace-ctrlram-normal"] = evidence.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-mp"] = evidence.Require("MP_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = evidence.Require("VN_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = evidence.Require("NF_Ctrlram.bin").Path,
        };
        if (StringComparer.Ordinal.Equals(topology, "cascade"))
        {
            slotPaths["replace-ctrlram-diff"] = evidence.Require("DiffDLM.bin").Path;
        }

        string v2OutputPath = workspace.PathFor("v2-output.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            topology,
            WorkbenchReplaceModes.CtrlRam,
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            v2OutputPath);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2OutputPath);
        Assert.Equal(currentOutputSha256, Hash(v2Bytes));
        Assert.Equal(currentOutputSha256, v2.OutputSha256);

        (long ownerDifferenceCount, ByteRange[] ownerDifferenceRanges) = FindDifferences(
            evidence.Expected.Bytes,
            v2Bytes);
        Assert.Equal(16, ownerDifferenceCount);
        Assert.Equal(
            [
                new ByteRange(0x1C, 4),
                new ByteRange(0xFC, 4),
                new ByteRange(0x32A8C, 4),
                new ByteRange(0x32B6C, 4),
            ],
            ownerDifferenceRanges);
        Assert.True(reference.AsSpan(TpWorkCapacity).SequenceEqual(v2Bytes.AsSpan(TpWorkCapacity)));

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, v2ProfileId);
        AssertProcessEvidence(v2Report.RootElement, topology);

        Assert.Equal(ownerExpectedSha256, Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            evidence.Artifacts,
            pair => Assert.Equal(immutableInputHashes[pair.Key], Hash(File.ReadAllBytes(pair.Value.Path))));
    }

    /// <summary>FW2 profile selection remains version-driven while PID and reported count do not gate the requested plan.</summary>
    [Theory]
    [InlineData("cascade", 2, 0x1309)]
    [InlineData("cascade", 3, 0xFFFF)]
    [InlineData("single", 1, 0x1309)]
    public async Task Fw200RouteAcceptsNonAuthoritativeMetadataVariationsAsync(
        string number,
        byte chipCount,
        ushort projectId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase(CascadeCaseId);
        using var workspace = TempWorkspace.Create("nfc-nt51926-fw200-negative-route");
        string referencePath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult standardMerge = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51926",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = evidence.Dp.Path,
                [CompositionAddressSpaceIds.TpInput] = evidence.Tp.Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            referencePath);
        Assert.True(standardMerge.Succeeded, standardMerge.ReportJson);

        byte[] reference = File.ReadAllBytes(referencePath);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int backupStart = checked((int)metadata.StructureStart);
        reference[backupStart + FirmwareConfigLayout.ChipNumberOffset] = chipCount;
        BinaryPrimitives.WriteUInt16LittleEndian(
            reference.AsSpan(backupStart + FirmwareConfigLayout.ProjectIdOffset),
            projectId);
        File.WriteAllBytes(referencePath, reference);

        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath,
            ["replace-ctrlram-normal"] = evidence.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-diff"] = evidence.Require("DiffDLM.bin").Path,
            ["replace-ctrlram-mp"] = evidence.Require("MP_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = evidence.Require("VN_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = evidence.Require("NF_Ctrlram.bin").Path,
        };
        string outputPath = workspace.PathFor("metadata-variation-output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            number,
            WorkbenchReplaceModes.CtrlRam,
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal(
            number == "single"
                ? "nt51926-ctrlram-replace-fw200-runtime-single"
                : "nt51926-ctrlram-replace-fw200-runtime-cascade",
            report.RootElement.GetProperty("ProfileId").GetString());
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
    }

    /// <summary>Proves every accepted NT51926 identifier form retains the reviewed FW2 V2 route.</summary>
    [Theory]
    [InlineData("51926")]
    [InlineData("nt51926")]
    [InlineData(" NT51926 ")]
    public async Task Fw200RouteCanonicalizesAcceptedIcIdentifiersAsync(string icId)
    {
        OwnerCase evidence = ReadOwnerCase(CascadeCaseId);
        using var workspace = TempWorkspace.Create("nfc-nt51926-fw200-canonical-id");
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = evidence.Expected.Path,
            ["replace-ctrlram-normal"] = evidence.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-diff"] = evidence.Require("DiffDLM.bin").Path,
            ["replace-ctrlram-mp"] = evidence.Require("MP_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = evidence.Require("VN_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = evidence.Require("NF_Ctrlram.bin").Path,
        };
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            icId,
            "cascade",
            slotPaths,
            build: true,
            workspace.PathFor("canonical-output.bin"),
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, "nt51926-ctrlram-replace-fw200-runtime-cascade");
    }

    private static OwnerCase ReadOwnerCase(string caseId)
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        JsonElement caseElement = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        var artifacts = caseElement.GetProperty("artifacts")
            .EnumerateArray()
            .Select(candidate => ReadArtifact(goldenRoot, candidate))
            .ToDictionary(static artifact => artifact.FileName, StringComparer.Ordinal);

        OwnerArtifact expected = artifacts.Values.Single(static artifact =>
            StringComparer.Ordinal.Equals(artifact.Role, "expected-final-output"));
        OwnerArtifact dp = artifacts.Values.Single(artifact =>
            StringComparer.Ordinal.Equals(artifact.Role, "standard-merge-dp-input"));
        OwnerArtifact tp = artifacts.Values.Single(artifact =>
            StringComparer.Ordinal.Equals(artifact.Role, "standard-merge-tp-input"));
        return new OwnerCase(artifacts, dp, tp, expected);
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

    private static void AssertReportIdentity(JsonElement report, string profileId)
    {
        Assert.Equal(profileId, report.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51926", report.GetProperty("IcId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ModeId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ExperienceId").GetString());
        Assert.Equal("Replace", report.GetProperty("CompositionKind").GetString());
    }

    private static string ReadProfileId(string reportJson)
    {
        using var report = JsonDocument.Parse(reportJson);
        return report.RootElement.GetProperty("ProfileId").GetString()!;
    }

    private static void AssertProcessEvidence(JsonElement report, string topology)
    {
        JsonElement session = Assert.Single(ReadProcessorSessions(report));
        Assert.Equal(2, session.GetProperty("ExecutedCommands").GetArrayLength());

        string[][] expectedArguments = ExpectedArguments(topology);
        Assert.Equal(expectedArguments, ReadNormalizedArguments([session]));
        AssertProcessorIdentity(session);
        Assert.Equal([new ByteRange(0, TpWorkCapacity)], ReadRanges(session, "ProcessorAllowedReadRanges"));
        Assert.Equal(ExpectedV2WriteRanges(topology), ReadRanges(session, "ProcessorAllowedWriteRanges"));
    }

    private static JsonElement[] ReadProcessorSessions(JsonElement report)
    {
        return [
            .. report.GetProperty("Operations").EnumerateArray().Where(operation =>
                StringComparer.Ordinal.Equals(
                    operation.GetProperty("Kind").GetString(),
                    "RunExternalProcessor")),
        ];
    }

    private static void AssertProcessorIdentity(JsonElement session)
    {
        Assert.Equal("nfc.nt51926.ctrlram-postbuild-v1", session.GetProperty("ProcessorId").GetString());
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

    private static string[][] ExpectedArguments(string topology)
    {
        var first = new List<string>
        {
            "CRC_Enable",
            "output/nt51926_fw.bin",
            "BIN/Normal_Ctrlram.bin", "0x0", "0x22800", "11264",
        };
        if (StringComparer.Ordinal.Equals(topology, "cascade"))
        {
            first.AddRange(["BIN/DiffDLM.bin", "0x0", "0x27800", "10240"]);
        }

        first.AddRange([
            "BIN/MP_Ctrlram.bin", "0x0", "0x25400", "9216",
            "BIN/VN_Ctrlram.bin", "0x0", "0x315D0", "5278",
            "BIN/NF_Ctrlram.bin", "0x0", "0x2C800", "11728",
            "output/nt51926_fw.bin", "0x22000", "0x3B000", "1920",
            "output/nt51926_fw.bin", "0x0", "0x32A70", "256",
        ]);
        return [
            [.. first],
            ["CRC_Enable", "output/nt51926_fw.bin", "output/nt51926_fw.bin", "0x0", "0x32A70", "256"],
        ];
    }

    private static ByteRange[] ExpectedV2WriteRanges(string topology)
    {
        List<ByteRange> ranges =
        [
            new(0x1C, 4),
            new(0x3C, 4),
            new(0xFC, 4),
            new(0x22800, 0x2C00),
            new(0x25400, 0x2400),
        ];
        if (StringComparer.Ordinal.Equals(topology, "cascade"))
        {
            ranges.Add(new ByteRange(0x27800, 0x2800));
        }

        ranges.AddRange([
            new ByteRange(0x2C800, 0x2DD0),
            new ByteRange(0x315D0, 0x149E),
            new ByteRange(0x32A70, 0x100),
            new ByteRange(0x3B000, 0x780),
        ]);
        return [.. ranges];
    }

    private static ByteRange[] ReadRanges(JsonElement operation, string propertyName)
    {
        return [
            .. operation.GetProperty(propertyName).EnumerateArray().Select(range => new ByteRange(
                range.GetProperty("Start").GetInt64(),
                range.GetProperty("Length").GetInt64())),
        ];
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

    private sealed record OwnerArtifact(
        string FileName,
        string RelativePath,
        string Role,
        string Path,
        byte[] Bytes);

    private sealed record OwnerCase(
        IReadOnlyDictionary<string, OwnerArtifact> Artifacts,
        OwnerArtifact Dp,
        OwnerArtifact Tp,
        OwnerArtifact Expected)
    {
        public OwnerArtifact Require(string fileName)
        {
            return Artifacts.TryGetValue(fileName, out OwnerArtifact? artifact)
                ? artifact
                : throw new InvalidOperationException($"Owner case is missing '{fileName}'.");
        }
    }
}
