using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Explicit shared-session callbacks consumed by the focused Replace child.</summary>
internal sealed record ReplaceStateBindings(
    Func<ShellTextResources> Text,
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<bool> IsRunInProgress,
    Func<bool> IsFirmwareInspectionLoading,
    Func<bool> IsGlobalBuildBlocked,
    Func<bool> IsWorkflowLoaded,
    Func<FirmwareSlotViewModel, long?> GetInspectedFileLength,
    Func<WorkbenchFirmwareInspection?> GetBaseInspection,
    Func<ReportPresentationViewModel> Reports,
    Func<IEnumerable<FirmwareSlotViewModel>, string> CreateOutputFileName,
    Func<IEnumerable<FirmwareSlotViewModel>, WorkbenchCtrlRamFirmwareVersionEdit?, string> CreateCtrlRamOutputFileName,
    CompositionRunInvoker RunCompositionAsync,
    Action ReplaceModeChanged,
    Action ResetRunResult,
    Func<Task> RefreshSelectedFirmwareInspections,
    Action RefreshShellCommandState);
