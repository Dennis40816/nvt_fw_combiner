using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets compact firmware facts decoded from a selected BIN file.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetFirmwareSlotFacts(
        string icId,
        string path,
        bool includeBaseFacts = false)
    {
        WorkbenchFirmwareConfigMetadata? metadata =
            WorkbenchCompositionService.TryReadFirmwareConfigMetadata(icId, path);
        if (metadata is null || (!metadata.IsFirmwareVersionBarValid && !includeBaseFacts))
        {
            return includeBaseFacts ? GetDpFirmwareSlotFacts(icId, path, path) : [];
        }

        List<FirmwareSlotFactViewModel> facts =
        [
            new("Common FW", metadata.CommonFwVersion),
            new("TP", FormattableString.Invariant($"T{metadata.FirmwareVersion:X2}-{metadata.FirmwareSubVersion:X2}"), !metadata.IsFirmwareVersionBarValid),
            new("PID", FormattableString.Invariant($"0x{metadata.ProjectId:X4}")),
        ];

        return includeBaseFacts ? [.. GetDpFirmwareSlotFacts(icId, path, path), .. facts] : facts;
    }

    /// <summary>Gets compact DP version facts decoded using gen_flash standard-merge version rules.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetDpFirmwareSlotFacts(
        string icId,
        string path,
        string? tpPath = null)
    {
        GenFlashDpVersionMetadata? legacyMetadata = WorkbenchCompositionService.TryReadDpVersionMetadata(
            icId,
            path);
        CmiDpCodeMetadata? cmiMetadata = WorkbenchCompositionService.TryReadCmiDpCodeMetadata(
            icId,
            path,
            tpPath);
        if (legacyMetadata is null && cmiMetadata is null)
        {
            return [new FirmwareSlotFactViewModel("DP", "Pending", true)];
        }

        string dpVersion = legacyMetadata is GenFlashDpVersionMetadata legacy
            ? FormatDpVersion(legacy.VersionToken)
            : FormatCmiDpVersion(cmiMetadata!.Value);
        List<FirmwareSlotFactViewModel> facts =
        [
            new FirmwareSlotFactViewModel("DP", dpVersion),
        ];

        if (cmiMetadata is CmiDpCodeMetadata cmi && !string.IsNullOrWhiteSpace(cmi.JiraBadge))
        {
            facts.Add(new FirmwareSlotFactViewModel("Jira", cmi.JiraBadge));
        }

        return facts;
    }

    private static string FormatDpVersion(string versionToken)
    {
        return versionToken.Length == 4
            ? $"D{versionToken[..2]}-{versionToken[2..]}"
            : $"D{versionToken}";
    }

    private static string FormatCmiDpVersion(CmiDpCodeMetadata metadata)
    {
        return FormattableString.Invariant($"D{metadata.MajorVersionByte:X2}-{metadata.MinorVersionNibble:X1}0");
    }

}
