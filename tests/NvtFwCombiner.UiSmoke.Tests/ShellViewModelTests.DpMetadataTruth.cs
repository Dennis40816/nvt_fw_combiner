using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Missing terminal classification cannot restore DP facts from the legacy Base shape.</summary>
    [Theory]
    [InlineData(BaseFirmwareArtifactKind.Unknown)]
    [InlineData(BaseFirmwareArtifactKind.TpFirmware)]
    [InlineData(BaseFirmwareArtifactKind.FlashCode)]
    public void MissingTerminalClassificationFailsClosedForEveryLegacyBaseKind(
        BaseFirmwareArtifactKind legacyBaseKind)
    {
        var inspection = new FirmwareInspectionSnapshot(
            "NT51927",
            null,
            null,
            null,
            null,
            null,
            legacyBaseKind);

        Assert.Empty(UiCompositionRunner.GetDpFirmwareSlotFacts(inspection));
    }

    /// <summary>The terminal DP decision wins both directions of a contradictory legacy Base shape.</summary>
    [Fact]
    public void TerminalDpMetadataDecisionWinsLegacyBaseShape()
    {
        var legacyFlashCode = new FirmwareInspectionSnapshot(
            "NT51927",
            null,
            new DpVersionMetadata("0102"),
            null,
            null,
            null,
            BaseFirmwareArtifactKind.FlashCode)
        {
            ArtifactClassification = CreateArtifactClassification(CompiledFirmwareArtifactKind.TpFirmware),
        };
        FirmwareInspectionSnapshot legacyTpFirmware = legacyFlashCode with
        {
            BaseFirmwareArtifactKind = BaseFirmwareArtifactKind.TpFirmware,
            ArtifactClassification = CreateArtifactClassification(CompiledFirmwareArtifactKind.FlashCode),
        };

        Assert.Empty(UiCompositionRunner.GetDpFirmwareSlotFacts(legacyFlashCode));
        Assert.NotEmpty(UiCompositionRunner.GetDpFirmwareSlotFacts(legacyTpFirmware));
    }

    private static CompiledFirmwareArtifactClassification CreateArtifactClassification(
        CompiledFirmwareArtifactKind kind)
    {
        return new CompiledFirmwareArtifactClassification(
            kind,
            [
                .. Enum.GetValues<CompiledFirmwareArtifactSignalKind>().Select(signalKind =>
                    new CompiledFirmwareArtifactSignal(
                        signalKind,
                        kind == CompiledFirmwareArtifactKind.TpFirmware &&
                        signalKind is CompiledFirmwareArtifactSignalKind.DpSourceCoverage or
                            CompiledFirmwareArtifactSignalKind.DpContentPlausibility
                            ? CompiledFirmwareArtifactSignalStatus.NotSatisfied
                            : CompiledFirmwareArtifactSignalStatus.Satisfied,
                        AddressSpaceId: null,
                        RequiredEndExclusive: 0,
                        FailedRange: null)),
            ]);
    }
}
