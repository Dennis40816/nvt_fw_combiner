using Avalonia.Headless.XUnit;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class VersionManagementSettingsTests
{
    /// <summary>Launcher handoff failure clears only the unlaunched request and leaves an actionable status.</summary>
    [Fact]
    public async Task LauncherHandoffFailureClearsPendingActivationAndReportsOpenState()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);
        SettingsVersionRowViewModel installed = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));
        viewModel.Settings.RequestVersionPrimaryActionCommand.Execute(installed);
        await viewModel.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);
        Assert.NotNull(experience.Current.State!.PendingActivation);

        bool cleared = await viewModel.Settings.HandleLauncherHandoffFailureAsync();

        Assert.True(cleared);
        Assert.Null(experience.Current.State!.PendingActivation);
        Assert.Contains("remains open", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
    }

    /// <summary>A failed pending-state clear stays visible and preserves launcher retry on the next close.</summary>
    [AvaloniaFact]
    public async Task HandoffAndPendingClearFailureRemainVisibleAndRetryable()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            FailPendingActivationCancellation = true,
        };
        var handoff = new RecordingStableLauncherHandoff(started: false);
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5", experience, handoff),
            ShellPreferenceSnapshot.Default);
        window.RequestStableLauncherRestart();

        Assert.False(await window.TryCompleteStableLauncherHandoffAsync());
        Assert.False(await window.TryCompleteStableLauncherHandoffAsync());

        Assert.Equal(2, handoff.Attempts);
        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.Contains("pending activation", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.True(window.IsEnabled);
    }

    /// <summary>The window reports a failed launcher start while it is still alive and usable.</summary>
    [AvaloniaFact]
    public async Task StableLauncherMustStartBeforeWindowCanCompleteHandoff()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices services = PresentationTestHost.CreateServices(
            "0.10.5",
            experience,
            handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            services,
            ShellPreferenceSnapshot.Default);
        window.RequestStableLauncherRestart();

        bool started = await window.TryCompleteStableLauncherHandoffAsync();

        Assert.False(started);
        Assert.Equal(1, handoff.Attempts);
        Assert.True(window.IsEnabled);
        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.Contains("remains open", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
    }

    /// <summary>A state-save failure during activation preparation never requests launcher handoff.</summary>
    [Fact]
    public async Task ActivationPreparationFailureRemainsVisibleWithoutClosing()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            FailActivationPreparation = true,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);
        bool activationRequested = false;
        viewModel.Settings.ActivationRequested += (_, _) => activationRequested = true;
        SettingsVersionRowViewModel installed = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));

        viewModel.Settings.RequestVersionPrimaryActionCommand.Execute(installed);
        await viewModel.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);

        Assert.False(activationRequested);
        Assert.Contains("could not be prepared", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
    }
}
