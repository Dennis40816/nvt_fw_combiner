namespace NvtFwCombiner.Bootstrap.Tests;

internal static class FirmwareInspectionTestSupport
{
    internal static FirmwareInspectionSnapshot InspectFirmware(
        string icId,
        string path,
        string? tpPath = null,
        CtrlRamInspectionRequest? ctrlRamRequest = null)
    {
        return BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical,
            icId,
            [new FirmwareInspectionSnapshotInput("test", path, tpPath, ctrlRamRequest)])
            .Single()
            .Inspection;
    }

    internal static FirmwareInspectionSnapshot InspectFirmware(
        string icId,
        string path,
        string? tpPath,
        CtrlRamInspectionRequest? ctrlRamRequest,
        Func<string, byte[]?> readFirmwareImage)
    {
        return BuiltInFirmwareInspection.InspectFirmware(BootstrapTestHost.Canonical,
            icId,
            path,
            tpPath,
            ctrlRamRequest,
            readFirmwareImage);
    }

    internal static DpVersionMetadata? TryReadDpVersionMetadata(
        string icId,
        string path)
    {
        return InspectFirmware(icId, path).DpVersion;
    }

    internal static CmiDpCodeMetadata? TryReadCmiDpCodeMetadata(
        string icId,
        string path,
        string? tpPath = null)
    {
        return InspectFirmware(icId, path, tpPath).CmiDpCode;
    }

    internal static FirmwareContextSuggestion? TryReadFirmwareContextSuggestion(
        string icId,
        string path)
    {
        return InspectFirmware(icId, path).ContextSuggestion;
    }
}
