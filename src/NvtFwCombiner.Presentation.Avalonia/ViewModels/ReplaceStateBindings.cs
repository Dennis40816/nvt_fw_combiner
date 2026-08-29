using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record ReplaceStateBindings(
    Func<ShellTextResources> Text,
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<string, string, bool> IsWorkflowAuthorable,
    Func<bool> IsRunInProgress,
    Func<bool> IsGlobalBuildBlocked,
    Func<bool> IsWorkflowLoaded,
    Func<FirmwareSlotViewModel, long?> GetInspectedFileLength,
    Func<FirmwareInspectionSnapshot?> GetBaseInspection,
    Func<ReportPresentationViewModel> Reports,
    CompositionRunInvoker RunCompositionAsync,
    Func<CompositionRunReport, Task> ShowDiagnosticPreviewAsync,
    Action<CapabilityActionReadinessSnapshot, bool> ShowActionReadiness,
    Action ApplyAcceptedModeContext,
    Action ResetRunResult,
    Action RefreshShellCommandState,
    OutputDeliveryConfirmationViewModel OutputDelivery);
