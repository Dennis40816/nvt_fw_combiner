using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps Build actions compact and responsive without hover popups obscuring the first click.</summary>
    [Fact]
    public void BuildActionsUseCompactAnimatedToolbarStyleWithoutTooltips()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string replaceSelection = ReadPresentationFile("Views/ReplaceSelectionModal.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string toolbarAction = ExtractStyle(styles, "Button.toolbarAction");
        string toolbarPresenter = ExtractStyle(
            styles,
            "Button.toolbarAction /template/ ContentPresenter#PART_ContentPresenter");

        Assert.Contains("MinHeight\" Value=\"34\"", toolbarAction, StringComparison.Ordinal);
        Assert.Contains("MinWidth\" Value=\"80\"", toolbarAction, StringComparison.Ordinal);
        Assert.Contains("Padding\" Value=\"10,6\"", toolbarAction, StringComparison.Ordinal);
        Assert.Contains("BrushTransition Property=\"Background\" Duration=\"0:0:0.12\"", toolbarPresenter, StringComparison.Ordinal);
        Assert.Contains("BrushTransition Property=\"BorderBrush\" Duration=\"0:0:0.12\"", toolbarPresenter, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.toolbarAction:pointerover", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.toolbarAction:pressed", styles, StringComparison.Ordinal);
        Assert.Equal(2, shell.Split("Classes=\"toolbarAction\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("BuildActionTip", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildActionTip", replaceSelection, StringComparison.Ordinal);
    }

    /// <summary>Build stays in the fixed bottom-right rail established by the stable v0.9.10 shell.</summary>
    [Fact]
    public void CompositionBuildActionsStayInTheBottomRightRail()
    {
        var shell = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement rail = Assert.Single(
            shell.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "CompositionBuildActionRail");
        string[] handlers =
        [
            .. rail
                .Descendants()
                .Select(element => (string?)element.Attribute("Click"))
                .Where(handler => handler?.StartsWith("Build", StringComparison.Ordinal) == true)
                .Cast<string>(),
        ];

        Assert.Equal("4", (string?)rail.Attribute("Grid.RowSpan"));
        Assert.Equal("Right", (string?)rail.Attribute("HorizontalAlignment"));
        Assert.Equal("Bottom", (string?)rail.Attribute("VerticalAlignment"));
        Assert.Equal("0,0,24,24", (string?)rail.Attribute("Margin"));
        Assert.Equal(
            ["BuildMergeButton_OnClick", "BuildReplaceButton_OnClick"],
            handlers);
        Assert.Equal(
            2,
            shell.Descendants().Count(element =>
                ((string?)element.Attribute("Click"))?.StartsWith("Build", StringComparison.Ordinal) == true));
    }
}
