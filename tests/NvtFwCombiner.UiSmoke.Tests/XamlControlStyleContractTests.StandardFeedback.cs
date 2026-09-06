using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Production report templates expose the concise summary and preserve visible original diagnostics.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ReportFeedbackTemplatesRenderSummaryAndOriginalDiagnostic(bool dark, bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var presentation = new ReportPresentationViewModel(() => ShellTextResources.For(language), static () => { });
        presentation.LoadReportJson(ReportInputFeedbackTests.Json(ReportInputFeedbackTests.Report([
            new("input.artifact.read-failed", "Original read failure: dp-input (IOException).", "inspect-dp"),
            new("TP_UNIFORM_CONTENT_WARNING", "Original warning: repeated-byte placeholder and cannot be used.", "inspect-tp", "warning"),
            new("CUSTOM_INFO", "An informational diagnostic.", "inspect", "info"),
        ])), "rendered.json");
        var content = new ContentControl { Content = presentation };
        var host = new Window
        {
            Width = 760,
            Height = 900,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = content,
        };
        string[] resources = ["MainWindowReportTemplates.axaml", "MainWindowReportChangeTemplates.axaml", "MainWindowReportPanels.axaml"];
        foreach (string resource in resources)
        {
            var uri = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/Resources/{resource}");
            host.Resources.MergedDictionaries.Add(new ResourceInclude(uri) { Source = uri });
        }
        host.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri) { Source = ProductionMainWindowStylesUri });
        host.Styles.Add(new StyleInclude(ProductionVisualStylesUri) { Source = ProductionVisualStylesUri });
        Assert.True(host.TryGetResource("ReportSummaryPanelTemplate", host.RequestedThemeVariant, out object? summaryTemplate));
        content.ContentTemplate = Assert.IsType<IDataTemplate>(summaryTemplate, exactMatch: false);
        host.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            TextBlock summary = Assert.Single(content.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == presentation.LoadedReport.SummaryIssueDescriptions);
            Assert.True(summary.IsEffectivelyVisible);
            Assert.True(summary.Bounds.Height > 0);
            Assert.Equal(TextWrapping.Wrap, summary.TextWrapping);
            Assert.All(summary.TextLayout.TextLines, static line => Assert.False(line.HasCollapsed));
            Assert.Contains(chinese ? "無法讀取輸入檔" : "Input file cannot be read", summary.Text, StringComparison.Ordinal);
            Assert.Contains(chinese ? "警告:" : "Warning:", summary.Text, StringComparison.Ordinal);

            Assert.True(host.TryGetResource("ReportIssueRowTemplate", host.RequestedThemeVariant, out object? issueTemplate));
            content.ContentTemplate = Assert.IsType<IDataTemplate>(issueTemplate, exactMatch: false);
            content.Content = presentation.LoadedReport.Issues[1];
            Dispatcher.UIThread.RunJobs();
            TextBlock raw = Assert.Single(content.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == presentation.LoadedReport.Issues[1].CodeBlock);
            Assert.True(raw.IsEffectivelyVisible);
            Assert.True(raw.Bounds.Height > 0);
            Assert.Contains("cannot be used", raw.Text, StringComparison.Ordinal);
            Assert.Equal(TextWrapping.Wrap, raw.TextWrapping);
            Assert.Contains(content.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == presentation.LoadedReport.Issues[1].Detail);
            Border warning = Assert.Single(content.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("statusNotice"));
            Assert.Equal(new Thickness(3, 0, 0, 0), warning.BorderThickness);
            Assert.Contains(content.GetVisualDescendants().OfType<Label>(),
                label => label.IsEffectivelyVisible && Equals(label.Content, chinese ? "警告" : "Warning"));
            Assert.Contains(content.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == (chinese ? "診斷碼" : "Code"));
            TextBlock codeFact = Assert.Single(content.GetVisualDescendants().OfType<TextBlock>(), block => block.IsEffectivelyVisible && block.Text == "TP_UNIFORM_CONTENT_WARNING");
            TextBlock stepFact = Assert.Single(content.GetVisualDescendants().OfType<TextBlock>(), block => block.IsEffectivelyVisible && block.Text == "inspect-tp");
            Assert.InRange(Math.Abs(codeFact.TranslatePoint(default, content)!.Value.Y - stepFact.TranslatePoint(default, content)!.Value.Y), 0, 2);
            content.Content = presentation.LoadedReport.Issues[2];
            Dispatcher.UIThread.RunJobs();
            Border info = Assert.Single(content.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("statusNotice"));
            Assert.Contains("info", info.Classes);
            Assert.DoesNotContain("warning", info.Classes);
            Assert.DoesNotContain("error", info.Classes);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>Real status controls expose the same understandable warning through pointer, keyboard and automation.</summary>
    [AvaloniaTheory]
    [InlineData(620, false, false)]
    [InlineData(620, true, true)]
    [InlineData(1362, false, true)]
    [InlineData(1362, true, false)]
    public void StandardFeedbackIsReadableAndReachable(double width, bool dark, bool chinese)
    {
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(
            StandardMergeFeedbackTests.Status("TP_UNIFORM_CONTENT_WARNING", AuthoringSlotLifecycle.Warning), chinese);
        var card = new FirmwareSlotCard
        {
            Width = width,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
            DataContext = slot,
            BrowseLabel = "Browse",
            ClearSelectionLabel = "Clear",
        };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = width;
        host.Height = 900;
        host.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        host.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            ToggleButton state = Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>(),
                control => control.Classes.Contains("slotStateAction"));
            TextBlock detail = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(),
                control => control.Classes.Contains("slotStateDetail"));
            Assert.Contains("warning", state.Classes);
            Assert.DoesNotContain("verified", state.Classes);
            Assert.Equal(slot.SemanticStateAutomationText, AutomationProperties.GetName(state));
            _ = Assert.IsType<ToolTip>(ToolTip.GetTip(state));
            Assert.Contains(chinese ? "不會阻擋 Build" : "Does not block Build",
                AutomationProperties.GetName(state), StringComparison.Ordinal);

            Point point = state.TranslatePoint(new Point(state.Bounds.Width / 2, state.Bounds.Height / 2), host)!.Value;
            host.MouseMove(point, RawInputModifiers.None);
            host.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            host.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(slot.IsSemanticStateDetailExpanded);
            Assert.True(detail.IsVisible);
            Assert.Equal(slot.SemanticStateAutomationText, detail.Text);
            Assert.Equal(TextWrapping.Wrap, detail.TextWrapping);
            Assert.NotEmpty(detail.TextLayout.TextLines);
            Assert.All(detail.TextLayout.TextLines, static line => Assert.False(line.HasCollapsed));
            Assert.True(detail.Bounds.Width <= card.Bounds.Width);

            host.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            host.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Dispatcher.UIThread.RunJobs();
            Assert.False(slot.IsSemanticStateDetailExpanded);
            Button browse = card.FindControl<Button>("BrowseButton")!;
            Assert.True(browse.Focus());
            Assert.True(state.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            Assert.True(ToolTip.GetIsOpen(state));
            Assert.False(slot.BlocksBuild);
        }
        finally
        {
            host.Close();
        }
    }
}
