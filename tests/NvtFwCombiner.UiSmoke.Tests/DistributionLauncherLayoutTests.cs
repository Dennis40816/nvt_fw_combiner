using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.DistributionLauncher;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Measured geometry regressions for the standalone distribution Launcher.</summary>
[Collection(UiProcessWideObservationCollection.Name)]
public sealed class DistributionLauncherLayoutTests
{
    /// <summary>Packaged smoke can read exact terminal Setup diagnostics through UI Automation.</summary>
    [AvaloniaFact]
    public async Task SetupDiagnosticsExposeStableAutomationIds()
    {
        using var window = new LauncherWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        foreach (string name in new[]
        {
            "SourceStatusText",
            "OperationProgressText",
            "OutcomeText",
            "PrimaryButton",
        })
        {
            Control control = Assert.IsType<Control>(
                window.FindControl<Control>(name),
                exactMatch: false);
            Assert.Equal(name, AutomationProperties.GetAutomationId(control));
        }
    }

    /// <summary>The approved compact pencil remains outside rather than overlaying the path field.</summary>
    [AvaloniaFact]
    public async Task SetupPathPencilIsCompactAndOutsideThePathField()
    {
        using var window = new LauncherWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        Border path = Assert.IsType<Border>(window.FindControl<Border>("InstallLocationField"));
        Button pencil = Assert.IsType<Button>(window.FindControl<Button>("EditLocationButton"));

        Assert.InRange(pencil.Bounds.Width, 33.5, 34.5);
        Assert.True(
            pencil.Bounds.Left >= path.Bounds.Right,
            $"Expected external pencil after path field, got field {path.Bounds} and pencil {pencil.Bounds}.");
    }

    /// <summary>The action row follows the approved setup content axis and centers both button labels.</summary>
    [AvaloniaFact]
    public async Task SetupActionsAlignWithTheContentPanelAndCenterTheirLabels()
    {
        using var window = new LauncherWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        Grid setup = Assert.IsType<Grid>(window.FindControl<Grid>("SetupPanel"));
        Grid actions = Assert.IsType<Grid>(window.FindControl<Grid>("ActionPanel"));
        Button cancel = Assert.IsType<Button>(window.FindControl<Button>("CancelButton"));
        Button primary = Assert.IsType<Button>(window.FindControl<Button>("PrimaryButton"));

        Assert.True(
            Math.Abs(actions.Bounds.Left - setup.Bounds.Left) <= 0.5,
            $"Expected aligned left edges, got setup {setup.Bounds} and actions {actions.Bounds}.");
        Assert.True(
            Math.Abs(actions.Bounds.Right - setup.Bounds.Right) <= 0.5,
            $"Expected aligned right edges, got setup {setup.Bounds} and actions {actions.Bounds}.");
        Assert.InRange(Math.Abs(cancel.Bounds.Top - primary.Bounds.Top), 0, 0.5);
        Assert.InRange(Math.Abs(cancel.Bounds.Height - primary.Bounds.Height), 0, 0.5);
        Assert.Equal(HorizontalAlignment.Center, cancel.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, cancel.VerticalContentAlignment);
        Assert.Equal(HorizontalAlignment.Center, primary.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, primary.VerticalContentAlignment);
        Assert.Equal(cancel.Padding, primary.Padding);
    }

