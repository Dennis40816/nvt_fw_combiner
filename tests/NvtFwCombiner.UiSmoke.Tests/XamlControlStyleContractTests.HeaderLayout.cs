using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Mode bindings retain the wide-header position and the approved compact Replace second row.</summary>
    [Fact]
    public void WorkflowModeSelectorsStayAtV0915HeaderRightPosition()
    {
        var shell = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        XElement replaceHeader = Assert.Single(shell.Descendants(), element =>
            HasXamlName(element, "ReplacePageHeader"));
        XElement mergeHeader = Assert.Single(shell.Descendants(), element =>
            HasXamlName(element, "MergePageHeader"));

        Assert.Equal("*,Auto", (string?)replaceHeader.Attribute("ColumnDefinitions"));
        Assert.Equal("*,Auto", (string?)mergeHeader.Attribute("ColumnDefinitions"));
        Assert.Equal("Auto,Auto", (string?)replaceHeader.Attribute("RowDefinitions"));
        Assert.Null(mergeHeader.Attribute("RowDefinitions"));

        XElement replaceMode = Assert.Single(replaceHeader.Descendants(), element =>
            element.Name.LocalName == "ComboBox" &&
            ((string?)element.Attribute("SelectedItem"))?.Contains(
                "SelectedReplaceMode",
                StringComparison.Ordinal) == true);
        XElement mergeMode = Assert.Single(mergeHeader.Descendants(), element =>
            element.Name.LocalName == "ComboBox" &&
            ((string?)element.Attribute("SelectedItem"))?.Contains(
                "SelectedMergeMode",
                StringComparison.Ordinal) == true);

        Assert.Equal(
            "{Binding SelectedReplaceMode, Mode=TwoWay}",
            (string?)replaceMode.Attribute("SelectedItem"));
        Assert.Equal(
            "{Binding SelectedMergeMode, Mode=TwoWay}",
            (string?)mergeMode.Attribute("SelectedItem"));

        // CtrlRamSelectorLayoutTests also measures the real 980/1440 layouts and resize anchors.
        XElement replaceActions = Assert.Single(replaceMode.Ancestors(), element => element.Parent == replaceHeader);
        Assert.Equal("replaceHeaderActions", (string?)replaceActions.Attribute("Classes"));
        Assert.Equal("Center", (string?)replaceActions.Attribute("VerticalAlignment"));
        XElement wideStyle = Assert.Single(shell.Descendants(), element =>
            element.Name.LocalName == "Style" && (string?)element.Attribute("Selector") == "Grid.replaceHeaderActions" &&
            element.Parent?.Name.LocalName != "ContainerQuery");
        Assert.Equal("1", (string?)Assert.Single(wideStyle.Elements(), element =>
            (string?)element.Attribute("Property") == "Grid.Column").Attribute("Value"));
        XElement compact = Assert.Single(shell.Descendants(), element => element.Name.LocalName == "ContainerQuery" &&
            (string?)element.Attribute("Name") == "ReplaceContent");
        Assert.Equal("max-width:700", (string?)compact.Attribute("Query"));
        XElement compactActions = Assert.Single(compact.Elements(), element =>
            (string?)element.Attribute("Selector") == "Grid.replaceHeaderActions");
        foreach ((string property, string value) in new[] { ("Grid.Column", "0"), ("Grid.Row", "1"), ("Grid.ColumnSpan", "2"), ("HorizontalAlignment", "Left") })
        {
            Assert.Equal(value, (string?)Assert.Single(compactActions.Elements(), element =>
                (string?)element.Attribute("Property") == property).Attribute("Value"));
        }
        AssertModeContainerOccupiesHeaderRightColumn(mergeMode, mergeHeader);
    }

    /// <summary>Shell headers stay readable at compact width and vertically aligned while startup is running.</summary>
    [Fact]
    public void StartupPreparationHeaderCentersLabelsAndRightAlignsPercentage()
    {
        var shell = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        XElement host = Assert.Single(shell.Descendants(), element =>
            HasXamlName(element, "OptionalPreloadStatusHost"));
        XElement[] centeredElements =
        [
            Assert.Single(host.Descendants(), element =>
                (string?)element.Attribute("Text") ==
                "{ReflectionBinding $parent[Window].DataContext.Text.PreloadStatusTitle}"),
            Assert.Single(host.Descendants(), element =>
                (string?)element.Attribute("Text") == "{ReflectionBinding SummaryStage.PositionLabel}"),
            Assert.Single(host.Descendants(), element =>
                (string?)element.Attribute("Grid.Column") == "2" &&
                element.Name.LocalName == "StackPanel" &&
                (string?)element.Attribute("Orientation") == "Horizontal"),
            Assert.Single(host.Descendants(), element =>
                (string?)element.Attribute("Text") == "{ReflectionBinding SummaryStage.ProgressLabel}"),
        ];

        Assert.All(centeredElements, element =>
            Assert.Equal("Center", (string?)element.Attribute("VerticalAlignment")));
        XElement percentage = centeredElements[^1];
        Assert.Equal("44", (string?)percentage.Attribute("MinWidth"));
        Assert.Equal("Right", (string?)percentage.Attribute("TextAlignment"));
    }

    private static void AssertModeContainerOccupiesHeaderRightColumn(
        XElement modeSelector,
        XElement header)
    {
        XElement modeContainer = Assert.Single(modeSelector.Ancestors(), element =>
            element.Parent == header);
        Assert.Equal("Grid", modeContainer.Name.LocalName);
        Assert.Equal("1", (string?)modeContainer.Attribute("Grid.Column"));
        Assert.Null(modeContainer.Attribute("Grid.Row"));
        Assert.Equal("Center", (string?)modeContainer.Attribute("VerticalAlignment"));
    }
}
