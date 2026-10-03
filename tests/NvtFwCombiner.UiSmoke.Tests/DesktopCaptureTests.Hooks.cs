using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DesktopCaptureTests
{
    /// <summary>Simulates later targets through the shared validator and public refusal owner, including no inputs.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InvalidDestinationNamesProtectedInputsAndExits64(int inputCount)
    {
        using var workspace = TempWorkspace.Create("capture-no-input-validation");
        UiLaunchOptions options = ParseOptions(["--page", "home", "--capture", workspace.PathFor("out.txt")]);
        // A later parser hook admits the target and supplies the same issue list to the shared validator.
        List<string> issues = Assert.IsType<List<string>>(options.Issues);
        issues.Clear();
        List<string> inputs = Assert.IsType<List<string>>(options.ProtectedInputs);
        for (int index = 0; index < inputCount; index++) { inputs.Add(workspace.PathFor($"input-{index}.bin")); }
        Assert.Null(UiLaunchOptions.ValidateCapturePath(new LocalFileStore(),
            options.CapturePath!, options.ProtectedInputs, overwrite: false, issues));
        _ = Assert.Single(issues);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(64, DesktopApplication.CompletePublicRequest(options, output, error));
        Assert.Contains("valid .png filename", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("report '", error.ToString(), StringComparison.Ordinal);
        foreach (string input in inputs) { Assert.Contains(input, error.ToString(), StringComparison.Ordinal); }
        if (inputCount == 0) { Assert.DoesNotContain("protected inputs", error.ToString(), StringComparison.Ordinal); }
    }

    /// <summary>Every protected source is named even when only one of them aliases the destination.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AliasDiagnosticNamesAllProtectedInputs(int inputCount)
    {
        using var workspace = TempWorkspace.Create("capture-input-diagnostic");
        string[] inputs = [workspace.Write("source.png", [1]), workspace.Write("other.bin", [2])];
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            DesktopScreenshotCapture.ValidateDestinationAsync(new LocalFileStore(),
                inputs[0], inputs[..inputCount], overwrite: true, cancellationToken: TestContext.Current.CancellationToken).AsTask());
        foreach (string input in inputs[..inputCount])
        {
            Assert.Contains(input, exception.Message, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("report file", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Async hooks await each registration before admitting the next registration.</summary>
    [AvaloniaTheory]
    [InlineData("readiness")]
    [InlineData("publication")]
    [InlineData("inputs")]
    public async Task AsyncHooksRunInRegistrationOrder(string stage)
    {
        using var workspace = TempWorkspace.Create("capture-hook-order");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var context = new DesktopLaunchContext(window, Assert.IsType<MainWindowViewModel>(window.DataContext),
            UiLaunchOptions.Empty, services, new(false));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<int>();
        async Task First(DesktopLaunchContext _, CancellationToken token)
        {
            calls.Add(1);
            await release.Task.WaitAsync(token);
            calls.Add(2);
        }
        Task Second(DesktopLaunchContext _, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            calls.Add(3);
            return Task.CompletedTask;
        }
        if (stage == "readiness")
        {
            context.TargetReadiness += First;
            context.TargetReadiness += Second;
        }
        else if (stage == "publication")
        {
            context.AfterTargetPublication += First;
            context.AfterTargetPublication += Second;
        }
        else
        {
            context.BeforeInputs += First;
            context.BeforeInputs += Second;
        }
        Task run = RunHookStage(context, stage);
        try
        {
            Assert.Equal([1], calls);
            Assert.False(run.IsCompleted);
        }
        finally
        {
            release.SetResult();
            await run;
        }
        Assert.Equal([1, 2, 3], calls);
    }

    /// <summary>A rejected hook never admits another registration in the same chain.</summary>
    [AvaloniaTheory]
    [InlineData("readiness")]
    [InlineData("publication")]
    [InlineData("inputs")]
    [InlineData("navigation")]
    public async Task FailedHookStopsRemainingRegistrations(string stage)
    {
        using var workspace = TempWorkspace.Create("capture-hook-failure");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        DesktopLaunchContext context = window.LaunchCoordinator.Context;
        int laterCalls = 0;
        Task Failure(DesktopLaunchContext _, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromException(new InvalidOperationException("first hook failed"));
        }
        Task Later(DesktopLaunchContext _, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            laterCalls++;
            return Task.CompletedTask;
        }
        switch (stage)
        {
            case "readiness": context.TargetReadiness += Failure; context.TargetReadiness += Later; break;
            case "publication": context.AfterTargetPublication += Failure; context.AfterTargetPublication += Later; break;
            case "inputs": context.BeforeInputs += Failure; context.BeforeInputs += Later; break;
            case "navigation":
                context.BeforeNavigation += _ => throw new InvalidOperationException("first hook failed");
                context.BeforeNavigation += _ => laterCalls++;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(stage));
        }
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => RunHookStage(context, stage));
        Assert.Equal("first hook failed", exception.Message);
        Assert.Equal(0, laterCalls);
    }

    private static Task RunHookStage(DesktopLaunchContext context, string stage)
    {
        switch (stage)
        {
            case "readiness": return context.AwaitTargetReadinessAsync(TestContext.Current.CancellationToken);
            case "publication": return context.AfterTargetPublicationAsync(TestContext.Current.CancellationToken);
            case "inputs": return context.BeforeInputsAsync(TestContext.Current.CancellationToken);
            default: context.BeforeNavigationNow(); return Task.CompletedTask;
        }
    }

    /// <summary>Interactive target publication runs before unrelated optional stages finish.</summary>
    [AvaloniaFact]
    public async Task InteractiveHookRunsAtTargetPublicationWhileOtherStageIsPending()
    {
        using var workspace = TempWorkspace.Create("interactive-target-publication");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        DesktopLaunchCoordinator coordinator = window.LaunchCoordinator;
        var target = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherStage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<int>();
        coordinator.Context.BeforeNavigation += _ => calls.Add(1);
        coordinator.Context.BeforeNavigation += _ => calls.Add(2);
        coordinator.Context.TargetReadiness += (_, token) => target.Task.WaitAsync(token);
        coordinator.Context.AfterTargetPublication += (_, _) =>
        {
            calls.Add(4);
            published.SetResult();
            return Task.CompletedTask;
        };
        var work = new ShellOptionalPreloadWork(() => calls.Add(3), _ => Task.CompletedTask,
            null, _ => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
        Task run = coordinator.RunOptionalStagesAsync(stageWork =>
        {
            stageWork.ApplyLaunchPage();
            return otherStage.Task;
        }, work, TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal([1, 2, 3], calls);
            Assert.False(published.Task.IsCompleted);
            target.SetResult();
            await published.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            Assert.False(otherStage.Task.IsCompleted);
            Assert.False(run.IsCompleted);
            Assert.Equal([1, 2, 3, 4], calls);
        }
        finally
        {
            _ = target.TrySetResult();
            otherStage.SetResult();
            await run.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>An ordinary saved-report launch publishes even when the capture-only layout check would fail.</summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InteractiveSavedReportPublicationIgnoresCaptureOnlyLayoutFailure(bool validReport)
    {
        using var workspace = TempWorkspace.Create("interactive-report-readiness");
        string report = workspace.Write("report.json", System.Text.Encoding.UTF8.GetBytes(
            validReport ? ReportJsonSamples.Succeeded() : "{"));
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(ParseOptions(["--report", report, "--open-report"]),
            StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        DesktopLaunchCoordinator coordinator = window.LaunchCoordinator;
        bool published = false;
        window.Show();
        try
        {
            await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            // Check dispatch directly after the real load, so startup fault handling cannot mask a capture exception.
            window.FindControl<Control>("ReportModalHost")!.IsVisible = false;
            coordinator.Context.AfterTargetPublication += (_, _) =>
            {
                published = true;
                return Task.CompletedTask;
            };
            var work = new ShellOptionalPreloadWork(() => { }, _ => Task.CompletedTask,
                null, _ => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            await coordinator.RunOptionalStagesAsync(_ => Task.CompletedTask, work,
                TestContext.Current.CancellationToken);
            Assert.Equal(validReport, published);
            Assert.Equal(validReport, coordinator.Context.ViewModel.Reports.HasLoadedReport);
            Assert.Null(coordinator.CaptureSession.CaptureExitCode);
            Assert.True(window.IsVisible);
        }
        finally
        {
            await ReportControlTestHost.CloseAndFlushAsync(window);
        }
    }

    /// <summary>The parser's single protected-input list reaches target context and completion diagnostics.</summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CompletionDiagnosticUsesTheLaunchProtectedInputs(int inputCount)
    {
        using var workspace = TempWorkspace.Create("capture-completion-inputs");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        UiLaunchOptions options = ParseOptions(["--capture", workspace.PathFor("out.png")]);
        // Simulate later parser partials accumulating zero, one or multiple protected inputs.
        List<string> inputs = Assert.IsType<List<string>>(options.ProtectedInputs);
        for (int index = 0; index < inputCount; index++) { inputs.Add(workspace.PathFor($"input-{index}.bin")); }
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var context = new DesktopLaunchContext(window, Assert.IsType<MainWindowViewModel>(window.DataContext), options, services, new(true));
        Assert.Same(options.ProtectedInputs, context.ProtectedInputs);
        using var error = new StringWriter();
        using var preload = new ShellPreloadSession(_ => { }, context.ViewModel.Text, includeStartupReport: false);
        var session = new DesktopCaptureSession(window, options, StartupTraceSession.Disabled, services,
            preload, () => { })
        {
            CaptureError = error,
        };
        session.CompleteCapture(1, "published", "prompt X", requestClose: false);
        Assert.Equal(1, session.CaptureExitCode);
        Assert.Contains("prompt X", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("report '", error.ToString(), StringComparison.Ordinal);
        foreach (string input in inputs) { Assert.Contains(input, error.ToString(), StringComparison.Ordinal); }
        if (inputCount == 0) { Assert.DoesNotContain("protected inputs", error.ToString(), StringComparison.Ordinal); }
    }

    /// <summary>A prompt hook can save the real surface and then finish with exit 1 and its prompt diagnostic.</summary>
    [AvaloniaFact]
    public async Task HookCompletionPublishesPngThenExitsOneNamingPrompt()
    {
        using var workspace = TempWorkspace.Create("capture-prompt-completion");
        string report = workspace.Write("report.json", System.Text.Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded()));
        string destination = workspace.PathFor("prompt.png");
        using MainWindow window = await CreateWindowAsync(workspace, report, destination);
        DesktopLaunchContext context = window.LaunchCoordinator.Context;
        Assert.Equal(new DesktopCaptureCompletion(0, null), context.CaptureCompletion);
        context.AfterTargetPublication += (launch, _) =>
        {
            launch.CaptureCompletion = new(1, "prompt X");
            return Task.CompletedTask;
        };
        using var error = new StringWriter();
        window.LaunchCoordinator.CaptureSession.CaptureError = error;
        Task closed = WhenClosed(window);
        window.Show();
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        DriveFrame(window);
        await closed.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(1, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Contains("published --capture", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("prompt X", error.ToString(), StringComparison.Ordinal);
        using var bitmap = new Avalonia.Media.Imaging.Bitmap(destination);
        Assert.True(HasPixelContent(bitmap));
        Assert.Empty(Directory.EnumerateFiles(workspace.Root, "*.tmp", SearchOption.AllDirectories));
    }
}
