using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the exact owner-approved NT51920 Common FW 1.2.0 single and cascade routes.</summary>
public sealed class Nt51920CtrlRamFw120EvidenceTests
{
    private static readonly (int Start, int EndExclusive)[] AllowedOwnerDifferenceRanges =
    [
        (0x1C, 0x20),
        (0xFC, 0x100),
        (0x2669C, 0x266A0),
        (0x2677C, 0x26780),
    ];

    /// <summary>Proves the exact owner control produces the locked V2 bytes, process authority, and argv.</summary>
    [Theory]
    [InlineData(
        "single",
        "nt51920-ctrlram-replace-fw120-single",
        1,
        0xF401,
        "b9965def2946fd6e28165af5929ede885e1d0e3c0ab29266a737ac458225920d",
        "55a21ba086975e8d1709e4d07dcb0235cd9ce8a0fd21df5f86d5d7f208bdac1c")]
    [InlineData(
        "cascade",
        "nt51920-ctrlram-replace-fw120-cascade2",
        2,
        0x1403,
        "681f904ecdf5785ca26f94eabb8191ddaa8976e0e6f750145475568c6cde4d43",
        "31735fae59742e795ac90b25672ff4f42a31d308f7b92dd379d81ebba869f077")]
    public async Task ExactOwnerCasesRunThroughV2WithLockedProcessEvidenceAsync(
        string topology,
        string v2ProfileId,
        byte chipCount,
        ushort projectId,
        string ownerExpectedSha256,
        string outputSha256)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase ownerCase = ReadOwnerCase(topology);
        Assert.Equal(ownerExpectedSha256, Hash(ownerCase.Expected.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(
            ownerCase.Expected.Bytes,
            out FirmwareConfigMetadata metadata));
        Assert.Equal("1.2.0", metadata.CommonFwVersion);
        Assert.Equal(chipCount, metadata.ChipNumber);
        Assert.Equal(projectId, metadata.ProjectId);

        var immutableHashes = ownerCase.Artifacts.ToDictionary(
            static artifact => artifact.Path,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create($"nfc-nt51920-fw120-{topology}-parity");
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(ownerCase);
        string v2OutputPath = workspace.PathFor("v2-output.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51920",
            topology,
            WorkbenchReplaceModes.CtrlRam,
            slots,
            build: true,
            TestContext.Current.CancellationToken,
            v2OutputPath);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2OutputPath);
        Assert.Equal(outputSha256, Hash(v2Bytes));
        Assert.Equal(outputSha256, v2.OutputSha256);
        AssertOwnerCrcOnlyDifference(ownerCase.Expected.Bytes, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, v2ProfileId);
        AssertProcessEvidence(v2Report.RootElement, topology);
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Proves a different base or metadata tuple fails closed without producing output.</summary>
    [Theory]
    [InlineData("single", "single", "base")]
    [InlineData("single", "single", "pid")]
    [InlineData("single", "single", "version")]
    [InlineData("single", "single", "chip")]
    [InlineData("cascade", "cascade", "base")]
    [InlineData("cascade", "cascade", "pid")]
    [InlineData("cascade", "cascade", "version")]
    [InlineData("cascade", "cascade", "chip")]
    public async Task UnreviewedShapeFailsClosedWithoutOutputAsync(
        string topology,
        string number,
        string mutation)
    {
        OwnerCase ownerCase = ReadOwnerCase(topology);
        using var workspace = TempWorkspace.Create($"nfc-nt51920-fw120-{topology}-negative");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = [.. ownerCase.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int start = checked((int)metadata.FirmwareConfigStart);
        switch (mutation)
        {
            case "base":
                reference[0x1000] ^= 0x01;
                break;
            case "pid":
                BinaryPrimitives.WriteUInt16LittleEndian(
                    reference.AsSpan(start + FirmwareConfigLayout.ProjectIdOffset),
                    0xFFFF);
                break;
            case "version":
                reference[start + FirmwareConfigLayout.CommonFwMinorVersionOffset] = 3;
                break;
            case "chip":
                reference[start + FirmwareConfigLayout.ChipNumberOffset]++;
                break;
            default:
                throw new InvalidOperationException($"Unknown mutation '{mutation}'.");
        }

        File.WriteAllBytes(referencePath, reference);
        Dictionary<string, string> slots = CreateSlotPaths(ownerCase);
        slots[WorkbenchSlotIds.ReplaceBase] = referencePath;
        string outputPath = workspace.PathFor("unsupported-output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51920",
            number,
            WorkbenchReplaceModes.CtrlRam,
            slots,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.False(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(WorkbenchIssueCodes.ReplaceWorkflowNotSupported, issue.GetProperty("Code").GetString());
        Assert.False(File.Exists(outputPath));
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
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

        Assert.Equal(16, differences.Count);
        Assert.All(differences, index => Assert.Contains(
            AllowedOwnerDifferenceRanges,
            range => index >= range.Start && index < range.EndExclusive));
        Assert.All(
            AllowedOwnerDifferenceRanges,
            range => Assert.Equal(4, differences.Count(index => index >= range.Start && index < range.EndExclusive)));
    }

    private static void AssertReportIdentity(JsonElement report, string profileId)
    {
        Assert.Equal(profileId, report.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51920", report.GetProperty("IcId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ModeId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ExperienceId").GetString());
        Assert.Equal("Replace", report.GetProperty("CompositionKind").GetString());
    }

    private static void AssertProcessEvidence(JsonElement report, string topology)
    {
        JsonElement session = ReadProcessorSession(report);
        Assert.Equal("nfc.nt51920.ctrlram-postbuild-v1", session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
        Assert.Equal([new ByteRange(0, 0x40000)], ReadRanges(session, "ProcessorAllowedReadRanges"));
        ByteRange[] expectedWrites = StringComparer.Ordinal.Equals(topology, "cascade")
            ? [
                new(0x1C, 4), new(0x3C, 4), new(0xFC, 4),
                new(0x22780, 10240), new(0x24F80, 5888), new(0x26680, 256),
                new(0x26780, 10240), new(0x28F80, 5888), new(0x2A780, 8080),
                new(0x2C710, 4120), new(0x2D728, 416), new(0x2F000, 1920),
            ]
            : [
                new(0x1C, 4), new(0x3C, 4), new(0xFC, 4),
                new(0x22780, 10240), new(0x24F80, 5888), new(0x26680, 256),
                new(0x2A780, 8080), new(0x2C710, 4120), new(0x2F000, 1920),
            ];
        Assert.Equal(expectedWrites, ReadRanges(session, "ProcessorAllowedWriteRanges"));
        string[][] arguments = ReadArguments(session);
        Assert.Equal(2, arguments.Length);
        Assert.Equal(ExpectedArguments(topology), arguments);
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

    private static ByteRange[] ReadRanges(JsonElement session, string propertyName)
    {
        return [
            .. session.GetProperty(propertyName).EnumerateArray()
                .Select(range => new ByteRange(
                    range.GetProperty("Start").GetInt64(),
                    range.GetProperty("Length").GetInt64()))
                .OrderBy(static range => range.Start),
        ];
    }

    private static string[][] ExpectedArguments(string topology)
    {
        List<string> first =
        [
            "CRC_Enable",
            "output/nt51920_fw.bin",
            "BIN/Normal_Ctrlram.bin", "0x0", "0x22780", "10240",
        ];
        if (StringComparer.Ordinal.Equals(topology, "cascade"))
        {
            first.AddRange(["BIN/Normal_Ctrlram_S.bin", "0x0", "0x26780", "10240"]);
        }

        first.AddRange(["BIN/MP_Ctrlram.bin", "0x0", "0x24F80", "5888"]);
        if (StringComparer.Ordinal.Equals(topology, "cascade"))
        {
            first.AddRange(["BIN/MP_Ctrlram_S.bin", "0x0", "0x28F80", "5888"]);
        }

        first.AddRange(
        [
            "BIN/VN_Ctrlram.bin", "0x0", "0x2C710", "4120",
            "BIN/NF_Ctrlram.bin", "0x0", "0x2A780", "8080",
        ]);
        if (StringComparer.Ordinal.Equals(topology, "cascade"))
        {
            first.AddRange(["BIN/Vector_Ctrlram.bin", "0x0", "0x2D728", "600"]);
        }

        first.AddRange(
        [
            "output/nt51920_fw.bin", "0x22000", "0x2F000", "1920",
            "output/nt51920_fw.bin", "0x0", "0x26680", "256",
        ]);
        return
        [
            [.. first],
            ["CRC_Enable", "output/nt51920_fw.bin", "output/nt51920_fw.bin", "0x0", "0x26680", "256"],
        ];
    }

    private static Dictionary<string, string> CreateSlotPaths(OwnerCase ownerCase)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = ownerCase.Expected.Path,
            ["replace-ctrlram-normal-master"] = ownerCase.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-mp-master"] = ownerCase.Require("MP_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = ownerCase.Require("NF_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = ownerCase.Require("VN_Ctrlram.bin").Path,
        };
        if (StringComparer.Ordinal.Equals(ownerCase.Topology, "cascade"))
        {
            slots["replace-ctrlram-normal-slave"] = ownerCase.Require("Normal_Ctrlram_S.bin").Path;
            slots["replace-ctrlram-mp-slave"] = ownerCase.Require("MP_Ctrlram_S.bin").Path;
            slots["replace-ctrlram-vector"] = ownerCase.Require("Vector_Ctrlram.bin").Path;
        }

        return slots;
    }

    private static OwnerCase ReadOwnerCase(string topology)
    {
        string prefix = $"fixtures/20260717/NT51920/replace/ctrlram/{topology}/";
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenRoot, "manifest.20260717.json")));
        OwnerArtifact[] artifacts = [
            .. manifest.RootElement.GetProperty("payloads").EnumerateArray()
                .Where(entry => entry.GetProperty("path").GetString()!.StartsWith(prefix, StringComparison.Ordinal))
                .Select(entry =>
                {
                    string path = RepositoryPaths.ManifestPath(GoldenRoot, entry);
                    byte[] bytes = File.ReadAllBytes(path);
                    Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
                    Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
                    return new OwnerArtifact(
                        entry.GetProperty("originalFileName").GetString()!,
                        entry.GetProperty("role").GetString()!,
                        path,
                        bytes);
                }),
        ];
        OwnerArtifact expected = Assert.Single(
            artifacts,
            static artifact => StringComparer.Ordinal.Equals(artifact.Role, "expected-final-output"));
        return new OwnerCase(topology, expected, artifacts);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record OwnerCase(
        string Topology,
        OwnerArtifact Expected,
        IReadOnlyList<OwnerArtifact> Artifacts)
    {
        internal OwnerArtifact Require(string fileName)
        {
            return Artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.FileName, fileName));
        }
    }

    private sealed record OwnerArtifact(string FileName, string Role, string Path, byte[] Bytes);

    private static string GoldenRoot => RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
}
