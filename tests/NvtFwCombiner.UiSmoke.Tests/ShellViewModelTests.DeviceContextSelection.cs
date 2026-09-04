using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
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

    /// <summary>Home AB confirmation retains the selected mode in the live top-right selector.</summary>
    [AvaloniaFact]
    public async Task HomeAbContextConfirmationRetainsSelectedModeInLiveTopRightSelector()
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        CapabilitySelectorPublication publication = services.Composition.Capabilities
            .GetSelectorPublication();
        Assert.False(publication.IsWorkflowAuthorable("NT51926", ExperienceIds.AbMerge));
        Assert.True(publication.IsWorkflowAuthorable("NT51950", ExperienceIds.AbMerge));

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            services,
            ShellPreferenceSnapshot.Default)
        {
            DataContext = viewModel,
        };
        var workflowChanges = new List<string>();
        var modeProjectionChanges = new List<string>();
        var modePublicationEvents = new List<string>();

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            ComboBox initialSelector = GetVisibleMergeModeSelector(window, viewModel);
            Assert.Equal(ExperienceIds.StandardMerge, initialSelector.SelectedItem);
            object? originalItemsSource = initialSelector.ItemsSource;
            Assert.NotNull(originalItemsSource);
            System.Collections.Specialized.INotifyCollectionChanged modeProjection =
                Assert.IsType<System.Collections.Specialized.INotifyCollectionChanged>(
                    viewModel.Merge.MergeModeChoices,
                    exactMatch: false);
            modeProjection.CollectionChanged += OnModeProjectionChanged;
            viewModel.MessageCenter.ToggleDebugActivityCommand.Execute(null);
            int modeActivityCount = viewModel.MessageCenter.ActivityItems.Count(static item =>
                item.Title == "Mode selected");

            viewModel.WorkflowSession.PropertyChanged += OnWorkflowPropertyChanged;
            viewModel.Merge.PropertyChanged += OnMergePropertyChanged;
            try
            {
                viewModel.ShowHomeCommand.Execute(null);
                viewModel.BeginAbMergeFromHomeCommand.Execute(null);
                Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
                viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51950";
                viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            finally
            {
                viewModel.WorkflowSession.PropertyChanged -= OnWorkflowPropertyChanged;
                viewModel.Merge.PropertyChanged -= OnMergePropertyChanged;
            }

            ComboBox selector = GetVisibleMergeModeSelector(window, viewModel);
            Assert.Same(originalItemsSource, selector.ItemsSource);
            Assert.Equal(ExperienceIds.AbMerge, selector.SelectedItem);
            Assert.True(viewModel.IsMergeVisible);
            Assert.Equal(ShellPage.Merge, viewModel.SelectedPage);
            Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
            Assert.Equal(
                "NT51950",
                viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
            Assert.Contains("NT51950", viewModel.WorkflowSession.IcChoices);
            Assert.DoesNotContain("NT51926", viewModel.WorkflowSession.IcChoices);
            Assert.Equal(modeActivityCount, viewModel.MessageCenter.ActivityItems.Count(static item =>
                item.Title == "Mode selected"));
            Assert.DoesNotContain("Reset", modeProjectionChanges);
            int addIndex = modePublicationEvents.FindIndex(change => change == "Collection:Add");
            int selectedIndex = modePublicationEvents.FindIndex(change =>
                change == $"Property:{nameof(MergePresentationViewModel.SelectedMergeMode)}");
            Assert.True(addIndex >= 0);
            Assert.True(selectedIndex >= 0);
            Assert.True(addIndex < selectedIndex);
            Assert.Contains(
                nameof(WorkflowSessionPresentationViewModel.IcChoices),
                workflowChanges);
        }
        finally
        {
            window.Close();
        }

        void OnWorkflowPropertyChanged(
            object? _,
            System.ComponentModel.PropertyChangedEventArgs args)
        {
            workflowChanges.Add(args.PropertyName ?? string.Empty);
        }

        void OnMergePropertyChanged(
            object? _,
            System.ComponentModel.PropertyChangedEventArgs args)
        {
            string propertyName = args.PropertyName ?? string.Empty;
            modePublicationEvents.Add($"Property:{propertyName}");
        }

        void OnModeProjectionChanged(
            object? _,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            string action = args.Action.ToString();
            modeProjectionChanges.Add(action);
            modePublicationEvents.Add($"Collection:{action}");
        }
    }

    /// <summary>A cold Home-to-AB confirmation keeps the live selector bound through an AB/Standard round trip.</summary>
    [AvaloniaFact]
    public async Task ColdHomeAbConfirmationKeepsLiveModeSelectorSwitchable()
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        CapabilitySelectorPublication publication = services.Composition.Capabilities
            .GetSelectorPublication();
        Assert.True(publication.IsWorkflowAuthorable("NT51950", ExperienceIds.AbMerge));
        Assert.True(publication.IsWorkflowAuthorable("NT51950", ExperienceIds.StandardMerge));
        Assert.False(publication.IsWorkflowAuthorable("NT51926", ExperienceIds.AbMerge));
        Assert.Equal(ShellPage.Home, viewModel.SelectedPage);
        Assert.False(viewModel.IsMergeVisible);
        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);

        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            services,
            ShellPreferenceSnapshot.Default)
        {
            DataContext = viewModel,
        };
        var modeWrites = new List<string?>();

        try
        {
            window.Show();
            DrainUi();

            viewModel.BeginAbMergeFromHomeCommand.Execute(null);
            Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
            viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51950";
            viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
            DrainUi();

            ComboBox selector = GetVisibleMergeModeSelector(window, viewModel);
            object? originalItemsSource = selector.ItemsSource;
            Assert.NotNull(originalItemsSource);
            Assert.Equal(ExperienceIds.AbMerge, selector.SelectedItem);
            Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
            Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
            Assert.Contains("NT51950", viewModel.WorkflowSession.IcChoices);
            Assert.DoesNotContain("NT51926", viewModel.WorkflowSession.IcChoices);

            viewModel.Merge.PropertyChanged += OnMergePropertyChanged;
            try
            {
                selector.SelectedItem = ExperienceIds.StandardMerge;
                DrainUi();
                Assert.Same(originalItemsSource, selector.ItemsSource);
                Assert.Equal(ExperienceIds.StandardMerge, selector.SelectedItem);
                Assert.Equal(ExperienceIds.StandardMerge, viewModel.Merge.SelectedMergeMode);
                Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);

                selector.SelectedItem = ExperienceIds.AbMerge;
                DrainUi();
                Assert.Same(originalItemsSource, selector.ItemsSource);
                Assert.Equal(ExperienceIds.AbMerge, selector.SelectedItem);
                Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
                Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
                Assert.Contains("NT51950", viewModel.WorkflowSession.IcChoices);
                Assert.DoesNotContain("NT51926", viewModel.WorkflowSession.IcChoices);
            }
            finally
            {
                viewModel.Merge.PropertyChanged -= OnMergePropertyChanged;
            }

            Assert.NotEmpty(modeWrites);
            Assert.All(modeWrites, static mode => Assert.False(string.IsNullOrWhiteSpace(mode)));
            Assert.Contains(ExperienceIds.StandardMerge, modeWrites);
            Assert.Equal(ExperienceIds.AbMerge, modeWrites[^1]);
        }
        finally
        {
            window.Close();
        }

        void OnMergePropertyChanged(
            object? _,
            System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(MergePresentationViewModel.SelectedMergeMode))
            {
                modeWrites.Add(viewModel.Merge.SelectedMergeMode);
            }
        }

        static void DrainUi()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static ComboBox GetVisibleMergeModeSelector(
        MainWindow window,
        MainWindowViewModel viewModel)
    {
        return Assert.Single(
            window.GetVisualDescendants().OfType<ComboBox>(),
            candidate => candidate.IsVisible && ReferenceEquals(candidate.DataContext, viewModel.Merge));
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
