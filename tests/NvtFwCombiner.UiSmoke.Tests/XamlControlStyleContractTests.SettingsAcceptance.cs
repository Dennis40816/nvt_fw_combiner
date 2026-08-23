using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>The approved Preferences surface fits the minimum window in Traditional Chinese.</summary>
    [AvaloniaFact]
    public async Task SettingsPreferencesFitsMinimumTraditionalChineseWindow()
    {
        foreach ((ThemeVariant Theme, string Name) variant in new[]
        {
            (ThemeVariant.Light, "light"),
            (ThemeVariant.Dark, "dark"),
        })
        {
            MainWindowViewModel viewModel = await Task.Run(
                () => PresentationTestHost.CreateViewModel(ShellLanguage.ChineseTraditional),
                TestContext.Current.CancellationToken);
            viewModel.Settings.Refresh(viewModel.Text);
            viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.Preferences);
            var modal = new SettingsModal
            {
                DataContext = viewModel,
                IsOpen = true,
            };
            var window = new Window
            {
                Width = 980,
                Height = 640,
                RequestedThemeVariant = variant.Theme,
                Content = modal,
            };
            AddSettingsAcceptanceResources(window);
            try
            {
                window.Show();
                window.Measure(new Size(980, 640));
                window.Arrange(new Rect(0, 0, 980, 640));
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Border surface = Assert.IsType<Border>(modal.FindControl<Control>("SettingsSurface"));
                Border rail = Assert.Single(
                    modal.GetVisualDescendants().OfType<Border>(),
                    control => control.Name == "SettingsNavigationRail");
                ScrollViewer content = Assert.Single(
                    modal.GetVisualDescendants().OfType<ScrollViewer>(),
                    control => control.Name == "SettingsContentViewport");
                Point origin = Assert.IsType<Point>(surface.TranslatePoint(new Point(), modal));
                Assert.True(origin.X >= 0 && origin.Y >= 0);
                Assert.True(origin.X + surface.Bounds.Width <= 980);
                Assert.True(origin.Y + surface.Bounds.Height <= 640);
                Assert.InRange(rail.Bounds.Width, 317.5, 318.5);
                Assert.True(content.Bounds.Width > 480);
                Assert.Contains(
                    modal.GetVisualDescendants().OfType<TextBlock>(),
                    text => text.IsVisible && text.Text == viewModel.Text.SettingsPreferencesTitle);

                RadioButton selected = Assert.Single(
                    modal.GetVisualDescendants().OfType<RadioButton>(),
                    button => button.IsVisible && button.Classes.Contains("selected"));
                Assert.Equal(viewModel.Text.SettingsPreferencesNavigationLabel, selected.GetValue(
                    Avalonia.Automation.AutomationProperties.NameProperty));
                Assert.All(
                    modal.GetVisualDescendants().OfType<ComboBox>().Where(static control => control.IsVisible),
                    control => AssertControlFits(control, content));
                ToggleSwitch toggle = Assert.Single(
                    modal.GetVisualDescendants().OfType<ToggleSwitch>(),
                    control => control.IsVisible);
                AssertControlFits(toggle, content);

                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    _ = Directory.CreateDirectory(outputDirectory);
                    await using FileStream output = File.Create(Path.Combine(
                        outputDirectory,
                        $"settings-preferences-compact-980x640-zh-tw-{variant.Name}.png"));
                    frame.Save(output);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    private static void AddSettingsAcceptanceResources(Window window)
    {
        foreach (Uri source in new[]
        {
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/SettingsVersionPageTemplate.axaml"),
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml"),
        })
        {
            window.Resources.MergedDictionaries.Add(new ResourceInclude(source) { Source = source });
        }

        foreach (Uri source in new[]
        {
            ProductionMainWindowStylesUri,
            ProductionButtonStylesUri,
            ProductionVisualStylesUri,
        })
        {
            window.Styles.Add(new StyleInclude(source) { Source = source });
        }
    }
}
