using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
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
        Assert.Equal("support-catalog-not-available", ownerCase.GetProperty("currentProfile").GetProperty("route").GetString());
        Assert.Equal("registered-tool-mode-parity-validated", ownerCase.GetProperty("targetV2").GetProperty("status").GetString());

        foreach (JsonElement entry in manifest.RootElement.GetProperty("payloads").EnumerateArray()
                     .Where(IsOwnerCaseEntry))
        {
            string path = RepositoryPaths.ManifestPath(GoldenRoot, entry);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        }

        JsonElement tool = manifest.RootElement.GetProperty("externalToolObservations").EnumerateArray()
            .Single(IsOwnerCaseEntry);
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

        JsonElement historicalEntry = ReadPayloadEntry(manifest, "NT51931_Flashcode_TM_1560_KVD_D8DfT82_3mux_nmos_20240721.bin");
        Assert.Equal("historical-non-same-build-flashcode", historicalEntry.GetProperty("role").GetString());
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
        string finalBatPath = RepositoryPaths.ManifestPath(
            GoldenRoot,
            manifest.RootElement.GetProperty("supportingFiles").EnumerateArray().Single(IsOwnerCaseEntry));
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

    private static JsonDocument ReadManifest()
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenRoot, "manifest.20260718.json")));
    }

    private static JsonElement ReadCase(JsonDocument manifest)
    {
        return manifest.RootElement.GetProperty("cases").EnumerateArray().Single(IsOwnerCaseEntry);
    }

    private static byte[] ReadPayload(JsonDocument manifest, string originalFileName)
    {
        return File.ReadAllBytes(RepositoryPaths.ManifestPath(GoldenRoot, ReadPayloadEntry(manifest, originalFileName)));
    }

    private static JsonElement ReadPayloadEntry(JsonDocument manifest, string originalFileName)
    {
        return manifest.RootElement.GetProperty("payloads").EnumerateArray().Single(entry =>
            IsOwnerCaseEntry(entry) &&
            StringComparer.Ordinal.Equals(entry.GetProperty("originalFileName").GetString(), originalFileName));
    }

    private static bool IsOwnerCaseEntry(JsonElement entry)
    {
        return StringComparer.Ordinal.Equals(entry.GetProperty("caseId").GetString(), CaseId);
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

    private static string GoldenRoot => RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
}
