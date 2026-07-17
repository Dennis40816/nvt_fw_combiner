using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>NT51931 visibly reports Not Supported and never enables a Replace action.</summary>
    [Fact]
    public void Nt51931ReplaceShowsCatalogSupportGate()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51931";
        OpenReplace(viewModel, "General");

        Assert.False(viewModel.CanBuildReplace);
        Assert.False(viewModel.PreviewReplaceCommand.CanExecute(null));
        Assert.Empty(viewModel.ReplaceSlots);
        Assert.False(viewModel.IsGeneralReplaceModeSelected);
        Assert.False(viewModel.IsStructuredReplaceModeSelected);
        Assert.Equal("NT51931 Replace: Not Supported.", viewModel.ReplaceReadinessStatus);
        Assert.Equal("Not Supported", viewModel.ReplaceMemoryRangeLabel);
    }
}
