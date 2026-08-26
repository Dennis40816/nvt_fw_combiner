using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>A transient empty context-selector value cannot publish an invalid destination draft.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("NT00000")]
    public void InvalidWorkflowContextSelectionFallsBackBeforePageActivation(string? invalidIc)
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.BeginCtrlRamReplaceFromHomeCommand.Execute(null);
        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Exception? exception = Record.Exception(
            () =>
            {
                viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = invalidIc!;
                viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
            });

        Assert.Null(exception);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.Contains(
            viewModel.WorkflowSession.SelectedIc,
            viewModel.WorkflowSession.IcChoices);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.WorkflowSession.SelectedIc));
    }

    /// <summary>A live ComboBox clearing its selection cannot overwrite the active page context.</summary>
    [Fact]
    public void TransientEmptyLiveIcSelectionKeepsTheLastValidPageContext()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        string retainedNumber = viewModel.WorkflowSession.SelectedNumber;

        Exception? exception = Record.Exception(
            () => viewModel.WorkflowSession.SelectedIc = null!);

        Assert.Null(exception);
        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("NT51950", viewModel.WorkflowSession.GetWorkflowPageIc(
            WorkflowInspectionOwner.Merge));
        Assert.Equal(retainedNumber, viewModel.WorkflowSession.SelectedNumber);
    }

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

    /// <summary>An AB-filtered live selector cannot write null while Replace restores its own IC.</summary>
    [AvaloniaFact]
    public async Task LiveAbIcSelectorCanRestoreAnIndependentReplaceContext()
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.ShowHomeCommand.Execute(null);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        var selector = new ComboBox { DataContext = viewModel };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("WorkflowSession.IcChoices"));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding("WorkflowSession.SelectedIc") { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("NT51950", selector.SelectedItem);
        Assert.DoesNotContain("NT51926", viewModel.WorkflowSession.IcChoices);
        Exception? exception = Record.Exception(
            () =>
            {
                viewModel.ShowReplaceCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
            });

        Assert.Null(exception);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);
        Assert.Contains("NT51926", viewModel.WorkflowSession.IcChoices);
        Assert.Contains("NT51926", selector.Items.Cast<string>());
        Assert.Equal("NT51926", selector.SelectedItem);

        exception = Record.Exception(
            () =>
            {
                viewModel.ShowMergeCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
            });

        Assert.Null(exception);
        Assert.True(viewModel.IsMergeVisible);
        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.DoesNotContain("NT51926", viewModel.WorkflowSession.IcChoices);
        Assert.Equal("NT51950", selector.SelectedItem);
    }

    /// <summary>Window refocus and Home modal cancel/confirm never synthesize a live IC selection.</summary>
    [AvaloniaFact]
    public async Task FocusedIcComboRefocusAndModalRoundTripPublishOnlyExplicitConfirmation()
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

        selector.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent)
        {
            NavigationMethod = NavigationMethod.Tab,
        });
        selector.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
        selector.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent)
        {
            NavigationMethod = NavigationMethod.Unspecified,
        });

        Assert.Equal("NT51929", selector.SelectedItem);
        Assert.Equal("NT51929", viewModel.WorkflowSession.SelectedIc);

        viewModel.ShowHomeCommand.Execute(null);
        viewModel.BeginNormalMergeFromHomeCommand.Execute(null);
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51932";
        viewModel.WorkflowSession.CancelWorkflowContextCommand.Execute(null);

        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.Equal(
            "NT51929",
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));

        viewModel.BeginNormalMergeFromHomeCommand.Execute(null);
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51932";
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsMergeVisible);
        Assert.Equal("NT51932", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("NT51932", selector.SelectedItem);
        Assert.Equal(
            "NT51932",
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
    }
}
