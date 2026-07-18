using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>FlashCode output-name parity and immutable-inspection projection tests.</summary>
public sealed class WorkbenchOutputNamingTests
{
    /// <summary>Verifies FlashCode output naming reads DP/FWConfig metadata outside the UI layer.</summary>
    [Fact]
    public void FlashCodeOutputNameUsesCatalogBackedDpAndTpMetadata()
    {
        string dpPath = GoldenPath("inputs/51926/dp.bin");
        string tpPath = GoldenPath("inputs/51926/tp.bin");

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
        string dpPath = GoldenPath("inputs/51926/dp.bin");
        string tpPath = GoldenPath("inputs/51926/tp.bin");
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
