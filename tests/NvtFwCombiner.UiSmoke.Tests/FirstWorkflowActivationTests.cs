using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>First navigation initializes only the entered workflow without losing deferred context.</summary>
public sealed class FirstWorkflowActivationTests
{
    /// <summary>Both direct navigation and Home confirmation leave the other page deferred until entry.</summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    public async Task FirstEntryDefersOtherPageAndSecondEntryPublishesItsCompleteContext(
        bool mergeFirst,
        bool useHomeSelection,
        bool reloadBeforeEntry)
    {
        MainWindowViewModel shell = PresentationTestHost.CreateProductViewModel();
        Assert.False(shell.WorkflowSession.IsWorkflowLoaded);
        Assert.Empty(shell.Merge.MergeSlots);
        Assert.Empty(shell.Replace.ReplaceSlots);

        if (reloadBeforeEntry)
        {
            await shell.MessageCenter.RefreshCommand.ExecuteAsync(null);
            Assert.False(shell.WorkflowSession.IsWorkflowLoaded);
            Assert.Empty(shell.Merge.MergeSlots);
            Assert.Empty(shell.Replace.ReplaceSlots);
        }

        if (useHomeSelection)
        {
            IRelayCommand begin = mergeFirst ? shell.BeginNormalMergeFromHomeCommand : shell.BeginCtrlRamReplaceFromHomeCommand;
            begin.Execute(null);
            Assert.True(shell.WorkflowSession.IsWorkflowContextModalOpen);
            shell.WorkflowSession.CancelWorkflowContextCommand.Execute(null);
            Assert.False(shell.WorkflowSession.IsWorkflowLoaded);
            begin.Execute(null);
            shell.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        }
        else
        {
            (mergeFirst ? shell.ShowMergeCommand : shell.ShowReplaceCommand).Execute(null);
        }

        Assert.True(shell.WorkflowSession.IsWorkflowLoaded);
        Assert.Equal(mergeFirst ? ShellPage.Merge : ShellPage.Replace, shell.SelectedPage);
        Assert.NotEmpty(mergeFirst ? shell.Merge.MergeSlots : shell.Replace.ReplaceSlots);
        Assert.Empty(mergeFirst ? shell.Replace.ReplaceSlots : shell.Merge.MergeSlots);

        shell.ShowHomeCommand.Execute(null);
        (mergeFirst ? shell.ShowReplaceCommand : shell.ShowMergeCommand).Execute(null);

        Assert.Equal(mergeFirst ? ShellPage.Replace : ShellPage.Merge, shell.SelectedPage);
        Assert.NotEmpty(shell.Merge.MergeSlots);
        Assert.NotEmpty(shell.Replace.ReplaceSlots);
        Assert.NotEmpty(shell.Merge.MergeModeChoices);
        Assert.False(shell.Merge.CanBuildMerge);
        FirmwareSlotViewModel[] mergeSlots = [.. shell.Merge.MergeSlots];
        FirmwareSlotViewModel[] replaceSlots = [.. shell.Replace.ReplaceSlots];

        shell.ShowHomeCommand.Execute(null);
        shell.ShowMergeCommand.Execute(null);
        Assert.Equal(mergeSlots, shell.Merge.MergeSlots);
        shell.ShowReplaceCommand.Execute(null);
        Assert.Equal(replaceSlots, shell.Replace.ReplaceSlots);
    }

    /// <summary>The deferred production page binds the IC, topology, mode and complete current slot objects.</summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeferredPageUsesConfirmedContextInProductionControls(bool mergeFirst)
    {
        using var workspace = TempWorkspace.Create("workflow-first-activation");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            (mergeFirst ? shell.ShowMergeCommand : shell.ShowReplaceCommand).Execute(null);
            Assert.Empty(mergeFirst ? shell.Replace.ReplaceSlots : shell.Merge.MergeSlots);
            shell.ShowHomeCommand.Execute(null);
            (mergeFirst ? shell.BeginCtrlRamReplaceFromHomeCommand : shell.BeginAbMergeFromHomeCommand).Execute(null);
            string ic = mergeFirst ? "NT51927" : "NT51950";
            string number = mergeFirst ? "3" : IcNumberSelectionTokens.SingleChip;
            shell.WorkflowSession.WorkflowContextSetup.SelectedIc = ic;
            shell.WorkflowSession.WorkflowContextSetup.SelectedNumber = number;
            shell.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.Equal(ic, shell.WorkflowSession.SelectedIc);
            Assert.Equal(number, shell.WorkflowSession.SelectedNumber);
            string expectedMode = mergeFirst ? ExperienceIds.CtrlRamReplace : ExperienceIds.AbMerge;
            object owner = mergeFirst ? shell.Replace : shell.Merge;
            ComboBox selector = Assert.Single(window.GetVisualDescendants().OfType<ComboBox>(),
                candidate => candidate.IsVisible && ReferenceEquals(candidate.DataContext, owner));
            Assert.Equal(expectedMode, selector.SelectedItem);
            FirmwareSlotViewModel[] slots = [.. mergeFirst ? shell.Replace.ReplaceSlots : shell.Merge.MergeSlots];
            Assert.NotEmpty(slots);
            if (mergeFirst)
            {
                Assert.Equal(4, shell.Replace.ReplaceSlotGroups.Count);
                foreach (FirmwareSlotGroupViewModel group in shell.Replace.ReplaceSlotGroups) { group.IsExpanded = true; }
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }
            FirmwareSlotCard[] cards = [.. window.GetVisualDescendants().OfType<FirmwareSlotCard>()];
            foreach (FirmwareSlotViewModel slot in slots)
            {
                _ = Assert.Single(cards, card => ReferenceEquals(card.DataContext, slot));
            }
            Assert.False(shell.Merge.CanBuildMerge);
        }
        finally { await CloseAndFlushAsync(window); }
    }
}
