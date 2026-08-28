using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the live TwoWay binding used by the workflow mode selectors.</summary>
public sealed class ModeSelectorBindingTests
{
    /// <summary>The first Merge selection remains accepted without replacing its choices mid-event.</summary>
    [AvaloniaFact]
    public async Task FirstMergeModeSelectionAfterPageEntryRemainsSelected()
    {
        MainWindowViewModel viewModel = await CreateViewModelAsync();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        ComboBox selector = BindMergeModeSelector(viewModel);
        object? originalItemsSource = selector.ItemsSource;
        Assert.NotNull(originalItemsSource);

        selector.SelectedItem = ExperienceIds.AbMerge;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(originalItemsSource, selector.ItemsSource);
        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(ExperienceIds.AbMerge, selector.SelectedItem);
    }

    /// <summary>The first Replace selection remains accepted without replacing its choices mid-event.</summary>
    [AvaloniaFact]
    public async Task FirstReplaceModeSelectionAfterPageEntryRemainsSelected()
    {
        MainWindowViewModel viewModel = await CreateViewModelAsync();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        ComboBox selector = BindReplaceModeSelector(viewModel);
        object? originalItemsSource = selector.ItemsSource;
        Assert.NotNull(originalItemsSource);

        selector.SelectedItem = ExperienceIds.GeneralReplace;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(originalItemsSource, selector.ItemsSource);
        Assert.Equal(ExperienceIds.GeneralReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(ExperienceIds.GeneralReplace, selector.SelectedItem);
    }

    /// <summary>Merge and Replace retain independent modes during repeated page activation.</summary>
    [AvaloniaFact]
    public async Task RepeatedPageTransitionsPreserveIndependentModeSelections()
    {
        MainWindowViewModel viewModel = await CreateViewModelAsync();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        ComboBox mergeSelector = BindMergeModeSelector(viewModel);
        mergeSelector.SelectedItem = ExperienceIds.AbMerge;
        Dispatcher.UIThread.RunJobs();

        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        ComboBox replaceSelector = BindReplaceModeSelector(viewModel);
        replaceSelector.SelectedItem = ExperienceIds.GeneralReplace;
        Dispatcher.UIThread.RunJobs();

        for (int transition = 0; transition < 100; transition++)
        {
            viewModel.ShowMergeCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ShellPage.Merge, viewModel.SelectedPage);
            Assert.Equal(ExperienceIds.AbMerge, mergeSelector.SelectedItem);

            viewModel.ShowReplaceCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ShellPage.Replace, viewModel.SelectedPage);
            Assert.Equal(ExperienceIds.GeneralReplace, replaceSelector.SelectedItem);
        }

        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(ExperienceIds.GeneralReplace, viewModel.Replace.SelectedReplaceMode);
    }

    private static Task<MainWindowViewModel> CreateViewModelAsync()
    {
        return Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
    }

    private static ComboBox BindMergeModeSelector(MainWindowViewModel viewModel)
    {
        var selector = new ComboBox { DataContext = viewModel.Merge };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding(nameof(MergePresentationViewModel.MergeModeChoices)));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding(nameof(MergePresentationViewModel.SelectedMergeMode))
            {
                Mode = BindingMode.TwoWay,
            });
        Dispatcher.UIThread.RunJobs();
        return selector;
    }

    private static ComboBox BindReplaceModeSelector(MainWindowViewModel viewModel)
    {
        var selector = new ComboBox { DataContext = viewModel.Replace };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding(nameof(ReplacePresentationViewModel.ReplaceModeChoices)));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding(nameof(ReplacePresentationViewModel.SelectedReplaceMode))
            {
                Mode = BindingMode.TwoWay,
            });
        Dispatcher.UIThread.RunJobs();
        return selector;
    }
}
