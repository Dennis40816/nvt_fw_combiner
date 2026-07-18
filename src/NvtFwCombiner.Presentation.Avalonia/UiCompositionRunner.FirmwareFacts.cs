using NvtFwCombiner.Bootstrap;
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
        WorkbenchFirmwareArtifactSnapshot? snapshot =
            WorkbenchCompositionService.TryCaptureFirmwareArtifact(path);
        return snapshot is null
            ? includeBaseFacts
                ? [new FirmwareSlotFactViewModel("DP", "Pending", true)]
                : []
            : GetFirmwareSlotFacts(
                WorkbenchCompositionService.InspectFirmwareArtifact(
                    icId,
                    snapshot,
                    includeBaseFacts ? snapshot : null),
                includeBaseFacts);
    }

    /// <summary>Gets compact firmware facts without re-reading an already captured artifact.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetFirmwareSlotFacts(
        WorkbenchFirmwareInspection inspection,
        bool includeBaseFacts = false)
    {
        ArgumentNullException.ThrowIfNull(inspection);

        WorkbenchFirmwareConfigMetadata? metadata = inspection.FirmwareConfig;
        if (metadata is null || (!metadata.IsFirmwareVersionBarValid && !includeBaseFacts))
        {
            return includeBaseFacts ? GetDpFirmwareSlotFacts(inspection) : [];
        }

        List<FirmwareSlotFactViewModel> facts =
        [
            new("Common FW", metadata.CommonFwVersion),
            new("TP", FormattableString.Invariant($"T{metadata.FirmwareVersion:X2}-{metadata.FirmwareSubVersion:X2}"), !metadata.IsFirmwareVersionBarValid),
            new("PID", FormattableString.Invariant($"0x{metadata.ProjectId:X4}")),
        ];

        return includeBaseFacts ? [.. GetDpFirmwareSlotFacts(inspection), .. facts] : facts;
    }

    /// <summary>Gets compact DP version facts decoded using gen_flash standard-merge version rules.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetDpFirmwareSlotFacts(
        string icId,
        string path,
        string? tpPath = null)
    {
        WorkbenchFirmwareArtifactSnapshot? snapshot =
            WorkbenchCompositionService.TryCaptureFirmwareArtifact(path);
        WorkbenchFirmwareArtifactSnapshot? tpSnapshot = string.IsNullOrWhiteSpace(tpPath)
            ? null
            : string.Equals(
                path,
                tpPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                ? snapshot
                : WorkbenchCompositionService.TryCaptureFirmwareArtifact(tpPath);
        return snapshot is null
            ? [new FirmwareSlotFactViewModel("DP", "Pending", true)]
            : GetDpFirmwareSlotFacts(WorkbenchCompositionService.InspectFirmwareArtifact(
                icId,
                snapshot,
                tpSnapshot));
    }

    /// <summary>Gets compact DP facts without re-reading an already captured artifact.</summary>
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

        string dpVersion = legacyMetadata is not null
            ? FormatDpVersion(legacyMetadata.VersionToken)
            : FormatCmiDpVersion(cmiMetadata!);
        List<FirmwareSlotFactViewModel> facts = [new FirmwareSlotFactViewModel("DP", dpVersion)];

        if (cmiMetadata is not null && !string.IsNullOrWhiteSpace(cmiMetadata.JiraBadge))
        {
            facts.Add(new FirmwareSlotFactViewModel("Jira", cmiMetadata.JiraBadge));
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
