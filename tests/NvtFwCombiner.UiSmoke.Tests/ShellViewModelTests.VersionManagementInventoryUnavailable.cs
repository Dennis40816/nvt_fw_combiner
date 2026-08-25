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
}
