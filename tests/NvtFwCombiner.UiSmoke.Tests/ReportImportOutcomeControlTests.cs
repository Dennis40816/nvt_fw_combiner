using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Incomplete imports expose an unknown outcome and exact read-only raw evidence.</summary>
public sealed class ReportImportOutcomeControlTests
{
    /// <summary>The production Summary and Raw controls stay usable in both languages and themes.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, null)]
    [InlineData(false, true, "warning")]
    [InlineData(true, false, "error")]
    [InlineData(true, true, null)]
    public async Task UnknownOutcomeUsesQuestionMarkAndPreservesReadOnlyRaw(bool dark, bool chinese, string? severity)
    {
        using var workspace = TempWorkspace.Create("unknown-report-controls");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
            string json = severity is null ? "\r\n{\"Issues\":null}\t\r\n" :
                "{\"Issues\":[{\"Code\":\"recorded\",\"Message\":\"Original issue\",\"Severity\":\"" + severity + "\"}]}";
            shell.Reports.LoadReportJson(json, "incomplete.json");
            shell.Reports.ShowReportCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Grid icon = Assert.Single(window.GetVisualDescendants().OfType<Grid>(),
                item => AutomationProperties.GetName(item) == shell.Reports.LoadedReport.OutcomeAccessibilityLabel);
            TextBlock mark = Assert.Single(icon.Children.OfType<TextBlock>());
            Assert.Equal("?", mark.Text);
            Assert.True(mark.IsEffectivelyVisible);
            Border background = Assert.Single(icon.Children.OfType<Border>(), item => item.IsVisible);
            Assert.True(window.TryFindResource("NfcTextSecondaryBrush", window.ActualThemeVariant, out object? neutralBrush));
            Assert.Equal(neutralBrush, background.Background);
            Assert.Equal(chinese ? "未知" : "Unknown", shell.Reports.LoadedReport.OutcomeTitle);
            TextBlock title = Assert.Single(icon.GetVisualParent()!.GetVisualDescendants().OfType<TextBlock>(),
                item => item.Classes.Contains("cardTitle") && item.Text == shell.Reports.LoadedReport.OutcomeTitle);
            Assert.True(title.IsEffectivelyVisible);
            Assert.DoesNotContain("error", title.Classes);
            if (severity is not null)
            {
                Assert.Equal("Original issue", Assert.Single(shell.Reports.LoadedReport.Issues).Detail);
            }
            Assert.False(Assert.Single(shell.Reports.ReportHistoryEntries).IsSuccess);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
                using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(directory, $"unknown-report-{dark}-{chinese}.png"));
            }
            TabControl tabs = Assert.Single(window.GetVisualDescendants().OfType<TabControl>(),
                item => item.Classes.Contains("reportTabs"));
            tabs.SelectedItem = Assert.Single(tabs.Items.OfType<TabItem>(),
                item => Equals(item.Header, shell.Text.ReportTabRaw));
            Dispatcher.UIThread.RunJobs();
            TextBox raw = Assert.Single(window.GetVisualDescendants().OfType<TextBox>(),
                item => item.Classes.Contains("readOnlyRaw"));
            Assert.True(raw.IsReadOnly);
            Assert.True(raw.IsEffectivelyVisible);
            Assert.Equal(json, raw.Text);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }
}
