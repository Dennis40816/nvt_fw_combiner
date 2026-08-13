namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record MergeStateBindings(
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<bool> IsRunInProgress,
    Func<bool> IsFirmwareInspectionLoading,
    Func<bool> IsGlobalBuildBlocked,
    Func<bool> IsWorkflowLoaded,
    Func<bool> IsWorkflowLoading,
    Func<FirmwareSlotViewModel, long?> GetInspectedFileLength,
    Func<ReportPresentationViewModel> Reports,
    CompositionRunInvoker RunCompositionAsync,
    Action<UiRunResultViewModel> PublishRunResult,
    Action RefreshNumberChoices,
    Action NotifySharedContextChanged,
    Func<Task> RefreshSelectedFirmwareInspections,
    Action ResetRunResult,
    Action RefreshShellCommandState);
