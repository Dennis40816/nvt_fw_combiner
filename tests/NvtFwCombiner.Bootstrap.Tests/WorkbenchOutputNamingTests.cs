using System.Text.Json;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>FlashCode output-name parity and immutable-inspection projection tests.</summary>
public sealed class WorkbenchOutputNamingTests
{
    /// <summary>Every direct CtrlRAM owner golden exposes usable full-flash D/T naming metadata.</summary>
    [Fact]
    public void DirectCtrlRamGoldenBasesExposeOutputNamingMetadata()
    {
        using JsonDocument manifest = CanonicalGoldenTestData.LoadDirectWorkflowManifest("ctrlram-replace");
        var gaps = new List<string>();

        foreach (JsonElement goldenCase in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            string icId = $"NT{goldenCase.GetProperty("ic").GetString()}";
            bool carriesStandardMergeNamingInputs =
                goldenCase.TryGetProperty("artifacts", out JsonElement artifacts) &&
                artifacts.EnumerateArray().Any(static artifact =>
                    artifact.TryGetProperty("sourceRole", out JsonElement sourceRole) &&
                    sourceRole.GetString() == "standard-merge-dp-input") &&
                artifacts.EnumerateArray().Any(static artifact =>
                    artifact.TryGetProperty("sourceRole", out JsonElement sourceRole) &&
                    sourceRole.GetString() == "standard-merge-tp-input");
            if (!WorkbenchCompositionService.GetSupportedIcIds().Contains(icId, StringComparer.Ordinal))
            {
                // Retired fixtures remain immutable historical evidence, not production naming claims.
                continue;
            }

            if (!carriesStandardMergeNamingInputs)
            {
                // A narrow hot-fix golden does not claim the DP/TP metadata needed by output naming.
                continue;
            }

            string caseId = goldenCase.GetProperty("caseId").GetString()!;
            WorkbenchOutputFileNameSuggestion suggestion = InspectDirectCtrlRamGolden(icId, caseId);

            if (!suggestion.HasDpVersion || !suggestion.HasTpVersion)
            {
                gaps.Add(caseId);
            }
        }

        Assert.Empty(gaps);
    }

    /// <summary>Every direct CtrlRAM owner golden projects its canonical DPCMI and TP version facts.</summary>
    [Theory]
    [InlineData("NT51923", "nt51923-fw141-single-auto-prj-662-20260717", "8000", "8100")]
    [InlineData("NT51923", "nt51923-fw141-cascade3-auto-prj-734-20260717", "8200", "8100")]
    [InlineData("NT51926", "nt51926-fw141-single-auto-prj-747-20260717", "8000", "8000")]
    [InlineData("NT51926", "nt51926-fw141-cascade2-auto-prj-597-20260717", "0200", "0600")]
    [InlineData("NT51926", "nt51926-fw200-single-auto-prj-597-20260718", "0200", "FF00")]
    [InlineData("NT51926", "nt51926-fw200-cascade3-auto-prj-597-20260718", "0200", "0000")]
    [InlineData("NT51927", "nt51927-fw141-single-auto-prj-529-20260717", "0400", "0100")]
    [InlineData("NT51929", "nt51929-fw200-single-auto-prj-594-20260717", "0600", "0500")]
    [InlineData("NT51932", "nt51932-fw200-cascade3-auto-prj-525-20260718", "0200", "8800")]
    [InlineData("NT51950", "nt51950-fw200-single-auto-prj-676-20260717", "8600", "8000")]
    [InlineData("NT51951", "nt51951-fw200-single-auto-prj-695-20260718", "0600", "0300")]
    public void DirectCtrlRamGoldenBaseKeepsExactOutputNamingMetadata(
        string icId,
        string caseId,
        string expectedDp,
        string expectedTp)
    {
        WorkbenchOutputFileNameSuggestion suggestion = InspectDirectCtrlRamGolden(icId, caseId);

        Assert.Equal(expectedDp, suggestion.DpVersionToken);
        Assert.Equal(expectedTp, suggestion.TpVersionToken);
    }

