namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Shell-lifetime callbacks consumed by the shared workflow-session presentation child.</summary>
internal sealed record WorkflowSessionStateBindings(
    Func<ShellPage> SelectedPage,
    Func<bool> IsRunInProgress,
    Func<bool> ActiveRunShowsNumberSelector,
    Func<string> DisplayedDeviceIc,
    Func<string> DisplayedDeviceNumber,
    Func<string> DisplayedDeviceContextRefreshSummary,
    Action ResetRunResult,
    Action RefreshCommandState,
    Action RefreshCommandAvailability,
    Action NotifyRunContextChanged);