    /// <summary>The one progress region renders measured and unknown work without changing commands.</summary>
    [AvaloniaFact]
    public async Task SetupOperationProgressRendersActualPercentAndIndeterminateStages()
    {
        using var window = new LauncherWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        Grid panel = Assert.IsType<Grid>(window.FindControl<Grid>("OperationProgressPanel"));
        TextBlock text = Assert.IsType<TextBlock>(window.FindControl<TextBlock>("OperationProgressText"));
        ProgressBar progress = Assert.IsType<ProgressBar>(window.FindControl<ProgressBar>("OperationProgressBar"));
        Button primary = Assert.IsType<Button>(window.FindControl<Button>("PrimaryButton"));
        Grid setup = Assert.IsType<Grid>(window.FindControl<Grid>("SetupPanel"));
        MethodInfo? setProgress = typeof(LauncherWindow).GetMethod(
            "SetOperationProgress",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? presentProgress = typeof(LauncherWindow).GetMethod(
            "PresentOperationProgress",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setProgress);
        Assert.NotNull(presentProgress);

        Assert.Equal(3, progress.Height);
        Assert.Equal(new Thickness(0, 0, 0, 18), panel.Margin);
        Assert.Equal(new Thickness(2, 0, 2, 9), text.Margin);
        Assert.False(panel.IsVisible);
        Assert.False(progress.IsVisible);
        Assert.False(progress.IsIndeterminate);
        Assert.False(progress.ShowProgressText);
        Point primaryBefore = Assert.IsType<Point>(primary.TranslatePoint(default, window));

        _ = setProgress.Invoke(window, [true]);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        Assert.True(panel.IsVisible);
        Assert.True(progress.IsVisible);
        Assert.True(progress.IsIndeterminate);
        Assert.Equal("Preparing installation…", text.Text);
        bool primaryEnabled = primary.IsEnabled;
        Point primaryAfter = Assert.IsType<Point>(primary.TranslatePoint(default, window));
        Assert.InRange(Math.Abs(primaryAfter.X - primaryBefore.X), 0, 0.5);
        Assert.InRange(Math.Abs(primaryAfter.Y - primaryBefore.Y), 0, 0.5);
        AssertNoSetupProgressOverlap(setup, panel, window);

        _ = presentProgress.Invoke(window,
        [
            new ManagedFirstInstallationProgress(
                ManagedFirstInstallationProgressStage.ReadingPackage,
                42,
                100),
        ]);
        Assert.False(progress.IsIndeterminate);
        Assert.Equal(42, progress.Value);
        Assert.Equal("Downloading update package — 42%", text.Text);
        Assert.Equal(primaryEnabled, primary.IsEnabled);

        _ = presentProgress.Invoke(window,
        [
            ManagedFirstInstallationProgress.Indeterminate(
                ManagedFirstInstallationProgressStage.FinalizingInstallation),
        ]);
        Assert.True(progress.IsIndeterminate);
        Assert.Equal("Finalizing installation…", text.Text);
        Assert.Equal(primaryEnabled, primary.IsEnabled);

        _ = setProgress.Invoke(window, [false]);
        Assert.False(panel.IsVisible);
        Assert.False(progress.IsVisible);
        Assert.False(progress.IsIndeterminate);
        Assert.Equal(string.Empty, text.Text);

        window.Width = 760;
        window.Height = 560;
        _ = setProgress.Invoke(window, [true]);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        AssertNoSetupProgressOverlap(setup, panel, window);
        _ = setProgress.Invoke(window, [false]);

        _ = presentProgress.Invoke(window,
        [
            new ManagedFirstInstallationProgress(
                ManagedFirstInstallationProgressStage.ReadingPackage,
                99,
                100),
        ]);
        Assert.False(panel.IsVisible);
        Assert.False(progress.IsVisible);
        Assert.False(progress.IsIndeterminate);
        Assert.Equal(string.Empty, text.Text);
    }

    private static void AssertNoSetupProgressOverlap(
        Control setup,
        Control progress,
        Visual window)
    {
        Point setupOrigin = Assert.IsType<Point>(setup.TranslatePoint(default, window));
        Point progressOrigin = Assert.IsType<Point>(progress.TranslatePoint(default, window));
        Assert.True(
            setupOrigin.Y + setup.Bounds.Height <= progressOrigin.Y,
            $"Setup bottom {setupOrigin.Y + setup.Bounds.Height} overlaps progress top {progressOrigin.Y}.");
    }

    /// <summary>Launcher admission failures show the observed cause without leaking raw internals.</summary>
    [Fact]
    public void SetupFailureTextShowsInheritedContextExitReason()
    {
        var result = new ManagedFirstInstallationResult(
            ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
            @"C:\private\managed-root",
            ManagedAppVersion.Parse("1.0.7"),
            new ManagedFirstInstallationLaunchFailure(
                ManagedFirstInstallationLaunchStage.LauncherAdmission,
                ManagedFirstInstallationLaunchIssue.InvalidInheritedContext,
                22));

        string text = LauncherWindow.Describe(result);

        Assert.Equal(
            "Launcher admission failed: Bootstrap inherited an incomplete process context. " +
            "Bootstrap exit code: 22. Close Setup, then reopen the Launcher to run recovery; " +
            "if recovery fails, report this stage and exit code.",
            text);
        Assert.DoesNotContain("InvalidInheritedContext", text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.ManagedRoot, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", text, StringComparison.Ordinal);
    }

    /// <summary>Timeouts remain factual when no child exit code was observed.</summary>
    [Fact]
    public void SetupFailureTextDoesNotInventAnExitCodeForTimeout()
    {
        var result = new ManagedFirstInstallationResult(
            ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
            @"C:\private\managed-root",
            ManagedAppVersion.Parse("1.0.7"),
            new ManagedFirstInstallationLaunchFailure(
                ManagedFirstInstallationLaunchStage.BootstrapStart,
                ManagedFirstInstallationLaunchIssue.TimedOut));

        string text = LauncherWindow.Describe(result);

        Assert.StartsWith("Bootstrap start failed: the bounded operation timed out.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("exit code:", text, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            "if recovery fails, report this stage.",
            text,
            StringComparison.Ordinal);
    }

    /// <summary>Contradictory final-result shapes fail closed without exposing result internals.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void SetupFailureTextRejectsMalformedFinalResult(int scenario)
    {
        ManagedFirstInstallationLaunchFailure valid = new(
            ManagedFirstInstallationLaunchStage.ApplicationReady,
            ManagedFirstInstallationLaunchIssue.StartFailed,
            15);
        var validResult = new ManagedFirstInstallationResult(
            ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
            @"C:\private\managed-root",
            ManagedAppVersion.Parse("1.0.7"),
            valid);
        ManagedFirstInstallationResult result = scenario switch
        {
            0 => validResult with { LaunchFailure = null },
            1 => validResult with { Outcome = ManagedFirstInstallationOutcome.Completed },
            2 => validResult with
            {
                LaunchFailure = valid with { Stage = ManagedFirstInstallationLaunchStage.None },
            },
            3 => validResult with
            {
                LaunchFailure = valid with { Issue = ManagedFirstInstallationLaunchIssue.None },
            },
            4 => validResult with
            {
                LaunchFailure = valid with { Stage = (ManagedFirstInstallationLaunchStage)999 },
            },
            5 => validResult with
            {
                LaunchFailure = valid with { Issue = (ManagedFirstInstallationLaunchIssue)999 },
            },
            6 => validResult with
            {
                Outcome = ManagedFirstInstallationOutcome.ReadyToInstall,
                LaunchFailure = null,
            },
            7 => validResult with
            {
                Outcome = ManagedFirstInstallationOutcome.Installing,
                LaunchFailure = null,
            },
            8 => validResult with
            {
                LaunchFailure = valid with { Issue = ManagedFirstInstallationLaunchIssue.Busy },
            },
            9 => validResult with
            {
                LaunchFailure = valid with { ExitCode = 16 },
            },
            10 => validResult with
            {
                LaunchFailure = valid with { Stage = ManagedFirstInstallationLaunchStage.PostPromotion },
            },
            11 => validResult with { Outcome = ManagedFirstInstallationOutcome.RecoveryRequired },
            12 => validResult with { LaunchFailure = valid with { ExitCode = null } },
            _ => throw new InvalidOperationException("Undefined malformed-result scenario."),
        };

        string text = LauncherWindow.Describe(result);

        Assert.Equal(
            "Setup returned an invalid post-install result. Close Setup, then reopen the Launcher to run recovery.",
            text);
        Assert.DoesNotContain(result.ManagedRoot, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ManagedFirstInstallation", text, StringComparison.Ordinal);
    }

    /// <summary>A promoted-root failure cannot leave Install or destination editing available.</summary>
    [AvaloniaFact]
    public async Task SetupFailureBecomesTerminalRecoveryOnlyState()
    {
        using var window = new LauncherWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        FieldInfo? planField = typeof(LauncherWindow).GetField(
            "_installationPlan",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(planField);
        planField.SetValue(
            window,
            RuntimeHelpers.GetUninitializedObject(typeof(ManagedFirstInstallationPlan)));
        var result = new ManagedFirstInstallationResult(
            ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
            @"C:\private\managed-root",
            ManagedAppVersion.Parse("1.0.7"),
            new ManagedFirstInstallationLaunchFailure(
                ManagedFirstInstallationLaunchStage.LauncherAdmission,
                ManagedFirstInstallationLaunchIssue.InvalidInheritedContext,
                22));

        window.PresentInstallationResult(result);

        Button install = Assert.IsType<Button>(window.FindControl<Button>("PrimaryButton"));
        Button edit = Assert.IsType<Button>(window.FindControl<Button>("EditLocationButton"));
        TextBlock status = Assert.IsType<TextBlock>(window.FindControl<TextBlock>("SourceStatusText"));
        TextBlock outcome = Assert.IsType<TextBlock>(window.FindControl<TextBlock>("OutcomeText"));
        Assert.Null(planField.GetValue(window));
        Assert.False(install.IsEnabled);
        Assert.False(edit.IsEnabled);
        Assert.Equal("Recovery required", install.Content);
        Assert.Equal("●  Recovery required", status.Text);
        Assert.Contains("Bootstrap inherited an incomplete process context", outcome.Text, StringComparison.Ordinal);
    }

    /// <summary>A recovery-owned result without a launch receipt is still terminal in Setup.</summary>
    [AvaloniaFact]
    public async Task SetupRecoveryOutcomeWithoutLaunchFailureBecomesTerminal()
    {
        using var window = new LauncherWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        FieldInfo? planField = typeof(LauncherWindow).GetField(
            "_installationPlan",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(planField);
        planField.SetValue(
            window,
            RuntimeHelpers.GetUninitializedObject(typeof(ManagedFirstInstallationPlan)));

        window.PresentInstallationResult(new ManagedFirstInstallationResult(
            ManagedFirstInstallationOutcome.RecoveryRequired,
            @"C:\private\managed-root",
            ManagedAppVersion.Parse("1.0.7")));

        Assert.Null(planField.GetValue(window));
        Assert.False(Assert.IsType<Button>(window.FindControl<Button>("PrimaryButton")).IsEnabled);
        Assert.False(Assert.IsType<Button>(window.FindControl<Button>("EditLocationButton")).IsEnabled);
    }

    /// <summary>A pre-promotion state-read failure remains retryable and editable.</summary>
    [AvaloniaFact]
    public async Task SetupPrePromotionStateUnavailableRemainsRetryable()
    {
        using var window = new LauncherWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        window.PresentInstallationResult(new ManagedFirstInstallationResult(
            ManagedFirstInstallationOutcome.StateUnavailable,
            @"C:\private\managed-root",
            ManagedAppVersion.Parse("1.0.7")));

        Assert.True(Assert.IsType<Button>(window.FindControl<Button>("PrimaryButton")).IsEnabled);
        Assert.True(Assert.IsType<Button>(window.FindControl<Button>("EditLocationButton")).IsEnabled);
    }
}
