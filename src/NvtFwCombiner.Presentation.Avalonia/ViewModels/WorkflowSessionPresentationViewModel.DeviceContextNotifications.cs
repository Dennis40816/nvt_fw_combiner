using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    internal void PublishFullWorkflowContext()
    {
        _merge.PublishFullContext();
        _replace.PublishFullContext();
        PublishActiveNavigationContext();
    }

    internal void PublishActiveNavigationContext()
    {
        OnPropertyChanged(nameof(IcChoices));
        OnPropertyChanged(nameof(SelectedIc));
        NotifySharedContextTextChanged();
    }

    internal void PublishRefreshedSharedContext()
    {
        NotifySharedContextTextChanged();
    }

    internal void PublishAcceptedMergeSharedContext()
    {
        RecordAcceptedModeSelection(_merge.SelectedMergeMode, "merge");
        PublishCanonicalCatalogIcChoices();
        NotifySharedContextTextChanged();
    }

    private void RecordAcceptedModeSelection(string mode, string page)
    {
        _recordActivity(new SystemActivityDraft(
            SystemActivityCodes.ModeSelected,
            SystemActivityImportance.Debug,
            SystemActivityCategory.Workflow,
            SystemActivitySeverity.Information,
            mode,
            page));
    }

    private void NotifySharedContextTextChanged()
    {
        OnPropertyChanged(nameof(SelectedIcFamilySummary));
        OnPropertyChanged(nameof(SelectedIcFamilyLabel));
        OnPropertyChanged(nameof(SelectedIcFamilyTooltip));
        OnPropertyChanged(nameof(HasSelectedIcFamily));
        OnPropertyChanged(nameof(SelectedIcDetailFamily));
        OnPropertyChanged(nameof(SelectedIcDetailReuse));
        OnPropertyChanged(nameof(IsDpReplaceAvailable));
        OnPropertyChanged(nameof(SelectedIcDetailRuntime));
        OnPropertyChanged(nameof(SelectedIcDetailEvidence));
        OnPropertyChanged(nameof(SelectedIcDetailSupport));
        OnPropertyChanged(nameof(SelectedIcDetailAutomationText));
        NotifyRunStateChanged();
        _stateBindings.NotifyRunContextChanged();
    }

    internal void NotifyRunStateChanged()
    {
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(IsDeviceContextSelectionVisible));
        OnPropertyChanged(nameof(IsDeviceContextNumberSelectionVisible));
        OnPropertyChanged(nameof(IsDeviceContextFamilyBadgeVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
    }
}
