using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises inspection disclosure through the real shell controls and lifecycle.</summary>
public sealed class InspectionStatusDisclosureTests
{
    /// <summary>Each page retains its diagnostic and Retry across disclosure and resets for later attempts.</summary>
    [AvaloniaTheory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public async Task DisclosurePreservesPageFailureAndRetryWithMouseAndKeyboard(
        bool merge, bool chinese, bool keyboard)
    {
        using var workspace = TempWorkspace.Create("inspection-disclosure");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, new ShellPreferenceSnapshot(chinese ? "Dark" : "Light", chinese ? "Traditional Chinese" : "English"))
        {
            Width = 1180,
            Height = 760,
            RequestedThemeVariant = chinese ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            shell.ShowMergeCommand.Execute(null);
            shell.ShowReplaceCommand.Execute(null);
            (merge ? shell.ShowMergeCommand : shell.ShowReplaceCommand).Execute(null);
            Flush();
            WorkflowInspectionLifecycle active = merge ? shell.Merge.Inspection : shell.Replace.Inspection;
            WorkflowInspectionLifecycle other = merge ? shell.Replace.Inspection : shell.Merge.Inspection;
            var retryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseRetry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int attempts = 0;
            async Task<WorkflowInspectionOperationResult> Inspect(
                IProgress<AuthoringInspectionProgress> progress, Func<bool> isCurrent, CancellationToken cancellationToken)
            {
                if (++attempts == 1)
                {
                    return new(false, nameof(IOException));
                }
                retryEntered.SetResult();
                await releaseRetry.Task.WaitAsync(cancellationToken);
                return new(true);
            }

            _ = await active.StartAsync(shell.Text, Inspect, TestContext.Current.CancellationToken);
            _ = await other.StartAsync(shell.Text,
                static (_, _, _) => Task.FromResult(new WorkflowInspectionOperationResult(false, nameof(UnauthorizedAccessException))),
                TestContext.Current.CancellationToken);
            (merge ? shell.ShowMergeCommand : shell.ShowReplaceCommand).Execute(null);
            Flush();
            string prefix = merge ? "Merge" : "Replace";
            string otherPrefix = merge ? "Replace" : "Merge";
            ContentControl card = Assert.IsType<ContentControl>(window.FindControl<Control>($"{prefix}InspectionStatusCard"));
            Button collapse = Assert.IsType<Button>(window.FindControl<Control>($"{prefix}InspectionCollapseButton"));
            Button reopen = Assert.IsType<Button>(window.FindControl<Control>($"{prefix}InspectionReopenButton"));
            string diagnostic = active.Loading.Detail;
            string otherDiagnostic = other.Loading.Detail;
            Assert.Same(active.Loading, card.Content);
            Assert.True(card.IsEffectivelyVisible);
            Button retry = Assert.Single(card.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, active.Loading.RetryCommand));
            Assert.True(card.IsEffectivelyVisible);
            Assert.True(retry.IsEffectivelyVisible);
            Assert.Equal(chinese ? "收合檢查狀態" : "Collapse inspection status", AutomationProperties.GetName(collapse));
            Assert.Equal(chinese ? "開啟檢查狀態" : "Open inspection status", AutomationProperties.GetName(reopen));
            Assert.True(collapse.IsEffectivelyVisible);
            var collapseBounds = new Rect(Assert.IsType<Point>(collapse.TranslatePoint(default, window)), collapse.Bounds.Size);
            var retryBounds = new Rect(Assert.IsType<Point>(retry.TranslatePoint(default, window)), retry.Bounds.Size);
            Assert.False(collapseBounds.Intersects(retryBounds),
                $"{prefix} ({shell.SelectedLanguage}) Collapse {collapseBounds} must not overlap Retry {retryBounds}.");

            Activate(window, collapse, keyboard, Key.Enter);

            Assert.False(card.IsEffectivelyVisible);
            Assert.False(retry.IsEffectivelyVisible);
            Assert.True(active.Loading.IsVisible);
            Assert.True(active.Loading.CanRetry);
            Assert.Equal(WorkflowInspectionAttemptState.Failed, active.State);
            Assert.Equal(diagnostic, active.Loading.Detail);
            Assert.True(reopen.IsEffectivelyVisible);
            Assert.True(reopen.IsFocused);
            Assert.Equal(44, reopen.Bounds.Width);
            Assert.Equal(reopen.Bounds.Width, reopen.Bounds.Height);
            shell.SelectedLanguage = chinese ? "English" : "Traditional Chinese";
            Flush();
            Assert.Equal(chinese ? "Collapse inspection status" : "收合檢查狀態", AutomationProperties.GetName(collapse));
            Assert.Equal(chinese ? "Open inspection status" : "開啟檢查狀態", AutomationProperties.GetName(reopen));
            shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
            Flush();
            Point position = Assert.IsType<Point>(reopen.TranslatePoint(default, window));
            Assert.InRange(window.Bounds.Width - position.X - reopen.Bounds.Width, 20, 28);
            Assert.InRange(window.Bounds.Height - position.Y - reopen.Bounds.Height, 0, 150);

            (merge ? shell.ShowReplaceCommand : shell.ShowMergeCommand).Execute(null);
            Flush();
            Assert.False(reopen.IsEffectivelyVisible);
            Assert.True(window.FindControl<ContentControl>($"{otherPrefix}InspectionStatusCard")!.IsEffectivelyVisible);
            Assert.False(window.FindControl<Button>($"{otherPrefix}InspectionReopenButton")!.IsEffectivelyVisible);
            (merge ? shell.ShowMergeCommand : shell.ShowReplaceCommand).Execute(null);
            Flush();
            Assert.True(reopen.IsEffectivelyVisible);
            Assert.False(card.IsEffectivelyVisible);

            Activate(window, reopen, keyboard, Key.Space);

            Assert.True(card.IsEffectivelyVisible);
            Assert.False(reopen.IsEffectivelyVisible);
            Assert.Equal(diagnostic, active.Loading.Detail);
            Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == diagnostic);
            Press(window, Key.Tab);
            Assert.True(retry.IsFocused);
            Press(window, Key.Enter);
            await retryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Flush();
            Assert.Equal(2, attempts);
            Assert.True(active.IsRunning);
            Assert.True(card.IsEffectivelyVisible);
            Assert.False(collapse.IsEffectivelyVisible);
            Assert.False(reopen.IsEffectivelyVisible);
            releaseRetry.SetResult();
            await active.ActiveTask;
            Flush();
            Assert.Equal(WorkflowInspectionAttemptState.Succeeded, active.State);
            Assert.False(card.IsEffectivelyVisible);
            Assert.False(reopen.IsEffectivelyVisible);
            Assert.Equal(WorkflowInspectionAttemptState.Failed, other.State);
            Assert.Equal(otherDiagnostic, other.Loading.Detail);

            _ = await active.StartAsync(shell.Text,
                static (_, _, _) => Task.FromResult(new WorkflowInspectionOperationResult(false, nameof(InvalidDataException))),
                TestContext.Current.CancellationToken);
            Flush();
            Assert.True(card.IsEffectivelyVisible);
            Assert.True(retry.IsEffectivelyVisible);
            Assert.False(reopen.IsEffectivelyVisible);
            Assert.NotEqual(diagnostic, active.Loading.Detail);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static void Activate(Window window, Button button, bool keyboard, Key key)
    {
        if (keyboard)
        {
            Assert.True(button.Focus(NavigationMethod.Tab));
            Press(window, key);
            return;
        }
        Point center = Assert.IsType<Point>(button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), window));
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Flush();
    }

    private static void Press(Window window, Key key)
    {
        PhysicalKey physicalKey = key == Key.Tab ? PhysicalKey.Tab : key == Key.Space ? PhysicalKey.Space : PhysicalKey.Enter;
        window.KeyPress(key, RawInputModifiers.None, physicalKey, keySymbol: null);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, keySymbol: null);
        Flush();
    }

    private static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
