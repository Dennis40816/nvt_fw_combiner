using System.Xml.Linq;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Mode selectors use new product wording while stable contract tokens remain unchanged.</summary>
    [Fact]
    public void CompositionModeSelectorsUseDisplayOnlyProductNames()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");

        Assert.Equal("Normal", WorkbenchMergeModes.Standard);
        Assert.Equal("General", WorkbenchMergeModes.General);
        Assert.Equal("Standard", WorkbenchModeDisplayConverters.GetDisplayName(WorkbenchMergeModes.Standard));
        Assert.Equal("Customized", WorkbenchModeDisplayConverters.GetDisplayName(WorkbenchMergeModes.General));
        Assert.Equal(2, shell.Split("WorkbenchModeDisplayConverters.DisplayName", StringSplitOptions.None).Length - 1);
    }

    /// <summary>FWConfig Number mismatch uses the shared modal surface with explicit accessible actions.</summary>
    [Fact]
    public void FirmwareNumberMismatchUsesAccessibleConfirmationSurface()
    {
        var modal = XDocument.Parse(ReadPresentationFile("Views/FirmwareNumberMismatchModal.axaml"));
        XElement surface = Assert.Single(modal.Descendants(), element =>
            string.Equals((string?)element.Attribute("Classes"), "modalSurface", StringComparison.Ordinal));
        Assert.Equal("22", (string?)surface.Attribute("Padding"));
        Assert.NotNull(surface.Attribute("AutomationProperties.Name"));

        XElement cancel = Assert.Single(modal.Descendants(), element =>
            string.Equals(
                (string?)element.Attribute("Command"),
                "{Binding DismissFirmwareNumberMismatchCommand}",
                StringComparison.Ordinal));
        XElement accept = Assert.Single(modal.Descendants(), element =>
            string.Equals(
                (string?)element.Attribute("Command"),
                "{Binding AcceptFirmwareNumberMismatchCommand}",
                StringComparison.Ordinal));
        Assert.Equal("secondary", (string?)cancel.Attribute("Classes"));
        Assert.Equal("primary", (string?)accept.Attribute("Classes"));
        Assert.NotNull(cancel.Attribute("AutomationProperties.Name"));
        Assert.NotNull(accept.Attribute("AutomationProperties.Name"));
        Assert.Null(cancel.Attribute("ToolTip.Tip"));
        Assert.Null(accept.Attribute("ToolTip.Tip"));
    }

    /// <summary>Keeps the vertical action rail compact, left-expanding, accessible, and free of hover popups.</summary>
    [Fact]
    public void BuildActionsUseExpandableAccessibleRailStyleWithoutTooltips()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string replaceSelection = ReadPresentationFile("Views/ReplaceSelectionModal.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string railAction = ExtractStyle(styles, "Button.railAction");
        string railPresenter = ExtractStyle(
            styles,
            "Button.railAction /template/ ContentPresenter#PART_ContentPresenter");
        string primaryAction = ExtractStyle(styles, "Button.primaryRailAction");
        string actionLabel = ExtractStyle(styles, "TextBlock.railActionLabel");
        string focusPresenter = ExtractStyle(
            styles,
            "Button.railAction:focus-visible /template/ ContentPresenter#PART_ContentPresenter");
        string reducedMotionAction = ExtractStyle(styles, "Button.railAction.reducedMotion");
        string reducedMotionPresenter = ExtractStyle(
            styles,
            "Button.railAction.reducedMotion /template/ ContentPresenter#PART_ContentPresenter");
        string reducedMotionLabel = ExtractStyle(
            styles,
            "Button.railAction.reducedMotion TextBlock.railActionLabel");
        string globalPressed = ExtractStyle(
            styles,
            "Button:pressed /template/ ContentPresenter#PART_ContentPresenter");
        string primaryHover = ExtractStyle(styles, "Button.primary:pointerover");
        string primaryHoverPresenter = ExtractStyle(
            styles,
            "Button.primary:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string primaryPresenter = ExtractStyle(
            styles,
            "Button.primary /template/ ContentPresenter#PART_ContentPresenter");
        string primaryText = ExtractStyle(styles, "Button.primary TextBlock");
        string primaryRailText = ExtractStyle(styles, "Button.primaryRailAction TextBlock");
        string railIcon = ExtractStyle(styles, "Path.railActionIcon");
        string outputRailIcon = ExtractStyle(styles, "Button.outputRailAction Path.railActionIcon");

        Assert.Contains("Width\" Value=\"44\"", railAction, StringComparison.Ordinal);
        Assert.Contains("Height\" Value=\"44\"", railAction, StringComparison.Ordinal);
        Assert.Contains("ClipToBounds\" Value=\"True\"", railAction, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment\" Value=\"Right\"", railAction, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Stretch\"", railAction, StringComparison.Ordinal);
        Assert.Contains("DoubleTransition Property=\"Width\" Duration=\"0:0:0.16\"", railAction, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBrush", primaryAction, StringComparison.Ordinal);
        Assert.Contains("BrushTransition Property=\"Background\" Duration=\"0:0:0.12\"", railPresenter, StringComparison.Ordinal);
        Assert.Contains("BrushTransition Property=\"BorderBrush\" Duration=\"0:0:0.12\"", railPresenter, StringComparison.Ordinal);
        Assert.Contains("Opacity\" Value=\"0\"", actionLabel, StringComparison.Ordinal);
        Assert.Contains("DoubleTransition Property=\"Opacity\" Duration=\"0:0:0.12\"", actionLabel, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.outputRailAction:pointerover\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.outputRailAction:focus-visible\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.buildRailAction:pointerover\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.buildRailAction:focus-visible\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction:pointerover TextBlock.railActionLabel\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction:focus-visible TextBlock.railActionLabel\"", styles, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", focusPresenter, StringComparison.Ordinal);
        Assert.Contains("Transitions\" Value=\"{x:Null}\"", reducedMotionAction, StringComparison.Ordinal);
        Assert.Contains("Transitions\" Value=\"{x:Null}\"", reducedMotionPresenter, StringComparison.Ordinal);
        Assert.Contains("Transitions\" Value=\"{x:Null}\"", reducedMotionLabel, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", globalPressed, StringComparison.Ordinal);
        Assert.Contains("Opacity\" Value=\"0.84\"", globalPressed, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primaryHover, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", primaryHoverPresenter, StringComparison.Ordinal);
        Assert.Contains("TextElement.Foreground\" Value=\"{DynamicResource NfcSurfaceBrush}", primaryPresenter, StringComparison.Ordinal);
        Assert.Contains("TextElement.Foreground\" Value=\"{DynamicResource NfcSurfaceBrush}", primaryHoverPresenter, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primaryText, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primaryRailText, StringComparison.Ordinal);
        Assert.Contains("Stretch\" Value=\"Uniform\"", railIcon, StringComparison.Ordinal);
        Assert.Contains("StrokeThickness\" Value=\"1.8\"", railIcon, StringComparison.Ordinal);
        Assert.Contains("Fill\" Value=\"{DynamicResource NfcTextBrush}\"", outputRailIcon, StringComparison.Ordinal);
        Assert.Contains("Stroke\" Value=\"Transparent\"", outputRailIcon, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction:pointerover", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction:pressed", styles, StringComparison.Ordinal);
        Assert.Equal(2, shell.Split("Classes=\"railAction primaryRailAction buildRailAction\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, shell.Split("Classes.reducedMotion=\"{Binding IsReducedMotionEnabled}\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("BuildActionTip", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildActionTip", replaceSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip.Tip=\"{Binding Text.BuildActionLabel}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderTransform", railAction + railPresenter + primaryAction + actionLabel, StringComparison.Ordinal);
    }

    /// <summary>Build floats inside the scroll-content row without reserving a full-width bottom band.</summary>
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

        Assert.Equal("3", (string?)rail.Attribute("Grid.Row"));
        Assert.Null(rail.Attribute("Grid.RowSpan"));
        Assert.Equal("1", (string?)rail.Attribute("ZIndex"));
        Assert.Equal("Right", (string?)rail.Attribute("HorizontalAlignment"));
        Assert.Equal("Bottom", (string?)rail.Attribute("VerticalAlignment"));
        Assert.Equal("0,0,24,16", (string?)rail.Attribute("Margin"));
        XElement actions = Assert.Single(rail.Elements());
        Assert.Equal("Vertical", (string?)actions.Attribute("Orientation"));
        Assert.Equal("Right", (string?)actions.Attribute("HorizontalAlignment"));
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
        Assert.Equal("railAction outputRailAction", (string?)folder.Attribute("Classes"));
        Assert.NotNull(folder.Attribute("AutomationProperties.Name"));
        Assert.NotNull(folder.Attribute("AutomationProperties.HelpText"));
        Assert.Null(folder.Attribute("ToolTip.Tip"));
        AssertRailContentKeepsIconInFixedRightColumn(folder);

        XElement[] buildButtons =
        [
            .. rail.Descendants().Where(element =>
                ((string?)element.Attribute("Click"))?.StartsWith("Build", StringComparison.Ordinal) == true),
        ];
        Assert.All(buildButtons, button =>
        {
            Assert.Equal("railAction primaryRailAction buildRailAction", (string?)button.Attribute("Classes"));
            Assert.NotNull(button.Attribute("AutomationProperties.HelpText"));
            Assert.Null(button.Attribute("ToolTip.Tip"));
            AssertRailContentKeepsIconInFixedRightColumn(button);
        });
    }

    private static void AssertRailContentKeepsIconInFixedRightColumn(XElement button)
    {
        XElement content = Assert.Single(button.Elements());
        Assert.Equal("Grid", content.Name.LocalName);
        Assert.Equal("*,Auto", (string?)content.Attribute("ColumnDefinitions"));
        Assert.Equal("{DynamicResource NfcSpace8}", (string?)content.Attribute("ColumnSpacing"));
        XElement icon = Assert.Single(content.Elements(), element => element.Name.LocalName == "Path");
        Assert.Equal("1", (string?)icon.Attribute("Grid.Column"));
    }
}
