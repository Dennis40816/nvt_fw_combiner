using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the exact owner-approved NT51927 Common FW 1.4.0 three-chip engineering route.</summary>
public sealed class Nt51927CtrlRamFw140ThreeChipEvidenceTests
{
    private static readonly (int Start, int EndExclusive)[] CrcRanges =
    [
        (0x22C, 0x230),
        (0x24C, 0x250),
        (0x1E25C, 0x1E260),
        (0x1E27C, 0x1E280),
        (0x1E29C, 0x1E2A0),
        (0x1E2AC, 0x1E2B0),
        (0x1E2CC, 0x1E2D0),
        (0x1E2DC, 0x1E2E0),
        (0x2725C, 0x27260),
        (0x2727C, 0x27280),
        (0x2729C, 0x272A0),
        (0x272AC, 0x272B0),
        (0x272CC, 0x272D0),
        (0x272DC, 0x272E0),
        (0x3025C, 0x30260),
        (0x3027C, 0x30280),
        (0x3029C, 0x302A0),
        (0x302AC, 0x302B0),
        (0x302CC, 0x302D0),
        (0x302DC, 0x302E0),
        (0x32FDC, 0x32FE0),
        (0x32FEC, 0x32FF0),
        (0x3300C, 0x33010),
        (0x3302C, 0x33030),
        (0x3303C, 0x33040),
        (0x3305C, 0x33060),
        (0x3306C, 0x33070),
        (0x3307C, 0x33080),
        (0x3309C, 0x330A0),
    ];

    private static readonly (int Start, int EndExclusive)[] DeclaredReplacementRanges =
    [
        (0x1CBD8, 0x1CBD9),
        (0x1CC08, 0x1CC10),
        (0x1CC88, 0x1CC90),
        (0x1DEDB, 0x1DEE0),
    ];

    /// <summary>Proves the exact three-chip base produces the locked V2 bytes and process evidence.</summary>
    [Theory]
    [InlineData("NT51927", "nt51927-ctrlram-replace-fw140-threechip", "nfc.nt51927.ctrlram-postbuild-v1")]
    [InlineData("NT51917", "nt51917-ctrlram-replace-fw140-threechip", "nfc.nt51917.ctrlram-postbuild-v1")]
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
            "bc44561cc1cb338b9a49bbe701e5d7cbfe78ea40deda0926197fb22002b3061c",
            Hash(ownerCase.Base.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(
            ownerCase.Base.Bytes,
            out FirmwareConfigMetadata metadata));
        Assert.Equal("1.4.0", metadata.CommonFwVersion);
        Assert.Equal(3, metadata.ChipNumber);
        Assert.Equal(0x570A, metadata.ProjectId);

