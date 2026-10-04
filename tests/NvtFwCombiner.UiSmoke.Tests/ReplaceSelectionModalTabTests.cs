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

/// <summary>Actual Tab navigation stays in Replace selection and follows its visual order.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class ReplaceSelectionModalTabTests
{
    /// <summary>Both directions wrap inside the modal and skip Build when it is unavailable.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TabFollowsVisualOrderAndCyclesWithCurrentBuildReadiness(bool canBuild)
    {
        using TempWorkspace workspace = TempWorkspace.Create("replace-selection-tab");
        MainWindowViewModel shell = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        shell.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(shell, ExperienceIds.GeneralReplace);
        if (canBuild)
        {
            await shell.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase,
                workspace.Write("base.bin", CreatePattern(0x40000, 0x26)),
                TestContext.Current.CancellationToken);
            GeneralReplaceMappingViewModel mapping = Assert.Single(shell.Replace.GeneralReplaceMappings);
            mapping.TargetStartAddress = "0x3E020";
            mapping.Length = "0x2";
            await shell.WorkflowSession.SetSlotFileAsync(mapping.MappingId,
                workspace.Write("replacement.bin", [0xA5, 0x5A]),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(canBuild, shell.Replace.CanBuildReplace);
        var modal = new ReplaceSelectionModal { DataContext = shell.Replace };
        var modalHost = new ContentControl { Content = modal, DataContext = shell.Replace };
        using IDisposable visibility = modalHost.Bind(UserControl.IsVisibleProperty,
            new Binding(nameof(shell.Replace.IsReplaceSelectionModalOpen)));
        var outsideBefore = new Button { Content = "Before modal" };
        var outsideAfter = new Button { Content = "After modal" };
        var window = new Window
        {
            Width = 1000,
            Height = 800,
            Content = new StackPanel { Children = { outsideBefore, modalHost, outsideAfter } },
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.True(outsideBefore.Focus(NavigationMethod.Tab));
            shell.Replace.ShowReplaceSelectionCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Button[] actions = [.. modal.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Classes.Contains("semanticAction"))];
            Assert.Equal(3, actions.Length);
            Button topClose = actions[0];
            Button bottomClose = actions[1];
            Button build = actions[2];
            Assert.Same(shell.Replace.CloseReplaceSelectionCommand, topClose.Command);
            Assert.Same(topClose.Command, bottomClose.Command);
            Assert.Equal(canBuild, build.IsEnabled);
            Control[] order = canBuild
                ? [topClose, bottomClose, build]
                : [topClose, bottomClose];
            Assert.Same(bottomClose, window.FocusManager?.GetFocusedElement());
            PressTab(window, RawInputModifiers.None);
            Assert.Same(canBuild ? build : topClose, window.FocusManager?.GetFocusedElement());
            Assert.True(topClose.Focus(NavigationMethod.Tab));
            Assert.Same(topClose, window.FocusManager?.GetFocusedElement());

            for (int step = 1; step <= order.Length * 2; step++)
            {
                PressTab(window, RawInputModifiers.None);
                Assert.Same(order[step % order.Length], window.FocusManager?.GetFocusedElement());
            }

            for (int step = 1; step <= order.Length * 2; step++)
            {
                PressTab(window, RawInputModifiers.Shift);
                Assert.Same(order[(order.Length - (step % order.Length)) % order.Length],
                    window.FocusManager?.GetFocusedElement());
            }

            Assert.True(shell.Replace.IsReplaceSelectionModalOpen);
            foreach (Button close in new[] { topClose, bottomClose })
            {
                Assert.True(close.Focus(NavigationMethod.Tab));
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                Dispatcher.UIThread.RunJobs();
                Assert.False(shell.Replace.IsReplaceSelectionModalOpen);
                Assert.Same(outsideBefore, window.FocusManager?.GetFocusedElement());
                PressTab(window, RawInputModifiers.None);
                Assert.Same(outsideAfter, window.FocusManager?.GetFocusedElement());
                Assert.True(outsideBefore.Focus(NavigationMethod.Tab));
                shell.Replace.ShowReplaceSelectionCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(bottomClose, window.FocusManager?.GetFocusedElement());
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void PressTab(Window window, RawInputModifiers modifiers)
    {
        window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
        window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
    }
}
