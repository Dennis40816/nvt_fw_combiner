using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Retained TP inspection cannot permanently lose the confirmation's return focus.</summary>
    [AvaloniaFact]
    public async Task AbDummyDpReturnsKeyboardFocusAfterDelayedTpInspection()
    {
        using var workspace = TempWorkspace.Create("dummy-dp-delayed-focus");
        string tpPath = workspace.Write("tp.bin", CreateUiAbTpImage(0x81, 0, 1, 4, 1, 0x5102));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int pause = 0;
        MainWindowViewModel vm = await Task.Run(() =>
        {
            PresentationHostServices services = PresentationTestHost.CreateServices("test");
            var model = new MainWindowViewModel("test", "test", ShellLanguage.English, services,
                new DelayedDummyInspection(TestHost.FirmwareInspectionExperience,
                    async () =>
                    {
                        if (Volatile.Read(ref pause) != 0)
                        {
                            _ = entered.TrySetResult();
                            await release.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
                        }
                    }));
            _ = PresentationTestHost.PublishCanonicalCatalog(services, model);
            model.ShowMergeCommand.Execute(null);
            model.WorkflowSession.SelectedIc = "NT51929";
            model.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
            return model;
        }, TestContext.Current.CancellationToken);
        await vm.WorkflowSession.SetSlotFileAsync(CompositionAddressSpaceIds.TpAInput, tpPath,
            TestContext.Current.CancellationToken);
        var option = new CheckBox { Name = "AbDummyDpCheckBox", Command = vm.Merge.ToggleAbDummyDpCommand };
        var modal = new AbDummyDpConfirmationModal { DataContext = vm.Merge };
        _ = modal.Bind(AbDummyDpConfirmationModal.IsOpenProperty, new Binding(nameof(vm.Merge.IsAbDummyDpPromptOpen)));
        _ = modal.Bind(Avalonia.Visual.IsVisibleProperty, new Binding(nameof(vm.Merge.IsAbDummyDpPromptOpen)));
        var grid = new Grid();
        grid.Children.Add(option);
        grid.Children.Add(modal);
        var window = new Window { DataContext = vm, Content = grid };
        Task transition = Task.CompletedTask;
        try
        {
            window.Show();
            await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(modal.FindControl<Button>("CancelButton"), window.FocusManager?.GetFocusedElement());
            Volatile.Write(ref pause, 1);
            transition = vm.Merge.ConfirmAbDummyDpCommand.ExecuteAsync(null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.False(option.IsEffectivelyEnabled);
            Volatile.Write(ref pause, 0);
            _ = release.TrySetResult();
            await transition;
            Dispatcher.UIThread.RunJobs();
            Assert.True(option.IsEffectivelyEnabled);
            Assert.Same(option, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            Volatile.Write(ref pause, 0);
            _ = release.TrySetResult();
            await transition;
            window.Close();
        }
    }

    private sealed class DelayedDummyInspection(IFirmwareInspection inner, Func<Task> beforeInspection) : IFirmwareInspection
    {
        public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
            string icId, IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
            CancellationToken cancellationToken, IProgress<AuthoringInspectionProgress>? progress = null)
        {
            await beforeInspection();
            return await inner.InspectFirmwareBatchAsync(icId, inputs, cancellationToken, progress);
        }

        public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
            string icId, string numberToken, FirmwareConfigMetadataSnapshot? baseFirmware)
        {
            return inner.ProjectCtrlRamInspectionDisplay(icId, numberToken, baseFirmware);
        }
    }

    /// <summary>One mode transition owns the whole inspection await and rejects overlapping mode commands.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbDummyDpTransitionRejectsReentryUntilInspectionCompletes(bool failInspection)
    {
        using var workspace = TempWorkspace.Create("nfc-ui-dummy-reentry");
        string tpPath = workspace.Write("tp.bin", CreateUiAbTpImage(0x81, 0, 1, 4, 1, 0x5102));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int pause = 0;
        PresentationHostServices services = PresentationTestHost.CreateServices("test");
        var vm = new MainWindowViewModel("test", "test", ShellLanguage.English, services,
            new DelegatingFirmwareInspection(TestHost.FirmwareInspectionExperience,
                batchReader: (icId, inputs) =>
                {
                    if (Volatile.Read(ref pause) != 0)
                    {
                        _ = entered.TrySetResult();
                        release.Task.GetAwaiter().GetResult();
                        if (failInspection)
                        {
                            throw new IOException("Controlled Dummy transition inspection failure.");
                        }
                    }
                    return BuiltInFirmwareInspection.InspectFirmwareBatch(
                        (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience, icId, inputs);
                }));
        _ = PresentationTestHost.PublishCanonicalCatalog(services, vm);
        vm.ShowMergeCommand.Execute(null);
        vm.WorkflowSession.SelectedIc = "NT51929";
        vm.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await vm.WorkflowSession.SetSlotFileAsync(CompositionAddressSpaceIds.TpAInput, tpPath,
            TestContext.Current.CancellationToken);
        Volatile.Write(ref pause, 1);
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        Task transition = vm.Merge.ConfirmAbDummyDpCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.True(vm.WorkflowSession.IsAbDummyDpTransitionInProgress);
            Assert.False(vm.Merge.ToggleAbDummyDpCommand.CanExecute(null));
            Assert.False(vm.Merge.ConfirmAbDummyDpCommand.CanExecute(null));
            await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
            await vm.WorkflowSession.SetAbDummyDpModeAsync(false, TestContext.Current.CancellationToken);
            Assert.True(vm.Merge.UseDummyDpForAbMerge);
            Assert.False(vm.Merge.IsAbDummyDpPromptOpen);
            Assert.False(transition.IsCompleted);
        }
        finally
        {
            Volatile.Write(ref pause, 0);
            _ = release.TrySetResult();
            await transition;
        }
        Assert.False(vm.WorkflowSession.IsAbDummyDpTransitionInProgress);
        if (failInspection)
        {
            Assert.Equal(WorkflowInspectionAttemptState.Failed, vm.Merge.Inspection.State);
        }
        Assert.True(vm.Merge.ToggleAbDummyDpCommand.CanExecute(null));
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        Assert.False(vm.Merge.UseDummyDpForAbMerge);
    }

    /// <summary>Leaving Merge dismisses its pending prompt and off-page commands cannot reopen it.</summary>
    [Fact]
    public async Task AbDummyDpConfirmationCannotCrossPageNavigation()
    {
        MainWindowViewModel vm = PrepareAbSameTpViewModel();
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        vm.ShowHomeCommand.Execute(null);
        Assert.False(vm.Merge.IsAbDummyDpPromptOpen);
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        Assert.False(vm.Merge.IsAbDummyDpPromptOpen);
        vm.ShowMergeCommand.Execute(null);
        await vm.Merge.ConfirmAbDummyDpCommand.ExecuteAsync(null);
        Assert.False(vm.Merge.UseDummyDpForAbMerge);
    }

    /// <summary>Changing the selected topology invalidates a pending confirmation even after returning.</summary>
    [Fact]
    public async Task AbDummyDpPendingConfirmationCannotSurviveTopologyRoundTrip()
    {
        MainWindowViewModel vm = PrepareAbSameTpViewModel();
        vm.WorkflowSession.SelectedIc = "NT51950";
        vm.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        Assert.True(vm.Merge.IsAbDummyDpPromptOpen);
        vm.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        vm.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        Assert.False(vm.Merge.IsAbDummyDpPromptOpen);
        await vm.Merge.ConfirmAbDummyDpCommand.ExecuteAsync(null);
        Assert.False(vm.Merge.UseDummyDpForAbMerge);
    }

    /// <summary>A confirmation belongs to its original uninterrupted authoring context.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbDummyDpPendingConfirmationCannotSurviveContextRoundTrip(bool changeIc)
    {
        MainWindowViewModel vm = PrepareAbSameTpViewModel();
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        Assert.True(vm.Merge.IsAbDummyDpPromptOpen);
        if (changeIc)
        {
            vm.WorkflowSession.SelectedIc = "NT51932";
            vm.WorkflowSession.SelectedIc = "NT51929";
        }
        else
        {
            vm.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
            vm.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        }
        Assert.False(vm.Merge.IsAbDummyDpPromptOpen);
        await vm.Merge.ConfirmAbDummyDpCommand.ExecuteAsync(null);
        Assert.False(vm.Merge.UseDummyDpForAbMerge);
    }

    /// <summary>Request and Cancel do not accept Dummy mode or clear an existing DP selection.</summary>
    [Fact]
    public async Task AbDummyDpWaitsForConfirmationAndCancelPreservesSelection()
    {
        using var workspace = TempWorkspace.Create("nfc-ui-dummy-cancel");
        string dpPath = workspace.Write("dp.bin", new byte[0x80000]);
        MainWindowViewModel vm = PrepareAbSameTpViewModel();
        await vm.WorkflowSession.SetSlotFileAsync(CompositionAddressSpaceIds.DpAbInput, dpPath, TestContext.Current.CancellationToken);
        FirmwareSlotViewModel dp = vm.Merge.MergeSlots.Single(static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.False(vm.Merge.UseDummyDpForAbMerge);
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        Assert.True(vm.Merge.IsAbDummyDpPromptOpen);
        Assert.False(vm.Merge.UseDummyDpForAbMerge);
        Assert.Equal(dpPath, dp.FilePath);
        vm.Merge.CancelAbDummyDpCommand.Execute(null);
        Assert.False(vm.Merge.IsAbDummyDpPromptOpen);
        Assert.False(vm.Merge.UseDummyDpForAbMerge);
        Assert.Equal(dpPath, dp.FilePath);
    }

    /// <summary>Confirmed mode changes clear only DP selection and cannot silently restore it.</summary>
    [Fact]
    public async Task AbDummyDpConfirmationPreservesTpAndSourceFiles()
    {
        using var workspace = TempWorkspace.Create("nfc-ui-dummy-confirm");
        string dpPath = workspace.Write("dp.bin", new byte[0x80000]);
        string tpPath = workspace.Write("tp.bin", CreateUiAbTpImage(0x81, 0, 1, 4, 1, 0x5102));
        MainWindowViewModel vm = PrepareAbSameTpViewModel();
        await vm.WorkflowSession.SetSlotFileAsync(CompositionAddressSpaceIds.DpAbInput, dpPath, TestContext.Current.CancellationToken);
        await vm.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        await vm.WorkflowSession.SetSlotFileAsync(CompositionAddressSpaceIds.TpAInput, tpPath, TestContext.Current.CancellationToken);
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        await vm.Merge.ConfirmAbDummyDpCommand.ExecuteAsync(null);
        Assert.True(vm.Merge.UseDummyDpForAbMerge);
        Assert.True(vm.Merge.UseSameTpForAbMerge);
        Assert.All(vm.Merge.AbMergeSlots, static slot => Assert.NotEqual(CompositionAddressSpaceIds.DpAbInput, slot.SlotId));
        Assert.Equal(tpPath, AbTpSlot(vm, CompositionAddressSpaceIds.TpAInput).FilePath);
        Assert.Equal(tpPath, AbTpSlot(vm, CompositionAddressSpaceIds.TpBInput).FilePath);
        await vm.WorkflowSession.SetSlotFileAsync(CompositionAddressSpaceIds.DpAbInput, dpPath, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(vm.Merge.AbMergeSlots, static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
        await vm.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
        Assert.False(vm.Merge.UseDummyDpForAbMerge);
        Assert.Null(vm.Merge.MergeSlots.Single(static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput).FilePath);
        Assert.Equal(tpPath, AbTpSlot(vm, CompositionAddressSpaceIds.TpAInput).FilePath);
        Assert.True(File.Exists(dpPath));
        Assert.True(File.Exists(tpPath));
    }
}
