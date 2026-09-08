using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Protected section purpose and source stay separate in the shared presentation.</summary>
public sealed class MemorySourcePresentationTests
{
    /// <summary>Both row variants and the tooltip render localized purpose and source without overlap.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CustomerInformationShowsSectionAndSourceInSharedTemplates(bool dark, bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var segment = new MemoryCoverageSegmentViewModel(
            "0x37000-0x37FFF (len 0x1000)", "DP BIN",
            "customer-info. Compiled operations: copy-dp (Sequence 0).",
            MemoryCoverageFillRole.Neutral, 10,
            disposition: MemoryWorkflowDisposition.WillWrite,
            text: text, addressRangeLabel: "0x37000-0x37FFF", lengthLabel: "len 0x1000",
            compactDetail: text.GetMemoryPlanDetail(MemoryPlanDetailKind.ProtectedCustomerInformationFromDp),
            contentRole: MemoryContentRole.CustomerInformation);
        string title = chinese ? "客戶資訊" : "Customer information";
        string source = chinese ? "來源：DP BIN" : "Source: DP BIN";
        Assert.Equal(title, segment.DisplayTitle);
        Assert.Equal(source, segment.SourceCaption);
        Assert.Contains(title, segment.AccessibleDetail, StringComparison.Ordinal);
        Assert.Contains(source, segment.AccessibleDetail, StringComparison.Ordinal);
        Assert.Equal(MemoryCoverageFillRole.Neutral, segment.FillRole);
        Assert.False(segment.UsesKeptPattern);
        ThemeVariant theme = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var uri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
        var resources = new ResourceInclude(uri) { Source = uri };
        var stack = new StackPanel { Spacing = 16 };
        string[] templates = ["MemoryCoveragePlainSegmentListTemplate", "MemoryCoverageSegmentListTemplate", "MemoryCoverageTooltipTemplate"];
        foreach (string key in templates)
        {
            Assert.True(resources.TryGetResource(key, theme, out object? resource));
            stack.Children.Add(new ContentControl { Content = segment, ContentTemplate = Assert.IsType<IDataTemplate>(resource, exactMatch: false) });
        }
        var window = new Window { Width = 380, Height = 560, RequestedThemeVariant = theme, DataContext = new { Text = text, IsReducedMotionEnabled = false }, Content = new Border { Padding = new Thickness(16), Child = stack } };
        window.Resources.MergedDictionaries.Add(resources);
        var styleUri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowStyles.axaml");
        window.Styles.Add(new StyleInclude(styleUri) { Source = styleUri });
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            foreach (ContentControl card in stack.Children.OfType<ContentControl>())
            {
                TextBlock heading = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == title);
                TextBlock caption = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == source);
                TextBlock range = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == segment.RangeLabel || block.Text == segment.AddressRangeLabel);
                Point headingAt = heading.TranslatePoint(default, card)!.Value;
                Point captionAt = caption.TranslatePoint(default, card)!.Value;
                Assert.True(captionAt.Y >= headingAt.Y + heading.Bounds.Height);
                Assert.True(captionAt.Y >= range.TranslatePoint(default, card)!.Value.Y + range.Bounds.Height);
                Assert.True(heading.Bounds.Width > 0 && caption.Bounds.Width > 0);
                Assert.True(captionAt.X >= 0 && captionAt.X + caption.Bounds.Width <= card.Bounds.Width + 0.5);
                Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == segment.CompactDetail && block.IsEffectivelyVisible);
            }
            ContentControl tooltip = (ContentControl)stack.Children[2];
            Assert.Contains(tooltip.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == text.RangeLabel && block.IsEffectivelyVisible);
            Assert.Contains(tooltip.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == text.ResultLabel && block.IsEffectivelyVisible);
            Border row = stack.Children[0].GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("memoryCoverageLinkedRow"));
            Assert.Contains(title, AutomationProperties.GetName(row), StringComparison.Ordinal);
            Assert.True(row.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.True(segment.Interaction.IsActive);
            using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? destination = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrEmpty(destination))
            {
                _ = Directory.CreateDirectory(destination);
                using FileStream stream = File.Create(Path.Combine(destination, $"memory-source-{(dark ? "dark" : "light")}-{(chinese ? "zh" : "en")}.png"));
                frame.Save(stream);
            }
        }
        finally { window.Close(); }
    }

    /// <summary>Unchanged roles retain their original heading and do not gain a blank caption row.</summary>
    [Theory]
    [InlineData(MemoryContentRole.Dp)]
    [InlineData(MemoryContentRole.Tp)]
    [InlineData(MemoryContentRole.Reserved)]
    public void OtherRolesRetainSourceHeadingWithoutExtraCaption(MemoryContentRole role)
    {
        var segment = new MemoryCoverageSegmentViewModel("0x00000-0x0000F", "Existing source", "Existing detail", MemoryCoverageFillRole.Neutral, 10, contentRole: role);
        Assert.Equal("Existing source", segment.DisplayTitle);
        Assert.False(segment.HasSourceCaption);
        Assert.Empty(segment.SourceCaption);
    }
}
