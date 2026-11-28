using System.Diagnostics;
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
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Production AB output settings keep independent accessible editors inside the shared modal.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class AbCtrlRamVisualTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
    /// <summary>The bank viewport changes only display facts and leaves the selected write scope intact.</summary>
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
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, TestContext.Current.CancellationToken);
            await viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.A);
            await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
            Assert.NotEqual(OutputLayoutNoticeKind.Unavailable, viewModel.Replace.OutputLayout.Current.Panel?.Notice?.Kind);
            Assert.Equal(["TPA Version", "TPB Version"], viewModel.Replace.ReplaceBaseSlot.PrimaryFirmwareFacts.Take(2).Select(static fact => fact.Label));
            Assert.Equal("AB FlashCode", viewModel.Replace.ReplaceBaseSlot.DetectedBaseTypeLabel);
            OutputLayoutPanel panel = Assert.Single(window.GetVisualDescendants().OfType<OutputLayoutPanel>(),
                static candidate => candidate.IsEffectivelyVisible);
            OutputLayoutPanelModel a = Assert.IsType<OutputLayoutPanelModel>(viewModel.Replace.OutputLayout.Current.Panel);
            Assert.Same(a, panel.Model);
            Assert.NotNull(a.Bank);
            Assert.False(a.Bank.IsViewingBankB);
            Assert.Equal("0x00000", a.StartAddress);
            Assert.Equal("0x3FFFF", a.EndAddress);
            Assert.NotEmpty(a.LaneLinks);
            Assert.All(a.LaneLinks, link => Assert.Equal(chinese ? "主 IC" : "Master", link.Lane.Title));
            Assert.Contains(a.Segments, static segment => segment.SourceSlotId == "replace-ctrlram-nf" &&
                segment.SourceLabel == "NF CtrlRAM");
            long[] aStarts = [.. a.LaneLinks.Select(static link => link.Lane.Start)];
            CtrlRamAuthoringDraftState? draft = viewModel.Replace.CurrentCtrlRamDraft;
            AuthoringRevision revision = viewModel.Replace.ReplaceInputAuthoringRevision;
            object? inspection = viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection;
            WorkflowInspectionAttemptState inspectionState = viewModel.Replace.Inspection.State;
            a.Bank.SelectBankCommand.Execute(true);
            Dispatcher.UIThread.RunJobs();
            OutputLayoutPanelModel b = Assert.IsType<OutputLayoutPanelModel>(viewModel.Replace.OutputLayout.Current.Panel);
            Assert.Same(b, panel.Model);
            Assert.True(b.Bank!.IsViewingBankB);
            Assert.Equal("0x40000", b.StartAddress);
            Assert.Equal("0x7FFFF", b.EndAddress);
            Assert.Equal(viewModel.Text.FormatCtrlRamBankView(true, false), b.Bank.BankBLabel);
            Assert.Equal(aStarts.Select(static start => start + 0x40000), b.LaneLinks.Select(static link => link.Lane.Start));
            Assert.All(b.LaneLinks.SelectMany(static link => link.Lane.Ranges),
                static range => Assert.False(range.IsSelectedForWrite));
            Assert.Same(draft, viewModel.Replace.CurrentCtrlRamDraft);
            Assert.Equal(revision, viewModel.Replace.ReplaceInputAuthoringRevision);
            Assert.Same(inspection, viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
            Assert.Equal(inspectionState, viewModel.Replace.Inspection.State);
            Assert.True(viewModel.Replace.IsCtrlRamBankASelected);
            Assert.True(viewModel.Replace.CanBuildReplace);
            Assert.DoesNotContain(window.GetVisualDescendants(), static control =>
                control.Name is "MemorySliceCard" or "MemoryLocalView");
            b.Bank.SelectBankCommand.Execute(false);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("0x00000", Assert.IsType<OutputLayoutPanelModel>(viewModel.Replace.OutputLayout.Current.Panel).StartAddress);
            viewModel.WorkflowSession.SelectedIc = "NT51926";
            await viewModel.Replace.Inspection.ActiveTask;
            Assert.Contains(ExperienceIds.GeneralReplace, viewModel.Replace.ReplaceModeChoices);
            viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
            Assert.False(viewModel.Replace.HasCtrlRamBankView);
            Assert.Null(Assert.IsType<OutputLayoutPanelModel>(viewModel.Replace.OutputLayout.Current.Panel).Bank);
            viewModel.WorkflowSession.SelectedIc = "NT51929";
            viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, TestContext.Current.CancellationToken);
            await viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.Both);
            await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, TestContext.Current.CancellationToken);
            Assert.True(viewModel.Replace.HasCtrlRamBankView);
            viewModel.Replace.IsViewingCtrlRamBankB = true;
            Assert.Equal(["TPA Version", "TPB Version"], viewModel.Replace.ReplaceBaseSlot.PrimaryFirmwareFacts.Take(2).Select(static fact => fact.Label));
            viewModel.Replace.ReplaceBaseSlot.IsAdditionalFirmwareFactsExpanded = true;
            Assert.Contains(viewModel.Replace.ReplaceBaseSlot.AdditionalFirmwareFacts, static fact => fact.Label == "DPA Version");
            Assert.Contains(viewModel.Replace.ReplaceBaseSlot.AdditionalFirmwareFacts, static fact => fact.Label == "DPB Version");
            Assert.Contains(viewModel.Replace.ReplaceBaseSlot.AdditionalFirmwareFacts, static fact => fact.Label == "IC Count (A/B)");
            await viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.B);
            viewModel.Replace.IsViewingCtrlRamBankB = false;
            Assert.All(viewModel.Replace.CtrlRamFocusLanes.SelectMany(static lane => lane.Ranges),
                static range => Assert.False(range.IsSelectedForWrite));
            viewModel.Replace.IsViewingCtrlRamBankB = true;
            viewModel.WorkflowSession.SelectedIc = "NT51950";
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase,
                CanonicalGoldenTestData.ArtifactPath("ab-merge", "NT51950", "expected-output", "boe-d82t80"),
                TestContext.Current.CancellationToken);
            await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf",
                CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(
                    CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51950-fw200-single-auto-prj-676-20260717"),
                    "postbuild-nf-ctrlram")), TestContext.Current.CancellationToken);
            Assert.NotEqual(OutputLayoutNoticeKind.Unavailable, viewModel.Replace.OutputLayout.Current.Panel?.Notice?.Kind);
            Assert.True(viewModel.Replace.IsAbCtrlRamReference);
            Assert.False(viewModel.Replace.HasCtrlRamBankView);
            Assert.False(viewModel.Replace.IsViewingCtrlRamBankB);
            OutputLayoutPanelModel full = Assert.IsType<OutputLayoutPanelModel>(viewModel.Replace.OutputLayout.Current.Panel);
            Assert.Null(full.Bank);
            Assert.Equal("0x00000", full.StartAddress);
            Assert.Equal("0x7FFFF", full.EndAddress);
            Assert.Contains(full.LaneLinks, static link => link.Lane.EndExclusive <= 0x40000);
            Assert.Contains(full.LaneLinks, static link => link.Lane.Start >= 0x40000);
            viewModel.Replace.IsViewingCtrlRamBankB = true;
            Assert.False(viewModel.Replace.IsViewingCtrlRamBankB);
            await viewModel.WorkflowSession.ClearSlotFileCommand.ExecuteAsync(CompositionSlotIds.ReplaceBase);
            Assert.False(viewModel.Replace.HasCtrlRamBankView);
            Assert.Null(Assert.IsType<OutputLayoutPanelModel>(viewModel.Replace.OutputLayout.Current.Panel).Bank);
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
                frame.SavePng(Path.Combine(output, $"ab-ctrlram-980x720-{(dark ? "dark" : "light")}-{(chinese ? "zh-tw" : "en")}.png"));
            }
        }
        finally
        {
            window.Close();
        }
    }
}
