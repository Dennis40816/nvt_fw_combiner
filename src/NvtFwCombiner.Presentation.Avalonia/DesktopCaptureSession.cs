using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Transport;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Saved-report capture completion through the existing startup and window lifetime owners.</summary>
internal sealed class DesktopCaptureSession
{
    private readonly MainWindow _window;
    private readonly UiLaunchOptions _launchOptions;
    private readonly StartupTraceSession _startupTrace;
    private readonly PresentationHostServices _hostServices;
    private readonly ShellPreloadSession _preloadSession;
    private readonly Action _confirmedClose;

    internal DesktopCaptureSession(MainWindow window, UiLaunchOptions launchOptions,
        StartupTraceSession startupTrace, PresentationHostServices hostServices,
        ShellPreloadSession preloadSession, Action confirmedClose)
    {
        _window = window;
        _launchOptions = launchOptions;
        _startupTrace = startupTrace;
        _hostServices = hostServices;
        _preloadSession = preloadSession;
        _confirmedClose = confirmedClose;
    }

    internal const int CaptureDeadlineSeconds = 300;
    internal Func<TimeSpan, CancellationToken, Task> DeadlineFactory { get; set; } = Task.Delay;
    internal string Phase { get; private set; } = "preload";
    private CancellationToken _captureToken;
    private bool _captureCloseRequested;
    private Exception? _capturePreloadFailure;
    private readonly TaskCompletionSource _captureCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _captureFrameRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task CaptureFrameRequested => _captureFrameRequested.Task;

    internal int? CaptureExitCode { get; private set; }

    internal DesktopScreenshotCapture ScreenshotCapture { get; set; } = new();

    internal TextWriter CaptureError { get; set; } = Console.Error;

    internal async Task GuardStartupAsync(Task startup, CancellationToken startupCancellation)
    {
        if (_launchOptions.CapturePath is null)
        {
            await startup;
            return;
        }
        var captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(startupCancellation);
        _captureToken = captureCancellation.Token;
        using var stopDeadline = new CancellationTokenSource();
        try
        {
            Task deadline = DeadlineFactory(TimeSpan.FromSeconds(CaptureDeadlineSeconds), stopDeadline.Token);
            Task first = await Task.WhenAny(startup, deadline, _captureCompleted.Task);
            if (first == _captureCompleted.Task)
            {
                return;
            }
            if (first == deadline && CaptureExitCode is null)
            {
                CompleteCapture(70, Phase, $"Capture deadline of {CaptureDeadlineSeconds} seconds expired.");
                captureCancellation.Cancel();
            }
            else
            {
                await startup;
                if (CaptureExitCode is null)
                {
                    ShellPreloadStageSnapshot catalog = _preloadSession.CatalogStage;
                    CompleteCapture(catalog.State == ShellPreloadStageState.Failed ? 1 : 70,
                        "preload", catalog.CurrentAttempt?.Diagnostic ?? "Startup finished without capture completion.");
                }
            }
        }
        catch (Exception exception)
        {
            CompleteCapture(70, Phase, exception.Message);
            captureCancellation.Cancel();
        }
        finally
        {
            stopDeadline.Cancel();
            // A timed-out read may ignore cancellation; retain its source until the actual work settles.
            _ = ObserveStartupAndDisposeAsync(startup, captureCancellation);
        }
    }

