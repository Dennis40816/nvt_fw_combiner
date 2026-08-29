using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record MergeStateBindings(
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<string, string, bool> IsWorkflowAuthorable,
    Func<string, IReadOnlyList<CapabilityTopologyChoice>> GetAbMergeTopologyChoices,
    Func<bool> IsRunInProgress,
    Func<bool> IsGlobalBuildBlocked,
    Func<bool> IsWorkflowLoaded,
    Func<bool> IsWorkflowLoading,
    Func<FirmwareSlotViewModel, long?> GetInspectedFileLength,
    Func<ReportPresentationViewModel> Reports,
    CompositionRunInvoker RunCompositionAsync,
    Action<UiRunResultViewModel> PublishRunResult,
    Action RefreshNumberChoices,
    Action PublishAcceptedModeContext,
    Func<Task> RefreshSelectedFirmwareInspections,
    Func<string, CancellationToken, Task> SetAbSameTpFileAsync,
    Action ResetRunResult,
    Action RefreshShellCommandState,
    OutputDeliveryConfirmationViewModel OutputDelivery);
