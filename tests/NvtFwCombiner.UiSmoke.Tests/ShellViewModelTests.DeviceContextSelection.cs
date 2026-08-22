using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>The production binding contract commits and displays the newly selected catalog item.</summary>
    [AvaloniaFact]
    public async Task LiveIcSelectorBindingCommitsAndDisplaysNewIc()
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        var selector = new ComboBox
        {
            DataContext = viewModel,
        };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("WorkflowSession.IcChoices"));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding("WorkflowSession.SelectedIc") { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("NT51929", selector.SelectedItem);

        selector.SelectedItem = "NT51932";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("NT51932", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("NT51932", viewModel.WorkflowSession.GetWorkflowPageIc(
            WorkflowInspectionOwner.Merge));
        Assert.Equal("NT51932", selector.SelectedItem);
        Assert.Contains("NT51932", viewModel.WorkflowSession.DeviceContextStatus, StringComparison.Ordinal);
    }
}
