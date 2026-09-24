using System.Runtime.CompilerServices;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    private readonly ConditionalWeakTable<object, PickerSelectionVersion> _pickerSelectionVersions =
        CreatePickerSelectionVersions();
    private long _pickerContextVersion;
    private PickerContextSnapshot? _observedPickerContext;

    internal WorkflowPickerSelectionLease? BeginFirmwarePickerSelection(
        string slotId,
        object target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(target);

        if (!IsWorkflowLoaded)
        {
            EnsureWorkflowLoaded();
            RefreshContextState();
        }

        ObservePickerContextTransition();
        return IsWorkflowLoaded &&
            ActiveInspectionContext is { } context &&
            ReferenceEquals(FindPickerSelectionTarget(context, slotId), target) &&
            target is not (FirmwareSlotViewModel { CanSelectFile: false } or
                GeneralMappingRowViewModel { CanSelectFile: false })
                ? new WorkflowPickerSelectionLease(
                    slotId,
                    context,
                    _pickerContextVersion,
                    SelectedIc,
                    SelectedNumber,
                    target,
                    AdvancePickerSelectionVersion(target))
                : null;
    }

    internal void InvalidatePickerSelection(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _ = AdvancePickerSelectionVersion(target);
    }

    private bool TryAcceptPickerSelection(
        WorkflowPickerSelectionLease? lease,
        WorkflowInspectionContext context,
        object target)
    {
        ObservePickerContextTransition();
        if (lease is not null &&
            (target is FirmwareSlotViewModel { CanSelectFile: false } or
                GeneralMappingRowViewModel { CanSelectFile: false }))
        {
            return false;
        }

        if (lease is null)
        {
            _ = AdvancePickerSelectionVersion(target);
            return true;
        }

        if (lease.Context != context ||
            lease.ContextVersion != _pickerContextVersion ||
            !StringComparer.Ordinal.Equals(lease.IcId, SelectedIc) ||
            !StringComparer.Ordinal.Equals(lease.Number, SelectedNumber) ||
            !ReferenceEquals(lease.Target, target) ||
            !_pickerSelectionVersions.TryGetValue(target, out PickerSelectionVersion? currentVersion) ||
            currentVersion is null ||
            lease.TargetVersion != currentVersion.Value)
        {
            return false;
        }

        _ = AdvancePickerSelectionVersion(target);
        return true;
    }

    private object? FindPickerSelectionTarget(
        WorkflowInspectionContext context,
        string slotId)
    {
        GeneralMappingRowViewModel? mapping = context switch
        {
            { IsGeneralMerge: true } => _merge.GeneralMergeMappings.FirstOrDefault(
                row => StringComparer.Ordinal.Equals(row.MappingId, slotId)),
            { IsGeneralReplace: true } => _replace.GeneralReplaceMappings.FirstOrDefault(
                row => StringComparer.Ordinal.Equals(row.MappingId, slotId)),
            _ => null,
        };

        return (object?)mapping ?? FindInspectionSlot(context, slotId);
    }

    private long AdvancePickerSelectionVersion(object target)
    {
        PickerSelectionVersion version = _pickerSelectionVersions.GetValue(
            target,
            static _ => new PickerSelectionVersion());
        version.Value = checked(version.Value + 1);
        return version.Value;
    }

    private static ConditionalWeakTable<object, PickerSelectionVersion> CreatePickerSelectionVersions()
    {
        return [];
    }

    private void ObservePickerContextTransition()
    {
        var current = new PickerContextSnapshot(
            _stateBindings.SelectedPage(),
            _merge.SelectedMergeMode,
            _replace.SelectedReplaceMode,
            SelectedIc,
            SelectedNumber);
        if (_observedPickerContext is { } previous && previous != current)
        {
            _pickerContextVersion = checked(_pickerContextVersion + 1);
        }

        _observedPickerContext = current;
    }

    private sealed class PickerSelectionVersion
    {
        internal long Value { get; set; }
    }

    private readonly record struct PickerContextSnapshot(
        ShellPage Page,
        string MergeMode,
        string ReplaceMode,
        string IcId,
        string Number);
}

internal sealed record WorkflowPickerSelectionLease(
    string SlotId,
    WorkflowInspectionContext Context,
    long ContextVersion,
    string IcId,
    string Number,
    object Target,
    long TargetVersion);