        var immutableHashes = ownerCase.Artifacts.ToDictionary(
            static artifact => artifact.Path,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51927-fw140-threechip-v2");
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(ownerCase);
        string v2OutputPath = workspace.PathFor("v2-output.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            icId,
            "3",
            WorkbenchReplaceModes.CtrlRam,
            slots,
            build: true,
            TestContext.Current.CancellationToken,
            v2OutputPath);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2OutputPath);
        const string outputSha256 = "dc1ee8928977845fad334b75c60b3e7fa3989f0a7e177206f83104217bf3fe16";
        Assert.Equal(outputSha256, Hash(v2Bytes));
        AssertExactBaseDelta(ownerCase.Base.Bytes, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, expectedProfileId, icId);
        AssertProcessEvidence(v2Report.RootElement, expectedProcessorId, icId);
        JsonElement[] differences = [.. v2Report.RootElement.GetProperty("OutputDifferences").EnumerateArray()];
        Assert.Equal(CrcRanges.Length, differences.Count(IsPostbuildCrc));
        Assert.Equal(DeclaredReplacementRanges.Length, differences.Count(IsDeclaredReplacement));
        JsonElement firstCrcSemantic = differences.Single(difference =>
                difference.GetProperty("Range").GetProperty("Start").GetInt64() == 0x22C)
            .GetProperty("Semantic");
        Assert.Equal("tp-flash-header", firstCrcSemantic.GetProperty("CategoryId").GetString());
        Assert.Equal(
            $"{icId.ToLowerInvariant()}-header:header-0-ilm-crc",
            firstCrcSemantic.GetProperty("SubjectId").GetString());
        Assert.Equal("ILM CRC 0", firstCrcSemantic.GetProperty("SubjectLabel").GetString());
        Assert.All(differences, difference => Assert.True(difference.GetProperty("IsAccepted").GetBoolean()));
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>NT51917/NT51927 aliases retain the requested three-chip route across non-authoritative metadata.</summary>
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
        using var workspace = TempWorkspace.Create("nfc-nt51927-fw140-threechip-negative");
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
            "3",
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
                $"{icId.ToLowerInvariant()}-ctrlram-replace-fw140-threechip",
                icId);
        }
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    private static bool IsPostbuildCrc(JsonElement difference)
    {
        return StringComparer.Ordinal.Equals(
            difference.GetProperty("Classification").GetString(),
            "PostbuildCrcHeader");
    }

    private static bool IsDeclaredReplacement(JsonElement difference)
    {
        return StringComparer.Ordinal.Equals(
            difference.GetProperty("Classification").GetString(),
            "DeclaredReplacement");
    }

    private static void AssertExactBaseDelta(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        List<int> differences = [];
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
            {
                differences.Add(index);
            }
        }

        Assert.Equal(138, differences.Count);
        Assert.Equal(116, CountWithin(differences, CrcRanges));
        Assert.Equal(22, CountWithin(differences, DeclaredReplacementRanges));
        Assert.All(CrcRanges, range => Assert.Equal(4, CountWithin(differences, [range])));
        Assert.DoesNotContain(differences, index =>
            !Contains(CrcRanges, index) && !Contains(DeclaredReplacementRanges, index));
    }

    private static int CountWithin(
        IEnumerable<int> differences,
        IReadOnlyList<(int Start, int EndExclusive)> ranges)
    {
        return differences.Count(index => Contains(ranges, index));
    }

    private static bool Contains(IReadOnlyList<(int Start, int EndExclusive)> ranges, int index)
    {
        return ranges.Any(range => index >= range.Start && index < range.EndExclusive);
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
                "BIN/NF_Ctrlram.bin", "0x0", "0x16800", "16",
                "BIN/NF_Ctrlram.bin", "0xFD0", "0x16810", "4032",
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
                "BIN/NF_Ctrlram.bin", "0x1F90", "0x1F810", "4032",
                "BIN/Normal_Ctrlram_R.bin", "0x0", "0x207D0", "12288",
                "BIN/MP_Ctrlram_R.bin", "0x0", "0x237D0", "9216",
                "BIN/VN_Ctrlram.bin", "0x0", "0x25BD0", "5728",
            ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x16000", "0x28000", "36864" ],
            [
                "MERGE_MODE", firmware,
                firmware, "0x0", "0x0", "217088",
                "BIN/NF_Ctrlram.bin", "0x0", "0x28800", "4048",
                "BIN/Normal_Ctrlram_L.bin", "0x0", "0x297D0", "12288",
                "BIN/MP_Ctrlram_L.bin", "0x0", "0x2C7D0", "9216",
                "BIN/VN_Ctrlram.bin", "0x0", "0x2EBD0", "5728",
            ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x0", "0x32DC0", "1120" ],
            [ "NT51927BASED_GEN_CRC_MODE", "CRC32", firmware, firmware ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x200", "0x1E230", "400" ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x200", "0x27230", "400" ],
            [ "MERGE_MODE", firmware, firmware, "0x0", "0x0", "217088", firmware, "0x200", "0x30230", "400" ],
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
            "nt51927-3chip-self-20260705");
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
