using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class VersionManagementSettingsTests
{
    /// <summary>Unavailable inventory is distinct from an observed empty healthy/damaged inventory.</summary>
    [Fact]
    public void UnavailableInventoryDoesNotProjectFalseZeroCountsOrVerifiedStatus()
    {
        VersionManagementSnapshot unavailable = Snapshot(retentionReviewDue: false) with
        {
            Inventory = ManagedVersionInventory.Create([]),
            InventoryIssue = ManagedVersionInventoryReadIssue.Unavailable,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", new RecordingVersionExperience(unavailable)),
            ShellPreferenceSnapshot.Default);

        viewModel.Settings.ApplyVersionSnapshot(unavailable);

        Assert.False(viewModel.Settings.HasManagedCurrentVersion);
        Assert.Equal("Inventory unavailable", viewModel.Settings.CurrentStatusLabel);
        Assert.Equal("Inventory unavailable", viewModel.Settings.InventorySummary);
        Assert.Empty(viewModel.Settings.VersionRows);
    }

    /// <summary>Unusable state suppresses stale inventory actions and update prompts.</summary>
    [Theory]
    [InlineData(VersionManagerStateLoadIssue.Invalid)]
    [InlineData(VersionManagerStateLoadIssue.Unavailable)]
    [InlineData(VersionManagerStateLoadIssue.ManagedRootMismatch)]
    public void UnusableStateDoesNotProjectStaleVersionActionsOrPrompt(
        VersionManagerStateLoadIssue stateIssue)
    {
        VersionManagementSnapshot initial = Snapshot(retentionReviewDue: true);
        InstalledVersionSnapshot active = Assert.Single(initial.Inventory.Versions, row => row.IsActive);
        initial = initial with
        {
            VerifiedCandidate = new(active.Version, active.AdmissionIdentity, "Visible release notes"),
        };
        VersionManagementSnapshot unavailable = initial with
        {
            VerifiedCandidate = new(active.Version, active.AdmissionIdentity, "Stale release notes"),
            ShouldPromptForUpdate = true,
            StateIssue = stateIssue,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", new RecordingVersionExperience(initial)),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(initial);
        viewModel.Settings.ShowVerifiedReleaseNotesCommand.Execute(null);
        Assert.True(viewModel.Settings.IsVerifiedReleaseNotesVisible);
        SettingsVersionRowViewModel installed = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.IsLastKnownGood);
        viewModel.Settings.RequestVersionPrimaryActionCommand.Execute(installed);
        Assert.True(viewModel.Settings.IsVersionConfirmationOpen);

        viewModel.Settings.ApplyVersionSnapshot(unavailable);

        Assert.Equal("Inventory unavailable", viewModel.Settings.InventorySummary);
        Assert.Empty(viewModel.Settings.VersionRows);
        Assert.False(viewModel.Settings.HasVerifiedUpdate);
        Assert.Null(viewModel.Settings.VerifiedCandidateRow);
        Assert.False(viewModel.Settings.HasRetentionReview);
        Assert.False(viewModel.Settings.IsVersionConfirmationOpen);
        Assert.False(viewModel.Settings.IsVerifiedReleaseNotesVisible);
        Assert.Equal(string.Empty, viewModel.Settings.VerifiedUpdateMessage);
    }

    /// <summary>MainWindow never opens Settings automatically from an unusable snapshot.</summary>
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task UnusableAutomaticSnapshotDoesNotOpenSettings()
    {
        VersionManagementSnapshot unavailable = Snapshot(retentionReviewDue: true) with
        {
            Inventory = ManagedVersionInventory.Create([]),
            InventoryIssue = ManagedVersionInventoryReadIssue.Unavailable,
            ShouldPromptForUpdate = true,
            StateIssue = VersionManagerStateLoadIssue.Unavailable,
        };
        var experience = new RecordingVersionExperience(unavailable);
        PresentationHostServices services = await Task.Run(
            () => PresentationTestHost.CreateServices("0.10.5", experience),
            TestContext.Current.CancellationToken);
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            services,
            ShellPreferenceSnapshot.Default);

        await window.RunVersionDiscoveryAfterReadyAsync(TestContext.Current.CancellationToken);

        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.False(viewModel.IsSettingsModalOpen);
        Assert.False(viewModel.Settings.HasVerifiedUpdate);
        Assert.Empty(viewModel.Settings.VersionRows);
    }
}
