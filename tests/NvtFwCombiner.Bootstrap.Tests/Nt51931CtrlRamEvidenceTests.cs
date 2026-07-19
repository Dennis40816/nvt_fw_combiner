using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the support-neutral NT51931 AUTO_PRJ-158 diagnostic boundary.</summary>
public sealed class Nt51931CtrlRamEvidenceTests
{
    private const string CaseId = "nt51931-fw130-cascade6-auto-prj-158-20260718";
    private const string OwnerExpectedSha256 = "2268ac5b49df546a03e177b97858805f0f83fa58b3e55a3b1590899ce9fd07c3";
    private const string HistoricalFlashCodeSha256 = "d997b27199d93110f1f9753dd997fb12c6e814abfd22657e6b8b0c3baf400221";
    private const string OwnerToolSha256 = "778f6dcec718e809d41c118ca40ce056ac428bc932ed36d851bd842fd612af58";
    private const string RegisteredToolSha256 = "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf";
    private const string SelectedOutputSha256 = "f38fdecd95092d9bacabd8ca59c442cbdb601edd280c23efa088693cb256c594";

    private static readonly (int Start, int EndExclusive)[] ControlDifferenceRanges =
    [
        (0x1C, 0x20),
        (0xFC, 0x100),
        (0x1DA48, 0x1DA50),
        (0x1DA6C, 0x1DA70),
        (0x1DA7C, 0x1DA80),
        (0x1DA8C, 0x1DA90),
        (0x1DA9C, 0x1DAE8),
        (0x1DB2C, 0x1DB30),
    ];

    /// <summary>The exact intake stays hash-pinned without committing the recovered executable.</summary>
    [Fact]
    public void ExactPayloadAndToolEvidenceRemainHashPinned()
    {
        using JsonDocument manifest = ReadManifest();
        JsonElement ownerCase = ReadCase(manifest);
        Assert.Equal("expected-derived-self-replacement-control", ownerCase.GetProperty("baseKind").GetString());
        Assert.Equal(
            "nt51931-ctrlram-replace-fw130-cascade6",
            ownerCase.GetProperty("profileId").GetString());

        foreach (JsonElement entry in ownerCase.GetProperty("artifacts").EnumerateArray())
        {
            string path = RepositoryPaths.ManifestPath(GoldenRoot, entry);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        }

        JsonElement tool = Assert.Single(ownerCase.GetProperty("externalToolObservations").EnumerateArray());
        Assert.Equal(OwnerToolSha256, tool.GetProperty("sha256").GetString());
        Assert.Equal("1.2.0.4", tool.GetProperty("selfReportedVersion").GetString());
        Assert.False(tool.GetProperty("runtimeRegistrationAuthorized").GetBoolean());
        Assert.False(tool.GetProperty("redistributionAuthorized").GetBoolean());
        Assert.False(File.Exists(RepositoryPaths.FromRepositoryRoot(
            "external-tools", "legacy-combiner", "1.2.0.4", "Combiner.exe")));
    }

    /// <summary>The supplied D8DfT82 FlashCode is not misrepresented as the D8DT83 parity base.</summary>
    [Fact]
    public void SuppliedHistoricalFlashCodeIsNotTheExpectedBuild()
    {
        using JsonDocument manifest = ReadManifest();
        byte[] historical = ReadPayload(manifest, "NT51931_Flashcode_TM_1560_KVD_D8DfT82_3mux_nmos_20240721.bin");
        byte[] expected = ReadPayload(manifest, "NT51931_FlashCode_D8DT83_20260718.bin");

        Assert.Equal(HistoricalFlashCodeSha256, Hash(historical));
        Assert.Equal(OwnerExpectedSha256, Hash(expected));
        Assert.Equal(73645, CountDifferences(historical, expected));
        Assert.Equal(73637, CountDifferencesOutside(historical, expected, ControlDifferenceRanges));

        JsonElement historicalEntry = Assert.Single(
            ReadCase(manifest).GetProperty("diagnosticLegacyPaths").EnumerateArray());
        Assert.Equal("historical-non-same-build-flashcode", historicalEntry.GetProperty("classification").GetString());
        Assert.Equal(HistoricalFlashCodeSha256, historicalEntry.GetProperty("sha256").GetString());
    }

