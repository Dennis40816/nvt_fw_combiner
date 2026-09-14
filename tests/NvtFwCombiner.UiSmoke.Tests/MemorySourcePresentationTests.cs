using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Protected section purpose and source stay separate in the shared presentation.</summary>
public sealed class MemorySourcePresentationTests
{
    /// <summary>Only an explicit untouched initializer may be described as initialization.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnassignedSourceDoesNotClaimFillWhenWritesExistOrInitializationIsUnknown(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        CompositionOperation[] operations = [CompositionOperation.FillRange("fill", 0, "output", new ByteRange(0, 4), 0x00, OverlapPolicy.Reject, "declared operation")];
        Assert.Null(operations[0].SourceSpaceId);
        foreach ((bool isInitialization, string value, string detail) in new[] { text.GetMemoryUnassignedSource(0xFF, operations), text.GetMemoryUnassignedSource(null, []) })
        {
            Assert.False(isInitialization);
            Assert.Equal(chinese ? "未指定" : "Not assigned", value);
            Assert.Equal(chinese ? "此範圍未指定輸入來源。" : "No input source is assigned to this range.", detail);
        }
        Assert.Equal((true, "0xA5", chinese ? "目前計畫沒有寫入此範圍。" : "No writes planned for this range."), text.GetMemoryUnassignedSource(0xA5, []));
        Assert.Equal(chinese ? "保留區" : "Reserved", text.GetMemoryContentTitle(MemoryContentRole.Reserved, CtrlRamRegionRole.Other));
    }

    /// <summary>Both row variants and the tooltip render localized purpose and source without overlap.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CustomerInformationShowsSectionAndSourceInSharedTemplates(bool dark, bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        CompositionOperation[] operations = [CompositionOperation.CopyRange("copy-dp", 0, "dp", new ByteRange(0, 0x1000), "output", new ByteRange(0x37000, 0x1000), OverlapPolicy.Reject, "test copy")];
        var segment = new MemoryCoverageSegmentViewModel(
            "0x37000-0x37FFF (len 0x1000)", "DP BIN",
            text.FormatMemoryLayoutTechnicalDetail("customer-info", 0xFF,
                [CompositionOperation.CopyRange("copy-dp", 0, "dp", new ByteRange(0, 0x1000), "output", new ByteRange(0x37000, 0x1000), OverlapPolicy.Reject, "test copy")]),
            MemoryCoverageFillRole.Neutral, 10,
            disposition: MemoryWorkflowDisposition.WillWrite,
            text: text, addressRangeLabel: "0x37000-0x37FFF", lengthLabel: "len 0x1000",
            compactDetail: text.GetMemoryPlanDetail(MemoryPlanDetailKind.ProtectedCustomerInformationFromDp),
            contentRole: MemoryContentRole.CustomerInformation, rangeStart: 0x37000, rangeEndExclusive: 0x38000,
            processingFacts: text.FormatMemoryLayoutTechnicalFacts("customer-info", 0xFF, operations));
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
        var window = new Window { Width = 380, Height = 1100, RequestedThemeVariant = theme, DataContext = new { Text = text, IsReducedMotionEnabled = true }, Content = new Border { Padding = new Thickness(16), Child = stack } };
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
                if (card != stack.Children[2])
                {
                    Expander disclosure = Assert.Single(card.GetVisualDescendants().OfType<Expander>());
                    Assert.False(disclosure.IsExpanded);
                    // The native Expander header owns keyboard activation.
                    global::Avalonia.Controls.Primitives.ToggleButton toggle = Assert.Single(disclosure.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ToggleButton>());
                    Assert.InRange(toggle.Bounds.Height, 28, 30);
                    TextBlock disclosureLabel = Assert.Single(toggle.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == (chinese ? "技術細節" : "Technical details"));
                    TextBlock targetAddressLabel = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == text.MemoryTargetAddressLabel);
                    Assert.Equal(14d, disclosureLabel.FindResource("NfcFontSize14"));
                    Assert.Equal(14, Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(),
                        block => block.Text == title).FontSize);
                    Assert.Equal(14, disclosureLabel.FontSize);
                    Assert.Equal(11, targetAddressLabel.FontSize);
                    Assert.InRange(Math.Abs(disclosureLabel.TranslatePoint(default, card)!.Value.X - targetAddressLabel.TranslatePoint(default, card)!.Value.X), 0, 0.5);
                    Assert.DoesNotContain(disclosure.GetVisualDescendants().OfType<Border>(), border => border.Name == "MemoryTechnicalSeparator" && border.IsEffectivelyVisible);
                    Assert.True(toggle.Focus(NavigationMethod.Tab));
                    Assert.True(toggle.BorderThickness.Top > 0);
                    window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                    window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(disclosure.IsExpanded);
                    Border separator = Assert.Single(disclosure.GetVisualDescendants().OfType<Border>(), border => border.Name == "MemoryTechnicalSeparator");
                    Assert.True(separator.IsEffectivelyVisible);
                    Assert.True(separator.TranslatePoint(default, card)!.Value.Y >= toggle.TranslatePoint(default, card)!.Value.Y + toggle.Bounds.Height);
                    TextBlock size = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "0x1000 (4 KiB)");
                    TextBlock address = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == segment.AddressRangeLabel);
                    Assert.InRange(Math.Abs(size.TranslatePoint(default, card)!.Value.X - address.TranslatePoint(default, card)!.Value.X), 0, 0.5);
                    Border marker = Assert.Single(card.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("memoryCoverageMarker"));
                    Assert.Equal(10, marker.Bounds.Width);
                    Assert.Equal(10, marker.Bounds.Height);
                    Assert.NotNull(marker.Background);
                    foreach (TextBlock block in card.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible))
                    {
                        Point point = block.TranslatePoint(default, card)!.Value;
                        Assert.True(point.X >= 0 && point.X + block.Bounds.Width <= card.Bounds.Width + 0.5, block.Text);
                    }
                }
                TextBlock heading = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == title);
                TextBlock caption = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == (card == stack.Children[2] ? source : "DP BIN"));
                TextBlock range = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == segment.RangeLabel || block.Text == segment.AddressRangeLabel);
                Point headingAt = heading.TranslatePoint(default, card)!.Value;
                Point captionAt = caption.TranslatePoint(default, card)!.Value;
                Assert.True(captionAt.Y >= headingAt.Y + heading.Bounds.Height);
                Assert.True(captionAt.Y >= range.TranslatePoint(default, card)!.Value.Y + range.Bounds.Height);
                Assert.True(heading.Bounds.Width > 0 && caption.Bounds.Width > 0);
                Assert.True(captionAt.X >= 0 && captionAt.X + caption.Bounds.Width <= card.Bounds.Width + 0.5);
                Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == segment.CompactDetail && block.IsEffectivelyVisible);
            }
            // Present the approved collapsed/expanded comparison at the actual 348px card width.
            Assert.Single(stack.Children[0].GetVisualDescendants().OfType<Expander>()).IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            ContentControl tooltip = (ContentControl)stack.Children[2];
            Assert.Contains(tooltip.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == text.RangeLabel && block.IsEffectivelyVisible);
            Assert.Contains(tooltip.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == text.ResultLabel && block.IsEffectivelyVisible);
            TextBlock technical = Assert.Single(tooltip.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == segment.Detail);
            Point technicalAt = technical.TranslatePoint(default, window)!.Value;
            Assert.True(technicalAt.Y + technical.Bounds.Height <= window.Bounds.Height);
            Assert.True(technicalAt.X + technical.Bounds.Width <= window.Bounds.Width);
            Assert.Contains(chinese ? "（寫入前）" : "(before writes)", technical.Text, StringComparison.Ordinal);
            Border row = stack.Children[0].GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("memoryCoverageLinkedRow"));
            Assert.Contains(title, AutomationProperties.GetName(row), StringComparison.Ordinal);
            Assert.True(row.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.True(segment.Interaction.IsActive);
            window.Focusable = true;
            Assert.True(window.Focus());
            Assert.False(segment.Interaction.IsActive);
            Assert.True(stack.Children.Remove(tooltip));
            window.Height = 650;
            // Flush layout/compositor work and let the existing Fluent chevron reach its end state.
            for (int tick = 0; tick < 4; tick++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Thread.Sleep(100);
            }
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

    /// <summary>Generic processing labels remain readable in both existing Plan row templates.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void GenericPostprocessPlanLabelsFitSharedRows(bool dark, bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        ThemeVariant theme = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var uri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
        var resources = new ResourceInclude(uri) { Source = uri };
        var stack = new StackPanel { Spacing = 16 };
        MemoryPlanActionKind[] actions = [MemoryPlanActionKind.ReplaceAndCrc, MemoryPlanActionKind.Postbuild];
        string[] keys = ["ReplaceMemoryMapRowTemplate", "MergeMemoryMapRowTemplate"];
        for (int index = 0; index < keys.Length; index++)
        {
            Assert.True(resources.TryGetResource(keys[index], theme, out object? resource));
            var row = new MemoryMapRowViewModel("0x37000-0x37FFF (len 0x1000)", new(MemoryPlanSourceKind.BaseFirmware), actions[index], new(MemoryPlanSourceKind.DpBin), "Declared external processing; operation identity retained.", text);
            stack.Children.Add(new ContentControl { Content = row, ContentTemplate = Assert.IsType<IDataTemplate>(resource, exactMatch: false) });
        }
        var window = new Window { Width = 360, Height = 240, RequestedThemeVariant = theme, Content = new Border { Padding = new Thickness(16), Child = stack } };
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
                MemoryMapRowViewModel row = Assert.IsType<MemoryMapRowViewModel>(card.Content);
                TextBlock action = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == row.ActionLabel);
                Assert.DoesNotContain("CRC", action.Text, StringComparison.Ordinal);
                Assert.True(action.IsEffectivelyVisible && action.Bounds.Width > 0);
                foreach (TextBlock block in card.GetVisualDescendants().OfType<TextBlock>())
                {
                    Point point = block.TranslatePoint(default, window)!.Value;
                    Assert.True(point.X >= 0 && point.X + block.Bounds.Width <= window.Bounds.Width);
                    Assert.True(point.Y >= 0 && point.Y + block.Bounds.Height <= window.Bounds.Height);
                }
                Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == row.FlowLabel);
            }
            using global::Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? destination = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrEmpty(destination))
            {
                _ = Directory.CreateDirectory(destination);
                using FileStream stream = File.Create(Path.Combine(destination, $"memory-plan-{(dark ? "dark" : "light")}-{(chinese ? "zh" : "en")}.png"));
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
