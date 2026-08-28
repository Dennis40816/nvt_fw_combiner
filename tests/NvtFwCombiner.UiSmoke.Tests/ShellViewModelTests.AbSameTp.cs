using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>AB remains independent by default; linked mode waits on TPA and locks only TPB Browse.</summary>
    [Fact]
    public async Task AbSameTpModeIsExplicitAndPreservesTwoCards()
    {
        MainWindowViewModel viewModel = PrepareAbSameTpViewModel();

        Assert.False(viewModel.Merge.UseSameTpForAbMerge);
        Assert.True(AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput).CanSelectFile);
        Assert.True(AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).CanSelectFile);

        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);

        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.False(viewModel.Merge.IsAbSameTpConflictPromptOpen);
        Assert.Equal(3, viewModel.Merge.MergeSlots.Count);
        Assert.True(AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput).CanSelectFile);
        FirmwareSlotViewModel tpB = AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput);
        Assert.False(tpB.CanSelectFile);
        Assert.Contains("Same as TPA", tpB.DisplayNameWithSelectionContext, StringComparison.Ordinal);

        viewModel.SelectedLanguage = "Traditional Chinese";
        Assert.Contains("與 TPA 相同", tpB.DisplayNameWithSelectionContext, StringComparison.Ordinal);
        viewModel.SelectedLanguage = "English";
        Assert.Contains("Same as TPA", tpB.DisplayNameWithSelectionContext, StringComparison.Ordinal);
    }

    /// <summary>One selected TP is inspected as two independent logical bindings when linking is enabled.</summary>
    [Theory]
    [InlineData(CompositionAddressSpaceIds.TpAInput)]
    [InlineData(CompositionAddressSpaceIds.TpBInput)]
    public async Task AbSameTpModeReusesTheOnlySelectedTp(string selectedSlotId)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-same-tp-one-selected");
        string sharedPath = workspace.Write(
            "shared-tp.bin",
            CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102));
        MainWindowViewModel viewModel = PrepareAbSameTpViewModel();
        await viewModel.WorkflowSession.SetSlotFileAsync(
            selectedSlotId,
            sharedPath,
            TestContext.Current.CancellationToken);

        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);

        FirmwareSlotViewModel tpA = AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput);
        FirmwareSlotViewModel tpB = AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput);
        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.Equal(sharedPath, tpA.FilePath);
        Assert.Equal(sharedPath, tpB.FilePath);
        Assert.NotNull(tpA.CurrentInspectionProjection);
        Assert.NotNull(tpB.CurrentInspectionProjection);
        Assert.Contains(tpA.SemanticState, new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
        Assert.Contains(tpB.SemanticState, new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
    }

    /// <summary>Two independent slots already using one canonical path link without a conflict prompt.</summary>
    [Fact]
    public async Task AbSameTpModeAcceptsTwoExistingEqualPaths()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-same-tp-equal-paths");
        string sharedPath = workspace.Write(
            "shared-tp.bin",
            CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102));
        MainWindowViewModel viewModel = PrepareAbSameTpViewModel();
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            sharedPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            sharedPath,
            TestContext.Current.CancellationToken);

        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);

        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.False(viewModel.Merge.IsAbSameTpConflictPromptOpen);
        Assert.Equal(sharedPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput).FilePath);
        Assert.Equal(sharedPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).FilePath);
    }

    /// <summary>Conflicting TP selections are never overwritten before one explicit keep/cancel choice.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AbSameTpConflictRequiresExplicitChoice(bool keepTpA)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-same-tp-conflict");
        string tpAPath = workspace.Write(
            "tp-a.bin",
            CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102));
        string tpBPath = workspace.Write(
            "tp-b.bin",
            CreateUiAbTpImage(0x82, 0x03, 2, 0, 0, 0x6A5C));
        MainWindowViewModel viewModel = PrepareAbSameTpViewModel();
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            tpAPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            tpBPath,
            TestContext.Current.CancellationToken);

        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);

        Assert.False(viewModel.Merge.UseSameTpForAbMerge);
        Assert.True(viewModel.Merge.IsAbSameTpConflictPromptOpen);
        Assert.Equal(tpAPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput).FilePath);
        Assert.Equal(tpBPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).FilePath);

        viewModel.Merge.CancelAbSameTpConflictCommand.Execute(null);
        Assert.False(viewModel.Merge.IsAbSameTpConflictPromptOpen);
        Assert.False(viewModel.Merge.UseSameTpForAbMerge);

        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        if (keepTpA)
        {
            await viewModel.Merge.KeepTpAForAbSameTpCommand.ExecuteAsync(null);
        }
        else
        {
            await viewModel.Merge.KeepTpBForAbSameTpCommand.ExecuteAsync(null);
        }

        Assert.True(viewModel.Merge.UseSameTpForAbMerge);
        Assert.False(viewModel.Merge.IsAbSameTpConflictPromptOpen);
        string expectedPath = keepTpA ? tpAPath : tpBPath;
        Assert.All(
            [
                AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput),
                AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput),
            ],
            slot => Assert.Equal(expectedPath, slot.FilePath));
    }

    /// <summary>Linked TPA Browse updates both; unlink retains both and restores independent TPB selection.</summary>
    [Fact]
    public async Task AbSameTpUnlinkRetainsSelectionsAndRestoresIndependentBrowse()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-same-tp-unlink");
        string firstPath = workspace.Write(
            "shared-first.bin",
            CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102));
        string secondPath = workspace.Write(
            "shared-second.bin",
            CreateUiAbTpImage(0x82, 0x03, 2, 0, 0, 0x6A5C));
        MainWindowViewModel viewModel = PrepareAbSameTpViewModel();
        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            firstPath,
            TestContext.Current.CancellationToken);

        Assert.All(
            [
                AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput),
                AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput),
            ],
            slot => Assert.Equal(firstPath, slot.FilePath));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            secondPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(firstPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).FilePath);

        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);

        Assert.False(viewModel.Merge.UseSameTpForAbMerge);
        Assert.All(
            [
                AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput),
                AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput),
            ],
            slot => Assert.Equal(firstPath, slot.FilePath));
        Assert.True(AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).CanSelectFile);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            secondPath,
            TestContext.Current.CancellationToken);

        Assert.Equal(firstPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput).FilePath);
        Assert.Equal(secondPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).FilePath);
    }

    /// <summary>The conflict surface traps keyboard focus and Escape cancels without changing either file.</summary>
    [AvaloniaFact]
    public async Task AbSameTpConflictModalProvidesKeyboardIsolationAndEscapeCancel()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-same-tp-modal-focus");
        string tpAPath = workspace.Write(
            "tp-a.bin",
            CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102));
        string tpBPath = workspace.Write(
            "tp-b.bin",
            CreateUiAbTpImage(0x82, 0x03, 2, 0, 0, 0x6A5C));
        MainWindowViewModel viewModel = await Task.Run(
            PrepareAbSameTpViewModel,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            tpAPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            tpBPath,
            TestContext.Current.CancellationToken);
        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        var modal = new AbSameTpConflictModal { DataContext = viewModel.Merge };
        var backgroundButton = new Button();
        var host = new Grid();
        host.Children.Add(backgroundButton);
        host.Children.Add(modal);
        var window = new Window { Content = host };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(modal));
            Assert.Same(modal.FindControl<Button>("CancelButton"), window.FocusManager?.GetFocusedElement());
            modal.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
            });
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.Merge.IsAbSameTpConflictPromptOpen);
            Assert.False(viewModel.Merge.UseSameTpForAbMerge);
            Assert.Equal(tpAPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpAInput).FilePath);
            Assert.Equal(tpBPath, AbTpSlot(viewModel, CompositionAddressSpaceIds.TpBInput).FilePath);

            modal.IsVisible = false;
            Assert.True(backgroundButton.Focus());
            modal.IsVisible = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Same(modal.FindControl<Button>("CancelButton"), window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    private static MainWindowViewModel PrepareAbSameTpViewModel()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        return viewModel;
    }

    private static FirmwareSlotViewModel AbTpSlot(MainWindowViewModel viewModel, string addressSpaceId)
    {
        return viewModel.Merge.MergeSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, addressSpaceId));
    }
}
