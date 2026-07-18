using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the exact owner-approved NT51923 Common FW 1.4.1 single and cascade routes.</summary>
public sealed class Nt51923CtrlRamFw141EvidenceTests
{
    private static readonly (int Start, int EndExclusive)[] AllowedOwnerDifferenceRanges =
    [
        (0x1C, 0x20),
        (0xFC, 0x100),
        (0x3032C, 0x30330),
        (0x3040C, 0x30410),
    ];

    /// <summary>Proves the exact owner control produces identical V1 and V2 bytes, process authority, and argv.</summary>
    [Theory]
    [InlineData(
        "single",
        "nt51923-ctrlram-replace-fw141-single",
        1,
        0x6005,
        "a65ae33c9c11091f69d8935422ffc57db32262eb922590364d4bdd9c3af9916f",
        "4759a8e87ad7ff8a8e41ae91af6f0d05a847659ffbf06f3864d1ca093453da38")]
    [InlineData(
        "cascade",
        "nt51923-ctrlram-replace-fw141-cascade3",
        3,
        0x4C03,
        "06dda13a592c151a767d47fff60da993f33d7bda37666794dd9ea5cf92094d18",
        "017a157ba2419ff29cfb00c14a88da75a64cfeac9b4dabeecf54d523b1ad115c")]
    public async Task ExactOwnerCasesRunThroughV2WithLegacyProcessParityAsync(
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
        Assert.Equal("1.4.1", metadata.CommonFwVersion);
        Assert.Equal(chipCount, metadata.ChipNumber);
        Assert.Equal(projectId, metadata.ProjectId);

        var immutableHashes = ownerCase.Artifacts.ToDictionary(
            static artifact => artifact.Path,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create($"nfc-nt51923-fw141-{topology}-parity");
        string referencePath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult standardMerge = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51923",
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
            Assert.Equal("nt51923-standard-merge-gen-flash", ReadProfileId(standardMergeReport.RootElement));
        }

        Assert.Equal(ownerCase.Expected.Bytes, File.ReadAllBytes(referencePath));
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(ownerCase, referencePath);
        string legacyOutputPath = workspace.PathFor("legacy-output.bin");
        WorkbenchRunResult legacy = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51923",
            topology,
            WorkbenchReplaceModes.CtrlRam,
            slots,
            build: true,
            TestContext.Current.CancellationToken,
            legacyOutputPath,
            new WorkbenchCtrlRamFirmwareVersionEdit(metadata.FirmwareVersion, metadata.FirmwareSubVersion));
        string v2OutputPath = workspace.PathFor("v2-output.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51923",
            topology,
            WorkbenchReplaceModes.CtrlRam,
            slots,
            build: true,
            TestContext.Current.CancellationToken,
            v2OutputPath);

