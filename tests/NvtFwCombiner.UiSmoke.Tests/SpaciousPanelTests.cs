using Avalonia;
using Avalonia.Controls;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regression coverage for the owner-required spatial layout contract.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class SpaciousPanelTests
{
    /// <summary>Locks output and report blocks to one padded, spaced control contract.</summary>
    [Fact]
    public void OutputAndReportBlocksUseTheSpaciousPanelContract()
    {
        var panel = new SpaciousPanel();
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string sharedTemplates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string reportPanels = ReadPresentationFile("Resources/MainWindowReportPanels.axaml");
        string workflowTemplates = ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml");
        string outputTemplates = sharedTemplates + workflowTemplates;

        Assert.Contains("Selector=\"views|SpaciousPanel\"", styles, StringComparison.Ordinal);
        Assert.Equal(new Thickness(18, 16, 20, 18), panel.Padding);
        Assert.DoesNotContain("Property=\"Padding\" Value=\"18,16,20,18\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"views|SpaciousPanel.compact\"", styles, StringComparison.Ordinal);
        Assert.Contains("Property=\"Padding\" Value=\"4,4,8,4\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"views|SpaciousPanel.firmwareSlotGroupSurface\"", styles, StringComparison.Ordinal);
        Assert.Contains("Property=\"BorderThickness\" Value=\"2\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"ItemsControl.spaciousList\"", styles, StringComparison.Ordinal);
        Assert.Contains("Spacing=\"{DynamicResource NfcSpace8}\"", styles, StringComparison.Ordinal);
        Assert.Equal(2, outputTemplates.Split("<views:SpaciousPanel>", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, workflowTemplates.Split("Classes=\"spaciousList\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(
            "<ItemsControl Classes=\"spaciousList\" ItemTemplate=\"{StaticResource MemoryCoverageSegmentListTemplate}\"",
            outputTemplates,
            StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceSelectedCoverageItems}\"", workflowTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceBaseCoverageItems}\"", workflowTemplates, StringComparison.Ordinal);
        Assert.Contains("Spacing=\"{DynamicResource NfcSpace16}\"", outputTemplates, StringComparison.Ordinal);
        Assert.Contains("<views:SpaciousPanel Classes=\"compact\"", reportPanels, StringComparison.Ordinal);
        Assert.Contains(
            "<DataTemplate x:Key=\"FirmwareSlotGroupTemplate\" DataType=\"vm:FirmwareSlotGroupViewModel\">\n  <views:SpaciousPanel Classes=\"compact firmwareSlotGroupSurface\">",
            sharedTemplates,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryCoverageGroupTemplate", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains(
            "<ItemsControl Classes=\"spaciousList\" HorizontalAlignment=\"Stretch\" ItemContainerTheme=\"{StaticResource StretchContentPresenterTheme}\" ItemsSource=\"{Binding ReplaceSlotGroups}\">",
            workflowTemplates,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Padding=\"18,16\"", sharedTemplates, StringComparison.Ordinal);
    }

    /// <summary>Ensures the container measures and retains its real visual child.</summary>
    [Fact]
    public void SpaciousPanelMeasuresItsChildInsidePadding()
    {
        var child = new Border { Width = 80, Height = 40 };
        var panel = new SpaciousPanel
        {
            Child = child,
            Padding = new Thickness(4, 5, 6, 7),
        };

        panel.Measure(new Size(200, 200));

        Assert.Same(child, panel.Child);
        Assert.Equal(new Size(90, 52), panel.DesiredSize);
    }

    /// <summary>The shared memory bar consumes available width without a fixed-width Viewbox.</summary>
    [Fact]
    public void ProportionalStackPanelArrangesChildrenByWeightAtAvailableWidth()
    {
        var first = new Border { Height = 20 };
        var second = new Border { Height = 20 };
        ProportionalStackPanel.SetWeight(first, 1d);
        ProportionalStackPanel.SetWeight(second, 3d);
        var panel = new ProportionalStackPanel
        {
            Children = { first, second },
        };

        panel.Measure(new Size(400, 20));
        panel.Arrange(new Rect(0, 0, 400, 20));

        Assert.Equal(400, panel.Bounds.Width);
        Assert.Equal(100, first.Bounds.Width);
        Assert.Equal(300, second.Bounds.Width);
        Assert.Equal(100, second.Bounds.X);
    }

    private static string ReadPresentationFile(string relativePath)
    {
        return File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Presentation.Avalonia", relativePath));
    }
}
