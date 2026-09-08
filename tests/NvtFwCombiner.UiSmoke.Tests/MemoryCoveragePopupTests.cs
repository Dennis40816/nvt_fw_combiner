using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the actual grouped-memory overlay hierarchy and its terminal-slice cards.</summary>
public sealed class MemoryCoveragePopupTests
{
    /// <summary>Grouped focus exposes all local slices without a preselected card, and leaf Escape unwinds one overlay at a time.</summary>
    [AvaloniaTheory]
    [InlineData(240, false)]
    [InlineData(388, true)]
    public void GroupedFocusKeepsLocalStripStableAndEscapeUnwindsToTheMainGroup(int width, bool dark)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateWindow(width, dark, slices, out MemoryCoverageBar bar);
        try
        {
            Control group = MainTarget(bar, 1);

            Assert.True(group.Focus());
            Render();

            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
            ProportionalStackPanel strip = LocalStrip(local);
            Assert.Equal(8, strip.Children.Count);
            Rect stripBounds = strip.Bounds;

            MemoryCoverageSegmentViewModel selected = slices[6];
            Control localLeaf = FocusableControl(strip.Children[5]);
            Assert.Same(selected, localLeaf.DataContext);
            Assert.True(localLeaf.Focus());
            Render();

            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Same(selected, card.DataContext);
            Assert.Equal(stripBounds, strip.Bounds);

            Expander technicalDetails = Assert.Single(card.GetVisualDescendants().OfType<Expander>());
            ToggleButton technicalToggle = Assert.Single(
                technicalDetails.GetVisualDescendants().OfType<ToggleButton>(),
                button => button.Name == "ExpanderHeader");
            Assert.True(technicalToggle.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Render();
            Assert.True(technicalDetails.IsExpanded);
            Assert.Equal(stripBounds, strip.Bounds);

            PressEscape(window);
            Render();
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.NotNull(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.Same(localLeaf, window.FocusManager?.GetFocusedElement());

            PressEscape(window);
            Render();
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Null(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.Same(group, window.FocusManager?.GetFocusedElement());

            Assert.True(MainTarget(bar, 0).Focus());
            Render();
            Border directCard = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Same(slices[0], directCard.DataContext);
            Assert.Null(FindNamed<Border>(window, "MemoryLocalView"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Collection rebuild, disable and window resize immediately dismiss both tiers instead of leaving stale overlay state.</summary>
    [AvaloniaFact]
    public void CollectionResetDisableAndResizeClearOpenOverlays()
    {
        var slices = new ObservableCollection<MemoryCoverageSegmentViewModel>(MemoryCoverageBarProjectionTests.Example());
        Window window = CreateWindow(388, dark: false, slices, out MemoryCoverageBar bar);
        try
        {
            OpenGroupedCard(window, bar);
            slices.Clear();
            Render();
            AssertNoOverlay(window);

            foreach (MemoryCoverageSegmentViewModel slice in MemoryCoverageBarProjectionTests.Example())
            {
                slices.Add(slice);
            }
            Render();
            OpenGroupedCard(window, bar);
            bar.IsEnabled = false;
            Render();
            AssertNoOverlay(window);

            bar.IsEnabled = true;
            Render();
            OpenGroupedCard(window, bar);
            window.Width += 8;
            Render();
            AssertNoOverlay(window);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Normal cards use the approved reveal transition while reduced motion is immediately opaque and transition-free.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CardRevealHonorsReducedMotion(bool reducedMotion)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateWindow(388, dark: reducedMotion, slices, out MemoryCoverageBar bar);
        try
        {
            bar.ReducedMotion = reducedMotion;
            Assert.True(MainTarget(bar, 0).Focus());
            Render();

            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            if (reducedMotion)
            {
                Assert.Null(card.Transitions);
                Assert.Equal(1, card.Opacity);
            }
            else
            {
                Transitions transitions = Assert.IsType<Transitions>(card.Transitions);
                Assert.Equal(2, transitions.Count);
                Assert.Equal(
                    TimeSpan.FromMilliseconds(140),
                    Assert.Single(transitions.OfType<DoubleTransition>()).Duration);
                Assert.Equal(
                    TimeSpan.FromMilliseconds(140),
                    Assert.Single(transitions.OfType<TransformOperationsTransition>()).Duration);
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Moving from a main slice into its card keeps it open, while leaving both surfaces dismisses it after the bounded delay.</summary>
    [AvaloniaFact]
    public async Task PointerTraversalBetweenMainSliceAndCardKeepsThenDismissesTheCard()
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateWindow(388, dark: false, slices, out MemoryCoverageBar bar);
        try
        {
            Control mainLeaf = MainTarget(bar, 0);
            window.MouseMove(BoundsInWindow(mainLeaf, window).Center, RawInputModifiers.None);
            Render();
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));

            window.MouseMove(BoundsInWindow(card, window).Center, RawInputModifiers.None);
            Render();
            await Task.Delay(TimeSpan.FromMilliseconds(220), TestContext.Current.CancellationToken);
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));

            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await Task.Delay(TimeSpan.FromMilliseconds(220), TestContext.Current.CancellationToken);
            Render();
            Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Public scroll and detachment lifecycle events close both overlay tiers and discard their stale targets.</summary>
    [AvaloniaFact]
    public void AncestorScrollAndDetachClearOpenOverlays()
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateScrolledWindow(slices, out MemoryCoverageBar bar, out ScrollViewer scroll);
        try
        {
            OpenGroupedCard(window, bar);
            scroll.Offset = new Vector(0, 1);
            Render();
            AssertNoOverlay(window);

            scroll.Offset = default;
            Render();
            window.Focusable = true;
            Assert.True(window.Focus());
            OpenGroupedCard(window, bar);
            scroll.Content = null;
            Render();
            AssertNoOverlay(window);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The 388px reference state can emit a full two-tier upward-overlay capture in both supported language/theme combinations.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BottomAnchoredGroupedOverlayCanBeCaptured(bool darkChinese)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = CreateBottomWindow(darkChinese, slices, out MemoryCoverageBar bar);
        try
        {
            OpenGroupedCard(window, bar, localSliceIndex: 5);
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.Same(slices[6], card.DataContext);
            Assert.True(BoundsInWindow(local, window).Bottom < BoundsInWindow(bar, window).Top);
            Assert.InRange(card.Bounds.Width / bar.Bounds.Width, 0.65, 0.72);
            for (int tick = 0; tick < 4; tick++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(80), TestContext.Current.CancellationToken);
                Render();
            }
            Capture(window, darkChinese ? "388-dark-zh-bottom" : "388-light-en-bottom");
            Assert.Equal(new Thickness(0), local.BorderThickness);
            TextBlock heading = Assert.Single(local.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == ((ShellTextResources)bar.Labels!).MemoryLocalViewLabel);
            Assert.True(BoundsInWindow(heading, window).Bottom < BoundsInWindow(LocalStrip(local), window).Top);
            Assert.True(BoundsInWindow(card, window).Bottom < BoundsInWindow(heading, window).Top);
            Assert.Contains(window.GetVisualDescendants().OfType<Control>(), control => control.Name == "MemoryCardNotch");
            Assert.Contains(card.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemoryHoverTechnicalSeparator" && control.IsEffectivelyVisible);
            Assert.True(BoundsInWindow(card, window).Bottom < BoundsInWindow(LocalStrip(local), window).Top,
                $"Card {BoundsInWindow(card, window)}, strip {BoundsInWindow(LocalStrip(local), window)}, local {BoundsInWindow(local, window)}");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Edge selections retain exact anchors and never draw their stem through local text.</summary>
    [AvaloniaTheory]
    [InlineData(240, false)]
    [InlineData(240, true)]
    [InlineData(388, false)]
    [InlineData(388, true)]
    public void EdgeCardsKeepMetadataAndAnchorsClear(int width, bool above)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example();
        Window window = above ? CreateBottomWindow(false, slices, out MemoryCoverageBar bar) : CreateWindow(width, false, slices, out bar);
        window.Width = width + 32;
        Render();
        bar.ReducedMotion = true;
        try
        {
            foreach (int index in new[] { 0, 5, 7 })
            {
                OpenGroupedCard(window, bar, index);
                Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
                Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
                ProportionalStackPanel strip = LocalStrip(local);
                Rect leaf = BoundsInWindow(strip.Children[index], window);
                Rect body = BoundsInWindow(card, window);
                Rect rail = BoundsInWindow(bar, window);
                Assert.True(body.Left >= rail.Left - 1 && body.Right <= rail.Right + 1,
                    $"Card {body} leaves the memory rail column {rail}");
                global::Avalonia.Controls.Shapes.Ellipse dot = Assert.Single(window.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Ellipse>(), item => item.Name == "MemoryCardAnchor");
                Assert.InRange(Math.Abs(BoundsInWindow(dot, window).Center.X - leaf.Center.X), 0, 1);
                Assert.InRange(Math.Abs(BoundsInWindow(dot, window).Center.Y - (above ? leaf.Top : leaf.Bottom)), 0, 1);
                Assert.InRange(body.Left, 7, window.Bounds.Width);
                Assert.True(body.Right <= window.Bounds.Width - 7);
                Assert.True(body.Top >= 0 && body.Bottom <= window.Bounds.Height);
                foreach (TextBlock label in local.GetVisualDescendants().OfType<TextBlock>())
                {
                    Rect text = BoundsInWindow(label, window);
                    Assert.False(body.Intersects(text), $"Card {body} overlaps {label.Text} at {text}; above={above}");
                    foreach (global::Avalonia.Controls.Shapes.Line line in dot.GetVisualParent()!.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Line>())
                    {
                        Point lineStart = line.TranslatePoint(line.StartPoint, window)!.Value;
                        Point lineEnd = line.TranslatePoint(line.EndPoint, window)!.Value;
                        Assert.False(text.Contains(lineStart) || text.Contains(lineEnd));
                        Assert.False(lineStart.X >= text.Left && lineStart.X <= text.Right && lineStart.Y < text.Top && lineEnd.Y > text.Bottom);
                    }
                }
            }
        }
        finally { window.Close(); }
    }

    /// <summary>The local strip uses the larger available side instead of a fixed absolute Y threshold.</summary>
    [AvaloniaFact]
    public void LocalStripChoosesAvailableSpaceInAShorterViewport()
    {
        Window window = CreateBottomWindow(false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        window.Height = 620;
        Border spacer = Assert.IsType<Border>(Assert.IsType<StackPanel>(window.Content).Children[0]);
        spacer.Height = 330;
        Render();
        try
        {
            OpenGroupedCard(window, bar, 5);
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Border card = Assert.IsType<Border>(FindNamed<Border>(window, "MemorySliceCard"));
            Assert.True(BoundsInWindow(local, window).Bottom < BoundsInWindow(bar, window).Top);
            Assert.True(BoundsInWindow(card, window).Top >= 0);
        }
        finally { window.Close(); }
    }

    private static void OpenGroupedCard(Window window, MemoryCoverageBar bar, int localSliceIndex = 0)
    {
        Control group = MainTarget(bar, 1);
        Assert.True(group.Focus());
        Render();
        Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
        Control leaf = FocusableControl(LocalStrip(local).Children[localSliceIndex]);
        Assert.True(leaf.Focus());
        Render();
        Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
    }

    /// <summary>The shared flat row consumes the same primary flag as logical Merge/Replace rows.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlatSupportingRowHidesOnlyTechnicalTrace(bool dark)
    {
        MemoryCoverageSegmentViewModel[] slices = MemoryCoverageBarProjectionTests.Example(withTrace: true);
        Window window = CreateWindow(388, dark, slices, out MemoryCoverageBar bar);
        try
        {
            var template = (global::Avalonia.Controls.Templates.IDataTemplate)bar.FindResource("MemoryCoverageSegmentListTemplate")!;
            Control row = template.Build(slices[2])!;
            row.DataContext = slices[2];
            window.Content = row;
            Render();
            Assert.False(row.IsVisible);
            row.DataContext = slices[0];
            Render();
            Assert.True(row.IsVisible);
            Assert.True(row.Bounds.Height > 0);
        }
        finally { window.Close(); }
    }

    /// <summary>Trace geometry is inert and crossing it dismisses stale direct or local cards.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TechnicalTraceIsInertAndClearsOnlyItsHoveredOverlay(bool plain)
    {
        Window window = CreateWindow(388, false, MemoryCoverageBarProjectionTests.Example(withTrace: true), out MemoryCoverageBar bar);
        bar.IsPlain = plain;
        bar.ReducedMotion = true;
        Render();
        try
        {
            ProportionalStackPanel panel = MainPanel(bar);
            Control trace = Assert.Single(panel.GetVisualDescendants().OfType<Control>(), static control => control.Name == "MemoryTraceSpacer");
            Assert.False(trace.Focusable);
            Assert.False(trace.IsHitTestVisible);
            Assert.Null(ToolTip.GetTip(trace));
            Assert.True(string.IsNullOrEmpty(global::Avalonia.Automation.AutomationProperties.GetName(trace)));
            Assert.Equal(0x80000, panel.Children.Sum(ProportionalStackPanel.GetWeight));
            Control primary = MainTarget(bar, 0);
            window.MouseMove(BoundsInWindow(primary, window).Center, RawInputModifiers.None);
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
            window.MouseMove(BoundsInWindow(trace, window).Center, RawInputModifiers.None);
            Render();
            AssertNoOverlay(window);
            window.MouseMove(BoundsInWindow(primary, window).Center, RawInputModifiers.None);
            Render();
            Assert.True(MainTarget(bar, 3).Focus());
            Render();
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            Assert.True(FocusableControl(LocalStrip(local).Children[0]).Focus());
            Render();
            Assert.NotNull(FindNamed<Border>(window, "MemorySliceCard"));
            window.MouseMove(BoundsInWindow(trace, window).Center, RawInputModifiers.None);
            Render();
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }

    private static Window CreateWindow(
        int width,
        bool dark,
        IEnumerable<MemoryCoverageSegmentViewModel> slices,
        out MemoryCoverageBar bar)
    {
        bar = new MemoryCoverageBar
        {
            ItemsSource = slices,
            Labels = ShellTextResources.For(ShellLanguage.English),
        };
        var window = new Window
        {
            Width = width + 32,
            Height = 620,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            DataContext = new ShellFixture(ShellTextResources.For(ShellLanguage.English)),
            Content = new Border { Padding = new Thickness(16), Child = bar },
        };
        AddSharedTemplates(window);
        window.Show();
        Render();
        return window;
    }

    private static Window CreateScrolledWindow(
        IEnumerable<MemoryCoverageSegmentViewModel> slices,
        out MemoryCoverageBar bar,
        out ScrollViewer scroll)
    {
        bar = new MemoryCoverageBar
        {
            ItemsSource = slices,
            Labels = ShellTextResources.For(ShellLanguage.English),
        };
        scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Children =
                {
                    new Border { Padding = new Thickness(16), Child = bar },
                    new Border { Height = 640 },
                },
            },
        };
        var window = new Window
        {
            Width = 420,
            Height = 300,
            DataContext = new ShellFixture(ShellTextResources.For(ShellLanguage.English)),
            Content = scroll,
        };
        AddSharedTemplates(window);
        window.Show();
        Render();
        return window;
    }

    private static Window CreateBottomWindow(
        bool darkChinese,
        IEnumerable<MemoryCoverageSegmentViewModel> slices,
        out MemoryCoverageBar bar)
    {
        ShellTextResources text = ShellTextResources.For(
            darkChinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        bar = new MemoryCoverageBar { ItemsSource = slices, Labels = text };
        var window = new Window
        {
            Width = 420,
            Height = 900,
            RequestedThemeVariant = darkChinese ? ThemeVariant.Dark : ThemeVariant.Light,
            DataContext = new ShellFixture(text),
            Content = new StackPanel
            {
                Children =
                {
                    new Border { Height = 650 },
                    new Border { Padding = new Thickness(16), Child = bar },
                },
            },
        };
        AddSharedTemplates(window);
        window.Show();
        Render();
        return window;
    }

    private static void AddSharedTemplates(Window window)
    {
        var uri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
        window.Resources.MergedDictionaries.Add(new ResourceInclude(uri) { Source = uri });
        foreach (Uri styleUri in new[]
        {
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowStyles.axaml"),
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowButtonStyles.axaml"),
            new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowVisualStyles.axaml"),
        })
        {
            window.Styles.Add(new StyleInclude(styleUri) { Source = styleUri });
        }
    }

    private static ProportionalStackPanel MainPanel(MemoryCoverageBar bar)
    {
        return Assert.Single(bar.GetVisualDescendants().OfType<ProportionalStackPanel>());
    }

    private static Control MainTarget(MemoryCoverageBar bar, int index)
    {
        return FocusableControl(MainPanel(bar).Children[index]);
    }

    private static Control FocusableControl(Control root)
    {
        return Assert.Single(
            root.GetVisualDescendants().Append(root).OfType<Control>(),
            static control => control.Focusable);
    }

    private static ProportionalStackPanel LocalStrip(Border local)
    {
        return Assert.Single(local.GetVisualDescendants().OfType<ProportionalStackPanel>());
    }

    private static Rect BoundsInWindow(Control control, Window window)
    {
        return new Rect(Assert.IsType<Point>(control.TranslatePoint(default, window)), control.Bounds.Size);
    }

    private static T? FindNamed<T>(Window window, string name)
        where T : Control
    {
        return window.GetVisualDescendants().Append(window).OfType<T>()
            .SingleOrDefault(control => control.Name == name);
    }

    private static void PressEscape(Window window)
    {
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
    }

    private static void AssertNoOverlay(Window window)
    {
        Assert.Null(FindNamed<Border>(window, "MemoryLocalView"));
        Assert.Null(FindNamed<Border>(window, "MemorySliceCard"));
    }

    private static void Capture(Window window, string state)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"memory-popup-{state}.png"));
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed record ShellFixture(ShellTextResources Text);
}
