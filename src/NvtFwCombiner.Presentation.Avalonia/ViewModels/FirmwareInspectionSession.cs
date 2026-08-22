using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal static class FirmwareInspectionProjection
{
    internal static bool SupportsFacts(FirmwareSlotViewModel slot)
    {
        return slot.SlotKind is FirmwareSlotKind.Base or FirmwareSlotKind.Dp or FirmwareSlotKind.Tp;
    }

    internal static bool IsCurrent(
        FirmwareInspectionBatchRequest request,
        FirmwareInspectionBatchResult result,
        string selectedIc,
        string selectedNumber,
        WorkflowInspectionContext currentContext,
        Func<string, FirmwareSlotViewModel?> findSlot,
        string? currentTpPath)
    {
        return result.IsContentStable &&
            string.Equals(request.IcId, selectedIc, StringComparison.Ordinal) &&
            string.Equals(request.Number, selectedNumber, StringComparison.Ordinal) &&
            request.Context == currentContext &&
            request.Items.All(item =>
                findSlot(item.SlotId) is { } slot &&
                string.Equals(slot.FilePath, item.Path, StringComparison.Ordinal) &&
                (item.TpPath is null || string.Equals(currentTpPath, item.TpPath, StringComparison.Ordinal)));
    }

    internal static CtrlRamInspectionDisplay ResolveCtrlRamDisplay(
        IFirmwareInspection firmwareInspection,
        FirmwareInspectionSnapshot inspection,
        string icId,
        string number)
    {
        ArgumentNullException.ThrowIfNull(firmwareInspection);
        return inspection.CtrlRamDisplay is { } inspectedDisplay &&
            string.Equals(inspectedDisplay.NumberToken, number, StringComparison.Ordinal)
                ? inspectedDisplay
                : firmwareInspection.ProjectCtrlRamInspectionDisplay(
                    icId,
                    number,
                    inspection.FirmwareConfig);
    }

    internal static void ApplyAbInputFacts(
        FirmwareSlotViewModel slot,
        FirmwareInspectionSnapshot inspection,
        ShellTextResources text)
    {
        AbMergeInputFacts abInput = inspection.AbMergeFacts ??
            throw new ArgumentException("AB firmware facts require AB input facts.", nameof(inspection));
        slot.SetFirmwareFacts(
        [
            .. abInput.Versions.Select(version => new FirmwareSlotFactViewModel(
                ShellTextResources.GetAbVersionLabel(version.Kind),
                !version.IsKnown
                    ? text.FirmwareSlotUnknownValueLabel
                    : FormatAbVersion(version),
                !version.IsKnown ? FirmwareSlotFactState.Unknown : FirmwareSlotFactState.Ordinary,
                !version.IsKnown ? text.FirmwareSlotUnknownValueLabel : null,
                !version.IsKnown ? text.FirmwareSlotUnknownFactDetail : null)),
            // AB owns the bank-specific TP A/TP B version labels. Reuse the standard
            // typed FWConfig projection for the remaining per-input TP identity facts.
            .. UiCompositionRunner.GetFirmwareSlotFacts(inspection).Where(static fact =>
                !string.Equals(fact.Label, "TP", StringComparison.Ordinal)),
        ]);
    }

    internal static void ApplyInputSlotInspection(
        FirmwareSlotViewModel slot,
        AuthoringInputSlotStatus status,
        ShellTextResources text)
    {
        string readinessLabel = text.GetDpInputSelectionReadinessLabel(status.SelectionReadiness);
        string readinessDetail = text.GetDpInputSelectionReadinessDetail(status.SelectionReadiness);
        slot.SetSelectionReadiness(
            status.Readiness,
            readinessLabel,
            readinessDetail,
            text.GetInputSelectionReadinessAutomationText(readinessLabel, readinessDetail),
            status.CanSelect);

        if (!status.IsTerminal)
        {
            if (status.Readiness == ResolvedChildReadiness.Blocked)
            {
                slot.SetInputInspection(
                    FirmwareInputInspectionSeverity.Blocking,
                    readinessDetail);
            }
            else
            {
                slot.ClearInputInspection();
            }

            return;
        }

        FirmwareInputInspectionSeverity severity = status.InspectionLifecycle == AuthoringSlotLifecycle.Verified
            ? FirmwareInputInspectionSeverity.Valid
            : status.InspectionLifecycle == AuthoringSlotLifecycle.Warning
                ? FirmwareInputInspectionSeverity.Warning
                : FirmwareInputInspectionSeverity.Blocking;
        slot.SetInputInspection(severity, text.GetInputSlotInspectionStatus(status));
    }

    internal static bool ApplyStaleInputInspection(
        IEnumerable<FirmwareSlotViewModel> slots,
        FirmwareInspectionBatchRequest request,
        FirmwareInspectionBatchResult result,
        ShellTextResources text)
    {
        bool applied = false;
        foreach (FirmwareInspectionItemRequest item in request.Items.Where(static item =>
                     item.AbMergeAddressSpaceId is not null ||
                     item.DpReplaceAddressSpaceId is not null ||
                     item.CtrlRamReplaceAddressSpaceId is not null ||
                     item.StandardMergeAddressSpaceId is not null))
        {
            FirmwareSlotViewModel? slot = slots.FirstOrDefault(candidate =>
                string.Equals(candidate.SlotId, item.SlotId, StringComparison.Ordinal));
            if (!result.UnstableFilePaths.Contains(item.Path) ||
                slot is null ||
                !string.Equals(slot.FilePath, item.Path, StringComparison.Ordinal) ||
                !slot.IsInputInspectionPending)
            {
                continue;
            }

            slot.SetInputInspection(
                FirmwareInputInspectionSeverity.Blocking,
                text.FirmwareInspectionStaleFileStatus);
            applied = true;
        }

        return applied;
    }

    private static string FormatAbVersion(CompiledInputVersionObservation version)
    {
        string value = version.Kind is CompiledInputVersionKind.DpA or CompiledInputVersionKind.DpB
            ? DpVersionMetadata.FormatDisplayValue(
                FormattableString.Invariant($"{version.Major:X2}{version.Minor:X2}"))
            : FormattableString.Invariant($"T{version.Major:X2}-{version.Minor:X2}");
        return version.TrackerId is { } trackerId
            ? $"{value} · AUTO_PRJ-{trackerId}"
            : value;
    }
}

