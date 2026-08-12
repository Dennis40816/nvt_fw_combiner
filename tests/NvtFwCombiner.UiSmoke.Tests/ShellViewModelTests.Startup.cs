using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>The first Home shell must not force canonical catalog publication.</summary>
    [Fact]
    public async Task ShellConstructionDefersCanonicalCatalogPublication()
    {
        PresentationHostServices services = PresentationTestHost.CreateServices(
            ApplicationVersionProvider.InformationalVersion);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create(services);
        CommunityToolkit.Mvvm.Input.IRelayCommand[] catalogCommands =
        [
            viewModel.ShowMergeCommand,
            viewModel.ShowReplaceCommand,
            viewModel.BeginNormalMergeFromHomeCommand,
            viewModel.BeginAbMergeFromHomeCommand,
            viewModel.BeginGeneralMergeFromHomeCommand,
            viewModel.BeginDpReplaceFromHomeCommand,
            viewModel.BeginCtrlRamReplaceFromHomeCommand,
            viewModel.BeginGeneralReplaceFromHomeCommand,
        ];

        Assert.Empty(viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
        Assert.False(viewModel.WorkflowSession.IsCanonicalCatalogReady);
        Assert.All(catalogCommands, command => Assert.False(command.CanExecute(null)));

        CapabilityCatalogReloadResult reload = await PresentationTestHost.LoadCanonicalCatalogAsync(
            services.CanonicalCatalogLoader,
            CancellationToken.None);
        Assert.True(reload.Succeeded);
        viewModel.PublishCanonicalCatalogState();

        Assert.True(viewModel.WorkflowSession.IsCanonicalCatalogReady);
        Assert.NotEmpty(viewModel.WorkflowSession.IcChoices);
        Assert.Contains(
            viewModel.WorkflowSession.SelectedIc,
            viewModel.WorkflowSession.IcChoices);
        Assert.All(catalogCommands, command => Assert.True(command.CanExecute(null)));
    }
}
