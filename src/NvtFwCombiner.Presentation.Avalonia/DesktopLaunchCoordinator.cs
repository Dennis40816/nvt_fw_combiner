using Avalonia.Controls;
using Avalonia.Threading;
using Nvt.Core.RuntimeQuery;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Per-launch dispatch seams around the existing startup appliers and publication owners.</summary>
internal sealed partial class DesktopLaunchCoordinator
{
    internal DesktopLaunchCoordinator(MainWindow window, UiLaunchOptions options, StartupTraceSession trace,
        PresentationHostServices services, ShellPreloadSession preloadSession, MainWindowViewModel viewModel,
        Action confirmedClose)
    {
        Persistence = new(options.CapturePath is not null);
        CaptureSession = new(window, options, trace, services, preloadSession, confirmedClose);
        Context = new(window, viewModel, options, services, Persistence);
        Appearance = new(viewModel, Persistence, () => window.ClosePhase == WindowClosePhase.Open);
        RuntimeQuery = new(window, viewModel, preloadSession, services.SupportMatrix, Appearance);
        ConfigureReportTabs(Context);
        ConfigureAppearance(Context);
        ConfigureWorkflowState(Context);
        ConfigureNavigation(Context);
        ConfigureUtilities(Context);
        // Saved-report publication remains MainWindow/Reports' existing operation.
        if (options.ReportPath is not null && options.OpenReport && !options.OpenSettings && !options.HasStartupInputs)
        {
            Context.TargetPhase = "report";
            ReportReviewViewModel? publishedReport = null;
            if (!Context.HasTargetReadiness)
            {
                Context.TargetReadiness += async (context, token) =>
                {
                    await CaptureSession.AwaitStartupReportStageAsync(context.OptionalStartup, token);
                    if (context.Options.CapturePath is null) { return; }
                    await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout,
                        DispatcherPriority.Loaded, token);
                    ReportPresentationViewModel reports = context.ViewModel.Reports;
                    publishedReport ??= reports.LoadedReport;
                    Control? modal = window.FindControl<Control>("ReportModalHost");
                    if (!ReferenceEquals(reports.LoadedReport, publishedReport) || !reports.HasLoadedReport ||
                        !reports.IsReportModalOpen || modal is not { IsEffectivelyVisible: true } ||
                        modal.Bounds.Width <= 0 || modal.Bounds.Height <= 0)
                    {
                        throw new DesktopCaptureFailureException("layout", "The published saved-report modal is not visible and laid out.");
                    }
                };
            }
        }
    }

    internal CapturePersistenceScope Persistence { get; }
    internal LaunchAppearanceSession Appearance { get; }
    internal DesktopLaunchContext Context { get; }
    internal DesktopCaptureSession CaptureSession { get; }
    // Immutable query wiring; the referenced window/view-model facts are guarded by the UI thread.
    internal DesktopRuntimeQuery RuntimeQuery { get; }
    internal bool StartVersionDiscovery => Context.Options.CapturePath is null;

    internal Task GuardStartup(Task startup, CancellationToken cancellationToken)
    {
        return Context.Options.CapturePath is null ? startup : CaptureSession.GuardStartupAsync(startup, cancellationToken);
    }

    internal Task BeforeInputsAsync(Func<Task> applyInputs, CancellationToken cancellationToken)
    {
        return !Context.HasBeforeInputs ? applyInputs() : ApplyInputsWithHookAsync(applyInputs, cancellationToken);
    }

    private async Task ApplyInputsWithHookAsync(Func<Task> applyInputs, CancellationToken cancellationToken)
    {
        await Context.BeforeInputsAsync(cancellationToken);
        await applyInputs();
    }

    internal Task RunOptionalStagesAsync(
        Func<ShellOptionalPreloadWork, Task> runOptionalStages,
        ShellOptionalPreloadWork work, CancellationToken cancellationToken)
    {
        return (Context.Options.StartupCommands.Count == 0 || Context.Options.Issues.Count > 0) &&
            Context.Options.CapturePath is null && !Context.HasBeforeNavigation &&
            !Context.HasAfterTargetPublication
            ? runOptionalStages(work)
            : RunDispatchedOptionalsAsync(runOptionalStages, work, cancellationToken);
    }

    private async Task RunDispatchedOptionalsAsync(
        Func<ShellOptionalPreloadWork, Task> runOptionalStages,
        ShellOptionalPreloadWork work, CancellationToken cancellationToken)
    {
        bool capture = Context.Options.CapturePath is not null;
        // Required catalog/input startup and the managed ready notification are complete at this seam.
        if (Context.Options.StartupCommands.Count > 0 && Context.Options.Issues.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<RuntimeQueryStartupCallResult> results =
                await RuntimeQuery.Router.ExecuteStartupPhaseAsync(Context.Options.StartupCommands,
                    RuntimeQueryStartupPhase.AfterStartup);
            if (results.FirstOrDefault(static result => !result.Response.Ok) is { } failed)
            {
                if (!capture)
                {
                    // Close revokes the appearance owner; unexpected failures use the existing interactive startup guard.
                    if (Context.Window.ClosePhase == WindowClosePhase.Open)
                    {
                        throw new DesktopCaptureFailureException("appearance", failed.Response.Error!.Message);
                    }
                    return;
                }
                CaptureSession.CompleteCapture(1, "appearance", failed.Response.Error!.Message);
                return;
            }
        }
        if (Context.HasBeforeNavigation)
        {
            Action navigate = work.ApplyLaunchPage;
            work = work with { ApplyLaunchPage = () => { Context.BeforeNavigationNow(); navigate(); } };
        }
        if (!capture)
        {
            Context.OptionalStartup = runOptionalStages(work);
            if (Context.HasAfterTargetPublication)
            {
                await Task.WhenAll(Context.OptionalStartup, PublishInteractiveTargetAsync(cancellationToken));
            }
            else
            {
                await Context.OptionalStartup;
            }
            return;
        }
        if (!Context.HasTargetReadiness)
        {
            CaptureSession.CompleteCapture(64, "target", "The requested capture target is unsupported.");
            return;
        }
        if (work.LoadStartupReport is { } loadReport)
        {
            work = work with
            {
                LoadStartupReport = async (progress, token) =>
                {
                    try
                    {
                        await loadReport(progress, token);
                    }
                    catch (Exception exception)
                    {
                        CaptureSession.RecordPreloadFailure(exception is InvalidOperationException or ShellPreloadSupersededException
                            ? new DesktopCaptureFailureException("report", exception.Message, exception) : exception);
                        throw;
                    }
                },
            };
        }
        Context.OptionalStartup = runOptionalStages(work);
        // The preload session owns and tracks all admitted optional work through close.
        await CaptureSession.RunTargetCaptureAsync(Context, cancellationToken);
    }

    private async Task PublishInteractiveTargetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Context.AwaitTargetReadinessAsync(cancellationToken);
        }
        catch (DesktopCaptureFailureException)
        {
            // Capture-only refusal must not fault an interactive launch; preload owns its visible report failure.
            return;
        }
        await Context.AfterTargetPublicationAsync(cancellationToken);
    }

    partial void ConfigureReportTabs(DesktopLaunchContext context);
    partial void ConfigureAppearance(DesktopLaunchContext context);
    partial void ConfigureWorkflowState(DesktopLaunchContext context);
    partial void ConfigureNavigation(DesktopLaunchContext context);
    partial void ConfigureUtilities(DesktopLaunchContext context);
}

