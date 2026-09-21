using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record ReplaceStateBindings(
    Func<ShellTextResources> Text,
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<string> DeviceContextRefreshSummary,
    Func<string, string, bool> IsWorkflowAuthorable,
    Func<bool> IsRunInProgress,
    Func<bool> IsGlobalBuildBlocked,
    Func<bool> IsWorkflowLoaded,
    Func<FirmwareSlotViewModel, long?> GetInspectedFileLength,
    Func<FirmwareInspectionSnapshot?> GetBaseInspection,
    Func<ReportPresentationViewModel> Reports,
    CompositionRunInvoker RunCompositionAsync,
    Func<CompositionRunContext, CompositionRunReport, Task> ShowDiagnosticPreviewAsync,
    Action<CompositionRunContext, CapabilityActionReadinessSnapshot, bool> ShowActionReadiness,
    Action ApplyAcceptedModeContext,
    Func<Task> RefreshReplaceInspectionsAsync,
    Action<CompositionRunContext> ResetRunResult,
    Action RefreshShellCommandState,
    OutputDeliveryConfirmationViewModel OutputDelivery);
