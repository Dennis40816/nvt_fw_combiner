using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private const string CtrlRamByteCaseId = "nt51929-fw200-single-auto-prj-594-20260717";
    private const string AbByteCaseId = "nt51929-ab-t05-d06";
    private static readonly string[] PostbuildCtrlRamSources = ["nf", "normal", "vn"];

    /// <summary>The real CLI retains the existing complete NT51929 output and its approved CRC-only Golden bounds.</summary>
    [Fact]
    public async Task DefaultCtrlRamBuildMatchesExistingGoldenContractAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real CtrlRAM Golden Build requires the packaged Windows processor.");
        }

        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CtrlRamByteCaseId);
        string basePath = CliGoldenByteEvidence.ArtifactPath(goldenCase, "expected-output");
        string[] sources = [.. PostbuildCtrlRamSources.SelectMany(source => new[]
        {
            "--ctrlram", $"replace-ctrlram-{source}={CliGoldenByteEvidence.ArtifactPath(goldenCase, $"postbuild-{source}-ctrlram")}",
        })];
        using var workspace = TempWorkspace.Create("ctrlram-cli-default-golden");
        CliGoldenByteEvidence.BuildEvidence built = await CliGoldenByteEvidence.BuildAsync(workspace, "default",
            ["ctrlram-replace", "build", "--profile", "NT51929", "--ic-num", "single", "--base", basePath, .. sources]);

        CanonicalGoldenDifferenceResult differences = CanonicalGoldenTestData.AssertAllowedByteDifferences(
            goldenCase, File.ReadAllBytes(basePath), built.Bytes);
        Assert.Equal(goldenCase.GetProperty("phaseBResult").GetProperty("differenceCounts").GetProperty("ownerToV2").GetInt64(),
            differences.DifferenceCount);
        Assert.Equal(goldenCase.GetProperty("phaseBResult").GetProperty("v2OutputSha256").GetString(),
            CliGoldenByteEvidence.Hash(built.Bytes));
    }

    /// <summary>Every selected bank writes the real replacement, while every unselected bank retains every Base byte.</summary>
    [Theory]
    [InlineData("NT51929", AbByteCaseId, "nt51929-andes-normal-cross-110us-to-115us-20260724", "normal-ctrlram-input", "a")]
    [InlineData("NT51929", AbByteCaseId, "nt51929-andes-normal-cross-110us-to-115us-20260724", "normal-ctrlram-input", "b")]
    [InlineData("NT51929", AbByteCaseId, "nt51929-andes-normal-cross-110us-to-115us-20260724", "normal-ctrlram-input", "both")]
    [InlineData("NT51950", "nt51950-ab-osd-d03t02-20260924", "nt51950-fw200-single-auto-prj-676-20260717", "postbuild-normal-ctrlram", "a")]
    [InlineData("NT51950", "nt51950-ab-osd-d03t02-20260924", "nt51950-fw200-single-auto-prj-676-20260717", "postbuild-normal-ctrlram", "b")]
    [InlineData("NT51950", "nt51950-ab-osd-d03t02-20260924", "nt51950-fw200-single-auto-prj-676-20260717", "postbuild-normal-ctrlram", "both")]
    public async Task CtrlRamBankBuildWritesOnlySelectedRealBankRangesAsync(
        string profile, string baseCaseId, string sourceCaseId, string sourceArtifactId, string bank)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real CtrlRAM Golden Build requires the packaged Windows processor.");
        }

        JsonElement baseCase = CanonicalGoldenTestData.LoadDirectCase("ab-merge", baseCaseId);
        JsonElement sourceCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", sourceCaseId);
        string basePath = CliGoldenByteEvidence.ArtifactPath(baseCase, "expected-output");
        string sourcePath = CliGoldenByteEvidence.ArtifactPath(sourceCase, sourceArtifactId);
        string[] inputs = ["ctrlram-replace", "build", "--profile", profile, "--ic-num", "single", "--base", basePath,
            "--ctrlram", $"replace-ctrlram-normal={sourcePath}"];
        using var workspace = TempWorkspace.Create("ctrlram-cli-real-bank");
        CliGoldenByteEvidence.BuildEvidence both = await CliGoldenByteEvidence.BuildAsync(workspace, "both",
            [.. inputs, "--bank", "both"]);
        CliGoldenByteEvidence.BuildEvidence selected = bank == "both" ? both :
            await CliGoldenByteEvidence.BuildAsync(workspace, bank, [.. inputs, "--bank", bank]);
        byte[] reference = File.ReadAllBytes(basePath);
        byte[] source = File.ReadAllBytes(sourcePath);
        Assert.Equal(reference.Length, selected.Bytes.Length);
        ByteRange[] bankRanges = [CliGoldenByteEvidence.TargetRange(both.Report, "a-bank/publish"),
            CliGoldenByteEvidence.TargetRange(both.Report, "b-bank/publish")];
        foreach (string bankId in new[] { "a-bank", "b-bank" })
        {
            ByteRange range = CliGoldenByteEvidence.TargetRange(both.Report, bankId + "/publish");
            bool writesBank = bank == "both" || bankId.StartsWith(bank, StringComparison.Ordinal);
            if (!writesBank)
            {
                CliGoldenByteEvidence.EqualRange(reference, selected.Bytes, range);
                Assert.DoesNotContain(selected.Report.GetProperty("Operations").EnumerateArray(),
                    operation => operation.GetProperty("OperationId").GetString() == bankId + "/publish");
                continue;
            }

            if (bank != "both")
            {
                Assert.Equal(range, CliGoldenByteEvidence.TargetRange(selected.Report, bankId + "/publish"));
                CliGoldenByteEvidence.EqualRange(both.Bytes, selected.Bytes, range);
            }
            Assert.False(reference.AsSpan(checked((int)range.Start), checked((int)range.Length))
                .SequenceEqual(selected.Bytes.AsSpan(checked((int)range.Start), checked((int)range.Length))));
            AssertPublishedReplacement(selected.Report, bankId, source, selected.Bytes);
        }

        // Includes a real OSD tail outside the two declared local bank publications.
        for (int offset = 0; offset < reference.Length; offset++)
        {
            if (!bankRanges[0].Contains(offset) && !bankRanges[1].Contains(offset) && reference[offset] != selected.Bytes[offset])
            {
                Assert.Fail($"Preserved output-image offset 0x{offset:X} changed.");
            }
        }
    }

    /// <summary>Requested Standard/AB versions read back exactly; all replacement payloads remain identical and differences stay within version/non-payload processor writes.</summary>
    [Theory]
    [InlineData(false, "both", true, false)]
    [InlineData(true, "a", true, false)]
    [InlineData(true, "b", false, true)]
    [InlineData(true, "both", true, true)]
    public async Task CtrlRamVersionBuildChangesOnlyDeclaredWritesAndUsesAcceptedNameAsync(
        bool ab, string bank, bool editA, bool editB)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real CtrlRAM Golden Build requires the packaged Windows processor.");
        }

        JsonElement baseCase = CanonicalGoldenTestData.LoadDirectCase(ab ? "ab-merge" : "ctrlram-replace",
            ab ? AbByteCaseId : CtrlRamByteCaseId);
        JsonElement sourceCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CtrlRamByteCaseId);
        string basePath = CliGoldenByteEvidence.ArtifactPath(baseCase, "expected-output");
        Dictionary<string, string> sourcePaths = PostbuildCtrlRamSources.ToDictionary(
            static source => CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix + source,
            source => CliGoldenByteEvidence.ArtifactPath(sourceCase, $"postbuild-{source}-ctrlram"), StringComparer.Ordinal);
        string[] sources = [.. sourcePaths.SelectMany(static pair => new[] { "--ctrlram", $"{pair.Key}={pair.Value}" })];
        string[] selection = ab ? ["--bank", bank] : [];
        string[] inputs = ["ctrlram-replace", "build", "--profile", "NT51929", "--ic-num", "single", "--base", basePath,
            .. sources, .. selection];
        string[] aVersion = editA ? ["--a-firmware-version", "21", "--a-firmware-sub-version", "32"] : [];
        string[] bVersion = editB ? ["--b-firmware-version", "29", "--b-firmware-sub-version", "41"] : [];
        string[] versions = ab ? [.. aVersion, .. bVersion] : ["--firmware-version", "21", "--firmware-sub-version", "32"];
        using var workspace = TempWorkspace.Create("ctrlram-cli-real-version");
        CliGoldenByteEvidence.BuildEvidence control = await CliGoldenByteEvidence.BuildAsync(workspace, "control", inputs);
        CliGoldenByteEvidence.BuildEvidence edited = await CliGoldenByteEvidence.BuildAsync(workspace, "edited",
            [.. inputs, .. versions], automaticName: true);

        CtrlRamAuthoringDraftState draft = ab
            ? new AbCtrlRamDraftState(bank == "a" ? AbCtrlRamBankSelection.A : bank == "b" ? AbCtrlRamBankSelection.B : AbCtrlRamBankSelection.Both,
                editA ? new(0x21, 0x32) : null, editB ? new(0x29, 0x41) : null)
            : new CtrlRamFirmwareVersionDraftState(0x21, 0x32);
        AssertVersionDifferencesAreDeclared(control, edited, draft);
        AssertAcceptedOutputName(basePath, sourcePaths, draft, edited.Report);
    }
}
