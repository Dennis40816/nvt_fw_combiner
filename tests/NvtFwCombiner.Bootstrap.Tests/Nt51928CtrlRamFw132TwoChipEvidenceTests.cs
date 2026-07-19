using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the exact NT51928 non-NB Common FW 1.3.2 two-chip partial-family route.</summary>
public sealed class Nt51928CtrlRamFw132TwoChipEvidenceTests
{
    private const string BaseSha256 = "5064b3134031adbd7ae292c9038d728da116d5a013a2463ae809694a07f87e0e";
    private const string OutputSha256 = "fbe011c7903a2dfcdbc634b47b2b5148c6531ee84b01d34a289dcf8ec3f3f24c";

    /// <summary>Proves the canonical 512 KiB base preserves its DP/LDC tail and produces identical V1/V2 bytes.</summary>
    [Fact]
    public async Task ExactCanonicalBaseRunsThroughV2WithLegacyProcessParityAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        byte[] reference = File.ReadAllBytes(BasePath);
        Assert.Equal(BaseSha256, Hash(reference));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        Assert.Equal("1.3.2", metadata.CommonFwVersion);
        Assert.Equal(2, metadata.ChipNumber);
        Assert.Equal(0xF206, metadata.ProjectId);

        OwnerArtifact[] replacements = ReadReplacementInputs();
        var immutableHashes = replacements
            .Append(new OwnerArtifact(null, BasePath, reference))
            .ToDictionary(static artifact => artifact.Path, static artifact => Hash(artifact.Bytes), StringComparer.Ordinal);
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(replacements, BasePath);
        using var workspace = TempWorkspace.Create("nfc-nt51928-fw132-twochip-parity");
        string legacyPath = workspace.PathFor("legacy.bin");
        WorkbenchRunResult legacy = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51928", "2", WorkbenchReplaceModes.CtrlRam, slots, true,
            TestContext.Current.CancellationToken, legacyPath,
            new WorkbenchCtrlRamFirmwareVersionEdit(metadata.FirmwareVersion, metadata.FirmwareSubVersion));
        string v2Path = workspace.PathFor("v2.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51928", "2", WorkbenchReplaceModes.CtrlRam, slots, true,
            TestContext.Current.CancellationToken, v2Path);

        Assert.True(legacy.Succeeded, legacy.ReportJson);
        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] legacyBytes = File.ReadAllBytes(legacyPath);
        byte[] v2Bytes = File.ReadAllBytes(v2Path);
        Assert.Equal(OutputSha256, Hash(legacyBytes));
        Assert.Equal(OutputSha256, Hash(v2Bytes));
        Assert.Equal(legacyBytes, v2Bytes);
        Assert.Equal(reference.AsSpan(0x34800).ToArray(), v2Bytes.AsSpan(0x34800).ToArray());

        using var legacyReport = JsonDocument.Parse(legacy.ReportJson);
        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        Assert.Equal("nt51928-ctrlram-replace-workbench", ReadProfileId(legacyReport.RootElement));
        Assert.Equal("nt51928-ctrlram-replace-fw132-twochip", ReadProfileId(v2Report.RootElement));
        AssertReportIdentity(legacyReport.RootElement, v2Report.RootElement);
        AssertProcessParity(legacyReport.RootElement, v2Report.RootElement);
        Assert.All(
            replacements.Append(new OwnerArtifact(null, BasePath, reference)),
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Proves another base, metadata tuple, or selector cannot enter the exact V2 route.</summary>
    [Theory]
    [InlineData("base")]
    [InlineData("pid")]
    [InlineData("version")]
    [InlineData("chip")]
    [InlineData("selector")]
    public async Task UnreviewedShapesRetainLegacyFallbackAsync(string mutation)
    {
        byte[] reference = File.ReadAllBytes(BasePath);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int start = checked((int)metadata.FirmwareConfigStart);
        string selector = "2";
        switch (mutation)
        {
            case "base":
                reference[0x70000] ^= 0x01;
                break;
            case "pid":
                BinaryPrimitives.WriteUInt16LittleEndian(
                    reference.AsSpan(start + FirmwareConfigLayout.ProjectIdOffset),
                    0xFFFF);
                break;
            case "version":
                reference[start + FirmwareConfigLayout.CommonFwAdditionalVersionOffset]++;
                break;
            case "chip":
                reference[start + FirmwareConfigLayout.ChipNumberOffset]++;
                break;
            case "selector":
                selector = "3";
                break;
            default:
                throw new InvalidOperationException($"Unknown mutation '{mutation}'.");
        }

        using var workspace = TempWorkspace.Create("nfc-nt51928-fw132-twochip-negative");
        string referencePath = workspace.Write("reference.bin", reference);
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51928",
            selector,
            CreateSlotPaths(ReadReplacementInputs(), referencePath),
            build: true,
            workspace.PathFor("fallback.bin"),
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal("nt51928-ctrlram-replace-workbench", ReadProfileId(report.RootElement));
        if (!StringComparer.Ordinal.Equals(mutation, "selector"))
        {
            Assert.True(result.Succeeded, result.ReportJson);
        }
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
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

    private static void AssertProcessParity(JsonElement legacy, JsonElement v2)
    {
        JsonElement legacySession = ReadProcessorSession(legacy);
        JsonElement v2Session = ReadProcessorSession(v2);
        Assert.Equal("nfc.nt51928.ctrlram-postbuild-v1", legacySession.GetProperty("ProcessorId").GetString());
        Assert.Equal("nfc.nt51928.ctrlram-postbuild-v1", v2Session.GetProperty("ProcessorId").GetString());
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
        Assert.Equal(ExpectedArguments(), legacyArguments);
        Assert.Equal(ExpectedArguments(), v2Arguments);
    }

    private static string[][] ExpectedArguments()
    {
        const string firmware = "output/nt51928_fw.bin";
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
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x16000", "0x1F000", "36864" ],
            [
                "MERGE_MODE", firmware,
                firmware, "0x0", "0x0", "217088",
                "BIN/NF_Ctrlram.bin", "0x0", "0x1F800", "16",
                "BIN/NF_Ctrlram.bin", "0xFD0", "0x1F810", "4032",
                "BIN/Normal_Ctrlram_R.bin", "0x0", "0x207D0", "12288",
                "BIN/MP_Ctrlram_R.bin", "0x0", "0x237D0", "9216",
                "BIN/VN_Ctrlram.bin", "0x0", "0x25BD0", "5728",
            ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x0", "0x32DC0", "1120" ],
            [ "NT51927BASED_GEN_CRC_MODE", "CRC32", firmware, firmware ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x200", "0x1E230", "400" ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x200", "0x27230", "400" ],
            [ "NT51927BASED_GEN_CRC_MODE", "CRC32", firmware, firmware ],
        ];
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

    private static Dictionary<string, string> CreateSlotPaths(
        IEnumerable<OwnerArtifact> replacements,
        string referencePath)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath,
        };
        foreach (OwnerArtifact artifact in replacements)
        {
            slots.Add(artifact.SlotId!, artifact.Path);
        }

        return slots;
    }

    private static OwnerArtifact[] ReadReplacementInputs()
    {
        string root = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        JsonElement fixtureCase = manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "nt51927-2chip-self-20260705");
        return [
            .. fixtureCase.GetProperty("replacementInputs").EnumerateArray().Select(input =>
            {
                JsonElement file = input.GetProperty("file");
                string path = RepositoryPaths.ManifestPath(root, file);
                byte[] bytes = File.ReadAllBytes(path);
                Assert.Equal(file.GetProperty("size").GetInt64(), bytes.LongLength);
                Assert.Equal(file.GetProperty("sha256").GetString(), Hash(bytes));
                return new OwnerArtifact(input.GetProperty("slotId").GetString(), path, bytes);
            }),
        ];
    }

    private static string ReadInputIdentity(JsonElement input)
    {
        return string.Join(
            '|',
            input.GetProperty("AddressSpaceId").GetString(),
            input.GetProperty("Size").GetInt64(),
            input.GetProperty("Sha256").GetString());
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

    private sealed record OwnerArtifact(string? SlotId, string Path, byte[] Bytes);

    private static string BasePath => RepositoryPaths.FromRepositoryRoot(
        "testdata", "golden", "standard-merge-gen-flash", "expected", "51928", "flash.bin");
}
