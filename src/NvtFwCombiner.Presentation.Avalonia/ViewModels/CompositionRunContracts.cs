using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Captured presentation identity; Application remains the sole authoring admission owner.</summary>
internal sealed record CompositionRunContext(
    WorkflowRunState Owner,
    string Mode,
    string Ic,
    string Number,
    bool ShowsNumberSelector,
    string DeviceContextRefreshSummary,
    ActiveSessionSnapshot? AcceptedSession = null,
    AuthoringSessionState? AuthoringSession = null,
    AuthoringPublicationLease? PublicationLease = null)
{
    internal bool IsPublicationCurrent => PublicationLease is not null &&
        AuthoringSession?.IsPublicationCurrent(PublicationLease) == true;
}

internal delegate ValueTask<CompositionRunResult> CompositionRunWork(
    CompositionRunProgressFeed progress,
    CancellationToken cancellationToken);

internal delegate Task CompositionRunInvoker(
    CompositionRunContext context,
    bool build,
    CompositionRunWork run,
    Action<string, string> loadErrorReport);

/// <summary>Explicit context and publication callbacks consumed at the UI delivery boundary.</summary>
internal sealed record CompositionRunStateBindings(
    Func<ShellTextResources> Text,
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<WorkflowRunState> DisplayedOwner,
    Func<IEnumerable<WorkflowRunState>> Owners,
    Func<string> DeviceContextRefreshSummary,
    Func<bool> IsReducedMotionEnabled,
    Func<ReportPresentationViewModel> Reports,
    Func<CompositionRunResult, bool, bool> TryShowBuildCompleted,
    Action<string> RetainLatestCommittedOutput,
    Action RefreshCommandState,
    Action NotifyShellRunStateChanged);
