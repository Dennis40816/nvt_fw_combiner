using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Full-page renders preserve aligned controls, inline failure and safe modal focus.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, 1672, 941)]
    [InlineData(true, false, 1672, 941)]
    [InlineData(false, true, 980, 640)]
    [InlineData(true, true, 980, 640)]
    public async Task ToolchainReferenceGeometryAndInvalidStateStayOnOnePage(bool dark, bool chinese, double width, double height)
    {
        var session = new ToolchainUiSession();
        MainWindowViewModel vm = ToolchainSettingsTests.CreateToolchainViewModel(session,
            chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        vm.OpenSettingsCommand.Execute(null);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        var modal = new SettingsModal { DataContext = vm, IsOpen = true };
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = modal,
            DataContext = vm,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        AddSettingsAcceptanceResources(window);
        try
        {
            window.Show();
            RenderSettingsVersion(window);
            CaptureToolchainFrame(window, $"normal-{width}x{height}-{dark}-{chinese}");
            Grid page = Assert.Single(window.GetVisualDescendants().OfType<Grid>(), control => control.Name == "ToolchainPageRoot");
            Border rail = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "SettingsNavigationRail");
            Assert.InRange(rail.Bounds.Width, 317.5, 318.5);
            Rect bounds = EventBufferBounds(page, window);
            Assert.InRange(bounds.Left - EventBufferBounds(rail, window).Right, 35.5, 36.5);
            Button detect = Assert.Single(page.GetVisualDescendants().OfType<Button>(), control => control.Name == "ToolchainDetectButton");
            Button browse = Assert.Single(page.GetVisualDescendants().OfType<Button>(), control => control.Name == "ToolchainBrowseButton");
            Assert.InRange(Math.Abs(EventBufferBounds(detect, window).Top - EventBufferBounds(browse, window).Top), 0, 0.5);
            Assert.InRange(bounds.Right - EventBufferBounds(browse, window).Right, 0, 6);
            foreach (Button button in page.GetVisualDescendants().OfType<Button>().Where(control =>
                control.IsEffectivelyVisible && (control.Classes.Contains("semanticAction") || control is RadioButton)))
            {
                AssertEventBufferFits(EventBufferBounds(button, window), bounds);
            }
            Assert.Equal(2, page.GetVisualDescendants().OfType<RadioButton>().Count());
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(browse)));
            session.RejectInspection = true;
            await vm.Settings.InspectToolchainPathAsync("C:/runtime/untrusted.dll");
            RenderSettingsVersion(window);
            StackPanel issue = Assert.Single(page.GetVisualDescendants().OfType<StackPanel>(), control => control.Name == "ToolchainInlineIssue");
            Assert.True(issue.IsEffectivelyVisible);
            Assert.True(vm.Settings.IsUserToolchainSelected);
            Assert.Equal(vm.Text.ToolchainFailedLabel, vm.Settings.ToolchainVerification);
            Assert.Equal(2, page.GetVisualDescendants().OfType<RadioButton>().Count());
            CaptureToolchainFrame(window, $"invalid-{width}x{height}-{dark}-{chinese}");
            vm.CloseSettingsCommand.Execute(null);
            RenderSettingsVersion(window);
            Button keep = Assert.Single(page.GetVisualDescendants().OfType<Button>(), control => control.Command == vm.Settings.CancelToolchainCloseCommand);
            Assert.Same(keep, window.FocusManager?.GetFocusedElement());
            Assert.False(rail.IsEffectivelyEnabled);
            Assert.False(detect.IsEffectivelyEnabled);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            RenderSettingsVersion(window);
            Assert.False(vm.Settings.IsToolchainCloseConfirmationOpen);
            Assert.True(vm.IsSettingsModalOpen);
            Assert.True(vm.Settings.HasToolchainUnsavedChanges);
        }
        finally { window.Close(); }
    }

    private static void CaptureToolchainFrame(Window window, string state)
    {
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR") is { Length: > 0 } directory)
        {
            _ = Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory, $"toolchain-{state}.png"));
        }
    }
}
