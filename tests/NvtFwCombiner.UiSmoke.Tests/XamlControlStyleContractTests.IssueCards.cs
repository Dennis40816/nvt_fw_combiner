using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>The whole disabled Build target shares the input explanation in the approved status hierarchy and disappears after correction.</summary>
    [AvaloniaFact]
    public async Task DisabledBuildCardCoversWholeButtonAndClearsAfterInputCorrection()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        System.Text.Json.JsonElement goldenCase = golden.CaseByIc("51929");
        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input"));
        using var workspace = TempWorkspace.Create("v114-issue-card-build");
        string shortPath = workspace.PathFor("tp-short.bin");
        File.WriteAllBytes(shortPath, File.ReadAllBytes(tpPath)[..0x3FFFF]);
        PresentationHostServices services = await Task.Run(() => PresentationTestHost.CreateServices("ui-smoke"), TestContext.Current.CancellationToken);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.True((await PresentationTestHost.LoadCanonicalCatalogAsync(services.CanonicalCatalogLoader, TestContext.Current.CancellationToken)).Succeeded);
        viewModel.PublishCanonicalCatalogState();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeDp, dpPath, TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeTp, shortPath, TestContext.Current.CancellationToken);
        Assert.True(viewModel.HasMergeBuildBlocker);
        Assert.Equal(viewModel.Merge.MergeTpSlot.IssueCard!.Summary, viewModel.MergeBuildBlockerCard.Summary);
        Assert.Equal(viewModel.Merge.MergeTpSlot.IssueCard.DiagnosticCode, viewModel.MergeBuildBlockerCard.DiagnosticCode);
        Assert.True(viewModel.MergeBuildBlockerCard.IsBuildStatus);
        Assert.Equal("Build status", viewModel.MergeBuildBlockerCard.Caption);
        Assert.Equal("Build blocked", viewModel.MergeBuildBlockerCard.Subject);
        Assert.False(viewModel.MergeBuildBlockerCard.HasAdditionalBlockers);
        Assert.Contains("≥ 256 KiB", viewModel.MergeBuildBlockerCard.Summary, StringComparison.Ordinal);
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Width = 1710;
        window.Height = 896; // Approved 1710x920 PNG excluding its 24px native Windows title bar.
        try
        {
            Dispatcher.UIThread.RunJobs();
            FirmwareSlotCard tpSlot = Assert.Single(window.GetVisualDescendants().OfType<FirmwareSlotCard>(),
                item => ReferenceEquals(item.DataContext, viewModel.Merge.MergeTpSlot));
            ToggleButton tpBadge = Assert.Single(tpSlot.GetVisualDescendants().OfType<ToggleButton>(),
                item => item.Classes.Contains("slotStateAction"));
            Assert.True(tpBadge.Focus(NavigationMethod.Tab));
            await SaveReferenceFrameAsync(window, "error");
            Border target = window.FindControl<Border>("MergeBuildBlockerTarget")!;
            Button build = window.FindControl<Button>("MergeBuildButton")!;
            Assert.False(build.IsEnabled);
            Assert.True(target.IsEffectivelyVisible);
            Assert.Equal(build.Bounds.Size, target.Bounds.Size);
            ToolTip.SetShowDelay(target, 0);
            Point center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ToolTip.GetIsOpen(target));
            Assert.True(target.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            AssertIssueTooltipGeometry(window, target, above: true);
            ToolTip tooltip = Assert.IsType<ToolTip>(ToolTip.GetTip(target));
            _ = Assert.Single(tooltip.GetVisualDescendants().OfType<IssueDetailsCard>());
            Assert.False(tooltip.IsHitTestVisible);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Dispatcher.UIThread.RunJobs();
            Assert.False(ToolTip.GetIsOpen(target));
            string shortDp = workspace.PathFor("dp-short.bin");
            File.WriteAllBytes(shortDp, File.ReadAllBytes(dpPath)[..0x5FFF]);
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeDp, shortDp, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, viewModel.Merge.BuildAvailability.Blockers.Count);
            Assert.Equal(1, viewModel.MergeBuildBlockerCard.AdditionalBlockerCount);
            Assert.Contains("256 KiB", viewModel.MergeBuildBlockerCard.AdditionalBlockerText, StringComparison.Ordinal);
            _ = tpBadge.Focus();
            Assert.True(target.Focus(NavigationMethod.Tab));
            Assert.True(ToolTip.GetIsOpen(target));
            await SaveReferenceFrameAsync(window, "build");
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeDp, dpPath, TestContext.Current.CancellationToken);
            Assert.False(viewModel.MergeBuildBlockerCard.HasAdditionalBlockers);
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeTp, tpPath, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.True(build.IsEnabled);
            Assert.False(target.IsVisible);
            Assert.False(ToolTip.GetIsOpen(target));
            Assert.Null(viewModel.Merge.MergeTpSlot.IssueCard);
            Assert.Empty(viewModel.Reports.ReportHistoryEntries);
            Assert.False(viewModel.OutputDelivery.IsOpen);
            string uniformTp = workspace.PathFor("tp-uniform.bin");
            byte[] uniformBytes = File.ReadAllBytes(tpPath);
            uniformBytes.AsSpan(0x7000).Fill(0xFF); // This certified NT51929 fixture's declared TP source range.
            File.WriteAllBytes(uniformTp, uniformBytes);
            await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeTp, uniformTp, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.True(build.IsEnabled);
            Assert.Equal("TP region contains only 0xFF", viewModel.Merge.MergeTpSlot.IssueCard!.Summary);
            Assert.True(tpBadge.Focus(NavigationMethod.Tab));
            await SaveReferenceFrameAsync(window, "warning");
            string? captureDirectory = Environment.GetEnvironmentVariable("NFC_UI_REFERENCE_CAPTURE_DIR");
            if (!string.IsNullOrWhiteSpace(captureDirectory) && File.Exists(Path.Combine(captureDirectory, "actual-failed-run-report.json")))
            {
                ToolTip.SetIsOpen(tpBadge, false);
                viewModel.Reports.LoadReportJson(File.ReadAllText(Path.Combine(captureDirectory, "actual-failed-run-report.json")), "actual-failed-run-report.json");
                viewModel.Reports.ShowReportCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                TabControl tabs = Assert.Single(window.GetVisualDescendants().OfType<TabControl>(), item => item.Classes.Contains("reportTabs"));
                tabs.SelectedItem = Assert.Single(tabs.Items.OfType<TabItem>(), item => Equals(item.Header, viewModel.Text.ReportTabIssues));
                Assert.Equal("input.artifact.read-failed", viewModel.Reports.LoadedReport.PrimaryIssue.Title);
                await SaveReferenceFrameAsync(window, "report");
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task SaveReferenceFrameAsync(Window window, string state)
    {
        string? destination = Environment.GetEnvironmentVariable("NFC_UI_REFERENCE_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }
        string root = Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT")!;
        Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase);
        await Task.Delay(300, TestContext.Current.CancellationToken); // Allow the production popup fade to settle before retaining visual evidence.
        Dispatcher.UIThread.RunJobs();
        using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        _ = Directory.CreateDirectory(destination);
        frame.Save(Path.Combine(destination, $"{state}-production-render.png"));
    }

    /// <summary>Issue badges use the IC-details card hierarchy without changing slot geometry or readiness.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, false, 1362)]
    [InlineData(true, true, false, 1362)]
    [InlineData(false, true, true, 620)]
    [InlineData(true, false, true, 620)]
    public void IssueBadgeUsesCompactCardOnKeyboardFocus(bool dark, bool chinese, bool error, double width)
    {
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(
            error ? StandardMergeFeedbackTests.Status("input.source-view.incomplete", AuthoringSlotLifecycle.Error, 262143, 262144)
                : StandardMergeFeedbackTests.Status("TP_UNIFORM_CONTENT_WARNING", AuthoringSlotLifecycle.Warning), chinese);
        var card = new FirmwareSlotCard { Width = width, DataContext = slot, BrowseLabel = "Browse", ClearSelectionLabel = "Clear" };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Styles.Insert(0, new Avalonia.Themes.Fluent.FluentTheme());
        host.Resources.MergedDictionaries.Add(new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://NvtFwCombiner.Presentation.Avalonia/"))
        {
            Source = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/ThemeTokens.axaml"),
        });
        host.Width = width + 50;
        host.Height = 900;
        host.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        host.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            ToggleButton badge = Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>(),
                control => control.Classes.Contains("slotStateAction"));
            Rect before = card.Bounds;
            Assert.True(badge.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            Assert.True(ToolTip.GetIsOpen(badge));
            Assert.Null(badge.FocusAdorner);
            ContentPresenter badgeSurface = Assert.Single(badge.GetVisualDescendants().OfType<ContentPresenter>(),
                control => control.Name == "PART_ContentPresenter");
            Assert.DoesNotContain(host.GetVisualDescendants().OfType<Control>(), control =>
                AdornerLayer.GetAdornedElement(control) is { } target &&
                (ReferenceEquals(target, badge) || badge.GetVisualDescendants().Contains(target)));
            Assert.Contains(":focus-visible", badge.Classes);
            Assert.Equal(new Thickness(2), badgeSurface.BorderThickness);
            Assert.True(badgeSurface.CornerRadius.TopLeft >= badgeSurface.Bounds.Height / 2,
                "Keyboard focus must retain the existing pill-shaped border.");
            Assert.True(host.TryGetResource("NfcAccentStrongBrush", host.ActualThemeVariant, out object? focusBrush));
            Assert.Equal(Assert.IsType<Avalonia.Media.ISolidColorBrush>(focusBrush, exactMatch: false).Color,
                Assert.IsType<Avalonia.Media.ISolidColorBrush>(badgeSurface.BorderBrush, exactMatch: false).Color);
            ToolTip tooltip = Assert.IsType<ToolTip>(ToolTip.GetTip(badge));
            Assert.False(tooltip.IsHitTestVisible);
            Assert.Equal(default, tooltip.Padding);
            Assert.Equal(default, tooltip.BorderThickness);
            Assert.Equal(Avalonia.Media.Colors.Transparent, Assert.IsType<Avalonia.Media.ISolidColorBrush>(tooltip.Background, exactMatch: false).Color);
            Border surface = Assert.Single(tooltip.GetVisualDescendants().OfType<Border>(),
                control => control.Classes.Contains("icDetailCard"));
            Assert.InRange(surface.Bounds.Width, 200, 360);
            Assert.InRange(surface.Bounds.Height, 60, 260);
            Point surfaceOrigin = surface.TranslatePoint(default, tooltip)!.Value;
            Assert.True(surfaceOrigin.X >= 0 && surfaceOrigin.X + surface.Bounds.Width <= tooltip.Bounds.Width,
                "The native tooltip must not clip either vertical card border.");
            Assert.Equal(before, card.Bounds);
            Assert.Equal(error, slot.BlocksBuild);
            Assert.Contains(tooltip.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == "TP BIN");
            Assert.All(tooltip.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible),
                block => Assert.All(block.TextLayout.TextLines, line => Assert.False(line.HasCollapsed)));
            Assert.Empty(tooltip.GetVisualDescendants().OfType<Button>());
            host.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            host.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Dispatcher.UIThread.RunJobs();
            Assert.False(ToolTip.GetIsOpen(badge));
            Assert.True(badge.IsFocused);
            Assert.Equal(before, card.Bounds);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>The approved reference is an icon-led card, not a compressed text tooltip.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void IssueCardRetainsApprovedHeaderRowsAndDiagnosticFooter(bool error, bool dark)
    {
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(error
            ? StandardMergeFeedbackTests.Status("input.source-view.incomplete", AuthoringSlotLifecycle.Error, 262143, 262144)
            : StandardMergeFeedbackTests.Status("TP_UNIFORM_CONTENT_WARNING", AuthoringSlotLifecycle.Warning), false);
        var input = new FirmwareSlotCard { Width = 1224, DataContext = slot, BrowseLabel = "Browse", ClearSelectionLabel = "Clear" };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(input);
        host.Width = 1710;
        host.Height = 920;
        host.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        host.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            ToggleButton badge = Assert.Single(input.GetVisualDescendants().OfType<ToggleButton>(),
                control => control.Classes.Contains("slotStateAction"));
            Assert.True(badge.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            ToolTip tooltip = Assert.IsType<ToolTip>(ToolTip.GetTip(badge));
            IssueDetailsCard card = Assert.Single(tooltip.GetVisualDescendants().OfType<IssueDetailsCard>());
            Border surface = Assert.Single(card.GetVisualDescendants().OfType<Border>(), item => item.Classes.Contains("icDetailCard"));
            // Reference PNGs: Error ~332x239 and Warning ~297x232. Font rasterization
            // and the owner's revised concise text may affect width, not hierarchy.
            Assert.InRange(surface.Bounds.Width, 296, 360);
            Assert.InRange(surface.Bounds.Height, 216, 258);
            Border hero = Assert.Single(card.GetVisualDescendants().OfType<Border>(), item => item.Classes.Contains("icDetailHeroIcon"));
            Assert.InRange(hero.Bounds.Width, 36, 40);
            Assert.InRange(hero.Bounds.Height, 36, 40);
            TextBlock caption = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "Input issue");
            TextBlock subject = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "TP BIN");
            Assert.InRange(subject.FontSize, 14, 16);
            TextBlock reason = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == slot.IssueCard!.Summary);
            TextBlock impact = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == slot.IssueCard!.Impact);
            TextBlock action = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == slot.IssueCard!.Action);
            string code = error ? "input.source-view.incomplete" : "TP_UNIFORM_CONTENT_WARNING";
            TextBlock diagnostic = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == code);
            double Y(Control control)
            {
                return control.TranslatePoint(default, card)!.Value.Y;
            }
            Assert.True(Y(caption) < Y(subject) && Y(subject) < Y(impact));
            Assert.InRange(Y(reason) - Y(impact), 30, 40);
            Assert.InRange(Y(action) - Y(reason), 30, 40);
            Assert.True(Y(diagnostic) > Y(action) + 24);
            Assert.Equal(3, card.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
                .Count(item => item.Classes.Contains("icDetailRowIcon") && item.IsEffectivelyVisible));
            Assert.Equal(new Thickness(1), surface.BorderThickness);
        }
        finally
        {
            host.Close();
        }
    }
}
