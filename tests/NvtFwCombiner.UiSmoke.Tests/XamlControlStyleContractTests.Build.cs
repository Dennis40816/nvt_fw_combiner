using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps the bottom dock compact and responsive without hover popups obscuring the first click.</summary>
    [Fact]
    public void BuildActionsUseCompactAccessibleDockStyleWithoutTooltips()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string replaceSelection = ReadPresentationFile("Views/ReplaceSelectionModal.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string dockAction = ExtractStyle(styles, "Button.dockAction");
        string dockPresenter = ExtractStyle(
            styles,
            "Button.dockAction /template/ ContentPresenter#PART_ContentPresenter");
        string primaryAction = ExtractStyle(styles, "Button.primaryDockAction");
        string focusPresenter = ExtractStyle(
            styles,
            "Button.dockAction:focus-visible /template/ ContentPresenter#PART_ContentPresenter");
        string reducedMotionPresenter = ExtractStyle(
            styles,
            "Button.dockAction.reducedMotion /template/ ContentPresenter#PART_ContentPresenter");

        Assert.Contains("MinHeight\" Value=\"40\"", dockAction, StringComparison.Ordinal);
        Assert.Contains("MinWidth\" Value=\"88\"", primaryAction, StringComparison.Ordinal);
        Assert.Contains("Padding\" Value=\"12,7\"", primaryAction, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBrush", primaryAction, StringComparison.Ordinal);
        Assert.Contains("BrushTransition Property=\"Background\" Duration=\"0:0:0.12\"", dockPresenter, StringComparison.Ordinal);
        Assert.Contains("BrushTransition Property=\"BorderBrush\" Duration=\"0:0:0.12\"", dockPresenter, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", focusPresenter, StringComparison.Ordinal);
        Assert.Contains("Transitions\" Value=\"{x:Null}\"", reducedMotionPresenter, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.dockAction:pointerover", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.dockAction:pressed", styles, StringComparison.Ordinal);
        Assert.Equal(2, shell.Split("Classes=\"dockAction primaryDockAction\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, shell.Split("Classes.reducedMotion=\"{Binding IsReducedMotionEnabled}\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("BuildActionTip", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildActionTip", replaceSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip.Tip=\"{Binding Text.BuildActionLabel}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderTransform", dockAction + dockPresenter + primaryAction, StringComparison.Ordinal);
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

        Assert.Equal("4", (string?)rail.Attribute("Grid.Row"));
        Assert.Null(rail.Attribute("Grid.RowSpan"));
        Assert.Equal("Right", (string?)rail.Attribute("HorizontalAlignment"));
        Assert.Equal("Bottom", (string?)rail.Attribute("VerticalAlignment"));
        Assert.Equal("0,0,24,16", (string?)rail.Attribute("Margin"));
        XElement actions = Assert.Single(rail.Elements());
        Assert.Equal("Horizontal", (string?)actions.Attribute("Orientation"));
        Assert.Equal("{DynamicResource NfcSpace8}", (string?)actions.Attribute("Spacing"));
        Assert.Equal(
            ["BuildMergeButton_OnClick", "BuildReplaceButton_OnClick"],
            handlers);
        Assert.Equal(
            2,
            shell.Descendants().Count(element =>
                ((string?)element.Attribute("Click"))?.StartsWith("Build", StringComparison.Ordinal) == true));

        XElement folder = Assert.Single(
            rail.Descendants(),
            element => (string?)element.Attribute("Click") == "OpenLatestOutputFolderButton_OnClick");
        Assert.Equal("dockAction dockIconAction", (string?)folder.Attribute("Classes"));
        Assert.NotNull(folder.Attribute("ToolTip.Tip"));

        XElement[] buildButtons =
        [
            .. rail.Descendants().Where(element =>
                ((string?)element.Attribute("Click"))?.StartsWith("Build", StringComparison.Ordinal) == true),
        ];
        Assert.All(buildButtons, button =>
        {
            Assert.Equal("dockAction primaryDockAction", (string?)button.Attribute("Classes"));
            Assert.NotNull(button.Attribute("AutomationProperties.HelpText"));
            Assert.Null(button.Attribute("ToolTip.Tip"));
        });
    }
}