/// <summary>
/// Additive hooks run in registration order and stop on failure. Target readiness awaits the target's own
/// publication, not unrelated optional stages; absent readiness means an unsupported capture target.
/// Readiness hooks run up to three times per capture and must be idempotent. Capture-specific validation and
/// effects must remain capture-only; interactive publication can await readiness without capture refusal.
/// </summary>
internal sealed class DesktopLaunchContext(MainWindow window, MainWindowViewModel viewModel,
    UiLaunchOptions options, PresentationHostServices services, CapturePersistenceScope persistence)
{
    internal MainWindow Window { get; } = window;
    internal MainWindowViewModel ViewModel { get; } = viewModel;
    internal UiLaunchOptions Options { get; } = options;
    internal PresentationHostServices Services { get; } = services;
    internal CapturePersistenceScope Persistence { get; } = persistence;
    internal IReadOnlyList<string> ProtectedInputs => Options.ProtectedInputs;
    internal DesktopCaptureCompletion CaptureCompletion { get; set; }
    internal string TargetPhase { get; set; } = "target";
    internal Task OptionalStartup { get; set; } = Task.CompletedTask;
    internal event Func<DesktopLaunchContext, CancellationToken, Task>? BeforeInputs;
    internal event Action<DesktopLaunchContext>? BeforeNavigation;
    internal event Func<DesktopLaunchContext, CancellationToken, Task>? AfterTargetPublication;
    internal event Func<DesktopLaunchContext, CancellationToken, Task>? TargetReadiness;
    internal bool HasBeforeInputs => BeforeInputs is not null;
    internal bool HasBeforeNavigation => BeforeNavigation is not null;
    internal bool HasAfterTargetPublication => AfterTargetPublication is not null;
    internal bool HasTargetReadiness => TargetReadiness is not null;

    internal Task BeforeInputsAsync(CancellationToken cancellationToken)
    {
        return RunHooksAsync(BeforeInputs, cancellationToken);
    }

    internal void BeforeNavigationNow()
    {
        BeforeNavigation?.Invoke(this);
    }

    internal Task AfterTargetPublicationAsync(CancellationToken cancellationToken)
    {
        return RunHooksAsync(AfterTargetPublication, cancellationToken);
    }

    internal Task AwaitTargetReadinessAsync(CancellationToken cancellationToken)
    {
        return HasTargetReadiness ? RunHooksAsync(TargetReadiness, cancellationToken) :
            throw new InvalidOperationException("The requested target has no readiness hook.");
    }

    private async Task RunHooksAsync(Func<DesktopLaunchContext, CancellationToken, Task>? hooks, CancellationToken cancellationToken)
    {
        if (hooks is null) { return; }
        foreach (Delegate registration in hooks.GetInvocationList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hook = (Func<DesktopLaunchContext, CancellationToken, Task>)registration;
            await hook(this, cancellationToken);
        }
    }
}

/// <summary>Applied only after PNG publication; the default is a successful capture without a diagnostic.</summary>
internal readonly record struct DesktopCaptureCompletion(int ExitCode, string? Diagnostic);
