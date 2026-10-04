using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real Tab input stays within each active Hex Editor prompt.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class HexEditorModalTabTests
{
    /// <summary>Save actions retain their visual order and skip unavailable controls in both directions.</summary>
    [AvaloniaTheory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SaveTabCyclesThroughVisibleEnabledActions(bool enabled, bool visible)
    {
        using var workspace = TempWorkspace.Create("hex-save-tab");
        var editor = new HexEditorWorkspaceViewModel(
            ShellTextResources.For(ShellLanguage.English), new RawBinaryEditorFileSessionFactory());
        await editor.LoadAsync(workspace.Write("source.bin", [0x10, 0x20]), TestContext.Current.CancellationToken);
        editor.SetByteToFfCommand.Execute(0);
        var panel = new HexEditorPanel { DataContext = editor };
        var modal = new HexEditorSaveModal { DataContext = editor };
        using IDisposable visibility = modal.Bind(Visual.IsVisibleProperty, new Binding(nameof(editor.IsSaveConfirmationOpen)));
        var window = new Window { Width = 1100, Height = 700, Content = new Grid { Children = { panel, modal } } };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox background = Assert.IsType<TextBox>(panel.FindControl<TextBox>("GoToAddressTextBox"));
            Assert.True(background.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            IInputElement? next = window.FocusManager.GetFocusedElement();
            Assert.NotSame(background, next);
            Assert.True(background.Focus(NavigationMethod.Tab));
            editor.RequestSaveCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Button close = Assert.Single(modal.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, editor.CancelSaveCommand));
            Button save = Assert.Single(modal.GetVisualDescendants().OfType<Button>(), button => button != close);
            save.IsEnabled = enabled;
            save.IsVisible = visible;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(close, window.FocusManager.GetFocusedElement());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(modal.IsKeyboardFocusWithin);
            AssertTabCycle(window, modal, background, enabled && visible ? [close, save] : [close]);

            editor.CancelSaveCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(background, window.FocusManager.GetFocusedElement());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(next, window.FocusManager.GetFocusedElement());
            Assert.True(background.Focus(NavigationMethod.Tab));
            editor.RequestSaveCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(close, window.FocusManager.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The editable count precedes the footer actions, including when the insert command is disabled.</summary>
    [AvaloniaTheory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task InsertTabCyclesThroughCountAndVisibleEnabledActions(bool enabled, bool visible)
    {
        using var workspace = TempWorkspace.Create("hex-insert-tab");
        var editor = new HexEditorWorkspaceViewModel(
            ShellTextResources.For(ShellLanguage.English), new RawBinaryEditorFileSessionFactory());
        await editor.LoadAsync(workspace.Write("source.bin", [0x10, 0x20]), TestContext.Current.CancellationToken);
        var panel = new HexEditorPanel { DataContext = editor };
        var modal = new HexEditorInsertBytesModal { DataContext = editor };
        using IDisposable visibility = modal.Bind(Visual.IsVisibleProperty, new Binding(nameof(editor.IsInsertBytesPromptOpen)));
        var window = new Window { Width = 1100, Height = 700, Content = new Grid { Children = { panel, modal } } };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox background = Assert.IsType<TextBox>(panel.FindControl<TextBox>("GoToAddressTextBox"));
            Assert.True(background.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            IInputElement? next = window.FocusManager.GetFocusedElement();
            Assert.NotSame(background, next);
            Assert.True(background.Focus(NavigationMethod.Tab));
            editor.RequestInsertBytesBeforeCommand.Execute(0);
            Dispatcher.UIThread.RunJobs();
            NumericUpDown count = Assert.Single(modal.GetVisualDescendants().OfType<NumericUpDown>());
            TextBox input = Assert.Single(count.GetVisualDescendants().OfType<TextBox>());
            Button close = Assert.Single(modal.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, editor.CancelInsertBytesCommand));
            Button insert = Assert.Single(modal.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, editor.ConfirmInsertBytesCommand));
            if (!enabled)
            {
                editor.InsertByteCount = 1.5m;
            }
            insert.IsVisible = visible;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(enabled, insert.IsEffectivelyEnabled);
            Assert.Same(close, window.FocusManager.GetFocusedElement());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(modal.IsKeyboardFocusWithin);
            AssertTabCycle(window, modal, background, enabled && visible ? [input, close, insert] : [input, close]);

            if (enabled)
            {
                Assert.True(input.Focus(NavigationMethod.Tab));
                window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
                window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2m, editor.InsertByteCount);
            }
            Assert.Equal(2, editor.ViewportSnapshot.DocumentLength);
            editor.CancelInsertBytesCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(background, window.FocusManager.GetFocusedElement());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(next, window.FocusManager.GetFocusedElement());
            Assert.True(background.Focus(NavigationMethod.Tab));
            editor.RequestInsertBytesBeforeCommand.Execute(0);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(close, window.FocusManager.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertTabCycle(Window window, Control modal, Control background, Control[] order)
    {
        Assert.True(background.IsEffectivelyEnabled);
        Assert.True(order[0].Focus(NavigationMethod.Tab));
        foreach (RawInputModifiers modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
        {
            for (int step = 1; step <= order.Length * 2; step++)
            {
                window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
                window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                int index = modifiers == RawInputModifiers.None
                    ? step % order.Length
                    : (order.Length - (step % order.Length)) % order.Length;
                Control expected = order[index];
                Assert.Same(expected, window.FocusManager.GetFocusedElement());
                Assert.True(expected.IsEffectivelyVisible);
                Assert.True(expected.IsEffectivelyEnabled);
                Assert.Contains(modal, expected.GetVisualAncestors());
                Assert.False(background.IsFocused);
            }
        }
    }
}
