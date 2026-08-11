namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal delegate ValueTask<CompositionRunResult> CompositionRunWork(
    CompositionRunProgressFeed progress,
    CancellationToken cancellationToken);

internal delegate Task CompositionRunInvoker(
    bool build,
    CompositionRunWork run,
    Action<string, string> loadErrorReport);

/// <summary>Explicit context and publication callbacks consumed by the focused run child.</summary>
internal sealed record CompositionRunStateBindings(
    Func<ShellTextResources> Text,
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<string> SelectedMode,
    Func<bool> ShouldShowNumberSelector,
    Func<string> DeviceContextRefreshSummary,
    Func<bool> IsReducedMotionEnabled,
    Func<ReportPresentationViewModel> Reports,
    Func<CompositionRunResult, bool, bool> TryShowBuildCompleted,
    Action RefreshCommandState,
    Action NotifyShellRunStateChanged);