        Assert.True(legacy.Succeeded, legacy.ReportJson);
        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] legacyBytes = File.ReadAllBytes(legacyOutputPath);
        byte[] v2Bytes = File.ReadAllBytes(v2OutputPath);
        Assert.Equal(outputSha256, Hash(legacyBytes));
        Assert.Equal(outputSha256, Hash(v2Bytes));
        Assert.Equal(legacyBytes, v2Bytes);
        AssertOwnerCrcOnlyDifference(ownerCase.Expected.Bytes, v2Bytes);

        using var legacyReport = JsonDocument.Parse(legacy.ReportJson);
        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        Assert.Equal("nt51923-ctrlram-replace-workbench", ReadProfileId(legacyReport.RootElement));
        Assert.Equal(v2ProfileId, ReadProfileId(v2Report.RootElement));
        AssertReportIdentity(legacyReport.RootElement, v2Report.RootElement);
        AssertProcessParity(legacyReport.RootElement, v2Report.RootElement, topology);
        Assert.Equal(ownerExpectedSha256, Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Proves a different base, metadata tuple, or selector cannot enter either exact V2 route.</summary>
    [Theory]
    [InlineData("single", "single", "base")]
    [InlineData("single", "single", "pid")]
    [InlineData("single", "single", "version")]
    [InlineData("single", "single", "chip")]
    [InlineData("cascade", "cascade", "base")]
    [InlineData("cascade", "cascade", "pid")]
    [InlineData("cascade", "cascade", "version")]
    [InlineData("cascade", "cascade", "chip")]
    public async Task UnreviewedShapeRetainsLegacyFallbackAsync(
        string topology,
        string number,
        string mutation)
    {
        OwnerCase ownerCase = ReadOwnerCase(topology);
        using var workspace = TempWorkspace.Create($"nfc-nt51923-fw141-{topology}-negative");
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
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51923",
            number,
            slots,
            build: true,
            workspace.PathFor("fallback-output.bin"),
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal("nt51923-ctrlram-replace-workbench", ReadProfileId(report.RootElement));
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

    private static void AssertReportIdentity(JsonElement legacy, JsonElement v2)
    {
        Assert.Equal(legacy.GetProperty("IcId").GetString(), v2.GetProperty("IcId").GetString());
        Assert.Equal(legacy.GetProperty("ModeId").GetString(), v2.GetProperty("ModeId").GetString());
        Assert.Equal(legacy.GetProperty("ExperienceId").GetString(), v2.GetProperty("ExperienceId").GetString());
        Assert.Equal(legacy.GetProperty("CompositionKind").GetString(), v2.GetProperty("CompositionKind").GetString());
        Assert.Equal(
            legacy.GetProperty("Inputs").EnumerateArray().Select(ReadInputIdentity),
            v2.GetProperty("Inputs").EnumerateArray().Select(ReadInputIdentity));
    }

    private static string ReadInputIdentity(JsonElement input)
    {
        return string.Join(
            '|',
            input.GetProperty("AddressSpaceId").GetString(),
            input.GetProperty("Size").GetInt64(),
            input.GetProperty("Sha256").GetString());
    }

    private static void AssertProcessParity(JsonElement legacyReport, JsonElement v2Report, string topology)
    {
        JsonElement legacySession = ReadProcessorSession(legacyReport);
        JsonElement v2Session = ReadProcessorSession(v2Report);
        Assert.Equal("nfc.nt51923.ctrlram-postbuild-v1", legacySession.GetProperty("ProcessorId").GetString());
        Assert.Equal("nfc.nt51923.ctrlram-postbuild-v1", v2Session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", legacySession.GetProperty("ToolBindingId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", v2Session.GetProperty("ToolBindingId").GetString());
        Assert.Equal(
            legacySession.GetProperty("ProcessorAllowedReadRanges").GetRawText(),
            v2Session.GetProperty("ProcessorAllowedReadRanges").GetRawText());
        Assert.Equal(
            legacySession.GetProperty("ProcessorAllowedWriteRanges").GetRawText(),
            v2Session.GetProperty("ProcessorAllowedWriteRanges").GetRawText());

        string[][] legacyArguments = ReadArguments(legacySession);
        string[][] v2Arguments = ReadArguments(v2Session);
        Assert.Equal(2, legacyArguments.Length);
        Assert.Equal(ExpectedArguments(topology), legacyArguments);
        Assert.Equal(legacyArguments, v2Arguments);
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

    private static string[][] ExpectedArguments(string topology)
    {
        List<string> first =
        [
            "CRC_Enable",
            "output/nt51923_fw.bin",
            "BIN/Normal_Ctrlram.bin", "0x0", "0x22800", "14336",
        ];
        if (StringComparer.Ordinal.Equals(topology, "cascade"))
        {
            first.AddRange(
            [
                "BIN/DiffDLM.bin", "0x0", "0x28800", "3072",
                "BIN/DiffDLM.bin", "0x1400", "0x29400", "3072",
            ]);
        }

        first.AddRange(
        [
            "BIN/MP_Ctrlram.bin", "0x0", "0x26000", "10240",
            "BIN/VN_Ctrlram.bin", "0x0", "0x2E800", "5728",
            "BIN/NF_Ctrlram.bin", "0x0", "0x2A000", "17584",
        ]);

        first.AddRange(
        [
            "output/nt51923_fw.bin", "0x22000", "0x3B000", "2048",
            "output/nt51923_fw.bin", "0x0", "0x30310", "256",
        ]);
        return
        [
            [.. first],
            ["CRC_Enable", "output/nt51923_fw.bin", "output/nt51923_fw.bin", "0x0", "0x30310", "256"],
        ];
    }

    private static Dictionary<string, string> CreateSlotPaths(OwnerCase ownerCase, string? referencePath = null)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath ?? ownerCase.Expected.Path,
            ["replace-ctrlram-normal"] = ownerCase.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-mp"] = ownerCase.Require("MP_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = ownerCase.Require("NF_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = ownerCase.Require("VN_Ctrlram.bin").Path,
        };
        if (StringComparer.Ordinal.Equals(ownerCase.Topology, "cascade"))
        {
            slots["replace-ctrlram-diff"] = ownerCase.Require("DiffDLM.bin").Path;
        }

        return slots;
    }

    private static OwnerCase ReadOwnerCase(string topology)
    {
        string prefix = $"fixtures/20260717/NT51923/replace/ctrlram/{topology}/";
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
        string Topology,
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

    private static string GoldenRoot => RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
}
