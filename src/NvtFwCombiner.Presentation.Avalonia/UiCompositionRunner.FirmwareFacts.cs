using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets compact firmware facts decoded from a selected BIN file.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetFirmwareSlotFacts(
        string icId,
        string path,
        bool includeInvalid = false)
    {
        WorkbenchFirmwareConfigMetadata? metadata = WorkbenchCompositionService.TryReadFirmwareConfigMetadata(
            icId,
            path);
        if (metadata is null || (!metadata.IsFirmwareVersionBarValid && !includeInvalid))
        {
            return [];
        }

        string firmwareVersion = metadata.IsFirmwareVersionBarValid
            ? FormattableString.Invariant($"0x{metadata.FirmwareVersion:X2}.0x{metadata.FirmwareSubVersion:X2}")
            : FormattableString.Invariant($"0x{metadata.FirmwareVersion:X2}.0x{metadata.FirmwareSubVersion:X2} (bar 0x{metadata.FirmwareVersionBar:X2} mismatch)");
        List<FirmwareSlotFactViewModel> facts =
        [
            new("Common FW", metadata.CommonFwVersion),
            new("FW", firmwareVersion, !metadata.IsFirmwareVersionBarValid),
            new("PID", FormattableString.Invariant($"0x{metadata.ProjectId:X4}")),
        ];
        if (!string.IsNullOrWhiteSpace(metadata.PostbuildCategory))
        {
            facts.Add(new FirmwareSlotFactViewModel("Refresh", metadata.PostbuildCategory));
        }

        return facts;
    }

    /// <summary>Gets compact DP version facts decoded using gen_flash standard-merge version rules.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetDpFirmwareSlotFacts(string icId, string path)
    {
        WorkbenchDpVersionMetadata? metadata = WorkbenchCompositionService.TryReadDpVersionMetadata(
            icId,
            path);
        return metadata is null
            ? [new FirmwareSlotFactViewModel("DP", "Pending", true)]
            : [new FirmwareSlotFactViewModel("DP", metadata.DisplayVersion)];
    }

    /// <summary>Creates the catalog-backed FlashCode output file name suggestion.</summary>
    public static string CreateFlashCodeOutputFileName(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates)
    {
        return WorkbenchCompositionService.CreateFlashCodeOutputFileName(icId, candidates).FileName;
    }
}
