using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class VersionManagementSettingsTests
{
    /// <summary>Potentially slow network metadata checks never begin on the UI synchronization context.</summary>
    [Fact]
    public async Task EnvironmentSelfTestRunsSlowSourceInspectionOffUiContext()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            SelfTestGate = gate,
            SelfTestStarted = started,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        var uiContext = new SynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task checking;
        try
        {
            SynchronizationContext.SetSynchronizationContext(uiContext);
            checking = viewModel.Settings.RunVersionSelfTestCommand.ExecuteAsync(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.NotSame(uiContext, experience.SelfTestSynchronizationContext);
        Assert.True(viewModel.Settings.IsSourceChecking);
        Assert.True(viewModel.Settings.IsVersionSelfTestRunning);

        gate.SetResult();
        await checking;
    }

    /// <summary>Slow registry reads keep the approved loading state visible until content validation completes.</summary>
    [Fact]
    public async Task EnvironmentSelfTestKeepsLoadingStateForCompleteAsyncRead()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            SelfTestGate = gate,
            SelfTestStarted = started,
            SelfTestResult = new(UpdateSourceRegistryLoadIssue.InvalidManifest, []),
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);

        Task checking = viewModel.Settings.RunVersionSelfTestCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, experience.SelfTests);
        Assert.True(viewModel.Settings.IsSourceChecking);
        Assert.True(viewModel.Settings.IsVersionSelfTestRunning);
        Assert.True(viewModel.Settings.IsVersionBusy);
        Assert.Contains("Running", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);

        gate.SetResult();
        await checking;

        Assert.False(viewModel.Settings.IsSourceChecking);
        Assert.False(viewModel.Settings.IsVersionSelfTestRunning);
        Assert.False(viewModel.Settings.IsVersionBusy);
        Assert.Contains("invalid", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The Version heading action runs one typed self-test and exposes no source path.</summary>
    [Fact]
    public async Task EnvironmentSelfTestUsesApplicationResultAndClearsBusyState()
    {
        const string privateSource = @"C:\Users\operator\private-update-root";
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            SelfTestResult = new(
                UpdateSourceRegistryLoadIssue.None,
                [new(
                    privateSource,
                    UpdateSourceRegistryEntryStatus.Latest,
                    UpdateCatalogLoadIssue.None,
                    ManagedVersionInstallIssue.None,
                    ManagedAppVersion.Parse("0.10.6"),
                    isVerified: true)],
                [
                    new(1, UpdateSourceRegistryLoadIssue.None, 8, IsSelected: true),
                    new(2, UpdateSourceRegistryLoadIssue.None, 7, IsSelected: false),
                ]),
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);

        await viewModel.Settings.RunVersionSelfTestCommand.ExecuteAsync(null);

        Assert.Equal(1, experience.SelfTests);
        Assert.Contains("passed", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1/1", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.Contains("0.10.6", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.Contains("Primary", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.Contains("stale revision 7", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.DoesNotContain(privateSource, viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.False(viewModel.Settings.IsSourceChecking);
        Assert.False(viewModel.Settings.IsVersionSelfTestRunning);
        Assert.False(viewModel.Settings.IsVersionBusy);
    }

    /// <summary>Durable anti-rollback rejection is actionable and never rendered as passed.</summary>
    [Fact]
    public async Task EnvironmentSelfTestReportsSelectedStaleBackupAsFailure()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            SelfTestResult = new(
                UpdateSourceRegistryLoadIssue.None,
                [],
                [
                    new(1, UpdateSourceRegistryLoadIssue.RegistryMissing, null, IsSelected: false),
                    new(2, UpdateSourceRegistryLoadIssue.None, 7, IsSelected: true),
                ],
                UpdateSourceRegistryIssue.RevisionRollback,
                acceptedRegistryRevision: 8),
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);

        await viewModel.Settings.RunVersionSelfTestCommand.ExecuteAsync(null);

        Assert.Contains("failed", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("older than", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stale revision 7 (accepted 8)", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("passed", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A missing immutable locator is reported as a typed failure without throwing.</summary>
    [Fact]
    public async Task EnvironmentSelfTestReportsMissingRegistryLocator()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            SelfTestResult = new(UpdateSourceRegistryLoadIssue.NotConfigured, []),
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);

        await viewModel.Settings.RunVersionSelfTestCommand.ExecuteAsync(null);

        Assert.Equal(1, experience.SelfTests);
        Assert.Contains("failed", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not configured", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotConfigured", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.False(viewModel.Settings.IsVersionBusy);
    }

    /// <summary>Authentication and timeout failures remain actionable and localized without raw enum leakage.</summary>
    [Theory]
    [InlineData(
        UpdateSourceRegistryLoadIssue.AuthenticationRequired,
        "English",
        "configured HTTPS source")]
    [InlineData(
        UpdateSourceRegistryLoadIssue.AuthenticationRequired,
        "Traditional Chinese",
        "設定的 HTTPS 來源")]
    [InlineData(
        UpdateSourceRegistryLoadIssue.RegistryTimedOut,
        "English",
        "timed out")]
    [InlineData(
        UpdateSourceRegistryLoadIssue.RegistryTimedOut,
        "Traditional Chinese",
        "請求逾時")]
    public async Task EnvironmentSelfTestLocalizesRemoteRegistryFailures(
        UpdateSourceRegistryLoadIssue issue,
        string language,
        string expected)
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            SelfTestResult = new(issue, []),
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.SelectedLanguage = language;

        await viewModel.Settings.RunVersionSelfTestCommand.ExecuteAsync(null);

        Assert.Contains(expected, viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(issue.ToString(), viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("novatekcomtw", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.Settings.IsVersionBusy);
    }

    /// <summary>A working fallback is visible as attention, never a false all-clear or a raw enum.</summary>
    [Fact]
    public async Task EnvironmentSelfTestReportsRecoveredFallbackAsFriendlyWarning()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            SelfTestResult = new(
                UpdateSourceRegistryLoadIssue.None,
                [
                    new(
                        "private-latest",
                        UpdateSourceRegistryEntryStatus.Latest,
                        UpdateCatalogLoadIssue.SourceUnavailable,
                        packageIssue: null,
                        newestVersion: null,
                        isVerified: false),
                    new(
                        "private-fallback",
                        UpdateSourceRegistryEntryStatus.Available,
                        UpdateCatalogLoadIssue.None,
                        ManagedVersionInstallIssue.None,
                        ManagedAppVersion.Parse("0.10.6"),
                        isVerified: true),
                ]),
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);

        await viewModel.Settings.RunVersionSelfTestCommand.ExecuteAsync(null);

        Assert.Contains("needs attention", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1/2", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.Contains("source folder is unavailable", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SourceUnavailable", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("private-latest", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.False(viewModel.Settings.IsVersionBusy);
    }

}
