using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the live TwoWay binding used by the workflow mode selectors.</summary>
public sealed class ModeSelectorBindingTests
{
    /// <summary>A real user interaction on the production Merge selector publishes AB Code once.</summary>
    [AvaloniaFact]
    public async Task ProductionMergeModeSelectorPointerSelectionPublishesAbCodeOnce()
    {
        MainWindowViewModel viewModel = await CreateViewModelAsync();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.MessageCenter.ToggleDebugActivityCommand.Execute(null);
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            services,
            ShellPreferenceSnapshot.Default)
        {
            DataContext = viewModel,
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            ComboBox selector = Assert.Single(
                window.GetVisualDescendants().OfType<ComboBox>(),
                candidate => candidate.IsVisible && ReferenceEquals(candidate.DataContext, viewModel.Merge));
            Assert.Equal(ExperienceIds.StandardMerge, selector.SelectedItem);
            object? originalItemsSource = selector.ItemsSource;
            Assert.NotNull(originalItemsSource);
            int modeActivityCount = viewModel.MessageCenter.ActivityItems.Count(static item =>
                item.Title == "Mode selected");

            SelectAbCodeThroughUserInput(window, selector);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.Same(originalItemsSource, selector.ItemsSource);
            Assert.Equal(ExperienceIds.AbMerge, selector.SelectedItem);
            Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
            Assert.Equal(ShellPage.Merge, viewModel.SelectedPage);
            Assert.True(Assert.IsType<ContentControl>(
                window.FindControl<ContentControl>("MergePageHost"),
                exactMatch: false).IsVisible);
            Assert.True(viewModel.Merge.IsAbCodeMergeModeSelected);
            Assert.Equal(3, viewModel.Merge.MergeSlots.Count);
            Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.NotEmpty(slot.SlotId));
            Assert.Contains(
                window.GetVisualDescendants().OfType<TextBlock>(),
                candidate => candidate.IsVisible && candidate.Text == viewModel.Text.AbCodeMergeTitle);
            Assert.Equal(modeActivityCount + 1, viewModel.MessageCenter.ActivityItems.Count(static item =>
                item.Title == "Mode selected"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The first Merge selection remains accepted without replacing its choices mid-event.</summary>
    [AvaloniaFact]
    public async Task FirstMergeModeSelectionAfterPageEntryRemainsSelected()
    {
        MainWindowViewModel viewModel = await CreateViewModelAsync();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        ComboBox selector = BindMergeModeSelector(viewModel);
        ComboBox deviceSelector = BindDeviceSelector(viewModel);
        var modeChoiceChanges = new List<string>();
        TrackCollection(
            Assert.IsType<System.Collections.Specialized.INotifyCollectionChanged>(
                viewModel.Merge.MergeModeChoices,
                exactMatch: false),
            modeChoiceChanges);
        Assert.Contains("NT51926", viewModel.WorkflowSession.IcChoices);
        viewModel.MessageCenter.ToggleDebugActivityCommand.Execute(null);
        int modeActivityCount = viewModel.MessageCenter.ActivityItems.Count(static item =>
            item.Title == "Mode selected");
        object? originalItemsSource = selector.ItemsSource;
        Assert.NotNull(originalItemsSource);
        var mergeChanges = new List<string>();
        var replaceChanges = new List<string>();
        var workflowChanges = new List<string>();
        Track(viewModel.Merge, mergeChanges);
        Track(viewModel.Replace, replaceChanges);
        Track(viewModel.WorkflowSession, workflowChanges);

        selector.SelectedItem = ExperienceIds.AbMerge;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(originalItemsSource, selector.ItemsSource);
        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(ExperienceIds.AbMerge, selector.SelectedItem);
        Assert.Equal("NT51950", deviceSelector.SelectedItem);
        Assert.Contains("NT51950", viewModel.WorkflowSession.IcChoices);
        Assert.DoesNotContain("NT51926", viewModel.WorkflowSession.IcChoices);
        Assert.Equal(modeActivityCount + 1, viewModel.MessageCenter.ActivityItems.Count(static item =>
            item.Title == "Mode selected"));
        Assert.Empty(modeChoiceChanges);
        Assert.DoesNotContain(nameof(MergePresentationViewModel.MergeModeChoices), mergeChanges);
        AssertPublishedExactlyOnce(
            mergeChanges,
            nameof(MergePresentationViewModel.SelectedMergeMode),
            nameof(MergePresentationViewModel.Inspection),
            nameof(MergePresentationViewModel.IsAbCodeMergeModeSelected),
            nameof(MergePresentationViewModel.MergeOutputFileName),
            nameof(MergePresentationViewModel.MergeMemorySummary),
            nameof(MergePresentationViewModel.StandardMergeSupportSummary));
        AssertHiddenReplaceContextWasNotPublished(replaceChanges);
        Assert.Equal(
            1,
            workflowChanges.Count(change => change == nameof(WorkflowSessionPresentationViewModel.IcChoices)));
    }

    /// <summary>The first Replace selection remains accepted without replacing its choices mid-event.</summary>
    [AvaloniaFact]
    public async Task FirstReplaceModeSelectionAfterPageEntryRemainsSelected()
    {
        MainWindowViewModel viewModel = await CreateViewModelAsync();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        ComboBox selector = BindReplaceModeSelector(viewModel);
        viewModel.MessageCenter.ToggleDebugActivityCommand.Execute(null);
        int modeActivityCount = viewModel.MessageCenter.ActivityItems.Count(static item =>
            item.Title == "Mode selected");
        object? originalItemsSource = selector.ItemsSource;
        Assert.NotNull(originalItemsSource);
        var mergeChanges = new List<string>();
        var replaceChanges = new List<string>();
        var workflowChanges = new List<string>();
        Track(viewModel.Merge, mergeChanges);
        Track(viewModel.Replace, replaceChanges);
        Track(viewModel.WorkflowSession, workflowChanges);

        selector.SelectedItem = ExperienceIds.GeneralReplace;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(originalItemsSource, selector.ItemsSource);
        Assert.Equal(ExperienceIds.GeneralReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(ExperienceIds.GeneralReplace, selector.SelectedItem);
        Assert.Equal(modeActivityCount + 1, viewModel.MessageCenter.ActivityItems.Count(static item =>
            item.Title == "Mode selected"));
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.ReplaceModeChoices), replaceChanges);
        AssertPublishedExactlyOnce(
            replaceChanges,
            nameof(ReplacePresentationViewModel.SelectedReplaceMode),
            nameof(ReplacePresentationViewModel.Inspection),
            nameof(ReplacePresentationViewModel.IsGeneralReplaceModeSelected),
            nameof(ReplacePresentationViewModel.ReplaceOutputFileName),
            nameof(ReplacePresentationViewModel.ReplaceMemorySummary),
            nameof(ReplacePresentationViewModel.SelectedReplaceWorkflowReadiness),
            nameof(ReplacePresentationViewModel.SelectedReplaceModeEvidenceLabel));
        AssertHiddenMergeContextWasNotPublished(mergeChanges);
        Assert.DoesNotContain(nameof(WorkflowSessionPresentationViewModel.IcChoices), workflowChanges);

        viewModel.WorkflowSession.SelectedIc = string.Empty;
        Dispatcher.UIThread.RunJobs();
        originalItemsSource = selector.ItemsSource;
        mergeChanges.Clear();
        replaceChanges.Clear();
        workflowChanges.Clear();

        selector.SelectedItem = ExperienceIds.CtrlRamReplace;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(originalItemsSource, selector.ItemsSource);
        Assert.Equal(ExperienceIds.CtrlRamReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.ReplaceModeChoices), replaceChanges);
        AssertPublishedExactlyOnce(
            replaceChanges,
            nameof(ReplacePresentationViewModel.SelectedReplaceMode),
            nameof(ReplacePresentationViewModel.Inspection),
            nameof(ReplacePresentationViewModel.IsCtrlRamReplaceModeSelected),
            nameof(ReplacePresentationViewModel.ReplaceOutputFileName),
            nameof(ReplacePresentationViewModel.ReplaceMemorySummary));
        AssertHiddenMergeContextWasNotPublished(mergeChanges);
        Assert.DoesNotContain(nameof(WorkflowSessionPresentationViewModel.IcChoices), workflowChanges);
    }

