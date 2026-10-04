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

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Completed-output actions retain keyboard navigation inside the completion surface.</summary>
public sealed class BuildCompletedModalTabTests
{
    /// <summary>Visible output actions and bottom actions cycle in both directions without reaching the background.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TabCyclesThroughVisibleCompletedActionsAndSkipsBackground(bool hasAdditionalOutput)
    {
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        string outputPath = Path.Combine(Path.GetTempPath(), "completed-tab", "output.bin");
        string additionalPath = Path.Combine(Path.GetTempPath(), "completed-tab", "a.bin");
        CompositionRunResult source = CreateHistoricalReplaceInspectionResult();
        CompositionRunResult completed = CloneRunResult(source, outputPath, source.Report,
            deliveryArtifacts: hasAdditionalOutput
                ? [new CompositionDeliveryArtifact("ab-a-flashcode", additionalPath, "a.bin",
                    0x40000, new ByteRange(0, 0x40000), "a-hash")]
                : [],
            isDeliveryComplete: true);
        var modal = new BuildCompletedModal { DataContext = viewModel };
        var modalHost = new ContentControl { Content = modal, DataContext = viewModel };
        using IDisposable visibility = modalHost.Bind(UserControl.IsVisibleProperty, new Binding("BuildResult.IsOpen"));
        var before = new Button { Content = "Background before" };
        var after = new Button { Content = "Background after" };
        var host = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(modalHost, 1);
        Grid.SetRow(after, 2);
        host.Children.Add(before);
        host.Children.Add(modalHost);
        host.Children.Add(after);
        var window = new Window { Width = 800, Height = 700, Content = host };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            Assert.True(before.Focus(NavigationMethod.Tab));
            Assert.True(viewModel.TryShowBuildCompleted(completed, build: true));
            Dispatcher.UIThread.RunJobs();
            Button[] buttons = [.. modal.GetVisualDescendants().OfType<Button>()];
            Assert.Equal(4, buttons.Length);
            Button output = Assert.Single(buttons, button => button.Classes.Contains("fileRevealAction") &&
                ReferenceEquals(button.Command, viewModel.BuildResult.RevealOutputCommand));
            Button additional = Assert.Single(buttons, button =>
                ReferenceEquals(button.Command, viewModel.RevealFileCommand));
            Button folder = Assert.Single(buttons, button => button != output &&
                ReferenceEquals(button.Command, viewModel.BuildResult.RevealOutputCommand));
            Button close = Assert.Single(buttons, button =>
                ReferenceEquals(button.Command, viewModel.BuildResult.CloseCommand));
            Assert.Equal("output.bin", output.Content);
            Assert.Equal(outputPath, output.Tag);
            Assert.Equal(viewModel.Text.BuildCompletedOpenFolderLabel, folder.Content);
            Assert.Equal(viewModel.Text.BuildCompletedOkLabel, close.Content);
            Assert.Equal(hasAdditionalOutput, additional.IsEffectivelyVisible);
            Assert.Equal(hasAdditionalOutput ? additionalPath : string.Empty, additional.CommandParameter);
            Assert.Equal(hasAdditionalOutput ? "a.bin" : string.Empty, additional.Content);

            Button[] order = hasAdditionalOutput ? [output, additional, folder, close] : [output, folder, close];
            Assert.Same(close, window.FocusManager?.GetFocusedElement());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(output, window.FocusManager?.GetFocusedElement());
            foreach (bool reverse in new[] { false, true })
            {
                for (int step = 1; step <= order.Length * 2; step++)
                {
                    RawInputModifiers modifiers = reverse ? RawInputModifiers.Shift : RawInputModifiers.None;
                    window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
                    window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
                    Dispatcher.UIThread.RunJobs();
                    int expectedIndex = reverse
                        ? (order.Length - (step % order.Length)) % order.Length
                        : step % order.Length;
                    Assert.Same(order[expectedIndex], window.FocusManager?.GetFocusedElement());
                    Assert.False(before.IsFocused);
                    Assert.False(after.IsFocused);
                    Assert.True(order[expectedIndex].IsEffectivelyVisible);
                }
            }

            Assert.True(viewModel.BuildResult.IsOpen);
            Assert.Equal(outputPath, viewModel.BuildResult.OutputPath);
            Assert.Equal(outputPath, viewModel.BuildResult.LatestCommittedOutputPath);
            Assert.Equal(hasAdditionalOutput, viewModel.BuildResult.HasAdditionalOutput);
            Assert.Equal(hasAdditionalOutput ? additionalPath : string.Empty, viewModel.BuildResult.AdditionalOutputPath);
            viewModel.BuildResult.CloseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(before, window.FocusManager?.GetFocusedElement());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(after, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }
}
