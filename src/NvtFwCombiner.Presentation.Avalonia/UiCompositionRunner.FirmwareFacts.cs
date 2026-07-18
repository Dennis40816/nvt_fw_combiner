using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets compact firmware facts from one already-read inspection snapshot.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetFirmwareSlotFacts(
        WorkbenchFirmwareInspection inspection,
        bool includeBaseFacts = false)
    {
        ArgumentNullException.ThrowIfNull(inspection);

        WorkbenchFirmwareConfigMetadata? metadata = inspection.FirmwareConfig;
        IReadOnlyList<FirmwareSlotFactViewModel> dpFacts = includeBaseFacts
            ? GetDpFirmwareSlotFacts(inspection)
            : [];
        if (metadata is null || (!metadata.IsFirmwareVersionBarValid && !includeBaseFacts))
        {
            return dpFacts;
        }

        List<FirmwareSlotFactViewModel> facts =
        [
            new("Common FW", metadata.CommonFwVersion),
            new("TP", FormattableString.Invariant($"T{metadata.FirmwareVersion:X2}-{metadata.FirmwareSubVersion:X2}"), !metadata.IsFirmwareVersionBarValid),
            new("PID", FormattableString.Invariant($"0x{metadata.ProjectId:X4}")),
        ];
        return includeBaseFacts ? [.. dpFacts, .. facts] : facts;
    }

    /// <summary>Gets compact DP facts from one already-read inspection snapshot.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetDpFirmwareSlotFacts(
        WorkbenchFirmwareInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);

        WorkbenchDpVersionMetadata? legacyMetadata = inspection.DpVersion;
        WorkbenchCmiDpCodeMetadata? cmiMetadata = inspection.CmiDpCode;
        if (legacyMetadata is null && cmiMetadata is null)
        {
            return [new FirmwareSlotFactViewModel("DP", "Pending", true)];
        }

        string dpVersion = legacyMetadata is WorkbenchDpVersionMetadata legacy
            ? FormatDpVersion(legacy.VersionToken)
            : FormatCmiDpVersion(cmiMetadata!.Value);
        List<FirmwareSlotFactViewModel> facts = [new FirmwareSlotFactViewModel("DP", dpVersion)];
        if (cmiMetadata is WorkbenchCmiDpCodeMetadata cmi && !string.IsNullOrWhiteSpace(cmi.JiraBadge))
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

    private static string FormatCmiDpVersion(WorkbenchCmiDpCodeMetadata metadata)
    {
        return FormattableString.Invariant($"D{metadata.MajorVersionByte:X2}-{metadata.MinorVersionNibble:X1}0");
    }

}