    /// <summary>A retained CtrlRAM Base projection is prepared silently before one page publication.</summary>
    [AvaloniaFact]
    public async Task CtrlRamModeSelectionWithRetainedBaseInspectionPublishesPageContextOnce()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        MainWindowViewModel viewModel = await CreateViewModelAsync();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            CanonicalGoldenTestData.ArtifactPath(baseArtifact),
            TestContext.Current.CancellationToken);
        await viewModel.Replace.Inspection.ActiveTask.WaitAsync(
            TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);
        Assert.NotNull(viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        string alternateMode = viewModel.Replace.ReplaceModeChoices.First(mode =>
            mode != ExperienceIds.CtrlRamReplace);
        viewModel.Replace.SelectedReplaceMode = alternateMode;
        var replaceChanges = new List<string>();
        Track(viewModel.Replace, replaceChanges);

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;

        AssertPublishedExactlyOnce(
            replaceChanges,
            nameof(ReplacePresentationViewModel.SelectedReplaceMode),
            nameof(ReplacePresentationViewModel.Inspection),
            nameof(ReplacePresentationViewModel.IsCtrlRamReplaceModeSelected),
            nameof(ReplacePresentationViewModel.ReplaceMemorySummary),
            nameof(ReplacePresentationViewModel.IsReplaceCoverageGrouped));
    }

    /// <summary>A selected CtrlRAM Base awaiting inspection does not publish during preparation.</summary>
    [AvaloniaFact]
    public async Task CtrlRamModeSelectionWithBaseAwaitingInspectionPublishesPageContextOnce()
    {
        MainWindowViewModel viewModel = await CreateViewModelAsync();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        string alternateMode = viewModel.Replace.ReplaceModeChoices.First(mode =>
            mode != ExperienceIds.CtrlRamReplace);
        viewModel.Replace.SelectedReplaceMode = alternateMode;
        viewModel.Replace.ReplaceBaseSlot.FilePath = "C:\\pending-base.bin";
        var replaceChanges = new List<string>();
        Track(viewModel.Replace, replaceChanges);

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;

        Assert.Null(viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        AssertPublishedExactlyOnce(
            replaceChanges,
            nameof(ReplacePresentationViewModel.SelectedReplaceMode),
            nameof(ReplacePresentationViewModel.Inspection),
            nameof(ReplacePresentationViewModel.IsCtrlRamReplaceModeSelected),
            nameof(ReplacePresentationViewModel.ReplaceMemorySummary),
            nameof(ReplacePresentationViewModel.IsReplaceCoverageGrouped));
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

    private static ComboBox BindDeviceSelector(MainWindowViewModel viewModel)
    {
        var selector = new ComboBox { DataContext = viewModel };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("WorkflowSession.IcChoices"));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding("WorkflowSession.SelectedIc")
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

    private static void SelectAbCodeThroughUserInput(Window window, ComboBox selector)
    {
        Point selectorPoint = Assert.IsType<Point>(selector.TranslatePoint(
            new Point(selector.Bounds.Width / 2, selector.Bounds.Height / 2),
            window));
        window.MouseMove(selectorPoint, RawInputModifiers.None);
        window.MouseDown(selectorPoint, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(selectorPoint, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        ComboBoxItem abItem = Assert.Single(
            selector.GetLogicalDescendants().OfType<ComboBoxItem>(),
            static item => string.Equals(
                item.Content as string,
                ExperienceIds.AbMerge,
                StringComparison.Ordinal));
        TopLevel popupRoot = Assert.IsType<TopLevel>(TopLevel.GetTopLevel(abItem), exactMatch: false);
        Point itemPoint = Assert.IsType<Point>(abItem.TranslatePoint(
            new Point(abItem.Bounds.Width / 2, abItem.Bounds.Height / 2),
            popupRoot));
        popupRoot.MouseMove(itemPoint, RawInputModifiers.None);
        popupRoot.MouseDown(itemPoint, MouseButton.Left, RawInputModifiers.None);
        popupRoot.MouseUp(itemPoint, MouseButton.Left, RawInputModifiers.None);
    }

    private static void Track(INotifyPropertyChanged source, List<string> changes)
    {
        source.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is { } propertyName)
            {
                changes.Add(propertyName);
            }
        };
    }

    private static void TrackCollection(
        System.Collections.Specialized.INotifyCollectionChanged source,
        List<string> changes)
    {
        source.CollectionChanged += (_, args) => changes.Add(args.Action.ToString());
    }

    private static void AssertPublishedExactlyOnce(
        IReadOnlyCollection<string> changes,
        params string[] propertyNames)
    {
        Assert.All(
            propertyNames,
            propertyName => Assert.Equal(1, changes.Count(change => change == propertyName)));
    }

    private static void AssertHiddenMergeContextWasNotPublished(IReadOnlyCollection<string> changes)
    {
        string[] allowedCommandStateChanges =
        [
            nameof(MergePresentationViewModel.CanBuildMerge),
            nameof(MergePresentationViewModel.PrimaryBuildBlocker),
            nameof(MergePresentationViewModel.MergeReadinessStatus),
        ];

        Assert.Empty(changes.Except(allowedCommandStateChanges, StringComparer.Ordinal));
        Assert.DoesNotContain(nameof(MergePresentationViewModel.MergeModeChoices), changes);
        Assert.DoesNotContain(nameof(MergePresentationViewModel.SelectedMergeMode), changes);
        Assert.DoesNotContain(nameof(MergePresentationViewModel.Inspection), changes);
        Assert.DoesNotContain(nameof(MergePresentationViewModel.IsAbCodeMergeModeSelected), changes);
        Assert.DoesNotContain(nameof(MergePresentationViewModel.MergeOutputFileName), changes);
        Assert.DoesNotContain(nameof(MergePresentationViewModel.MergeMemorySummary), changes);
    }

    private static void AssertHiddenReplaceContextWasNotPublished(IReadOnlyCollection<string> changes)
    {
        string[] allowedCommandStateChanges =
        [
            nameof(ReplacePresentationViewModel.CanBuildReplace),
            nameof(ReplacePresentationViewModel.PrimaryBuildBlocker),
            nameof(ReplacePresentationViewModel.ReplaceReadinessStatus),
            nameof(ReplacePresentationViewModel.ReplaceSelectionCountLabel),
            nameof(ReplacePresentationViewModel.ReplaceSelectionSubtitle),
            nameof(ReplacePresentationViewModel.ReplaceSelectionStatusLabel),
            nameof(ReplacePresentationViewModel.ReplaceSelectionRunHint),
            nameof(ReplacePresentationViewModel.ReplaceSelectionRows),
            nameof(ReplacePresentationViewModel.ReplaceSelectionMissingRows),
            nameof(ReplacePresentationViewModel.HasReplaceSelectionMissingRows),
        ];

        Assert.Empty(changes.Except(allowedCommandStateChanges, StringComparer.Ordinal));
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.ReplaceModeChoices), changes);
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.SelectedReplaceMode), changes);
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.Inspection), changes);
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.IsGeneralReplaceModeSelected), changes);
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.ReplaceOutputFileName), changes);
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.ReplaceMemorySummary), changes);
    }
}
