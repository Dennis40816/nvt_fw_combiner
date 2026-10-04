using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Actual Tab traversal follows each AB confirmation's visual order without reaching the background.</summary>
public sealed class AbConfirmationModalTabTests
{
    /// <summary>Cancel and Enable wrap in both directions while background controls remain focusable.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DummyDpTabAndShiftTabCycleInVisualOrderWithoutFocusingBackground(bool reverse)
    {
        MainWindowViewModel shell = await Task.Run(
            () => PresentationTestHost.CreateViewModel(), TestContext.Current.CancellationToken);
        shell.ShowMergeCommand.Execute(null);
        shell.WorkflowSession.SelectedIc = "NT51929";
        shell.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        var modal = new AbDummyDpConfirmationModal { DataContext = shell.Merge };
        _ = modal.Bind(AbDummyDpConfirmationModal.IsOpenProperty,
            new Binding(nameof(shell.Merge.IsAbDummyDpPromptOpen)));
        _ = modal.Bind(Avalonia.Visual.IsVisibleProperty,
            new Binding(nameof(shell.Merge.IsAbDummyDpPromptOpen)));
        var before = new Button { Content = "Before modal" };
        var after = new Button { Content = "After modal" };
        var host = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(modal, 1);
        Grid.SetRow(after, 2);
        host.Children.Add(before);
        host.Children.Add(modal);
        host.Children.Add(after);
        var window = new Window { Content = host, Width = 800, Height = 600 };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.False(modal.IsVisible);
            Assert.True(before.Focus());
            Assert.Same(before, window.FocusManager?.GetFocusedElement());
            Assert.True(after.Focus());
            Assert.Same(after, window.FocusManager?.GetFocusedElement());

            await shell.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(shell.Merge.IsAbDummyDpPromptOpen);
            Assert.True(modal.IsEffectivelyVisible);
            Assert.True(before.IsEffectivelyEnabled);
            Assert.True(after.IsEffectivelyEnabled);
            Button cancel = modal.FindControl<Button>("CancelButton")!;
            Button enable = modal.FindControl<Button>("EnableButton")!;
            Assert.True(cancel.IsEffectivelyEnabled);
            Assert.True(enable.IsEffectivelyEnabled);
            Assert.True(cancel.TranslatePoint(default, window)!.Value.X < enable.TranslatePoint(default, window)!.Value.X);
            Assert.Same(cancel, window.FocusManager?.GetFocusedElement());

            RawInputModifiers modifiers = reverse ? RawInputModifiers.Shift : RawInputModifiers.None;
            Button[] cycle = [enable, cancel];
            foreach (Button expected in cycle.Concat(cycle))
            {
                window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
                window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(expected, window.FocusManager?.GetFocusedElement());
                Assert.False(before.IsFocused);
                Assert.False(after.IsFocused);
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Cancel, Keep B and Keep A wrap forward and backward without escaping to focusable siblings.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameTpConflictTabAndShiftTabCycleInVisualOrderWithoutFocusingBackground(bool reverse)
    {
        using var workspace = TempWorkspace.Create("ab-confirmation-tab");
        string tpAPath = workspace.Write("tp-a.bin", CreateUiAbTpImage(0x81, 0, 1, 4, 1, 0x5102));
        string tpBPath = workspace.Write("tp-b.bin", CreateUiAbTpImage(0x82, 3, 2, 0, 0, 0x6A5C));
        MainWindowViewModel shell = await Task.Run(
            () => PresentationTestHost.CreateViewModel(), TestContext.Current.CancellationToken);
        shell.ShowMergeCommand.Execute(null);
        shell.WorkflowSession.SelectedIc = "NT51929";
        shell.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await shell.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput, tpAPath, TestContext.Current.CancellationToken);
        await shell.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput, tpBPath, TestContext.Current.CancellationToken);
        var modal = new AbSameTpConflictModal { DataContext = shell.Merge };
        _ = modal.Bind(Avalonia.Visual.IsVisibleProperty,
            new Binding(nameof(shell.Merge.IsAbSameTpConflictPromptOpen)));
        var before = new Button { Content = "Before modal" };
        var after = new Button { Content = "After modal" };
        var host = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(modal, 1);
        Grid.SetRow(after, 2);
        host.Children.Add(before);
        host.Children.Add(modal);
        host.Children.Add(after);
        var window = new Window { Content = host, Width = 800, Height = 600 };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.False(modal.IsVisible);
            Assert.True(before.Focus());
            Assert.Same(before, window.FocusManager?.GetFocusedElement());
            Assert.True(after.Focus());
            Assert.Same(after, window.FocusManager?.GetFocusedElement());

            await shell.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(shell.Merge.IsAbSameTpConflictPromptOpen);
            Assert.True(modal.IsEffectivelyVisible);
            Assert.True(before.IsEffectivelyEnabled);
            Assert.True(after.IsEffectivelyEnabled);
            Button cancel = modal.FindControl<Button>("CancelButton")!;
            Button keepB = Assert.Single(modal.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, shell.Merge.KeepTpBForAbSameTpCommand));
            Button keepA = Assert.Single(modal.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, shell.Merge.KeepTpAForAbSameTpCommand));
            Assert.All<Button>([cancel, keepB, keepA], button => Assert.True(button.IsEffectivelyEnabled));
            Assert.True(cancel.TranslatePoint(default, window)!.Value.X < keepB.TranslatePoint(default, window)!.Value.X);
            Assert.True(keepB.TranslatePoint(default, window)!.Value.X < keepA.TranslatePoint(default, window)!.Value.X);
            Assert.Same(cancel, window.FocusManager?.GetFocusedElement());

            Button[] cycle = reverse ? [keepA, keepB, cancel] : [keepB, keepA, cancel];
            RawInputModifiers modifiers = reverse ? RawInputModifiers.Shift : RawInputModifiers.None;
            foreach (Button expected in cycle.Concat(cycle))
            {
                window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
                window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(expected, window.FocusManager?.GetFocusedElement());
                Assert.False(before.IsFocused);
                Assert.False(after.IsFocused);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
