using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>FlashCode output-name parity and immutable-inspection projection tests.</summary>
public sealed class WorkbenchOutputNamingTests
{
    /// <summary>Verifies FlashCode output naming reads DP/FWConfig metadata outside the UI layer.</summary>
    [Fact]
    public void FlashCodeOutputNameUsesCatalogBackedDpAndTpMetadata()
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

        Assert.Equal("NT51926_FlashCode_D0102T0100_20260708.bin", suggestion.FileName);
        Assert.Equal("0102", suggestion.DpVersionToken);
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

    /// <summary>NT51951 uses its fixed CMI location without requiring an IC-count context.</summary>
    [Fact]
    public void Nt51951OutputNameUsesFixedCmiDpVersion()
    {
        string dpPath = GoldenArtifactPath("51951", "dp-input");

        WorkbenchOutputFileNameSuggestion suggestion = WorkbenchCompositionService.CreateFlashCodeOutputFileName(
            "NT51951",
            [new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Dp, dpPath)],
            new DateOnly(2026, 7, 20));

        Assert.Equal("0500", suggestion.DpVersionToken);
        Assert.Equal("NT51951_FlashCode_D0500Txxxx_20260720.bin", suggestion.FileName);
        Assert.True(suggestion.HasDpVersion);
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
        Assert.Equal("0102", pathBackedCtrlRam.DpVersionToken);
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
}
