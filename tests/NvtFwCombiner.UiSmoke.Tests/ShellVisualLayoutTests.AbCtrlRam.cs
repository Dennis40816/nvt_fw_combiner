using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Production AB output settings keep independent accessible editors inside the shared modal.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class AbCtrlRamVisualTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
    /// <summary>Repeated keyboard clicks keep one selected Reference and bank in the real page.</summary>
    [AvaloniaFact]
    public async Task AbCtrlRamSelectedSegmentsStayCheckedOnRepeatedClicks()
    {
        PresentationHostServices services = await Task.Run(() => PresentationTestHost.CreateServices("ui-smoke"), TestContext.Current.CancellationToken);
        MainWindowViewModel viewModel = PresentationTestHost.PublishCanonicalCatalog(services,
            ShellViewModelFactory.Create(services, ShellLanguage.English));
        ConfigureAbCtrlRamPage(viewModel);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        {
            DataContext = viewModel,
            Width = 1440,
            Height = 1000,
        };
        try
        {
            window.Show();
            window.Measure(new Size(1440, 1000));
            window.Arrange(new Rect(0, 0, 1440, 1000));
            Dispatcher.UIThread.RunJobs();
            ToggleButton ab = Assert.Single(window.GetVisualDescendants().OfType<ToggleButton>(), static item => item.Name == "CtrlRamAbReferenceChoice");
            Assert.True(ab.Focus());
            PressSpace(window);
            Assert.True(viewModel.Replace.IsAbCtrlRamReference);
            Assert.True(ab.IsChecked);
            PressSpace(window);
            Assert.True(ab.IsChecked);
            ToggleButton both = Assert.Single(window.GetVisualDescendants().OfType<ToggleButton>(), item =>
                ReferenceEquals(item.Command, viewModel.Replace.SelectCtrlRamBanksCommand) &&
                Equals(item.CommandParameter, AbCtrlRamBankSelection.Both));
            Assert.True(both.IsChecked);
            Assert.True(both.Focus());
            PressSpace(window);
            Assert.True(both.IsChecked);
            Assert.True(viewModel.Replace.IsCtrlRamBothBanksSelected);
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, TestContext.Current.CancellationToken);
            await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, TestContext.Current.CancellationToken);
            Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
            Assert.False(viewModel.Replace.HasMemoryLayoutDisplayError);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            string? output = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            if (output is not null)
            {
                _ = Directory.CreateDirectory(output);
                frame.Save(Path.Combine(output, "ab-ctrlram-page-1440x1000-light-en.png"));
            }
            viewModel.WorkflowSession.SelectedIc = "NT51950";
            Dispatcher.UIThread.RunJobs();
            Assert.False(ab.IsEffectivelyEnabled);
        }
        finally
        {
            await ReportControlTestHost.CloseAndFlushAsync(window);
        }
    }

    private static void PressSpace(Window window)
    {
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Both banks render in order with the same geometry, focus behavior and fixed footer.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AbCtrlRamEditorsRenderInBothThemesAndLanguages(bool dark, bool chinese)
    {
        MainWindowViewModel viewModel = await Task.Run(async () =>
        {
            MainWindowViewModel ready = await CreateAbCtrlRamReadyAsync(AbCtrlRamBankSelection.Both);
            if (chinese)
            {
                ready.SelectedLanguage = "Traditional Chinese";
            }
            Assert.True(await ready.Replace.RequestCtrlRamBuildSettingsAsync());
            foreach (CtrlRamFirmwareVersionEditorViewModel editor in ready.Replace.AbCtrlRamVersionEditors)
            {
                editor.EditCommand.Execute(null);
            }
            return ready;
        }, TestContext.Current.CancellationToken);
        var modal = new OutputDeliveryConfirmationModal { DataContext = viewModel.OutputDelivery, IsOpen = true };
        var window = new Window
        {
            Width = 980,
            Height = 720,
            Content = modal,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        foreach (string style in new[] { "MainWindowStyles", "MainWindowButtonStyles", "MainWindowVisualStyles" })
        {
            var uri = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/Styles/{style}.axaml");
            window.Styles.Add(new StyleInclude(uri) { Source = uri });
        }
        try
        {
            window.Show();
            window.Measure(new Size(980, 720));
            window.Arrange(new Rect(0, 0, 980, 720));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Border surface = modal.FindControl<Border>("BuildSettingsSurface")!;
            ItemsControl bankList = modal.FindControl<ItemsControl>("AbCtrlRamVersionEditors")!;
            Grid[] rows = [.. bankList.GetVisualDescendants().OfType<Grid>().Where(static grid => grid.Classes.Contains("abVersionEditor"))];
            Assert.Equal(2, rows.Length);
            Assert.InRange(surface.Bounds.Width, 759.5, 760.5);
            Assert.True(surface.Bounds.Height <= 720);
            Assert.True(rows.All(static row => row.Bounds.Height > 70));
            Point a = rows[0].TranslatePoint(new Point(), bankList)!.Value;
            Point b = rows[1].TranslatePoint(new Point(), bankList)!.Value;
            Assert.True(b.Y >= a.Y + rows[0].Bounds.Height);
            Assert.InRange(Math.Abs(rows[0].Bounds.Width - rows[1].Bounds.Width), 0, 0.5);
            TextBox[] fields = [.. bankList.GetVisualDescendants().OfType<TextBox>()];
            Assert.Equal(4, fields.Length);
            Assert.All(fields, static field => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(field))));
            Assert.True(fields[0].Focus());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(fields[1], window.FocusManager!.GetFocusedElement());
            fields[0].Text = "2A";
            fields[1].Text = "0C";
            Assert.Equal("2A", viewModel.Replace.AbCtrlRamVersionEditors[0].VersionText);
            Assert.NotEqual("2A", viewModel.Replace.AbCtrlRamVersionEditors[1].VersionText);
            Button confirm = modal.FindControl<Button>("ConfirmButton")!;
            Point footer = confirm.TranslatePoint(new Point(), surface)!.Value;
            Assert.InRange(footer.Y, 0, surface.Bounds.Height - confirm.Bounds.Height);
            Assert.True(confirm.IsEffectivelyVisible);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? output = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (output is not null)
            {
                _ = Directory.CreateDirectory(output);
                frame.Save(Path.Combine(output, $"ab-ctrlram-980x720-{(dark ? "dark" : "light")}-{(chinese ? "zh-tw" : "en")}.png"));
            }
        }
        finally
        {
            window.Close();
        }
    }
}
