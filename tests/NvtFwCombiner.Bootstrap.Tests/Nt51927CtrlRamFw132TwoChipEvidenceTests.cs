using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the exact owner-approved NT51927 Common FW 1.3.2 two-chip engineering route.</summary>
public sealed class Nt51927CtrlRamFw132TwoChipEvidenceTests
{
    private static readonly (int Start, int EndExclusive)[] ExpectedDerivedCrcRanges =
    [
        (0x23C, 0x240),
        (0x24C, 0x250),
        (0x26C, 0x270),
        (0x27C, 0x280),
        (0x1E24C, 0x1E250),
        (0x1E25C, 0x1E260),
        (0x1E26C, 0x1E270),
        (0x1E27C, 0x1E280),
        (0x1E28C, 0x1E290),
        (0x1E29C, 0x1E2A0),
        (0x1E2AC, 0x1E2B0),
        (0x2724C, 0x27250),
        (0x2725C, 0x27260),
        (0x2726C, 0x27270),
        (0x2727C, 0x27280),
        (0x2728C, 0x27290),
        (0x2729C, 0x272A0),
        (0x272AC, 0x272B0),
        (0x32FDC, 0x32FE0),
        (0x32FEC, 0x32FF0),
        (0x32FFC, 0x33000),
        (0x3300C, 0x33010),
        (0x3301C, 0x33020),
        (0x3302C, 0x33030),
        (0x3303C, 0x33040),
    ];

    /// <summary>Proves the exact two-chip base produces the locked V2 bytes and process evidence.</summary>
    [Theory]
    [InlineData("NT51927", "nt51927-ctrlram-replace-fw132-twochip", "nfc.nt51927.ctrlram-postbuild-v1")]
    [InlineData("NT51917", "nt51917-ctrlram-replace-fw132-twochip", "nfc.nt51917.ctrlram-postbuild-v1")]
    public async Task ExactExpectedDerivedCaseProducesLockedV2EvidenceAsync(
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
            "11700ec5580f2e07195c7aec3788f929609eef5355d773287d3f88aa1f984dae",
            Hash(ownerCase.Base.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(
            ownerCase.Base.Bytes,
            out FirmwareConfigMetadata metadata));
        Assert.Equal("1.3.2", metadata.CommonFwVersion);
        Assert.Equal(2, metadata.ChipNumber);
        Assert.Equal(0x1615, metadata.ProjectId);

        var immutableHashes = ownerCase.Artifacts.ToDictionary(
            static artifact => artifact.Path,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51927-fw132-twochip-v2");
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(ownerCase);
        string v2OutputPath = workspace.PathFor("v2-output.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            icId,
            "2",
            WorkbenchReplaceModes.CtrlRam,
            slots,
            build: true,
            TestContext.Current.CancellationToken,
            v2OutputPath);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2OutputPath);
        const string outputSha256 = "6f0bbde7662dc6701cfe0a242d4cc363cd24c7056a52611fbba965c7d7fb5f58";
        Assert.Equal(outputSha256, Hash(v2Bytes));
        AssertExpectedDerivedCrcOnlyDifference(ownerCase.Base.Bytes, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, expectedProfileId, icId);
        AssertProcessEvidence(v2Report.RootElement, expectedProcessorId, icId);
        JsonElement[] differences = [.. v2Report.RootElement.GetProperty("OutputDifferences").EnumerateArray()];
        Assert.Equal(ExpectedDerivedCrcRanges.Length, differences.Length);
        Assert.All(differences, difference =>
        {
            Assert.Equal("PostbuildCrcHeader", difference.GetProperty("Classification").GetString());
            Assert.True(difference.GetProperty("IsAccepted").GetBoolean());
        });
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>NT51917/NT51927 aliases retain the requested two-chip route across non-authoritative metadata.</summary>
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
        using var workspace = TempWorkspace.Create("nfc-nt51927-fw132-twochip-negative");
        byte[] reference = [.. ownerCase.Base.Bytes];
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

        string referencePath = workspace.Write("reference.bin", reference);
        Dictionary<string, string> slots = CreateSlotPaths(ownerCase);
        slots[WorkbenchSlotIds.ReplaceBase] = referencePath;
        string outputPath = workspace.PathFor("metadata-variation-output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            icId,
            "2",
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
                $"{icId.ToLowerInvariant()}-ctrlram-replace-fw132-twochip",
                icId);
        }
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    private static void AssertExpectedDerivedCrcOnlyDifference(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        List<int> differences = [];
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
            {
                differences.Add(index);
            }
        }

        Assert.Equal(100, differences.Count);
        Assert.All(differences, index => Assert.Contains(
            ExpectedDerivedCrcRanges,
            range => index >= range.Start && index < range.EndExclusive));
        Assert.All(
            ExpectedDerivedCrcRanges,
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

    private static Dictionary<string, string> CreateSlotPaths(OwnerCase ownerCase)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = ownerCase.Base.Path,
        };
        foreach (OwnerArtifact artifact in ownerCase.Artifacts)
        {
            if (artifact.SlotId is not null)
            {
                slots.Add(artifact.SlotId, artifact.Path);
            }
        }

        return slots;
    }

    private static OwnerCase ReadOwnerCase()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-2chip-self-20260705");
        JsonElement[] canonicalArtifacts = [.. fixtureCase.GetProperty("artifacts").EnumerateArray()];
        JsonElement baseArtifact = canonicalArtifacts.Single(item =>
            item.GetProperty("slotId").GetString() == WorkbenchSlotIds.ReplaceBase);
        OwnerArtifact ownerBase = ReadArtifact(baseArtifact, slotId: null);
        OwnerArtifact[] artifacts = [
            ownerBase,
            .. canonicalArtifacts
                .Where(item => item.GetProperty("slotId").GetString() != WorkbenchSlotIds.ReplaceBase)
                .Select(item => ReadArtifact(item, item.GetProperty("slotId").GetString())),
        ];
        return new OwnerCase(ownerBase, artifacts);
    }

    private static OwnerArtifact ReadArtifact(JsonElement document, string? slotId)
    {
        string path = RepositoryPaths.ManifestPath(GoldenRoot, document);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(document.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(document.GetProperty("sha256").GetString(), Hash(bytes));
        return new OwnerArtifact(slotId, path, bytes);
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
        OwnerArtifact Base,
        IReadOnlyList<OwnerArtifact> Artifacts);

    private sealed record OwnerArtifact(string? SlotId, string Path, byte[] Bytes);

    private static string GoldenRoot => CanonicalGoldenTestData.Root;
}
