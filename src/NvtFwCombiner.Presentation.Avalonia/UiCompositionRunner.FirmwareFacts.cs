using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
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

        if (includeBaseFacts && inspection.CtrlRamBaseInspection is { Kind: CtrlRamBaseKind.AbFlash } ab)
        {
            return GetAbBaseFacts(ab.Banks, text);
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

    private static List<FirmwareSlotFactViewModel> GetAbBaseFacts(
        IReadOnlyList<CtrlRamBaseBankInspection> banks, ShellTextResources text)
    {
        List<FirmwareSlotFactViewModel> facts = [];
        foreach (CtrlRamBaseBankInspection bank in banks)
        {
            string label = bank.BankId == "a-bank" ? "TPA Version" : "TPB Version";
            facts.Add(bank.TpVersion is { IsKnown: true } tp
                ? new(label, FormattableString.Invariant($"T{tp.Major:X2}-{tp.Minor:X2}"))
                : Unknown(label));
        }
        AddMetadata("PID", static metadata => FormattableString.Invariant($"0x{metadata.ProjectId:X4}"),
            static (a, b) => a.ProjectId == b.ProjectId);
        AddMetadata("Common FW Version", static metadata => metadata.CommonFwVersion,
            static (a, b) => a.CommonFwVersion == b.CommonFwVersion);
        AddMetadata("IC Count", static metadata => FormattableString.Invariant($"{metadata.ChipNumber}"),
            static (a, b) => a.ChipNumber == b.ChipNumber, FirmwareSlotFactPriority.Details);
        foreach (CtrlRamBaseBankInspection bank in banks)
        {
            string bankLabel = bank.BankId == "a-bank" ? "DPA" : "DPB";
            if (bank.DpVersion is { IsKnown: true } dp)
            {
                facts.Add(new($"{bankLabel} Version", DpVersionMetadata.FormatDisplayValue(
                    FormattableString.Invariant($"{dp.Major:X2}{dp.Minor:X2}")), priority: FirmwareSlotFactPriority.Details));
                if (dp.TrackerId is > 0)
                {
                    facts.Add(new($"{bankLabel} Jira Index", FormattableString.Invariant($"AUTO_PRJ-{dp.TrackerId}"),
                        priority: FirmwareSlotFactPriority.Details));
                }
            }
            else
            {
                facts.Add(Unknown($"{bankLabel} Version", FirmwareSlotFactPriority.Details));
            }
        }
        foreach (CtrlRamBaseBankInspection bank in banks)
        {
            string label = $"{text.EventBufferVersionLabel} ({(bank.BankId == "a-bank" ? "A" : "B")})";
            facts.Add(bank.EventBufferFormatVersion is byte raw
                ? new(label, FormattableString.Invariant($"0x{raw:X2} - {FirmwareEventBufferFormatDisplayNames.GetDisplayName(raw) ?? text.FirmwareSlotUnknownValueLabel}"))
                : new(label, text.FirmwareFactNotProvidedLabel,
                    stateDetail: text.FirmwareFactNotProvidedDetail, priority: FirmwareSlotFactPriority.Details));
        }
        return facts;

        FirmwareSlotFactViewModel Unknown(string label, FirmwareSlotFactPriority priority = FirmwareSlotFactPriority.Primary)
        {
            return new(label, text.FirmwareSlotUnknownValueLabel, FirmwareSlotFactState.Unknown,
                text.FirmwareSlotUnknownValueLabel, text.FirmwareSlotUnknownFactDetail, priority);
        }

        void AddMetadata(string label, Func<FirmwareConfigMetadataSnapshot, string> value,
            Func<FirmwareConfigMetadataSnapshot, FirmwareConfigMetadataSnapshot, bool> same,
            FirmwareSlotFactPriority priority = FirmwareSlotFactPriority.Primary)
        {
            if (banks.Count == 2 && banks[0].FirmwareConfig is { } a && banks[1].FirmwareConfig is { } b && same(a, b))
            {
                facts.Add(new($"{label} (A/B)", value(a), priority: priority));
                return;
            }
            foreach (CtrlRamBaseBankInspection bank in banks)
            {
                string bankLabel = $"{label} ({(bank.BankId == "a-bank" ? "A" : "B")})";
                facts.Add(bank.FirmwareConfig is { } metadata ? new(bankLabel, value(metadata), priority: priority) : Unknown(bankLabel, priority));
            }
        }
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
