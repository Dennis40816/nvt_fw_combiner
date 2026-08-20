using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static partial class UiCompositionRunner
{
    /// <summary>Gets compact firmware facts from one already-read inspection snapshot.</summary>
    internal static IReadOnlyList<FirmwareSlotFactViewModel> GetFirmwareSlotFacts(
        FirmwareInspectionSnapshot inspection,
        bool includeBaseFacts = false,
        ShellTextResources? text = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        text ??= ShellTextResources.For(ShellLanguage.English);

        FirmwareConfigMetadataSnapshot? metadata = inspection.FirmwareConfig;
        IReadOnlyList<FirmwareSlotFactViewModel> dpFacts = includeBaseFacts
            ? GetDpFirmwareSlotFacts(inspection, text)
            : [];
        if (metadata is null || (!metadata.IsFirmwareVersionBarValid && !includeBaseFacts))
        {
            return dpFacts;
        }

        List<FirmwareSlotFactViewModel> facts =
        [
            new("Common FW Version", metadata.CommonFwVersion),
            new(
                "TP Version",
                FormattableString.Invariant($"T{metadata.FirmwareVersion:X2}-{metadata.FirmwareSubVersion:X2}"),
                metadata.IsFirmwareVersionBarValid ? FirmwareSlotFactState.Ordinary : FirmwareSlotFactState.Warning,
                metadata.IsFirmwareVersionBarValid ? null : text.FirmwareSlotWarningLabel,
                metadata.IsFirmwareVersionBarValid ? null : text.FirmwareSlotWarningFactDetail),
            new("PID", FormattableString.Invariant($"0x{metadata.ProjectId:X4}")),
        ];
        return includeBaseFacts ? [.. dpFacts, .. facts] : facts;
    }

    /// <summary>Gets compact DP facts from one already-read inspection snapshot.</summary>
    internal static IReadOnlyList<FirmwareSlotFactViewModel> GetDpFirmwareSlotFacts(
        FirmwareInspectionSnapshot inspection,
        ShellTextResources? text = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        text ??= ShellTextResources.For(ShellLanguage.English);

        DpVersionMetadata? legacyMetadata = inspection.DpVersion;
        CmiDpCodeMetadata? cmiMetadata = inspection.CmiDpCode;
        if (legacyMetadata is null && cmiMetadata is null)
        {
            if (StringComparer.Ordinal.Equals(
                    inspection.DpMetadataPrerequisite?.ArtifactBindingId,
                    CompositionAddressSpaceIds.TpInput))
            {
                (string label, string detail) = text.GetPendingInputText(
                    CompositionAddressSpaceIds.TpInput,
                    "TP BIN");
                return
                [
                    new FirmwareSlotFactViewModel(
                        "DP Version",
                        label,
                        FirmwareSlotFactState.PendingInput,
                        text.WaitingForRequiredInputsLabel,
                        detail),
                ];
            }

            return
            [
                new FirmwareSlotFactViewModel(
                    "DP Version",
                    text.FirmwareSlotUnknownValueLabel,
                    FirmwareSlotFactState.Unknown,
                    text.FirmwareSlotUnknownValueLabel,
                    text.FirmwareSlotUnknownFactDetail),
            ];
        }

        string dpVersion = legacyMetadata is DpVersionMetadata legacy
            ? legacy.DisplayValue
            : DpVersionMetadata.FormatDisplayValue(cmiMetadata!.Value.VersionToken);
        List<FirmwareSlotFactViewModel> facts = [new FirmwareSlotFactViewModel("DP Version", dpVersion)];
        if (cmiMetadata is CmiDpCodeMetadata cmi && !string.IsNullOrWhiteSpace(cmi.JiraBadge))
        {
            facts.Add(new FirmwareSlotFactViewModel("Jira Index", cmi.JiraBadge));
        }

        return facts;
    }
}