    /// <summary>Every physical CtrlRAM payload already matches the owner expected output.</summary>
    [Theory]
    [InlineData("NF_Ctrlram.bin", 0x16800, 616)]
    [InlineData("Normal_Ctrlram.bin", 0x177D0, 10240)]
    [InlineData("MP_Ctrlram.bin", 0x19FD0, 9216)]
    [InlineData("VN_Ctrlram.bin", 0x1C3D0, 5728)]
    [InlineData("DiffDLM.bin", 0x22800, 97280)]
    public void ExpectedDerivedControlHasZeroReplacementPayloadDrift(
        string fileName,
        int targetStart,
        int consumedBytes)
    {
        using JsonDocument manifest = ReadManifest();
        byte[] expected = ReadPayload(manifest, "NT51931_FlashCode_D8DT83_20260718.bin");
        byte[] source = ReadPayload(manifest, fileName);

        Assert.True(source.Length >= consumedBytes);
        Assert.Equal(source.AsSpan(0, consumedBytes), expected.AsSpan(targetStart, consumedBytes));
    }

    /// <summary>The conflicting owner BATs and selected registered-tool pairing remain explicit.</summary>
    [Fact]
    public void BatConflictIsResolvedByRegisteredToolModeParity()
    {
        using JsonDocument manifest = ReadManifest();
        JsonElement ownerCase = ReadCase(manifest);
        JsonElement phaseB = ownerCase.GetProperty("phaseBResult");
        Assert.Equal("nt51931-ctrlram-replace-fw130-cascade6", phaseB.GetProperty("routeProfileId").GetString());
        Assert.Equal(0, phaseB.GetProperty("differenceCounts").GetProperty("legacyToV2").GetInt32());
        string finalBatPath = RepositoryPaths.ManifestPath(
            GoldenRoot,
            ownerCase.GetProperty("artifacts").EnumerateArray().Single(entry =>
                StringComparer.Ordinal.Equals(
                    entry.GetProperty("sourceRole").GetString(),
                    "postbuild-command-evidence")));
        string finalCommand = File.ReadLines(finalBatPath).Single(line =>
            line.StartsWith("@output\\Combiner.exe ", StringComparison.Ordinal) &&
            line.Contains("BIN\\DiffDLM.bin", StringComparison.Ordinal));
        string[] finalArguments = finalCommand["@output\\Combiner.exe ".Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string historicalBatPath = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260717",
            "NT51931",
            "51931_1.3.0_PostbuildSetup.bat");
        string historicalCommand = File.ReadLines(historicalBatPath).Single(line =>
            line.StartsWith("@output\\Combiner.exe ", StringComparison.Ordinal) &&
            line.Contains("BIN\\DiffDLM.bin", StringComparison.Ordinal));
        string[] historicalArguments = historicalCommand["@output\\Combiner.exe ".Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] selectedArguments =
        [
            .. phaseB.GetProperty("selectedCommand").GetProperty("orderedArguments").EnumerateArray()
                .Select(argument => argument.GetString()!.Replace('/', '\\')),
        ];

        Assert.Equal("NT51930BASED_NORMAL_MODE", finalArguments[0]);
        Assert.Equal("NT51931BASED_NORMAL_MODE", historicalArguments[0]);
        Assert.Equal("NT51931BASED_NORMAL_MODE", selectedArguments[0]);
        Assert.Equal(selectedArguments[1..], finalArguments[1..]);
        JsonElement sourceConflict = phaseB.GetProperty("sourceBatConflict");
        Assert.Equal("NT51931BASED_NORMAL_MODE", sourceConflict.GetProperty("20260717").GetProperty("mode").GetString());
        Assert.Equal("NT51930BASED_NORMAL_MODE", sourceConflict.GetProperty("20260718").GetProperty("mode").GetString());
        Assert.Equal(sourceConflict.GetProperty("20260717").GetProperty("sha256").GetString(), Hash(File.ReadAllBytes(historicalBatPath)));
        Assert.Equal(sourceConflict.GetProperty("20260718").GetProperty("sha256").GetString(), Hash(File.ReadAllBytes(finalBatPath)));
        Assert.Equal(sourceConflict.GetProperty("20260717").GetProperty("diffDlmBytes").GetInt32().ToString(CultureInfo.InvariantCulture), historicalArguments[^1]);
        Assert.Equal(sourceConflict.GetProperty("20260718").GetProperty("diffDlmBytes").GetInt32().ToString(CultureInfo.InvariantCulture), finalArguments[^1]);

        JsonElement selected = phaseB.GetProperty("selectedCommand");
        Assert.Equal(RegisteredToolSha256, selected.GetProperty("toolSha256").GetString());
        JsonElement rejected = phaseB.GetProperty("rejectedPairing");
        Assert.Equal("NT51930BASED_NORMAL_MODE", rejected.GetProperty("mode").GetString());
        Assert.Equal("0xC0000005", rejected.GetProperty("exitCodeHex").GetString());
        Assert.True(rejected.GetProperty("outputUnchanged").GetBoolean());

        JsonElement parity = phaseB.GetProperty("modeParityExperiment");
        Assert.Equal(0, parity.GetProperty("fullByteDifferenceBytes").GetInt32());
        Assert.Equal(
            parity.GetProperty("registered113").GetProperty("outputSha256").GetString(),
            parity.GetProperty("owner1204Control").GetProperty("outputSha256").GetString());
        Assert.Equal(
            "legacy-combiner-1.13.0/NT51931BASED_NORMAL_MODE/CRC8",
            parity.GetProperty("selectedRuntimePairing").GetString());
        Assert.False(phaseB.GetProperty("expectedDerivedControl").GetProperty("fullByteParityToOwnerExpected").GetBoolean());
        Assert.True(phaseB.GetProperty("expectedDerivedControl").GetProperty("toolModeFullByteParityClaimed").GetBoolean());
        Assert.Equal(108, phaseB.GetProperty("expectedDerivedControl").GetProperty("differenceBytes").GetInt32());
        Assert.Equal(0, phaseB.GetProperty("expectedDerivedControl").GetProperty("replacementPayloadDifferenceBytes").GetInt32());
        Assert.Equal("out-of-scope-pre-step-nonblocking", phaseB.GetProperty("insertSidScope").GetString());
        Assert.False(IcSupportCatalog.SupportsWorkflow("NT51931", IcWorkflowIds.CtrlRamReplace));
    }

