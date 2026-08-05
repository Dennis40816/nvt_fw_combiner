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

    /// <summary>Proves the canonical 512 KiB base preserves its DP/LDC tail in the locked V2 output.</summary>
    [Fact]
    public async Task ExactCanonicalBaseProducesLockedV2EvidenceAsync()
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
        using var workspace = TempWorkspace.Create("nfc-nt51928-fw132-twochip-v2");
        string v2Path = workspace.PathFor("v2.bin");
        WorkbenchRunResult v2 = await CompositionExecutionAdapter.RunReplaceAsync(
            "NT51928", "2", WorkbenchReplaceModes.CtrlRam, slots, true,
            TestContext.Current.CancellationToken, v2Path);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2Path);
        Assert.Equal(OutputSha256, Hash(v2Bytes));
        Assert.Equal(reference.AsSpan(0x34800).ToArray(), v2Bytes.AsSpan(0x34800).ToArray());

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement);
        AssertProcessEvidence(v2Report.RootElement);
        Assert.All(
            replacements.Append(new OwnerArtifact(null, BasePath, reference)),
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>The partial two-chip route accepts non-authoritative metadata without expanding its plan authority.</summary>
    [Theory]
    [InlineData("pid")]
    [InlineData("version")]
    public async Task ProductionRouteAcceptsNonAuthoritativeMetadataVariationsAsync(string mutation)
    {
        byte[] reference = File.ReadAllBytes(BasePath);
        OwnerArtifact[] replacements = ReadReplacementInputs();
        var immutableHashes = replacements
            .Append(new OwnerArtifact(null, BasePath, reference))
            .ToDictionary(static artifact => artifact.Path, static artifact => Hash(artifact.Bytes), StringComparer.Ordinal);
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
                reference[start + FirmwareConfigLayout.CommonFwAdditionalVersionOffset]++;
                break;
            default:
                throw new InvalidOperationException($"Unknown mutation '{mutation}'.");
        }

        using var workspace = TempWorkspace.Create("nfc-nt51928-fw132-twochip-negative");
        string referencePath = workspace.Write("reference.bin", reference);
        string outputPath = workspace.PathFor("metadata-variation-output.bin");
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51928",
            "2",
            CreateSlotPaths(replacements, referencePath),
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(File.Exists(outputPath));
        using (var report = JsonDocument.Parse(result.ReportJson))
        {
            AssertReportIdentity(report.RootElement);
        }
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            replacements.Append(new OwnerArtifact(null, BasePath, reference)),
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Single and three-chip reuse their matching NT51927 TP branches inside the 512 KiB image.</summary>
    [Theory]
    [InlineData("single", 1, "nt51928-ctrlram-replace-fw141-single")]
    [InlineData("3", 3, "nt51928-ctrlram-replace-fw140-threechip")]
    public async Task AdditionalNonNbPlansBuildAndPreserveDpLdcTailAsync(
        string number,
        byte chipCount,
        string expectedProfileId)
    {
        using var workspace = TempWorkspace.Create("nfc-nt51928-additional-non-nb-plan");
        byte[] reference = File.ReadAllBytes(BasePath);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        reference[checked((int)metadata.StructureStart + FirmwareConfigLayout.ChipNumberOffset)] = chipCount;
        string referencePath = workspace.Write("reference.bin", reference);
        OwnerArtifact nf = ReadReplacementInputs().Single(
            static artifact => artifact.SlotId == "replace-ctrlram-nf");
        string nfPath = chipCount == 3
            ? workspace.Write(
                "NF_Ctrlram.bin",
                [.. Enumerable.Range(0, 0x2F50).Select(index => nf.Bytes[index % nf.Bytes.Length])])
            : nf.Path;
        IReadOnlyDictionary<string, string> slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath,
            [nf.SlotId!] = nfPath,
        };
        string outputPath = workspace.PathFor("output.bin");

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51928",
            number,
            slots,
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(reference.AsSpan(0x34800).ToArray(), output.AsSpan(0x34800).ToArray());
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal(expectedProfileId, ReadProfileId(report.RootElement));
    }

    private static void AssertReportIdentity(JsonElement report)
    {
        Assert.Equal("nt51928-ctrlram-replace-fw132-twochip", ReadProfileId(report));
        Assert.Equal("NT51928", report.GetProperty("IcId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ModeId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ExperienceId").GetString());
        Assert.Equal("Replace", report.GetProperty("CompositionKind").GetString());
    }

    private static void AssertProcessEvidence(JsonElement report)
    {
        JsonElement session = ReadProcessorSession(report);
        Assert.Equal("nfc.nt51928.ctrlram-postbuild-v1", session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
        Assert.Equal(ExpectedArguments(), ReadArguments(session));
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
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-2chip-self-20260705");
        return [
            .. fixtureCase.GetProperty("artifacts").EnumerateArray()
                .Where(item => item.GetProperty("slotId").GetString() != WorkbenchSlotIds.ReplaceBase)
                .Select(item =>
            {
                string path = CanonicalGoldenTestData.ArtifactPath(item);
                byte[] bytes = File.ReadAllBytes(path);
                return new OwnerArtifact(item.GetProperty("slotId").GetString(), path, bytes);
            }),
        ];
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

    private static string BasePath => CanonicalGoldenTestData.ArtifactPath(
        "standard-merge",
        "51928",
        "expected-output");
}
