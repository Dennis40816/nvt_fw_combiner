using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>A real issue tooltip stays close to its badge and flips above it at the viewport bottom.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task IssueTooltipFitsViewportNearBothEdges(bool nearBottom, bool dark, bool chinese)
    {
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(
            StandardMergeFeedbackTests.Status("CTRLRAM_SIZE_WARNING", AuthoringSlotLifecycle.Warning,
                655360, 23552, ignoredTrailingBytes: true), chinese);
        var card = new FirmwareSlotCard { Width = 620, DataContext = slot, BrowseLabel = "Browse", ClearSelectionLabel = "Clear" };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Content = null;
        var canvas = new Canvas { Height = 1200 };
        canvas.Children.Add(card);
        Canvas.SetLeft(card, 16);
        Canvas.SetTop(card, 440);
        var scroll = new ScrollViewer { Content = canvas };
        host.Content = scroll;
        host.Width = 700;
        host.Height = 640;
        host.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        host.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            scroll.Offset = new Vector(0, nearBottom ? 0 : 424);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            ToggleButton badge = Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>(),
                control => control.Classes.Contains("slotStateAction"));
            Point badgeOrigin = badge.TranslatePoint(default, host)!.Value;
            Rect badgeRect = new(badgeOrigin, badge.Bounds.Size);
            host.MouseMove(badgeRect.Center, RawInputModifiers.None);
            await Task.Delay(500, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.True(badge.IsPointerOver);
            Assert.True(ToolTip.GetIsOpen(badge));
            AssertIssueTooltipGeometry(host, badge, nearBottom);
            Assert.False(slot.BlocksBuild);
            host.MouseMove(new Point(1, 1), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.False(ToolTip.GetIsOpen(badge));

            // Reopening after scrolling/layout movement must not retain the previous arrow side.
            scroll.Offset = new Vector(0, nearBottom ? 424 : 0);
            Dispatcher.UIThread.RunJobs();
            Assert.True(badge.Focus(NavigationMethod.Tab));
            await Task.Delay(300, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            AssertIssueTooltipGeometry(host, badge, !nearBottom);
            scroll.Offset = new Vector(0, nearBottom ? 0 : 424);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            Assert.False(ToolTip.GetIsOpen(badge));
            Assert.True(card.FindControl<Button>("BrowseButton")!.Focus());
            Assert.True(badge.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            AssertIssueTooltipGeometry(host, badge, nearBottom);
            host.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            host.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Dispatcher.UIThread.RunJobs();
            Assert.False(ToolTip.GetIsOpen(badge));
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>All current issue-card anchors share a zero offset; framework flipping must not overlap the target.</summary>
    [Fact]
    public void AllIssueCardConsumersUseTheSamePlacementContract()
    {
        foreach (string file in new[] { "MainWindow.axaml", "Views/FirmwareSlotCard.axaml" })
        {
            var document = System.Xml.Linq.XDocument.Parse(ReadPresentationFile(file));
            System.Xml.Linq.XElement[] cards = [.. document.Descendants().Where(e => e.Name.LocalName == "IssueDetailsCard")];
            Assert.Equal(file == "MainWindow.axaml" ? 2 : 1, cards.Length);
            foreach (System.Xml.Linq.XElement card in cards)
            {
                System.Xml.Linq.XElement anchor = card.Ancestors().First(e => e.Elements().Any(c => c.Name.LocalName == "ToolTip.Tip"));
                Assert.Equal("0", (string?)anchor.Attribute("ToolTip.VerticalOffset"));
                Assert.NotNull(anchor.Attribute("ToolTip.Placement"));
            }
        }
    }

    private static void AssertIssueTooltipGeometry(Window host, Control target, bool above)
    {
        Assert.True(ToolTip.GetIsOpen(target));
        ToolTip tip = Assert.IsType<ToolTip>(ToolTip.GetTip(target));
        Rect badge = new(host.PointToClient(target.PointToScreen(default)), target.Bounds.Size);
        Rect popup = new(host.PointToClient(tip.PointToScreen(default)), tip.Bounds.Size);
        string geometry = $"badge={badge}; popup={popup}; viewport={host.ClientSize}";
        Assert.True(popup.Top >= 0 && popup.Bottom <= host.ClientSize.Height, geometry);
        Assert.True(popup.Left >= 0 && popup.Right <= host.ClientSize.Width, geometry);
        double gap = above ? badge.Top - popup.Bottom : popup.Top - badge.Bottom;
        Assert.True(gap is >= 0 and <= 8, geometry);
        IssueDetailsCard card = Assert.Single(tip.GetVisualDescendants().OfType<IssueDetailsCard>());
        Border surface = Assert.Single(card.GetVisualDescendants().OfType<Border>(), c => c.Classes.Contains("icDetailCard"));
        Control pointer = Assert.Single(card.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>(),
            p => p.Width == 20 && p.Height == 10 && p.IsEffectivelyVisible);
        Rect arrow = new(host.PointToClient(pointer.PointToScreen(default)), pointer.Bounds.Size);
        Rect body = new(host.PointToClient(surface.PointToScreen(default)), surface.Bounds.Size);
        Assert.True(above ? arrow.Top >= body.Bottom - 1 : arrow.Bottom <= body.Top + 1,
            $"Arrow must face the anchor after framework flip: arrow={arrow}; body={body}; {geometry}");
        Assert.InRange(Math.Abs(arrow.Center.X - badge.Center.X), 0, 12);
    }
}
