using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the exact owner-approved NT51927 Common FW 1.4.1 single-chip route.</summary>
public sealed class Nt51927CtrlRamFw141SingleEvidenceTests
{
    private static readonly (int Start, int EndExclusive)[] AllowedOwnerDifferenceRanges =
    [
        (0x1E26C, 0x1E270),
        (0x1E27C, 0x1E280),
        (0x32FDC, 0x32FE0),
        (0x32FEC, 0x32FF0),
        (0x32FFC, 0x33000),
        (0x3300C, 0x33010),
    ];

    /// <summary>Proves the exact owner control produces the locked V2 bytes, process authority, and argv.</summary>
    [Theory]
    [InlineData("NT51927", "nt51927-ctrlram-replace-fw141-single", "nfc.nt51927.ctrlram-postbuild-v1")]
    [InlineData("NT51917", "nt51917-ctrlram-replace-fw141-single", "nfc.nt51917.ctrlram-postbuild-v1")]
    public async Task ExactOwnerCaseProducesLockedV2EvidenceAsync(
        string icId,
        string expectedProfileId,
        string expectedProcessorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase ownerCase = ReadOwnerCase();
        Assert.Equal(
            "fc4d2f9701c626b1c7cddd2b448970611d332295c64f86415af2855f1569c55a",
            Hash(ownerCase.Expected.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(
            ownerCase.Expected.Bytes,
            out FirmwareConfigMetadata metadata));
        Assert.Equal("1.4.1", metadata.CommonFwVersion);
        Assert.Equal(1, metadata.ChipNumber);
        Assert.Equal(0x5709, metadata.ProjectId);

        var immutableHashes = ownerCase.Artifacts.ToDictionary(
            static artifact => artifact.Path,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51927-fw141-single-v2");
        string referencePath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult standardMerge = await CompositionExecutionAdapter.RunStandardMergeAsync(
            "NT51927",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = ownerCase.RequireRole("standard-merge-dp-input").Path,
                [CompositionAddressSpaceIds.TpInput] = ownerCase.RequireRole("standard-merge-tp-input").Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            referencePath);
        Assert.True(standardMerge.Succeeded, standardMerge.ReportJson);
        using (var standardMergeReport = JsonDocument.Parse(standardMerge.ReportJson))
        {
            Assert.Equal("nt51927-standard-merge-gen-flash", ReadProfileId(standardMergeReport.RootElement));
        }

        Assert.Equal(ownerCase.Expected.Bytes, File.ReadAllBytes(referencePath));
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(ownerCase, referencePath);
        string v2OutputPath = workspace.PathFor("v2-output.bin");
        WorkbenchRunResult v2 = await CompositionExecutionAdapter.RunReplaceAsync(
            icId,
            "single",
            WorkbenchReplaceModes.CtrlRam,
            slots,
            build: true,
            TestContext.Current.CancellationToken,
            v2OutputPath);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2OutputPath);
        const string outputSha256 = "fdb8fef05bdb375e175091eb75d555c2b1c5ddb216a2815f02e25c6533020ab9";
        Assert.Equal(outputSha256, Hash(v2Bytes));
        AssertOwnerCrcOnlyDifference(ownerCase.Expected.Bytes, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, expectedProfileId, icId);
        AssertProcessEvidence(v2Report.RootElement, expectedProcessorId, icId);
        Assert.Equal(Hash(ownerCase.Expected.Bytes), Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>NT51917/NT51927 aliases retain the requested single route across non-authoritative metadata.</summary>
    [Theory]
    [InlineData("NT51927", "pid")]
    [InlineData("NT51927", "version")]
    [InlineData("NT51917", "pid")]
    [InlineData("NT51917", "version")]
    public async Task ProductionRouteAcceptsNonAuthoritativeMetadataVariationsAsync(string icId, string mutation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        OwnerCase ownerCase = ReadOwnerCase();
        var immutableHashes = ownerCase.Artifacts.ToDictionary(
            static artifact => artifact.Path,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51927-fw141-single-negative");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = [.. ownerCase.Expected.Bytes];
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
                reference[start + FirmwareConfigLayout.CommonFwMinorVersionOffset] = 3;
                break;
            default:
                throw new InvalidOperationException($"Unknown mutation '{mutation}'.");
        }

        File.WriteAllBytes(referencePath, reference);
        Dictionary<string, string> slots = CreateSlotPaths(ownerCase);
        slots[WorkbenchSlotIds.ReplaceBase] = referencePath;
        string outputPath = workspace.PathFor("metadata-variation-output.bin");
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            icId,
            "single",
            slots,
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        using (var report = JsonDocument.Parse(result.ReportJson))
        {
            AssertReportIdentity(
                report.RootElement,
                $"{icId.ToLowerInvariant()}-ctrlram-replace-fw141-single",
                icId);
        }
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    private static void AssertOwnerCrcOnlyDifference(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        List<int> differences = [];
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
            {
                differences.Add(index);
            }
        }

        Assert.Equal(24, differences.Count);
        Assert.All(differences, index => Assert.Contains(
            AllowedOwnerDifferenceRanges,
            range => index >= range.Start && index < range.EndExclusive));
        Assert.All(
            AllowedOwnerDifferenceRanges,
            range => Assert.Equal(4, differences.Count(index => index >= range.Start && index < range.EndExclusive)));
    }

    private static void AssertReportIdentity(JsonElement report, string expectedProfileId, string icId)
    {
        Assert.Equal(expectedProfileId, ReadProfileId(report));
        Assert.Equal(icId, report.GetProperty("IcId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ModeId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ExperienceId").GetString());
        Assert.Equal("Replace", report.GetProperty("CompositionKind").GetString());
    }

    private static void AssertProcessEvidence(
        JsonElement report,
        string expectedProcessorId,
        string icId)
    {
        JsonElement session = ReadProcessorSession(report);
        Assert.Equal(expectedProcessorId, session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
        Assert.Equal(ExpectedArguments(icId), ReadArguments(session));
    }

    private static JsonElement ReadProcessorSession(JsonElement report)
    {
        return Assert.Single(
            report.GetProperty("Operations").EnumerateArray(),
            operation => StringComparer.Ordinal.Equals(
                operation.GetProperty("Kind").GetString(),
                "RunExternalProcessor"));
    }

    private static string[][] ReadArguments(JsonElement session)
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

    private static string[][] ExpectedArguments(string icId)
    {
        string firmware = $"output/{icId.ToLowerInvariant()}_fw.bin";
        return
        [
            [
                "MERGE_MODE", firmware,
                firmware, "0x0", "0x0", "217088",
                "BIN/NF_Ctrlram.bin", "0x0", "0x16800", "4048",
                "BIN/Normal_Ctrlram.bin", "0x0", "0x177D0", "12288",
                "BIN/MP_Ctrlram.bin", "0x0", "0x1A7D0", "9216",
                "BIN/VN_Ctrlram.bin", "0x0", "0x1CBD0", "5728",
            ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x16000", "0x34000", "2048" ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x200", "0x1E230", "400" ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x0", "0x32DC0", "1120" ],
            [ "NT51927BASED_GEN_CRC_MODE", "CRC32", firmware, firmware ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x200", "0x1E230", "400" ],
            [ "NT51927BASED_GEN_CRC_MODE", "CRC32", firmware, firmware ],
        ];
    }

    private static Dictionary<string, string> CreateSlotPaths(OwnerCase ownerCase, string? referencePath = null)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath ?? ownerCase.Expected.Path,
            ["replace-ctrlram-normal-master"] = ownerCase.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-mp-master"] = ownerCase.Require("MP_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = ownerCase.Require("NF_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = ownerCase.Require("VN_Ctrlram.bin").Path,
        };
    }

    private static OwnerCase ReadOwnerCase()
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51927-fw141-single-auto-prj-529-20260717");
        OwnerArtifact[] artifacts = [
            .. goldenCase.GetProperty("artifacts").EnumerateArray()
                .Select(entry =>
                {
                    string path = RepositoryPaths.ManifestPath(GoldenRoot, entry);
                    byte[] bytes = File.ReadAllBytes(path);
                    Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
                    Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
                    return new OwnerArtifact(
                        entry.GetProperty("originalFileName").GetString()!,
                        entry.GetProperty("sourceRole").GetString()!,
                        path,
                        bytes);
                }),
        ];
        OwnerArtifact expected = Assert.Single(
            artifacts,
            static artifact => StringComparer.Ordinal.Equals(artifact.Role, "expected-final-output"));
        return new OwnerCase(expected, artifacts);
    }

    private static string ReadProfileId(JsonElement report)
    {
        return report.GetProperty("ProfileId").GetString()!;
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

    private sealed record OwnerCase(
        OwnerArtifact Expected,
        IReadOnlyList<OwnerArtifact> Artifacts)
    {
        internal OwnerArtifact Require(string fileName)
        {
            return Artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.FileName, fileName));
        }

        internal OwnerArtifact RequireRole(string role)
        {
            return Artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.Role, role));
        }
    }

    private sealed record OwnerArtifact(string FileName, string Role, string Path, byte[] Bytes);

    private static string GoldenRoot => CanonicalGoldenTestData.Root;
}
