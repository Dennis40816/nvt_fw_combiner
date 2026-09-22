using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static partial class UiCompositionRunner
{
    /// <summary>Gets compact firmware facts from one already-read inspection snapshot.</summary>
    internal static IReadOnlyList<FirmwareSlotFactViewModel> GetFirmwareSlotFacts(
        FirmwareInspectionSnapshot inspection,
        bool includeBaseFacts = false,
        ShellTextResources? text = null,
        bool includeTpVersion = true)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        text ??= ShellTextResources.For(ShellLanguage.English);

        if (includeBaseFacts && inspection.InputSlotStatus?.Observation.ReferenceBanks is { Count: > 0 } banks)
        {
            // Only project observed banks. The current contract does not supply unselected-bank metadata.
            return [.. banks.Select(bank => new FirmwareSlotFactViewModel(
                bank.BankId == "a-bank" ? "TPA Version" : "TPB Version",
                bank.Version.IsKnown ? FormattableString.Invariant($"T{bank.Version.Major:X2}-{bank.Version.Minor:X2}") : text.FirmwareSlotUnknownValueLabel,
                bank.Version.IsKnown ? FirmwareSlotFactState.Ordinary : FirmwareSlotFactState.Unknown,
                bank.Version.IsKnown ? null : text.FirmwareSlotUnknownValueLabel,
                bank.Version.IsKnown ? null : text.FirmwareSlotUnknownFactDetail))];
        }

        FirmwareConfigMetadataSnapshot? metadata = inspection.FirmwareConfig;
        IReadOnlyList<FirmwareSlotFactViewModel> dpFacts = includeBaseFacts
            ? GetDpFirmwareSlotFacts(inspection, text, FirmwareSlotFactPriority.Details)
            : [];
        if (metadata is null || (!metadata.IsFirmwareVersionBarValid && !includeBaseFacts && inspection.AbMergeFacts is null))
        {
            return dpFacts;
        }

        List<FirmwareSlotFactViewModel> facts = [];
        if (includeTpVersion)
        {
            facts.Add(new(
                "TP Version",
                FormattableString.Invariant($"T{metadata.FirmwareVersion:X2}-{metadata.FirmwareSubVersion:X2}"),
                metadata.IsFirmwareVersionBarValid ? FirmwareSlotFactState.Ordinary : FirmwareSlotFactState.Warning,
                metadata.IsFirmwareVersionBarValid ? null : text.FirmwareSlotWarningLabel,
                metadata.IsFirmwareVersionBarValid ? null : text.FirmwareSlotWarningFactDetail));
        }
        facts.AddRange([
            new("PID", FormattableString.Invariant($"0x{metadata.ProjectId:X4}")),
            new("Common FW Version", metadata.CommonFwVersion),
            new("IC Count", FormattableString.Invariant($"{metadata.ChipNumber}"), priority: FirmwareSlotFactPriority.Details),
        ]);
        return includeBaseFacts ? [.. facts, .. dpFacts] : facts;
    }

    /// <summary>Gets compact DP facts from one already-read inspection snapshot.</summary>
    internal static IReadOnlyList<FirmwareSlotFactViewModel> GetDpFirmwareSlotFacts(
        FirmwareInspectionSnapshot inspection,
        ShellTextResources? text = null,
        FirmwareSlotFactPriority priority = FirmwareSlotFactPriority.Primary)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        text ??= ShellTextResources.For(ShellLanguage.English);

        if (inspection.ArtifactClassification?.IsDpMetadataApplicable != true)
        {
            return [];
        }

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
                        detail, priority),
                ];
            }

            return
            [
                new FirmwareSlotFactViewModel(
                    "DP Version",
                    text.FirmwareSlotUnknownValueLabel,
                    FirmwareSlotFactState.Unknown,
                    text.FirmwareSlotUnknownValueLabel,
                    text.FirmwareSlotUnknownFactDetail, priority),
            ];
        }

        string dpVersion = legacyMetadata is DpVersionMetadata legacy
            ? legacy.DisplayValue
            : DpVersionMetadata.FormatDisplayValue(cmiMetadata!.Value.VersionToken);
        List<FirmwareSlotFactViewModel> facts = [new FirmwareSlotFactViewModel("DP Version", dpVersion, priority: priority)];
        if (cmiMetadata is CmiDpCodeMetadata cmi && !string.IsNullOrWhiteSpace(cmi.JiraBadge))
        {
            facts.Add(new FirmwareSlotFactViewModel("Jira Index", cmi.JiraBadge, priority: priority));
        }

        return facts;
    }
}