    private static async Task ObserveStartupAndDisposeAsync(Task startup, CancellationTokenSource cancellation)
    {
        try
        {
            await startup;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Late capture startup failed: {0}", exception);
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    internal async Task RunTargetCaptureAsync(
        DesktopLaunchContext context,
        CancellationToken cancellationToken)
    {
        Phase = context.TargetPhase;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _captureToken);
        cancellationToken = linked.Token;
        try
        {
            // Readiness is checked up to three times: hooks must be idempotent, with capture-only validation/effects.
            await context.AwaitTargetReadinessAsync(cancellationToken);
            _startupTrace.Mark("startup-launch-options.ready");
            await context.AfterTargetPublicationAsync(cancellationToken);
            await context.AwaitTargetReadinessAsync(cancellationToken);
            Phase = "layout/frame";
            await AwaitCaptureSurfaceAsync(context.ViewModel, cancellationToken);
            await context.AwaitTargetReadinessAsync(cancellationToken);
            Phase = "draw/save";
            await ScreenshotCapture.CaptureAsync(_hostServices.LocalFiles, _hostServices.LocalStateDirectory, _window, _launchOptions.CapturePath!,
                context.ProtectedInputs, _launchOptions.OverwriteCapture, cancellationToken);
            CompleteCapture(context.CaptureCompletion.ExitCode, "published", context.CaptureCompletion.Diagnostic);
        }
        catch (DesktopCaptureFailureException exception)
        {
            CompleteCapture(1, exception.Phase, exception.Message);
        }
        catch (ArgumentException exception) when (Phase == "draw/save")
        {
            // Destination identity may have changed since pre-window validation.
            CompleteCapture(64, Phase, exception.Message);
        }
        catch (Exception exception)
        {
            CompleteCapture(70, Phase, exception.Message);
        }
    }

    internal void RecordPreloadFailure(Exception exception)
    {
        _capturePreloadFailure = exception;
    }

    internal async Task AwaitStartupReportStageAsync(Task optionalStartup, CancellationToken cancellationToken)
    {
        TaskCompletionSource<ShellPreloadStageSnapshot> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            ShellPreloadStageSnapshot report = _preloadSession.Stage(ShellPreloadSession.ReportStageId);
            if (report.State is ShellPreloadStageState.Succeeded or ShellPreloadStageState.Failed or
                ShellPreloadStageState.DependencyBlocked or ShellPreloadStageState.Skipped or ShellPreloadStageState.Cancelled)
            {
                _ = ready.TrySetResult(report);
            }
        }
        _preloadSession.PropertyChanged += Observe;
        try
        {
            Observe(null, new(null));
            _ = await Task.WhenAny(ready.Task, optionalStartup).WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ready.Task.IsCompleted)
            {
                await optionalStartup;
                throw new InvalidOperationException("Startup completed without a terminal report stage.");
            }
            ShellPreloadStageSnapshot report = await ready.Task;
            if (report.State != ShellPreloadStageState.Succeeded)
            {
                if (_capturePreloadFailure is { } failure && failure is not ShellPreloadSupersededException)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                }
                throw new DesktopCaptureFailureException("report",
                    $"Startup report stage ended with {report.State}: {report.CurrentAttempt?.Diagnostic}");
            }
        }
        finally
        {
            _preloadSession.PropertyChanged -= Observe;
        }
    }

    private async Task AwaitCaptureSurfaceAsync(
        MainWindowViewModel viewModel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Dispatcher.UIThread.InvokeAsync(_window.UpdateLayout, DispatcherPriority.Loaded, cancellationToken);
        if (!_window.IsVisible || _window.ClientSize.Width <= 0 || _window.ClientSize.Height <= 0)
        {
            throw new DesktopCaptureFailureException("layout", "The window client surface is not visible and laid out.");
        }
        CompositionVisual visual = ElementComposition.GetElementVisual(_window) ??
            throw new DesktopCaptureFailureException("frame", "The window has no composition visual.");
        // This signal is the rendered batch, not an animation tick or dispatcher turn.
        _window.InvalidateVisual();
        CompositionBatch batch = visual.Compositor.RequestCompositionBatchCommitAsync();
        _ = _captureFrameRequested.TrySetResult();
        await batch.Rendered.WaitAsync(cancellationToken);
        await Dispatcher.UIThread.InvokeAsync(_window.UpdateLayout, DispatcherPriority.Loaded, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(_window.DataContext, viewModel) || _window.ClosePhase != WindowClosePhase.Open)
        {
            throw new DesktopCaptureFailureException("frame", "The report surface changed before capture.");
        }
    }

    internal bool CancelForClose()
    {
        if (_launchOptions.CapturePath is null) { return false; }
        CompleteCapture(70, "cancel", "Capture was cancelled before publication.", requestClose: false);
        return true;
    }

    internal void CompleteCapture(int exitCode, string phase, string? diagnostic, bool requestClose = true)
    {
        if (_launchOptions.CapturePath is null || CaptureExitCode is not null)
        {
            return;
        }
        CaptureExitCode = exitCode;
        _ = _captureCompleted.TrySetResult();
        _ = _startupTrace.Complete(exitCode == 0 ? "startup-capture.completed"
            : exitCode == 70 ? "startup-capture.cancelled-or-unexpected" : "startup-capture.failed", _preloadSession.Stages);
        if (requestClose && !_captureCloseRequested && _window.ClosePhase == WindowClosePhase.Open)
        {
            _captureCloseRequested = true;
            // Let StartupWork settle before the existing close owner drains admitted tasks.
            Dispatcher.UIThread.Post(_confirmedClose);
        }
        if (diagnostic is not null)
        {
            CaptureError.WriteLine($"{phase} --capture '{_launchOptions.CapturePath}'{DesktopScreenshotCapture.DescribeProtectedInputs(_launchOptions.ProtectedInputs)}: {diagnostic}");
        }
    }
}
