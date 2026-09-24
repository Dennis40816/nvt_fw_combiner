using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal static class FirmwareInspectionProjection
{
    internal static bool SupportsFacts(FirmwareSlotViewModel slot, FirmwareInspectionSnapshot? inspection = null)
    {
        return (slot.SlotKind is FirmwareSlotKind.Base or FirmwareSlotKind.Dp or FirmwareSlotKind.Tp) && inspection?.InputSlotStatus is not { BlocksBuild: true, FileStamp: null };
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
        ShellTextResources text,
        bool expandAdditionalByDefault = false)
    {
        AbMergeInputFacts abInput = inspection.AbMergeFacts ??
            throw new ArgumentException("AB firmware facts require AB input facts.", nameof(inspection));
        List<FirmwareSlotFactViewModel> facts = [];
        foreach (CompiledInputVersionObservation version in abInput.Versions)
        {
            bool isDp = version.Kind is CompiledInputVersionKind.DpA or CompiledInputVersionKind.DpB;
            string bankLabel = ShellTextResources.GetAbVersionLabel(version.Kind);
            string value = !version.IsKnown
                ? text.FirmwareSlotUnknownValueLabel
                : isDp
                    ? DpVersionMetadata.FormatDisplayValue(
                        FormattableString.Invariant($"{version.Major:X2}{version.Minor:X2}"))
                    : FormattableString.Invariant($"T{version.Major:X2}-{version.Minor:X2}");
            facts.Add(new FirmwareSlotFactViewModel(
                isDp ? $"{bankLabel} Version" : bankLabel,
                value,
                !version.IsKnown ? FirmwareSlotFactState.Unknown : FirmwareSlotFactState.Ordinary,
                !version.IsKnown ? text.FirmwareSlotUnknownValueLabel : null,
                !version.IsKnown ? text.FirmwareSlotUnknownFactDetail : null,
                isDp && slot.SlotKind == FirmwareSlotKind.Base ? FirmwareSlotFactPriority.Details : FirmwareSlotFactPriority.Primary));
            if (isDp && version.TrackerId is > 0)
            {
                facts.Add(new FirmwareSlotFactViewModel(
                    $"{bankLabel} Jira Index",
                    FormattableString.Invariant($"AUTO_PRJ-{version.TrackerId}"),
                    priority: slot.SlotKind == FirmwareSlotKind.Base ? FirmwareSlotFactPriority.Details : FirmwareSlotFactPriority.Primary));
            }
        }

        // AB owns the bank-specific version label. The remaining facts come from
        // existing typed projections, never from reading bytes or matching Config here.
        facts.AddRange(UiCompositionRunner.GetFirmwareSlotFacts(inspection, text: text, includeTpVersion: false));
        if (abInput.EventBufferFormat is { } format)
        {
            facts.Add(UiCompositionRunner.CreateEventBufferFact(format.RawByte, text,
                fallbackDisplayName: format.DisplayName));
        }
        slot.SetFirmwareFacts(facts, expandAdditionalByDefault);
    }

    internal static void ApplyInputSlotInspection(
        FirmwareSlotViewModel slot,
        AuthoringInputSlotStatus status,
        ShellTextResources text)
    {
        if (status.ConfigurationBlocker is not null)
        {
            ApplyConfigurationPrerequisite(slot, status, text);
            return;
        }
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
        slot.SetInputInspection(severity, text.GetInputSlotInspectionStatus(status), status);
    }

    internal static void ApplyAuthoringIssues(
        FirmwareSlotViewModel slot,
        IReadOnlyList<CompositionIssue> issues,
        ShellTextResources text)
    {
        if (slot.CurrentInspectionProjection?.InputSlotStatus is { ConfigurationBlocker: not null } status)
        {
            ApplyConfigurationPrerequisite(slot, status, text);
            return;
        }
        slot.SetInputInspection(
            FirmwareInputInspectionSeverity.Blocking,
            string.Join(Environment.NewLine, issues.Select(static issue =>
                issue.OperationId is { } operationId
                    ? $"{issue.Code} [{operationId}]: {issue.Message}"
                    : $"{issue.Code}: {issue.Message}")));
    }

    private static void ApplyConfigurationPrerequisite(FirmwareSlotViewModel slot, AuthoringInputSlotStatus status, ShellTextResources text)
    {
        // The shared Build prerequisite owns the actionable message; a config failure is not a BIN verdict.
        slot.SetSelectionReadiness(status.Readiness, text.EventBufferFormatTitle, text.EventBufferFormatInvalidLabel,
            text.GetInputSelectionReadinessAutomationText(text.EventBufferFormatTitle, text.EventBufferFormatInvalidLabel), status.CanSelect);
        slot.ClearInputInspection();
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
    internal bool IsGeneralMerge => IsMerge && Mode == ExperienceIds.GeneralMerge;
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
    string? StandardMergeAddressSpaceId,
    string? CtrlRamReplaceAddressSpaceId = null,
    AuthoringSlotInspectionLease? InspectionLease = null);