    /// <summary>The exact owner shape runs through V2 with locked full-byte and process evidence.</summary>
    [Fact]
    public async Task ExactOwnerShapeRunsThroughV2WithFullByteParityAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using JsonDocument manifest = ReadManifest();
        Dictionary<string, string> paths = ReadPayloadPaths(manifest);
        var immutableHashes = paths.ToDictionary(
            static pair => pair.Key,
            static pair => Hash(File.ReadAllBytes(pair.Value)),
            StringComparer.Ordinal);
        IReadOnlyDictionary<string, string> slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = paths["NT51931_FlashCode_D8DT83_20260718.bin"],
            ["replace-ctrlram-normal"] = paths["Normal_Ctrlram.bin"],
            ["replace-ctrlram-diff"] = paths["DiffDLM.bin"],
            ["replace-ctrlram-mp"] = paths["MP_Ctrlram.bin"],
            ["replace-ctrlram-vn"] = paths["VN_Ctrlram.bin"],
            ["replace-ctrlram-nf"] = paths["NF_Ctrlram.bin"],
        };
        using var workspace = TempWorkspace.Create("nfc-nt51931-fw130-route-parity");
        string v2OutputPath = workspace.PathFor("v2-output.bin");
        IExternalProcessor processor = Assert.IsType<IExternalProcessor>(
            ExternalProcessorFactory.CreateOrNull(),
            exactMatch: false);

        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51931",
            "cascade",
            slotPaths,
            build: true,
            v2OutputPath,
            firmwareVersionEdit: null,
            processor,
            TestContext.Current.CancellationToken);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2OutputPath);
        Assert.Equal(SelectedOutputSha256, Hash(v2Bytes));
        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        Assert.Equal("nt51931-ctrlram-replace-fw130-cascade6", v2Report.RootElement.GetProperty("ProfileId").GetString());
        AssertProcessEvidence(v2Report.RootElement);
        Assert.All(paths, pair => Assert.Equal(immutableHashes[pair.Key], Hash(File.ReadAllBytes(pair.Value))));
    }

    /// <summary>A different NT51931 build cannot enter the exact candidate route by matching only IC metadata.</summary>
    [Fact]
    public async Task HistoricalBaseFailsClosedAsync()
    {
        using JsonDocument manifest = ReadManifest();
        Dictionary<string, string> paths = ReadPayloadPaths(manifest);
        IReadOnlyDictionary<string, string> slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = paths["NT51931_Flashcode_TM_1560_KVD_D8DfT82_3mux_nmos_20240721.bin"],
            ["replace-ctrlram-normal"] = paths["Normal_Ctrlram.bin"],
            ["replace-ctrlram-diff"] = paths["DiffDLM.bin"],
            ["replace-ctrlram-mp"] = paths["MP_Ctrlram.bin"],
            ["replace-ctrlram-vn"] = paths["VN_Ctrlram.bin"],
            ["replace-ctrlram-nf"] = paths["NF_Ctrlram.bin"],
        };
        using var workspace = TempWorkspace.Create("nfc-nt51931-fw130-route-negative");

        string outputPath = workspace.PathFor("unsupported-output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51931",
            "cascade",
            slotPaths,
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(result, outputPath);
    }

    /// <summary>Wrong project, Common FW version, chip count, or selector cannot enter the exact V2 route.</summary>
    [Theory]
    [InlineData("cascade", 1, 3, 0, 6, 0xFFFF)]
    [InlineData("cascade", 1, 2, 0, 6, 0x131B)]
    [InlineData("cascade", 1, 3, 0, 5, 0x131B)]
    [InlineData("6", 1, 3, 0, 6, 0x131B)]
    public async Task UnreviewedShapesFailClosedAsync(
        string number,
        byte major,
        byte minor,
        byte additional,
        byte chipCount,
        ushort projectId)
    {
        using JsonDocument manifest = ReadManifest();
        Dictionary<string, string> paths = ReadPayloadPaths(manifest);
        using var workspace = TempWorkspace.Create("nfc-nt51931-fw130-negative-route");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = File.ReadAllBytes(paths["NT51931_FlashCode_D8DT83_20260718.bin"]);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int start = checked((int)metadata.FirmwareConfigStart);
        reference[start + FirmwareConfigLayout.CommonFwMajorVersionOffset] = major;
        reference[start + FirmwareConfigLayout.CommonFwMinorVersionOffset] = minor;
        reference[start + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = additional;
        reference[start + FirmwareConfigLayout.ChipNumberOffset] = chipCount;
        BinaryPrimitives.WriteUInt16LittleEndian(
            reference.AsSpan(start + FirmwareConfigLayout.ProjectIdOffset),
            projectId);
        File.WriteAllBytes(referencePath, reference);

        IReadOnlyDictionary<string, string> slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath,
            ["replace-ctrlram-normal"] = paths["Normal_Ctrlram.bin"],
            ["replace-ctrlram-diff"] = paths["DiffDLM.bin"],
            ["replace-ctrlram-mp"] = paths["MP_Ctrlram.bin"],
            ["replace-ctrlram-vn"] = paths["VN_Ctrlram.bin"],
            ["replace-ctrlram-nf"] = paths["NF_Ctrlram.bin"],
        };
        string outputPath = workspace.PathFor("unsupported-output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51931",
            number,
            slotPaths,
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(result, outputPath);
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
    }

    private static void AssertProcessEvidence(JsonElement report)
    {
        JsonElement session = Assert.Single(
            report.GetProperty("Operations").EnumerateArray(),
            operation => StringComparer.Ordinal.Equals(operation.GetProperty("Kind").GetString(), "RunExternalProcessor"));
        Assert.Equal("nfc.nt51931.ctrlram-postbuild-v1", session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        JsonElement command = Assert.Single(session.GetProperty("ExecutedCommands").EnumerateArray());
        using JsonDocument manifest = ReadManifest();
        string[] expectedArguments = [
            .. ReadCase(manifest).GetProperty("phaseBResult").GetProperty("selectedCommand")
                .GetProperty("orderedArguments").EnumerateArray()
                .Select(argument => argument.GetString()!.Replace('\\', '/')),
        ];
        Assert.Equal(expectedArguments, NormalizeArguments(command));
    }

    private static void AssertWorkflowNotSupported(WorkbenchRunResult result, string outputPath)
    {
        Assert.False(result.Succeeded, result.ReportJson);
        Assert.False(File.Exists(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == "replace.workflow.not-supported");
        Assert.False(report.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }

    private static string[] NormalizeArguments(JsonElement command)
    {
        string workingDirectory = command.GetProperty("WorkingDirectory").GetString()!;
        return [
            .. command.GetProperty("Arguments").EnumerateArray()
                .Select(argument => argument.GetString()!)
                .Select(argument => Path.IsPathRooted(argument)
                    ? Path.GetRelativePath(workingDirectory, argument).Replace('\\', '/')
                    : argument.Replace('\\', '/')),
        ];
    }

    private static JsonDocument ReadManifest()
    {
        return JsonDocument.Parse(
            CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId).GetRawText());
    }

    private static JsonElement ReadCase(JsonDocument manifest)
    {
        return manifest.RootElement;
    }

    private static byte[] ReadPayload(JsonDocument manifest, string originalFileName)
    {
        if (StringComparer.Ordinal.Equals(
                originalFileName,
                "NT51931_Flashcode_TM_1560_KVD_D8DfT82_3mux_nmos_20240721.bin"))
        {
            byte[] historical = File.ReadAllBytes(HistoricalFlashCodePath);
            Assert.Equal(HistoricalFlashCodeSha256, Hash(historical));
            return historical;
        }

        return File.ReadAllBytes(RepositoryPaths.ManifestPath(GoldenRoot, ReadPayloadEntry(manifest, originalFileName)));
    }

    private static JsonElement ReadPayloadEntry(JsonDocument manifest, string originalFileName)
    {
        return ReadCase(manifest).GetProperty("artifacts").EnumerateArray().Single(entry =>
            StringComparer.Ordinal.Equals(entry.GetProperty("originalFileName").GetString(), originalFileName));
    }

    private static Dictionary<string, string> ReadPayloadPaths(JsonDocument manifest)
    {
        var paths = ReadCase(manifest).GetProperty("artifacts")
            .EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("originalFileName").GetString()!,
                entry => RepositoryPaths.ManifestPath(GoldenRoot, entry),
                StringComparer.Ordinal);
        paths.Add(
            "NT51931_Flashcode_TM_1560_KVD_D8DfT82_3mux_nmos_20240721.bin",
            HistoricalFlashCodePath);
        return paths;
    }

    private static int CountDifferences(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        int count = Math.Abs(left.Length - right.Length);
        for (int index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            count += left[index] == right[index] ? 0 : 1;
        }

        return count;
    }

    private static int CountDifferencesOutside(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        IReadOnlyList<(int Start, int EndExclusive)> allowed)
    {
        int count = Math.Abs(left.Length - right.Length);
        for (int index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            count += left[index] != right[index] &&
                !allowed.Any(range => index >= range.Start && index < range.EndExclusive)
                    ? 1
                    : 0;
        }

        return count;
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

    private static string GoldenRoot => CanonicalGoldenTestData.Root;

    private static string HistoricalFlashCodePath => RepositoryPaths.FromRepositoryRoot(
        "testdata",
        "golden",
        "ctrlram-replace",
        "fixtures",
        "20260718",
        "NT51931",
        "replace",
        "ctrlram",
        "1.3.0",
        "cascade",
        "case-01",
        "NT51931_Flashcode_TM_1560_KVD_D8DfT82_3mux_nmos_20240721.bin");
}
