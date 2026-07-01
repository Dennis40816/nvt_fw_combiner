using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Smoke coverage for shell view-model surfaces used by the Avalonia UI.</summary>
public sealed class ShellViewModelTests
{
    /// <summary>Verifies Settings exposes catalog-backed status without requiring workflow context.</summary>
    [Fact]
    public void SettingsUsesCatalogBackedRowsWithoutDeviceContext()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        Assert.Equal("0.5.0-dev.0", viewModel.AppVersion);
        Assert.Contains(viewModel.SettingsProfileRows, row => row.Title == "Built-in profiles" && row.Value.Contains("merge", StringComparison.Ordinal));
        Assert.Contains(viewModel.SettingsToolRows, row => row.Title == "External tool binding" && row.Value.Contains("legacy-combiner-1.13.0", StringComparison.Ordinal));
        Assert.Contains(viewModel.SettingsDiagnosticsRows, row => row.Title == "Report review");
        Assert.Contains(viewModel.SettingsReadinessRows, row => row.Title == "Device context" && row.Value == "Workflow pages only");
    }

    /// <summary>Verifies breadcrumb history can return to an earlier page level.</summary>
    [Fact]
    public void NavigationTrailReturnsToEarlierPage()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowSettingsCommand.Execute(null);
        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Settings > Replace", viewModel.NavigationPath);

        viewModel.NavigationTrail[1].NavigateCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Settings", viewModel.NavigationPath);
    }
}