    /// <summary>Verifies FlashCode output naming reads DP/FWConfig metadata outside the UI layer.</summary>
    [Fact]
    public void FlashCodeOutputNameUsesCanonicalDpAndTpMetadata()
    {
        string dpPath = GoldenArtifactPath("51926", "dp-input");
        string tpPath = GoldenArtifactPath("51926", "tp-input");

        WorkbenchOutputFileNameSuggestion suggestion = WorkbenchCompositionService.CreateFlashCodeOutputFileName(
            "51926",
            [
                new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Dp, dpPath),
                new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Tp, tpPath),
            ],
            new DateOnly(2026, 7, 8));

        Assert.Equal("NT51926_FlashCode_D0100T0100_20260708.bin", suggestion.FileName);
        Assert.Equal("0100", suggestion.DpVersionToken);
        Assert.True(suggestion.HasDpVersion);
        Assert.Equal("0100", suggestion.TpVersionToken);
        Assert.True(suggestion.HasTpVersion);
        Assert.Equal("20260708", suggestion.DateToken);
    }

    /// <summary>Already-inspected output naming preserves path-backed D/T token and fallback semantics.</summary>
    [Fact]
    public void FlashCodeOutputNameInspectionProjectionMatchesPathBackedSuggestion()
    {
        string dpPath = GoldenArtifactPath("51926", "dp-input");
        string tpPath = GoldenArtifactPath("51926", "tp-input");
        DateOnly date = new(2026, 7, 8);
        WorkbenchOutputFileNameSuggestion pathBacked =
            WorkbenchCompositionService.CreateFlashCodeOutputFileName(
                "51926",
                [
                    new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Dp, dpPath),
                    new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Tp, tpPath),
                ],
                date);
        WorkbenchOutputFileNameSuggestion inspected =
            WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                "51926",
                [
                    new WorkbenchOutputNameInspectionCandidate(
                        WorkbenchOutputNameCandidateKind.Dp,
                        WorkbenchCompositionService.InspectFirmware("51926", dpPath)),
                    new WorkbenchOutputNameInspectionCandidate(
                        WorkbenchOutputNameCandidateKind.Tp,
                        WorkbenchCompositionService.InspectFirmware("51926", tpPath)),
                ],
                date);

        Assert.Equal(pathBacked, inspected);
    }

    /// <summary>NT51950 uses TP ChipNumber to decode its CMI DP token for both path and inspection naming.</summary>
    [Fact]
    public void Nt51950OutputNameUsesSingleIcCmiDpVersion()
    {
        string dpPath = GoldenArtifactPath("51950", "dp-input");
        string tpPath = GoldenArtifactPath("51950", "tp-input");
        DateOnly date = new(2026, 7, 20);
        WorkbenchOutputFileNameSuggestion pathBacked = WorkbenchCompositionService.CreateFlashCodeOutputFileName(
            "NT51950",
            [
                new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Dp, dpPath),
                new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Tp, tpPath),
            ],
            date);
        WorkbenchFirmwareInspection dpInspection = WorkbenchCompositionService.InspectFirmware(
            "NT51950",
            dpPath,
            tpPath);
        WorkbenchOutputFileNameSuggestion inspected =
            WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                "NT51950",
                [new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Dp, dpInspection)],
                date);

        Assert.Equal("CC00", pathBacked.DpVersionToken);
        Assert.Equal("NT51950_FlashCode_DCC00T0400_20260720.bin", pathBacked.FileName);
        Assert.Equal("CC00", inspected.DpVersionToken);
        Assert.True(inspected.HasDpVersion);
    }

    /// <summary>NT51951 keeps DP naming pending until its declared TP prerequisite is available.</summary>
    [Fact]
    public void Nt51951OutputNameWithoutTpKeepsDpVersionUnknown()
    {
        string dpPath = GoldenArtifactPath("51951", "dp-input");

        WorkbenchOutputFileNameSuggestion suggestion = WorkbenchCompositionService.CreateFlashCodeOutputFileName(
            "NT51951",
            [new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Dp, dpPath)],
            new DateOnly(2026, 7, 20));

        Assert.Equal("xxxx", suggestion.DpVersionToken);
        Assert.Equal("NT51951_FlashCode_DxxxxTxxxx_20260720.bin", suggestion.FileName);
        Assert.False(suggestion.HasDpVersion);
    }

    /// <summary>CtrlRAM-style workflows preserve DP, so their base is the version source when no DP input exists.</summary>
    [Fact]
    public void FlashCodeOutputNameUsesBaseDpVersionOnlyWhenNoDpInputExists()
    {
        static WorkbenchFirmwareInspection Inspection(string? dpVersion)
        {
            return new WorkbenchFirmwareInspection(
                null,
                null,
                dpVersion is null ? null : new WorkbenchDpVersionMetadata(dpVersion),
                null,
                null,
                null);
        }

        DateOnly date = new(2026, 7, 20);
        WorkbenchOutputFileNameSuggestion ctrlRam =
            WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                "NT51923",
                [
                    new WorkbenchOutputNameInspectionCandidate(
                        WorkbenchOutputNameCandidateKind.Base,
                        Inspection("8102")),
                    new WorkbenchOutputNameInspectionCandidate(
                        WorkbenchOutputNameCandidateKind.CtrlRam,
                        Inspection(null)),
                ],
                date);
        WorkbenchOutputFileNameSuggestion unreadableDp =
            WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                "NT51923",
                [
                    new WorkbenchOutputNameInspectionCandidate(
                        WorkbenchOutputNameCandidateKind.Base,
                        Inspection("8102")),
                    new WorkbenchOutputNameInspectionCandidate(
                        WorkbenchOutputNameCandidateKind.Dp,
                        Inspection(null)),
                ],
                date);
        WorkbenchOutputFileNameSuggestion pathBackedCtrlRam =
            WorkbenchCompositionService.CreateFlashCodeOutputFileName(
                "NT51926",
                [
                    new WorkbenchOutputNameCandidate(
                        WorkbenchOutputNameCandidateKind.Base,
                        GoldenArtifactPath("51926", "expected-output")),
                ],
                date);

        Assert.Equal("8102", ctrlRam.DpVersionToken);
        Assert.True(ctrlRam.HasDpVersion);
        Assert.Equal("xxxx", unreadableDp.DpVersionToken);
        Assert.False(unreadableDp.HasDpVersion);
        Assert.Equal("0100", pathBackedCtrlRam.DpVersionToken);
        Assert.True(pathBackedCtrlRam.HasDpVersion);
    }

    /// <summary>Inspection candidates preserve role priority, valid-bar fallback, and unknown behavior.</summary>
    [Fact]
    public void FlashCodeOutputNameInspectionProjectionPreservesTpCandidatePriority()
    {
        static WorkbenchFirmwareInspection Firmware(byte version, bool valid = true)
        {
            return new WorkbenchFirmwareInspection(
                null,
                new WorkbenchFirmwareConfigMetadata(
                    0,
                    "1.0.0",
                    version,
                    0,
                    valid,
                    0,
                    1,
                    0,
                    null,
                    default),
                null,
                null,
                null,
                null);
        }

        (string, WorkbenchOutputNameInspectionCandidate[] Candidates, string ExpectedTp)[] cases =
        [
            (
                "TP wins regardless of input order",
                Candidates:
                [
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, Firmware(0x30)),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Dp, Firmware(0x40)),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.CtrlRam, Firmware(0x20)),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Tp, Firmware(0x10)),
                ],
                ExpectedTp: "1000"),
            (
                "CtrlRAM wins over Base and DP",
                Candidates:
                [
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Dp, Firmware(0x40)),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, Firmware(0x30)),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.CtrlRam, Firmware(0x20)),
                ],
                ExpectedTp: "2000"),
            (
                "Invalid TP falls through to valid CtrlRAM",
                Candidates:
                [
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Tp, Firmware(0x10, valid: false)),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.CtrlRam, Firmware(0x20)),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, Firmware(0x30)),
                ],
                ExpectedTp: "2000"),
            (
                "Base wins over DP",
                Candidates:
                [
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Dp, Firmware(0x40)),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, Firmware(0x30)),
                ],
                ExpectedTp: "3000"),
            (
                "Null and invalid candidates remain unknown",
                Candidates:
                [
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Tp, null),
                    new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, Firmware(0x30, valid: false)),
                ],
                ExpectedTp: "xxxx"),
        ];

        foreach ((string name, WorkbenchOutputNameInspectionCandidate[] candidates, string expectedTp) in cases)
        {
            WorkbenchOutputFileNameSuggestion suggestion =
                WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                    "NT51926",
                    candidates,
                    new DateOnly(2026, 7, 18));

            Assert.True(
                string.Equals(expectedTp, suggestion.TpVersionToken, StringComparison.Ordinal),
                $"{name}: expected TP token {expectedTp}, actual {suggestion.TpVersionToken}.");
        }
    }

    /// <summary>Verifies missing metadata produces explicit unknown tokens without guessing offsets.</summary>
    [Fact]
    public void FlashCodeOutputNameUsesUnknownTokensWhenMetadataIsMissing()
    {
        WorkbenchOutputFileNameSuggestion suggestion = WorkbenchCompositionService.CreateFlashCodeOutputFileName(
            "NT51950",
            [],
            new DateOnly(2026, 7, 8));

        Assert.Equal("NT51950_FlashCode_DxxxxTxxxx_20260708.bin", suggestion.FileName);
        Assert.Equal("xxxx", suggestion.DpVersionToken);
        Assert.False(suggestion.HasDpVersion);
        Assert.Equal("xxxx", suggestion.TpVersionToken);
        Assert.False(suggestion.HasTpVersion);
    }

    /// <summary>The confirmed CtrlRAM edit owns the suggested TP token before output selection.</summary>
    [Fact]
    public void CtrlRamVersionEditOverridesInspectedTpToken()
    {
        WorkbenchOutputFileNameSuggestion suggestion =
            WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                "NT51926",
                [new WorkbenchOutputNameInspectionCandidate(
                    WorkbenchOutputNameCandidateKind.Base,
                    null)],
                new WorkbenchCtrlRamFirmwareVersionEdit(0x2A, 0x0C),
                new DateOnly(2026, 7, 22));

        Assert.Equal("NT51926_FlashCode_DxxxxT2A0C_20260722.bin", suggestion.FileName);
        Assert.Equal("2A0C", suggestion.TpVersionToken);
        Assert.True(suggestion.HasTpVersion);
    }

    /// <summary>CtrlRAM Replace names an inspected TP FW base separately without changing FlashCode naming elsewhere.</summary>
    [Fact]
    public void CtrlRamTpFirmwareBaseUsesTpFwNameOnlyForCtrlRamReplace()
    {
        WorkbenchFirmwareInspection tpFirmwareBase = new(
            null,
            null,
            null,
            null,
            null,
            null,
            WorkbenchBaseFirmwareArtifactKind.TpFirmware);
        WorkbenchFirmwareInspection flashCodeBase = tpFirmwareBase with
        {
            BaseFirmwareArtifactKind = WorkbenchBaseFirmwareArtifactKind.FlashCode,
        };
        WorkbenchCtrlRamFirmwareVersionEdit edit = new(0x05, 0x00);
        DateOnly date = new(2026, 7, 24);

        WorkbenchOutputFileNameSuggestion tpFirmware =
            WorkbenchCompositionService.CreateCtrlRamReplaceOutputFileNameFromInspections(
                "NT51950",
                [new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, tpFirmwareBase)],
                edit,
                date);
        WorkbenchOutputFileNameSuggestion flashCode =
            WorkbenchCompositionService.CreateCtrlRamReplaceOutputFileNameFromInspections(
                "NT51950",
                [new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, flashCodeBase)],
                edit,
                date);
        WorkbenchOutputFileNameSuggestion ordinary =
            WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                "NT51950",
                [new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, tpFirmwareBase)],
                edit,
                date);

        Assert.Equal("NT51950_TPFW_T0500_20260724.bin", tpFirmware.FileName);
        Assert.Equal("NT51950_FlashCode_DxxxxT0500_20260724.bin", flashCode.FileName);
        Assert.Equal("NT51950_FlashCode_DxxxxT0500_20260724.bin", ordinary.FileName);
    }

    /// <summary>NT51950's registered TP input uses the 0x37000 TP shape and its Backup TP version.</summary>
    [Fact]
    public void Nt51950TpFirmwareGoldenUsesTpFwCtrlRamName()
    {
        WorkbenchFirmwareInspection inspection = WorkbenchCompositionService.InspectFirmware(
            "NT51950",
            GoldenArtifactPath("51950", "tp-input"));

        WorkbenchOutputFileNameSuggestion suggestion =
            WorkbenchCompositionService.CreateCtrlRamReplaceOutputFileNameFromInspections(
                "NT51950",
                [new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, inspection)],
                date: new DateOnly(2026, 7, 24));

        Assert.Equal(WorkbenchBaseFirmwareArtifactKind.TpFirmware, inspection.BaseFirmwareArtifactKind);
        Assert.Equal("NT51950_TPFW_T0400_20260724.bin", suggestion.FileName);
    }

    private static WorkbenchOutputFileNameSuggestion InspectDirectCtrlRamGolden(
        string icId,
        string caseId,
        DateOnly? date = null)
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        JsonElement expected = goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(static artifact =>
                string.Equals(artifact.GetProperty("role").GetString(), "expected", StringComparison.Ordinal));
        WorkbenchFirmwareInspection inspection = WorkbenchCompositionService.InspectFirmware(
            icId,
            CanonicalGoldenTestData.ArtifactPath(expected));
        return WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
            icId,
            [new WorkbenchOutputNameInspectionCandidate(WorkbenchOutputNameCandidateKind.Base, inspection)],
            date ?? new DateOnly(2026, 7, 22));
    }
}
