using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Measures the complete production CtrlRAM page rather than a substitute layout.</summary>
public sealed class CtrlRamSelectorLayoutTests
{
    /// <summary>Base and grouped input cards share edges; hover/focus never mutate section geometry or add an outline.</summary>
    [AvaloniaTheory]
    [InlineData(980, 640, false, false, false)]
    [InlineData(980, 640, false, true, false)]
    [InlineData(980, 640, true, false, false)]
    [InlineData(980, 640, true, true, false)]
    [InlineData(1440, 900, false, false, false)]
    [InlineData(1440, 900, false, true, false)]
    [InlineData(1440, 900, true, false, false)]
    [InlineData(1440, 900, true, true, false)]
    [InlineData(980, 640, false, false, true)]
    [InlineData(980, 640, false, true, true)]
    [InlineData(980, 640, true, false, true)]
    [InlineData(980, 640, true, true, true)]
    [InlineData(1440, 900, false, false, true)]
    [InlineData(1440, 900, false, true, true)]
    [InlineData(1440, 900, true, false, true)]
    [InlineData(1440, 900, true, true, true)]
    [InlineData(1920, 1080, false, false, true)]
    public async Task CtrlRamSectionsKeepApprovedAnchorsAndOutlines(int width, int height, bool dark, bool chinese, bool selected)
    {
        using var workspace = TempWorkspace.Create("ctrlram-selector-layout");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Width = width;
        window.Height = height;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        await AwaitHistoryReadyAsync(window);
        try
        {
            shell.ShowReplaceCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51929";
            shell.WorkflowSession.SelectedNumber = "cascade_2to8";
            if (selected)
            {
                // Use an existing certified cascade fixture, not invented bytes or a forced green badge.
                JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace",
                    "nt51926-fw200-cascade3-auto-prj-597-20260718");
                shell.WorkflowSession.SelectedIc = "NT51926";
                shell.WorkflowSession.SelectedNumber = "cascade";
                await shell.WorkflowSession.SetSlotFileAsync("replace-base",
                    CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(fixture, "expected-output")),
                    TestContext.Current.CancellationToken);
                await shell.WorkflowSession.SetSlotFileAsync("replace-ctrlram-vn",
                    CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(fixture, "vn-ctrlram-input")),
                    TestContext.Current.CancellationToken);
                Assert.True(shell.Replace.ReplaceBaseSlot.HasFile);
                Assert.Equal(FirmwareSlotSemanticState.Verified, shell.Replace.ReplaceBaseSlot.SemanticState);
                Assert.Equal(WorkflowInspectionAttemptState.Succeeded, shell.Replace.Inspection.State);
                FirmwareSlotGroupViewModel common = Assert.Single(shell.Replace.ReplaceSlotGroups, g => g.Slots[0].RegionGroup == ReplaceRegionGroup.Common);
                Assert.Equal("replace-ctrlram-vn", Assert.Single(common.Slots, slot => slot.HasFile).SlotId);
                Assert.All(shell.Replace.ReplaceSlotGroups.Where(g => g.Slots[0].RegionGroup == ReplaceRegionGroup.Cascade).SelectMany(g => g.Slots), slot => Assert.False(slot.HasFile));
            }
            if (chinese)
            {
                shell.SelectedLanguage = "Traditional Chinese";
            }
            foreach (FirmwareSlotGroupViewModel group in shell.Replace.ReplaceSlotGroups)
            {
                group.IsExpanded = true;
            }
            Render();
            Grid pageHeader = Assert.Single(window.GetVisualDescendants().OfType<Grid>(), grid => grid.Name == "ReplacePageHeader");
            TextBlock pageTitle = Assert.Single(pageHeader.GetVisualDescendants().OfType<TextBlock>(), text => text.Classes.Contains("pageTitle"));
            TextBlock pageSubtitle = Assert.Single(pageHeader.GetVisualDescendants().OfType<TextBlock>(), text => text.Classes.Contains("pageSubtitle"));
            ComboBox mode = Assert.Single(pageHeader.GetVisualDescendants().OfType<ComboBox>());
            Button targets = Assert.Single(pageHeader.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("summaryChip"));
            Rect headerBounds = BoundsInWindow(pageHeader, window);
            Rect titleBounds = BoundsInWindow(pageTitle, window);
            Assert.InRange(pageTitle.TextLayout.WidthIncludingTrailingWhitespace, 1, titleBounds.Width + 0.5);
            _ = Assert.Single(pageSubtitle.TextLayout.TextLines);
            Assert.InRange(pageSubtitle.TextLayout.Height, 1, pageSubtitle.Bounds.Height + 0.5);
            foreach (Control action in new Control[] { mode, targets })
            {
                Rect actionBounds = BoundsInWindow(action, window);
                Assert.InRange(actionBounds.Left, headerBounds.Left, headerBounds.Right);
                Assert.True(actionBounds.Right <= headerBounds.Right + 0.5);
                if (width == 980)
                {
                    Assert.True(actionBounds.Top >= BoundsInWindow(pageSubtitle, window).Bottom + 12);
                }
                else
                {
                    Assert.True(actionBounds.Left >= titleBounds.Right);
                }
            }
            Dictionary<string, string?> selectedPaths = shell.Replace.ReplaceSlots.ToDictionary(slot => slot.SlotId, slot => slot.FilePath);
            FirmwareSlotCard baseCard = Assert.Single(window.GetVisualDescendants().OfType<FirmwareSlotCard>(),
                c => ReferenceEquals(c.DataContext, shell.Replace.ReplaceBaseSlot));
            Border baseBorder = Assert.Single(baseCard.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("firmwareSlot"));
            SpaciousPanel[] groups = [.. window.GetVisualDescendants().OfType<SpaciousPanel>().Where(p => p.Classes.Contains("firmwareSlotGroupSurface"))];
            Assert.Equal(2, groups.Length);
            Rect baseBounds = BoundsInWindow(baseBorder, window);
            Border inputPanel = baseCard.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("roomyPanel"));
            TextBlock inputTitle = inputPanel.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == shell.Text.InputFilesTitle);
            Assert.InRange(Math.Abs(BoundsInWindow(inputTitle, window).Left - baseBounds.Left), 0, 0.5);
            MemoryCoverageBar outputRail = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), rail => rail.IsEffectivelyVisible);
            Border outputPanel = outputRail.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("panelSurface"));
            Assert.InRange(Math.Abs(BoundsInWindow(inputPanel, window).Top - BoundsInWindow(outputPanel, window).Top), 0, 0.5);
            if (selected)
            {
                Button filename = Assert.Single(baseCard.GetVisualDescendants().OfType<Button>(),
                    button => button.Classes.Contains("fileRevealAction"));
                Grid content = baseCard.FindControl<Grid>("SlotLayout")!;
                ToggleButton details = Assert.Single(baseCard.GetVisualDescendants().OfType<ToggleButton>(), button => button.Classes.Contains("quietDisclosure"));
                Assert.True(details.IsEffectivelyVisible);
                Assert.InRange(Math.Abs(BoundsInWindow(details, window).Left - BoundsInWindow(content, window).Left), 0, 0.5);
                Rect filenameBounds = BoundsInWindow(filename, window);
                Rect contentBounds = BoundsInWindow(content, window);
                Assert.InRange(filenameBounds.Top - contentBounds.Bottom, 12, 16.5);
                Assert.InRange(Math.Abs(filenameBounds.Left - contentBounds.Left), 0, 0.5);
                Assert.InRange(Math.Abs(filenameBounds.Right - contentBounds.Right), 0, 0.5);
                TextBlock text = Assert.IsType<TextBlock>(filename.Content);
                Assert.Equal(shell.Replace.ReplaceBaseSlot.DisplayNameWithSelectionContext, text.Text);
                Assert.Equal(shell.Replace.ReplaceBaseSlot.FilePath, filename.CommandParameter);
                Assert.Equal(shell.Replace.ReplaceBaseSlot.DisplayDetail, ToolTip.GetTip(filename));
                Assert.Equal(text.Text, AutomationProperties.GetName(filename));
                Assert.InRange(text.TextLayout.Height, 1, text.Bounds.Height + 0.5);
                if (width == 1440)
                {
                    _ = Assert.Single(text.TextLayout.TextLines);
                }
            }
            Assert.InRange(BoundsInWindow(groups[0], window).Top - baseBounds.Bottom, 12, 12.5);
            Assert.InRange(BoundsInWindow(groups[1], window).Top - BoundsInWindow(groups[0], window).Bottom, 12, 12.5);
            // This reference matrix is 100% headless rendering, not native Windows 125% evidence.
            Assert.Equal(1.0, window.RenderScaling);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            Assert.Equal(new PixelSize(width, height), frame.PixelSize);
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"Headless reference: logical={window.ClientSize}, scale={window.RenderScaling}, " +
                $"pixels={frame.PixelSize}, theme={window.ActualThemeVariant}, language={(chinese ? "zh-TW" : "en")}");
            string? imageDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(imageDirectory))
            {
                _ = Directory.CreateDirectory(imageDirectory);
                frame.Save(Path.Combine(imageDirectory, $"ctrlram-selector-{width}-{height}-{(dark ? "dark" : "light")}-{(chinese ? "zh" : "en")}-{(selected ? "selected" : "empty")}.png"));
            }
            foreach (SpaciousPanel group in groups)
            {
                Rect bounds = BoundsInWindow(group, window);
                Assert.InRange(Math.Abs(baseBounds.Left - bounds.Left), 0, 0.5);
                Assert.InRange(Math.Abs(bounds.Right - baseBounds.Right), 0, 0.5);
                Assert.Equal(new Thickness(0, 1, 0, 0), group.BorderThickness);
                Expander expander = Assert.Single(group.GetVisualDescendants().OfType<Expander>());
                TextBlock groupTitle = Assert.Single(expander.GetVisualDescendants().OfType<TextBlock>(), block => block.Classes.Contains("cardTitle"));
                Assert.Equal(FontWeight.Bold, groupTitle.FontWeight);
                Assert.InRange(Math.Abs(BoundsInWindow(groupTitle, window).Left - baseBounds.Left), 0, 0.5);
                FirmwareSlotCard[] cards = [.. group.GetVisualDescendants().OfType<FirmwareSlotCard>()];
                FirmwareSlotCard first = cards.First();
                foreach (FirmwareSlotCard card in cards)
                {
                    Border outline = Assert.Single(card.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("firmwareSlot"));
                    Rect inputBounds = BoundsInWindow(outline, window);
                    TestContext.Current.TestOutputHelper!.WriteLine($"Input column: base={baseBounds}; group={bounds}; child={inputBounds}");
                    Assert.InRange(Math.Abs(inputBounds.Left - baseBounds.Left), 0, 0.5);
                    Assert.InRange(Math.Abs(inputBounds.Right - baseBounds.Right), 0, 0.5);
                    Assert.True(outline.BorderThickness.Left > 0 && outline.BorderThickness.Top > 0 &&
                        outline.BorderThickness.Right > 0 && outline.BorderThickness.Bottom > 0);
                    Border[] intermediate = [.. card.GetVisualAncestors().TakeWhile(v => v != group).OfType<Border>()];
                    Assert.All(intermediate, b => Assert.True(b.BorderThickness == default,
                        $"Unexpected content outline {b.Name}: {b.BorderThickness}"));
                }
                Assert.NotEmpty(AutomationProperties.GetName(expander) ?? string.Empty);
                ToggleButton header = Assert.Single(expander.GetVisualDescendants().OfType<ToggleButton>(), b => b.Name == "ExpanderHeader");
                // Bring the actual header into view before injecting a pointer or keyboard event.
                header.BringIntoView();
                Render();
                Rect groupBefore = group.Bounds;
                Rect cardBefore = first.Bounds;
                Rect headerBefore = header.Bounds;
                Thickness outlineBefore = group.BorderThickness;
                Thickness headerOutlineBefore = header.BorderThickness;
                Border contentBorder = Assert.Single(expander.GetVisualDescendants().OfType<Border>(), b => b.Name == "ExpanderContent");
                Thickness contentOutlineBefore = contentBorder.BorderThickness;
                Control headerContent = Assert.IsType<Control>(header.Content, exactMatch: false);
                Rect headerContentBefore = BoundsInWindow(headerContent, window);
                Rect[] cardBoundsBefore = [.. cards.Select(card => BoundsInWindow(card, window))];
                Thickness[] cardOutlinesBefore = [.. cards.Select(card => card.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("firmwareSlot")).BorderThickness)];
                Point pointer = Assert.IsType<Point>(header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), window));
                window.MouseMove(pointer, RawInputModifiers.None);
                Render();
                Assert.True(header.IsPointerOver);
                Assert.Equal(groupBefore, group.Bounds);
                Assert.Equal(cardBefore, first.Bounds);
                Assert.Equal(headerBefore, header.Bounds);
                Assert.Equal(outlineBefore, group.BorderThickness);
                Assert.Equal(headerOutlineBefore, header.BorderThickness);
                Assert.Equal(contentOutlineBefore, contentBorder.BorderThickness);
                Assert.Equal(headerContentBefore, BoundsInWindow(headerContent, window));
                Assert.Equal(cardBoundsBefore, cards.Select(card => BoundsInWindow(card, window)));
                Assert.Equal(cardOutlinesBefore, cards.Select(card => card.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("firmwareSlot")).BorderThickness));
                Assert.Equal(Colors.Transparent, Assert.IsType<ISolidColorBrush>(header.BorderBrush, exactMatch: false).Color);
                Assert.True(header.Focus(NavigationMethod.Tab));
                Render();
                Assert.Equal(headerBefore, header.Bounds);
                Assert.NotEqual(Colors.Transparent, Assert.IsType<ISolidColorBrush>(header.BorderBrush, exactMatch: false).Color);
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Render();
                Assert.False(expander.IsExpanded);
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Render();
                Assert.True(expander.IsExpanded);
                Assert.Equal(groupBefore, group.Bounds);
            }
            Assert.Equal(selectedPaths, shell.Replace.ReplaceSlots.ToDictionary(slot => slot.SlotId, slot => slot.FilePath));
            object? selectedMode = mode.SelectedItem;
            ToggleButton disclosure = Assert.Single(baseCard.GetVisualDescendants().OfType<ToggleButton>(), button => button.Classes.Contains("quietDisclosure"));
            if (selected)
            {
                Assert.True(disclosure.Focus(NavigationMethod.Tab));
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Render();
                Assert.True(shell.Replace.ReplaceBaseSlot.IsAdditionalFirmwareFactsExpanded);
            }
            foreach (int resizedWidth in new[] { width == 980 ? 1440 : 980, width })
            {
                window.Width = resizedWidth;
                Render();
                Rect resizedTitle = BoundsInWindow(pageTitle, window);
                Rect resizedMode = BoundsInWindow(mode, window);
                Assert.InRange(pageTitle.TextLayout.WidthIncludingTrailingWhitespace, 1, resizedTitle.Width + 0.5);
                Assert.True(resizedWidth == 980
                    ? resizedMode.Top >= BoundsInWindow(pageSubtitle, window).Bottom + 12
                    : resizedMode.Left >= resizedTitle.Right);
                Assert.Equal(selectedMode, mode.SelectedItem);
                Assert.Equal(selectedPaths, shell.Replace.ReplaceSlots.ToDictionary(slot => slot.SlotId, slot => slot.FilePath));
                if (selected)
                {
                    Grid content = baseCard.FindControl<Grid>("SlotLayout")!;
                    ItemsControl additional = baseCard.FindControl<ItemsControl>("AdditionalFirmwareFactsHost")!;
                    Assert.True(additional.IsEffectivelyVisible);
                    Assert.Equal(shell.Replace.ReplaceBaseSlot.AdditionalFirmwareFacts.Count, additional.ItemCount);
                    Assert.InRange(Math.Abs(BoundsInWindow(disclosure, window).Left - BoundsInWindow(content, window).Left), 0, 0.5);
                    Assert.InRange(Math.Abs(BoundsInWindow(additional, window).Left - BoundsInWindow(content, window).Left), 0, 0.5);
                    Assert.InRange(Math.Abs(additional.Bounds.Width - content.Bounds.Width), 0, 0.5);
                    Assert.True(shell.Replace.ReplaceBaseSlot.IsAdditionalFirmwareFactsExpanded);
                }
                Rect resizedBase = BoundsInWindow(baseBorder, window);
                Assert.InRange(Math.Abs(BoundsInWindow(inputPanel, window).Top - BoundsInWindow(outputPanel, window).Top), 0, 0.5);
                foreach (FirmwareSlotCard input in groups.SelectMany(group => group.GetVisualDescendants().OfType<FirmwareSlotCard>()))
                {
                    Rect inputBounds = BoundsInWindow(Assert.Single(input.GetVisualDescendants().OfType<Border>(),
                        border => border.Classes.Contains("firmwareSlot")), window);
                    Assert.InRange(Math.Abs(inputBounds.Left - resizedBase.Left), 0, 0.5);
                    Assert.InRange(Math.Abs(inputBounds.Right - resizedBase.Right), 0, 0.5);
                }
            }
            if (selected)
            {
                if (!string.IsNullOrWhiteSpace(imageDirectory))
                {
                    using Avalonia.Media.Imaging.Bitmap? expandedFrame = window.GetLastRenderedFrame();
                    Assert.NotNull(expandedFrame);
                    expandedFrame.Save(Path.Combine(imageDirectory, $"fw-details-{width}-{height}-{(dark ? "dark" : "light")}-{(chinese ? "zh" : "en")}-expanded.png"));
                }
                Assert.True(disclosure.Focus(NavigationMethod.Tab));
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Render();
                Assert.False(shell.Replace.ReplaceBaseSlot.IsAdditionalFirmwareFactsExpanded);
                Assert.False(baseCard.FindControl<ItemsControl>("AdditionalFirmwareFactsHost")!.IsEffectivelyVisible);
                Assert.Equal(selectedPaths, shell.Replace.ReplaceSlots.ToDictionary(slot => slot.SlotId, slot => slot.FilePath));
            }
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    internal static void AssertMergePanelAlignment(Window window)
    {
        FirmwareSlotCard[] cards = [.. window.GetVisualDescendants().OfType<FirmwareSlotCard>().Where(card => card.IsEffectivelyVisible)];
        Assert.NotEmpty(cards);
        Border inputPanel = cards[0].GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("roomyPanel"));
        Border outputPanel = Assert.Single(window.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("memoryInfoPanel") && border.IsEffectivelyVisible);
        Assert.InRange(Math.Abs(BoundsInWindow(inputPanel, window).Top - BoundsInWindow(outputPanel, window).Top), 0, 0.5);
        TextBlock title = inputPanel.GetVisualDescendants().OfType<TextBlock>().First(block => block.Classes.Contains("sectionTitle") && block.IsEffectivelyVisible);
        Rect titleBounds = BoundsInWindow(title, window);
        foreach (FirmwareSlotCard card in cards)
        {
            Rect bounds = BoundsInWindow(card, window);
            Assert.InRange(Math.Abs(bounds.Left - titleBounds.Left), 0, 0.5);
            Assert.InRange(Math.Abs(bounds.Right - BoundsInWindow(cards[0], window).Right), 0, 0.5);
        }
    }

    private static Rect BoundsInWindow(Control control, Window window)
    {
        return new Rect(Assert.IsType<Point>(control.TranslatePoint(default, window)), control.Bounds.Size);
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
