using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record ReplaceStateBindings(
    Func<ShellTextResources> Text,
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<bool> IsRunInProgress,
    Func<bool> IsGlobalBuildBlocked,
    Func<bool> IsWorkflowLoaded,
    Func<FirmwareSlotViewModel, long?> GetInspectedFileLength,
    Func<FirmwareInspectionSnapshot?> GetBaseInspection,
    Func<ReportPresentationViewModel> Reports,
    CompositionRunInvoker RunCompositionAsync,
    Func<CompositionRunReport, Task> ShowDiagnosticPreviewAsync,
    Action<CapabilityActionReadinessSnapshot, bool> ShowActionReadiness,
    Action ReplaceModeChanged,
    Action ResetRunResult,
    Action RefreshShellCommandState,
    OutputDeliveryConfirmationViewModel OutputDelivery);
