using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class VersionManagementSettingsTests
{
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
                    isVerified: true)]),
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
        Assert.DoesNotContain(privateSource, viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.False(viewModel.Settings.IsSourceChecking);
        Assert.False(viewModel.Settings.IsVersionBusy);
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