internal enum WorkflowInspectionOwner
{
    Merge,
    Replace,
}

internal readonly record struct WorkflowInspectionContext(
    WorkflowInspectionOwner Owner,
    string Mode)
{
    internal bool IsMerge => Owner == WorkflowInspectionOwner.Merge;
    internal bool IsReplace => Owner == WorkflowInspectionOwner.Replace;
    internal bool IsStandardMerge => IsMerge && Mode == ExperienceIds.StandardMerge;
    internal bool IsAbMerge => IsMerge && Mode == ExperienceIds.AbMerge;
    internal bool IsDpReplace => IsReplace && Mode == ExperienceIds.DpReplace;
    internal bool IsCtrlRamReplace => IsReplace && Mode == ExperienceIds.CtrlRamReplace;
    internal bool IsGeneralReplace => IsReplace && Mode == ExperienceIds.GeneralReplace;
}

internal readonly record struct FirmwareInspectionBatchRequest(
    AuthoringRevision AuthoringRevision,
    string IcId,
    string Number,
    WorkflowInspectionContext Context,
    IReadOnlyList<FirmwareInspectionItemRequest> Items);

internal readonly record struct FirmwareInspectionItemRequest(
    string SlotId,
    FirmwareSlotKind SlotKind,
    string Path,
    string? TpPath,
    CtrlRamInspectionRequest? CtrlRamRequest,
    bool PublishFacts,
    bool PromptForMismatch,
    bool ApplyVerifiedContext,
    string? AbMergeAddressSpaceId,
    string? AbMergeTopologyToken,
    string? DpReplaceAddressSpaceId,
    string? StandardMergeAddressSpaceId,
    string? CtrlRamReplaceAddressSpaceId = null,
    AuthoringSlotInspectionLease? InspectionLease = null);
