using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real Tab input stays in the active Settings surface in its existing visual order.</summary>
public sealed class SettingsModalTabTests
{
    /// <summary>Each section visits the header, visible rail and current page in both directions.</summary>
    [AvaloniaTheory]
    [InlineData("Overview")]
    [InlineData("Preferences")]
    [InlineData("EventBufferFormat")]
    [InlineData("Toolchain")]
    [InlineData("Version")]
    [InlineData("SupportMatrix")]
    public async Task TabAndShiftTabCycleThroughCurrentSection(string sectionName)
    {
        using TempWorkspace workspace = TempWorkspace.Create("settings-tab");
        IEventBufferFormatConfigurationSession session = await Task.Run(async () =>
        {
            CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
                null, workspace.Root, configurationPath: workspace.PathFor("config.json"));
            return await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);
        MainWindowViewModel vm = ToolchainSettingsTests.CreateToolchainViewModel(new ToolchainUiSession(), formatSession: session);
        var modal = new SettingsModal { DataContext = vm, IsVisible = false };
        var background = new Button { Content = "Settings trigger" };
        var otherBackground = new Button { Content = "Background action" };
        var window = new Window
        {
            Width = 1672,
            Height = 941,
            DataContext = vm,
            Content = new Grid { Children = { background, otherBackground, modal } },
        };
        foreach (string file in new[] { "SettingsVersionPageTemplate.axaml", "MainWindowPageTemplates.axaml" })
        {
            var source = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/Resources/{file}");
            window.Resources.MergedDictionaries.Add(new ResourceInclude(source) { Source = source });
        }
        foreach (string file in new[] { "MainWindowStyles.axaml", "MainWindowButtonStyles.axaml", "MainWindowVisualStyles.axaml" })
        {
            var source = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/Styles/{file}");
            window.Styles.Add(new StyleInclude(source) { Source = source });
        }
        try
        {
            window.Show();
            Assert.True(background.Focus());
            vm.OpenSettingsCommand.Execute(null);
            vm.Settings.SelectSectionCommand.Execute(Enum.Parse<SettingsSection>(sectionName));
            await vm.Settings.EventBufferFormatLoadTask;
            await vm.Settings.ToolchainLoadTask;
            modal.IsVisible = true;
            modal.IsOpen = true;
            UpdateLayout(window);

            Button close = modal.FindControl<Button>("CloseButton")!;
            Assert.Same(close, window.FocusManager!.GetFocusedElement());
            Border rail = Assert.Single(modal.GetVisualDescendants().OfType<Border>(), control => control.Name == "SettingsNavigationRail");
            Button[] navigation = [.. rail.GetVisualDescendants().OfType<Button>().Where(control => control.IsEffectivelyVisible)];
            SettingsSection[] sections = vm.Settings.IsConfigSelected
                ? [SettingsSection.Overview, SettingsSection.Preferences, SettingsSection.EventBufferFormat,
                    SettingsSection.EventBufferFormat, SettingsSection.Toolchain, SettingsSection.Version, SettingsSection.SupportMatrix]
                : [SettingsSection.Overview, SettingsSection.Preferences, SettingsSection.EventBufferFormat,
                    SettingsSection.Version, SettingsSection.SupportMatrix];
            Assert.Equal(sections, navigation.Select(control => Assert.IsType<SettingsSection>(control.CommandParameter)));
            Control[] page = PageControls(modal, vm, sectionName);
            Control[] expected = [close, .. navigation, .. page];
            Assert.All(expected, control =>
            {
                Assert.True(control.IsEffectivelyVisible);
                Assert.True(control.IsEffectivelyEnabled);
            });
            AssertCycle(window, modal, expected);
            Assert.True(background.IsEffectivelyEnabled);
            Assert.True(otherBackground.IsEffectivelyEnabled);
            Assert.False(background.IsFocused);
            Assert.False(otherBackground.IsFocused);
            foreach (bool reverse in new[] { false, true })
            {
                Assert.True(background.Focus());
                PressTab(window, reverse);
                Assert.Same(reverse ? expected[^1] : close, window.FocusManager.GetFocusedElement());
            }
            Assert.True(close.Focus());

            if (sectionName == "Preferences")
            {
                page[0].IsEnabled = false;
                page[^1].IsVisible = false;
                UpdateLayout(window);
                AssertCycle(window, modal, [close, .. navigation, .. page[1..^1]]);
            }
            if (sectionName == "EventBufferFormat")
            {
                EventBufferFormatDraftRowViewModel row = Assert.Single(vm.Settings.EventBufferFormatRows);
                foreach (bool reverse in new[] { false, true })
                {
                    Button remove = modal.GetVisualDescendants().OfType<Button>().First(control => control.Command == row.RemoveRecognitionValueCommand);
                    Assert.True(remove.Focus());
                    row.RemoveRecognitionValueCommand.Execute(remove.CommandParameter);
                    UpdateLayout(window);
                    Assert.Null(window.FocusManager.GetFocusedElement());
                    PressTab(window, reverse);
                    Assert.False(background.IsFocused);
                    Assert.False(otherBackground.IsFocused);
                    Button save = Assert.Single(modal.GetVisualDescendants().OfType<Button>(), control =>
                        control.Command == vm.Settings.SaveEventBufferFormatCommand);
                    Assert.True(save.IsEffectivelyEnabled);
                    Assert.Same(reverse ? save : close, window.FocusManager.GetFocusedElement());
                }
                Assert.True(close.Focus());
            }
            if (sectionName is "EventBufferFormat" or "Toolchain")
            {
                bool toolchain = sectionName == "Toolchain";
                if (toolchain)
                {
                    await vm.Settings.InspectToolchainPathAsync("C:/runtime/vcruntime140.dll");
                }
                vm.CloseSettingsCommand.Execute(null);
                UpdateLayout(window);
                Assert.False(close.IsEffectivelyEnabled);
                Assert.False(rail.IsEffectivelyEnabled);
                Assert.All(page.Where(control => control.GetVisualAncestors().Contains(modal)),
                    control => Assert.False(control.IsEffectivelyEnabled));
                Button keep = Assert.Single(modal.GetVisualDescendants().OfType<Button>(), control =>
                    control.Command == (toolchain ? vm.Settings.CancelToolchainCloseCommand : vm.Settings.CancelEventBufferFormatCloseCommand));
                Button discard = Assert.Single(modal.GetVisualDescendants().OfType<Button>(), control =>
                    control.Command == (toolchain ? vm.Settings.ConfirmToolchainCloseCommand : vm.Settings.ConfirmEventBufferFormatCloseCommand));
                foreach (bool reverse in new[] { false, true })
                {
                    Assert.True(background.Focus());
                    PressTab(window, reverse);
                    Assert.Same(reverse ? discard : keep, window.FocusManager.GetFocusedElement());
                }
                Assert.True(keep.Focus());
                AssertCycle(window, modal, [keep, discard]);
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                UpdateLayout(window);
                Assert.True(vm.IsSettingsModalOpen);
                Assert.True(rail.IsEffectivelyEnabled);
                Assert.Same(close, window.FocusManager.GetFocusedElement());
            }
            Assert.True(((Grid)window.Content!).Children.Remove(modal));
            Assert.True(background.Focus());
            PressTab(window, reverse: false);
            Assert.Same(otherBackground, window.FocusManager.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    private static Control[] PageControls(SettingsModal modal, MainWindowViewModel vm, string section)
    {
        Control[] visible = [.. modal.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible)];
        Button Action(System.Windows.Input.ICommand command)
        {
            return Assert.Single(visible.OfType<Button>(), control => control.Command == command);
        }
        switch (section)
        {
            case "Overview":
                Assert.Empty(visible.OfType<ComboBox>());
                return [];
            case "Preferences":
                Assert.Equal(2, visible.OfType<ComboBox>().Count());
                Assert.Equal(2, visible.OfType<ToggleSwitch>().Count());
                return [.. visible.OfType<ComboBox>(), .. visible.OfType<ToggleSwitch>()];
            case "EventBufferFormat":
                EventBufferFormatDraftRowViewModel row = Assert.Single(vm.Settings.EventBufferFormatRows);
                Assert.False(Action(vm.Settings.SaveEventBufferFormatCommand).IsEffectivelyEnabled);
                Assert.False(Action(vm.Settings.DiscardEventBufferFormatChangesCommand).IsEffectivelyEnabled);
                return [Action(vm.Settings.ReloadEventBufferFormatCommand), Assert.Single(visible.OfType<ComboBox>()),
                    Assert.Single(visible.OfType<TextBox>()),
                    .. visible.OfType<Button>().Where(control => control.Command == row.RemoveRecognitionValueCommand),
                    Action(row.BeginAddRecognitionValueCommand), Action(vm.Settings.RestoreEventBufferFormatDefaultsCommand)];
            case "Toolchain":
                Grid toolchain = Assert.Single(visible.OfType<Grid>(), control => control.Name == "ToolchainPageRoot");
                Assert.False(Action(vm.Settings.SaveToolchainCommand).IsEffectivelyEnabled);
                Assert.False(Action(vm.Settings.DiscardToolchainChangesCommand).IsEffectivelyEnabled);
                return [.. toolchain.GetVisualDescendants().OfType<RadioButton>(), Action(vm.Settings.DetectToolchainCommand),
                    Action(vm.Settings.BrowseToolchainCommand),
                    Assert.Single(toolchain.GetVisualDescendants().OfType<Button>(), control =>
                        control.Command == vm.Settings.SelectBundledToolchainCommand && control is not RadioButton)];
            case "Version":
                Assert.Empty(vm.Settings.VersionRows);
                Assert.False(vm.Settings.IsUpdateSourceEditing);
                return [Action(vm.Settings.RunVersionSelfTestCommand), Assert.Single(visible.OfType<TextBox>()),
                    Action(vm.Settings.BeginEditUpdateSourceCommand), Action(vm.Settings.CheckNowCommand)];
            case "SupportMatrix":
                Expander details = Assert.Single(visible.OfType<Expander>());
                return [.. visible.OfType<Border>().Where(control => control.DataContext is SupportMatrixCellViewModel),
                    Assert.Single(details.GetVisualDescendants().OfType<ToggleButton>())];
            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private static void AssertCycle(Window window, SettingsModal modal, Control[] expected)
    {
        Assert.Same(expected[0], window.FocusManager.GetFocusedElement());
        foreach (bool reverse in new[] { false, true })
        {
            for (int step = 1; step <= expected.Length * 2; step++)
            {
                PressTab(window, reverse);
                Control focused = Assert.IsType<Control>(window.FocusManager.GetFocusedElement(), exactMatch: false);
                Assert.Contains(modal, focused.GetVisualAncestors());
                int index = reverse ? (expected.Length - (step % expected.Length)) % expected.Length : step % expected.Length;
                Assert.True(ReferenceEquals(expected[index], focused),
                    $"{(reverse ? "Shift+Tab" : "Tab")} step {step}: expected {expected[index].GetType().Name} {expected[index].Name}, got {focused.GetType().Name} {focused.Name}");
            }
        }
    }

    private static void PressTab(Window window, bool reverse)
    {
        RawInputModifiers modifiers = reverse ? RawInputModifiers.Shift : RawInputModifiers.None;
        window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
        window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
        UpdateLayout(window);
    }

    private static void UpdateLayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
