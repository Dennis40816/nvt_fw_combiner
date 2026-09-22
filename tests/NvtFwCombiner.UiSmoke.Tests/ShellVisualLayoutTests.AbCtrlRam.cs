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
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Production AB output settings keep independent accessible editors inside the shared modal.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class AbCtrlRamVisualTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
    /// <summary>The compact bank viewport is keyboard operable and cannot change the write scope.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task AbCtrlRamBankViewportIsIndependentOfWriteScope(bool dark, bool chinese)
    {
        PresentationHostServices services = await Task.Run(() => PresentationTestHost.CreateServices("ui-smoke"), TestContext.Current.CancellationToken);
        MainWindowViewModel viewModel = PresentationTestHost.PublishCanonicalCatalog(services,
            ShellViewModelFactory.Create(services, chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English));
        ConfigureAbCtrlRamPage(viewModel);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        {
            DataContext = viewModel,
            Width = 1440,
            Height = 1000,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
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
            Assert.True(viewModel.Replace.IsCtrlRamBothBanksSelected);
            DropDownButton scope = Assert.Single(window.GetVisualDescendants().OfType<DropDownButton>(), item => item.Name == "CtrlRamBankScopeMenu");
            MenuFlyout choices = Assert.IsType<MenuFlyout>(scope.Flyout);
            Assert.True(scope.Focus());
            PressSpace(window);
            Assert.True(choices.IsOpen);
            Assert.Equal(3, choices.Items.Count);
            MenuItem aOnly = Assert.Single(choices.Items.OfType<MenuItem>(), item => Equals(item.CommandParameter, AbCtrlRamBankSelection.A));
            Assert.Same(viewModel.Replace.SelectCtrlRamBanksCommand, aOnly.Command);
            Assert.True(aOnly.Focus());
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            await viewModel.Replace.SelectCtrlRamBanksCommand.ExecutionTask!;
            choices.Hide();
            Assert.True(viewModel.Replace.IsCtrlRamBankASelected);
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, TestContext.Current.CancellationToken);
            await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, TestContext.Current.CancellationToken);
            Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
            Assert.False(viewModel.Replace.HasMemoryLayoutDisplayError);
            Assert.Equal("TPA Version", Assert.Single(viewModel.Replace.ReplaceBaseSlot.PrimaryFirmwareFacts).Label);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            ToggleButton bankView = Assert.Single(window.GetVisualDescendants().OfType<ToggleButton>(), item => item.Name == "CtrlRamBankViewSwitch");
            TextBlock subtitle = Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(), item => item.Name == "CtrlRamBankViewSubtitle");
            Assert.True(bankView.IsEffectivelyVisible);
            Border settings = Assert.Single(window.GetVisualDescendants().OfType<Border>(), item => item.Name == "CtrlRamModeSettings");
            Assert.True(settings.Bounds.Width > 1300);
            Assert.True(settings.TranslatePoint(default, window)!.Value.Y + settings.Bounds.Height < subtitle.TranslatePoint(default, window)!.Value.Y);
            Assert.False(bankView.IsChecked);
            Assert.InRange(bankView.Bounds.Width, 43.5, 44.5);
            Assert.InRange(bankView.Bounds.Height, 31.5, 32.5);
            double toggleCenter = bankView.TranslatePoint(default, window)!.Value.Y + (bankView.Bounds.Height / 2);
            double subtitleCenter = subtitle.TranslatePoint(default, window)!.Value.Y + (subtitle.Bounds.Height / 2);
            Assert.InRange(Math.Abs(toggleCenter - subtitleCenter), 0, 0.5);
            CtrlRamAuthoringDraftState? draft = viewModel.Replace.CurrentCtrlRamDraft;
            AuthoringRevision revision = viewModel.Replace.ReplaceInputAuthoringRevision;
            object? inspection = viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection;
            WorkflowInspectionAttemptState inspectionState = viewModel.Replace.Inspection.State;
            Assert.True(bankView.Focus());
            PressSpace(window);
            Assert.True(bankView.IsChecked);
            Assert.True(viewModel.Replace.IsViewingCtrlRamBankB);
            Assert.Equal("0x40000", viewModel.Replace.CtrlRamStartAddress);
            Assert.Equal("0x7FFFF", viewModel.Replace.CtrlRamEndAddress);
            Assert.Equal(viewModel.Text.FormatCtrlRamBankView(true, false), subtitle.Text);
            Assert.Same(draft, viewModel.Replace.CurrentCtrlRamDraft);
            Assert.Equal(revision, viewModel.Replace.ReplaceInputAuthoringRevision);
            Assert.Same(inspection, viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
            Assert.Equal(inspectionState, viewModel.Replace.Inspection.State);
            Assert.True(viewModel.Replace.IsCtrlRamBankASelected);
            Assert.True(viewModel.Replace.CanBuildReplace);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            string? output = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            if (output is not null)
            {
                _ = Directory.CreateDirectory(output);
                frame.Save(Path.Combine(output, $"ab-info-layout-a-only-1440x1000-{(dark ? "dark-zh" : "light-en")}.png"));
            }
            PressSpace(window);
            Assert.False(viewModel.Replace.IsViewingCtrlRamBankB);
            Assert.Equal("0x00000", viewModel.Replace.CtrlRamStartAddress);
            // General is authorable only on NT51926; exercise the real catalog transition.
            viewModel.WorkflowSession.SelectedIc = "NT51926";
            await viewModel.Replace.Inspection.ActiveTask;
            Assert.Contains(ExperienceIds.GeneralReplace, viewModel.Replace.ReplaceModeChoices);
            viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
            Dispatcher.UIThread.RunJobs();
            Assert.False(viewModel.Replace.HasCtrlRamBankView);
            Assert.Empty(viewModel.Replace.CtrlRamOverview);
            viewModel.Replace.IsViewingCtrlRamBankB = true;
            Assert.Empty(viewModel.Replace.CtrlRamOverview);
            viewModel.WorkflowSession.SelectedIc = "NT51929";
            viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
            await viewModel.Replace.SelectAbCtrlRamReferenceCommand.ExecuteAsync(null);
            await viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.Both);
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, TestContext.Current.CancellationToken);
            await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, TestContext.Current.CancellationToken);
            Assert.True(viewModel.Replace.HasCtrlRamBankView);
            Assert.Equal(["TPA Version", "TPB Version"], viewModel.Replace.ReplaceBaseSlot.PrimaryFirmwareFacts.Select(static fact => fact.Label));
            viewModel.Replace.IsViewingCtrlRamBankB = true;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using global::Avalonia.Media.Imaging.Bitmap? bothFrame = window.GetLastRenderedFrame();
            Assert.NotNull(bothFrame);
            if (output is not null)
            {
                bothFrame.Save(Path.Combine(output, $"ab-info-layout-1440x1000-{(dark ? "dark-zh" : "light-en")}.png"));
            }
            await viewModel.Replace.SelectStandardCtrlRamReferenceCommand.ExecuteAsync(null);
            Assert.False(viewModel.Replace.HasCtrlRamBankView);
            viewModel.WorkflowSession.SelectedIc = "NT51950";
            Dispatcher.UIThread.RunJobs();
            Assert.False(ab.IsEffectivelyEnabled);
            Assert.False(viewModel.Replace.HasCtrlRamBankView);
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
