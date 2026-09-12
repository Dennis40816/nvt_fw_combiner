using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Refresh reuses presentation groups without retaining obsolete slot data.</summary>
public sealed class FirmwareSlotGroupRefreshTests
{
    /// <summary>Every group receives complete current slots, not a hand-copied subset of fields.</summary>
    [Fact]
    public void CtrlRamRefreshRetainsGroupsButPublishesWholeFreshSlots()
    {
        MainWindowViewModel shell = CreateThreeChipShell();
        FirmwareSlotGroupViewModel[] groups = [.. shell.Replace.ReplaceSlotGroups];
        FirmwareSlotViewModel[] slots = [.. shell.Replace.ReplaceSlots];
        Assert.Equal(4, groups.Length);
        foreach (FirmwareSlotGroupViewModel group in groups) { group.IsExpanded = false; }

        shell.Replace.RefreshContextState();

        Assert.Equal(groups.Length, shell.Replace.ReplaceSlotGroups.Count);
        for (int index = 0; index < groups.Length; index++)
        {
            FirmwareSlotGroupViewModel group = shell.Replace.ReplaceSlotGroups[index];
            Assert.Same(groups[index], group);
            Assert.False(group.IsExpanded);
            foreach (FirmwareSlotViewModel slot in group.Slots)
            {
                Assert.Same(shell.Replace.ReplaceSlots.Single(candidate => candidate.SlotId == slot.SlotId), slot);
                Assert.NotSame(slots.Single(candidate => candidate.SlotId == slot.SlotId), slot);
            }
        }
    }

    /// <summary>Only the latest slot objects can update the retained group's counters.</summary>
    [Fact]
    public void RefreshedGroupUnsubscribesOldSlotsAndObservesCurrentSlots()
    {
        MainWindowViewModel shell = CreateThreeChipShell();
        FirmwareSlotGroupViewModel group = shell.Replace.ReplaceSlotGroups[0];
        FirmwareSlotViewModel oldSlot = group.Slots[0];
        shell.Replace.RefreshContextState();
        Assert.Same(group, shell.Replace.ReplaceSlotGroups[0]);
        var changes = new List<string?>();
        group.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        oldSlot.FilePath = "obsolete.bin";
        Assert.Empty(changes);
        group.Slots[0].FilePath = "current.bin";
        Assert.Contains(nameof(FirmwareSlotGroupViewModel.CountLabel), changes);
        Assert.Equal(1, group.SelectedCount);
        Assert.Equal($"1/{group.Slots.Count}", group.CountLabel);
    }

    /// <summary>Changed, removed and newly grouped slots publish whole objects in canonical display order.</summary>
    [Fact]
    public void GroupReconciliationPublishesNewInformationAndDisconnectsRemovedGroups()
    {
        var text = ShellTextResources.For(ShellLanguage.English);
        var common = new FirmwareSlotViewModel("common", "Old", "Old description", FirmwareSlotKind.CtrlRam);
        var master = new FirmwareSlotViewModel("master", "Master", "Master description", FirmwareSlotKind.CtrlRam,
            regionGroup: ReplaceRegionGroup.Master);
        ObservableCollection<FirmwareSlotGroupViewModel> groups = [];
        ReplaceRegionGroupBuilder.UpdateSlotGroups(groups, [master, common], text);
        FirmwareSlotGroupViewModel retained = groups[0];
        FirmwareSlotGroupViewModel removed = groups[1];
        var oldChanges = new List<string?>();
        removed.PropertyChanged += (_, args) => oldChanges.Add(args.PropertyName);
        var changes = new List<NotifyCollectionChangedAction>();
        groups.CollectionChanged += (_, args) => changes.Add(args.Action);
        var notifications = new List<string?>();
        retained.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        var latest = new FirmwareSlotViewModel("common", "New title", "New description", FirmwareSlotKind.CtrlRam,
            isOptional: true, regionId: "new-region", addressSpaceId: "new-space", compiledSlotId: "new-compiled")
        { FilePath = "new.bin" };
        latest.SetInputInspection(FirmwareInputInspectionSeverity.Warning, "New warning");
        var cascade = new FirmwareSlotViewModel("cascade", "Cascade", "Cascade description", FirmwareSlotKind.CtrlRam,
            regionGroup: ReplaceRegionGroup.Cascade);

        ReplaceRegionGroupBuilder.UpdateSlotGroups(groups, [latest, cascade], text);

        Assert.Equal([ReplaceRegionGroup.Cascade, ReplaceRegionGroup.Common], groups.Select(g => g.Slots[0].RegionGroup));
        Assert.Same(retained, groups[1]);
        // Whole-object identity covers current AND future slot properties without a field-copy allowlist.
        Assert.Same(latest, Assert.Single(retained.Slots));
        Assert.Contains(string.Empty, notifications);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, changes);
        master.FilePath = "removed.bin";
        Assert.Empty(oldChanges);
        ReplaceRegionGroupBuilder.UpdateSlotGroups(groups, [], text);
        Assert.Empty(groups);
    }

    /// <summary>Relocalization invalidates every derived group binding, including future additions.</summary>
    [Fact]
    public void RetainedGroupLanguageUpdateInvalidatesAllDerivedBindings()
    {
        var slot = new FirmwareSlotViewModel("slot", "Slot", "Description", FirmwareSlotKind.CtrlRam);
        var group = new FirmwareSlotGroupViewModel([slot], true, ShellTextResources.For(ShellLanguage.English));
        var changes = new List<string?>();
        group.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        string before = group.SelectionSummary;
        group.ApplyText(ShellTextResources.For(ShellLanguage.ChineseTraditional));
        Assert.Contains(string.Empty, changes);
        Assert.NotEqual(before, group.SelectionSummary);
        Assert.Same(slot, Assert.Single(group.Slots));
    }

    /// <summary>Compiled production bindings retain group controls but display the latest complete card models.</summary>
    [AvaloniaFact]
    public async Task LiveCtrlRamRefreshRetainsGroupControlsAndRebindsEveryCard()
    {
        using var workspace = TempWorkspace.Create("ctrlram-group-refresh");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            shell.ShowReplaceCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51927";
            shell.WorkflowSession.SelectedNumber = "3";
            foreach (FirmwareSlotGroupViewModel group in shell.Replace.ReplaceSlotGroups) { group.IsExpanded = true; }
            Render();
            SpaciousPanel[] before = GroupControls();
            Assert.Equal(4, before.Length);
            shell.Replace.RefreshContextState();
            Render();
            SpaciousPanel[] after = GroupControls();
            Assert.Equal(before.Length, after.Length);
            for (int index = 0; index < before.Length; index++) { Assert.Same(before[index], after[index]); }
            FirmwareSlotCard[] cards = [.. window.GetVisualDescendants().OfType<FirmwareSlotCard>()];
            foreach (FirmwareSlotViewModel slot in shell.Replace.ReplaceSlotGroups.SelectMany(group => group.Slots))
            {
                _ = Assert.Single(cards, card => ReferenceEquals(card.DataContext, slot));
            }
            SpaciousPanel[] GroupControls()
            {
                return [.. window.GetVisualDescendants().OfType<SpaciousPanel>()
                    .Where(panel => panel.Classes.Contains("firmwareSlotGroupSurface"))];
            }
            void Render()
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static MainWindowViewModel CreateThreeChipShell()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.ShowReplaceCommand.Execute(null);
        shell.WorkflowSession.SelectedIc = "NT51927";
        shell.WorkflowSession.SelectedNumber = "3";
        return shell;
    }
}
