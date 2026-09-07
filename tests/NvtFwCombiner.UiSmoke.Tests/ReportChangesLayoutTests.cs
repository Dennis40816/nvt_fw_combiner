using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Rendered layout regressions for the Report Changes workspace.</summary>
public sealed class ReportChangesLayoutTests
{
    /// <summary>The range navigator reserves its scrollbar beside cards instead of covering them.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ChangedRangeCardsReserveAStableScrollbarGutter(bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("report-changes-scroll-gutter");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            services,
            ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = 900,
            WindowState = WindowState.Normal,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        await AwaitHistoryReadyAsync(window);
        MainWindowViewModel shell = (MainWindowViewModel)window.DataContext!;
        try
        {
            shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
            shell.Reports.LoadReportJson(
                CreateReplayableRangeReport(),
                "report-changes-layout.json");
            OpenChanges(window, shell);
            Render();
            Assert.True(shell.Reports.LoadedReport.HexDiff.IsAvailable);
            shell.Reports.LoadedReport.HexDiff.ShowOriginalRows = true;
            Render();
            Assert.True(shell.Reports.LoadedReport.HexDiff.ViewportSnapshot.ShowComparisonRows);
            Assert.Contains(
                shell.Reports.LoadedReport.HexDiff.ViewportSnapshot.Rows,
                static row => row.HasComparison);

            ListBox ranges = Assert.Single(
                window.GetVisualDescendants().OfType<ListBox>(),
                item => item.Classes.Contains("reportHexDiffRanges"));
            ScrollViewer scroll = Assert.Single(ranges.GetVisualDescendants().OfType<ScrollViewer>());
            ScrollBar vertical = Assert.Single(
                scroll.GetVisualDescendants().OfType<ScrollBar>(),
                bar => bar.Orientation == Avalonia.Layout.Orientation.Vertical && bar.IsEffectivelyVisible);
            ListBoxItem first = ranges.GetVisualDescendants().OfType<ListBoxItem>().First(item => item.IsEffectivelyVisible);

            Assert.False(scroll.AllowAutoHide);
            Assert.InRange(vertical.Bounds.Width, 13.5, 14.5);
            Rect cardBounds = BoundsIn(first, ranges);
            Rect scrollbarBounds = BoundsIn(vertical, ranges);
            Assert.True(
                cardBounds.Right <= scrollbarBounds.Left - 4,
                $"Range card {cardBounds} overlaps the scrollbar gutter {scrollbarBounds}.");
            Assert.Equal(shell.Text.HexDiffRangeNavigatorTitle, AutomationProperties.GetName(ranges));

            SaveFrame(
                window,
                $"report-changes-1440x900-{(dark ? "dark" : "light")}-{(chinese ? "zh" : "en")}.png");
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Complete localized comparison labels keep a measured gap before the first byte.</summary>
    [AvaloniaTheory]
    [InlineData("original")]
    [InlineData("原始")]
    public void HexComparisonLabelKeepsMeasuredSpaceBeforeBytes(string comparisonLabel)
    {
        const long address = 0x1234_5678;
        var viewport = new HexViewportControl
        {
            ComparisonRowLabel = comparisonLabel,
            Snapshot = new HexViewportSnapshot(
                HexViewportCapabilityProfile.ReportDiff,
                "reported-output",
                documentLength: address + 1,
                startAddress: address,
                rows:
                [
                    new HexViewportRow(
                        address,
                        [new HexViewportCell(address, 0xAA, 0x55, HexViewportCellDecoration.DataChange)]),
                ],
                selectedAddress: null,
                showComparisonRows: true),
        };
        var window = new Window
        {
            Width = 900,
            Height = 120,
            Content = viewport,
            RequestedThemeVariant = ThemeVariant.Light,
        };
        window.Show();
        try
        {
            Render();
            Assert.True(viewport.TryGetCellBounds(address, out Rect firstByte));
            FontFamily fontFamily = Assert.IsType<FontFamily>(
                viewport.FindResource("NfcTechnicalFontFamily"),
                exactMatch: false);
            var label = new FormattedText(
                $"0x{address:X6}  {comparisonLabel}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily, FontStyle.Normal, FontWeight.SemiBold),
                13,
                Brushes.Black);

            Assert.True(
                firstByte.Left >= 4 + label.Width + 8,
                $"First byte starts at {firstByte.Left:0.##}, before the complete label ends at {4 + label.Width:0.##}.");
        }
        finally
        {
            window.Close();
        }
    }

    private static void OpenChanges(Window window, MainWindowViewModel shell)
    {
        shell.Reports.ShowReportCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        TabControl tabs = Assert.Single(
            window.GetVisualDescendants().OfType<TabControl>(),
            item => item.Classes.Contains("reportTabs"));
        tabs.SelectedItem = Assert.Single(
            tabs.Items.OfType<TabItem>(),
            item => Equals(item.Header, shell.Text.ReportTabChanges));
        Dispatcher.UIThread.RunJobs();
    }

    private static string CreateReplayableRangeReport()
    {
        JsonNode root = JsonNode.Parse(ReportJsonSamples.ReplaceWithManyOutputDifferences(
            count: 14,
            sectionCount: 1,
            reviewEvery: 7))!;
        JsonNode difference = root["OutputDifferences"]!.AsArray()[0]!;
        byte[] before = new byte[48];
        byte[] after = new byte[48];
        after[0] = 0x11;
        after[1] = 0x22;
        after[2] = 0x33;
        after[3] = 0x44;
        difference["BeforeSha256"] = Hash(before.AsSpan(0, 4));
        difference["AfterSha256"] = Hash(after.AsSpan(0, 4));
        difference["Replay"] = new JsonObject
        {
            ["Range"] = new JsonObject
            {
                ["Start"] = 0,
                ["EndExclusive"] = before.Length,
                ["Length"] = before.Length,
            },
            ["BeforeBytes"] = Convert.ToBase64String(before),
            ["AfterBytes"] = Convert.ToBase64String(after),
            ["BeforeSha256"] = Hash(before),
            ["AfterSha256"] = Hash(after),
        };
        return root.ToJsonString();
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static Rect BoundsIn(Control control, Visual ancestor)
    {
        return new Rect(Assert.IsType<Point>(control.TranslatePoint(default, ancestor)), control.Bounds.Size);
    }

    private static void SaveFrame(Window window, string fileName)
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        _ = Directory.CreateDirectory(outputDirectory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(outputDirectory, fileName));
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
