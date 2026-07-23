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
        string expandedRailAction = ExtractStyle(styles, "Button.railAction:pointerover");
        string focusedRailAction = ExtractStyle(styles, "Button.railAction:focus-visible");
        string primaryHover = ExtractStyle(styles, "Button.primary:pointerover");
        string primaryHoverPresenter = ExtractStyle(
            styles,
            "Button.primary:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string primaryPresenter = ExtractStyle(
            styles,
            "Button.primary /template/ ContentPresenter#PART_ContentPresenter");
        string primaryText = ExtractStyle(styles, "Button.primary TextBlock");
        string primaryRailText = ExtractStyle(styles, "Button.primaryRailAction TextBlock");
        string railIconSlot = ExtractStyle(styles, "Border.railActionIconSlot");
        string railIcon = ExtractStyle(styles, "Path.railActionIcon");
        string outputRailIcon = ExtractStyle(styles, "Button.outputRailAction Path.railActionIcon");
        string primaryRailIcon = ExtractStyle(styles, "Button.primaryRailAction Path.railActionIcon");

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
        Assert.Contains("HorizontalAlignment\" Value=\"Center\"", actionLabel, StringComparison.Ordinal);
        Assert.Contains("Margin\" Value=\"16,0,10,0\"", actionLabel, StringComparison.Ordinal);
        Assert.Contains("DoubleTransition Property=\"Opacity\" Duration=\"0:0:0.12\"", actionLabel, StringComparison.Ordinal);
        Assert.Contains("Width\" Value=\"136\"", expandedRailAction, StringComparison.Ordinal);
        Assert.Contains("Width\" Value=\"136\"", focusedRailAction, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector=\"Button.outputRailAction:pointerover\"", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector=\"Button.buildRailAction:pointerover\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction:pointerover TextBlock.railActionLabel\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction:focus-visible TextBlock.railActionLabel\"", styles, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", focusPresenter, StringComparison.Ordinal);
        Assert.Contains("Transitions\" Value=\"{x:Null}\"", reducedMotionAction, StringComparison.Ordinal);
        Assert.Contains("Transitions\" Value=\"{x:Null}\"", reducedMotionPresenter, StringComparison.Ordinal);
        Assert.Contains("Transitions\" Value=\"{x:Null}\"", reducedMotionLabel, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primaryHover, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", primaryHoverPresenter, StringComparison.Ordinal);
        Assert.Contains("TextElement.Foreground\" Value=\"{DynamicResource NfcSurfaceBrush}", primaryPresenter, StringComparison.Ordinal);
        Assert.Contains("TextElement.Foreground\" Value=\"{DynamicResource NfcSurfaceBrush}", primaryHoverPresenter, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primaryText, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primaryRailText, StringComparison.Ordinal);
        Assert.Contains("Width\" Value=\"44\"", railIconSlot, StringComparison.Ordinal);
        Assert.Contains("Height\" Value=\"44\"", railIconSlot, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment\" Value=\"Center\"", railIconSlot, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment\" Value=\"Center\"", railIconSlot, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment\" Value=\"Center\"", railIcon, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment\" Value=\"Center\"", railIcon, StringComparison.Ordinal);
        Assert.DoesNotContain("Margin", railIcon, StringComparison.Ordinal);
        Assert.Contains("Stretch\" Value=\"Uniform\"", railIcon, StringComparison.Ordinal);
        Assert.Contains("StrokeThickness\" Value=\"1.8\"", railIcon, StringComparison.Ordinal);
        Assert.Contains("Fill\" Value=\"{DynamicResource NfcTextBrush}\"", outputRailIcon, StringComparison.Ordinal);
        Assert.Contains("Stroke\" Value=\"Transparent\"", outputRailIcon, StringComparison.Ordinal);
        Assert.Contains("Fill\" Value=\"{DynamicResource NfcSurfaceBrush}\"", primaryRailIcon, StringComparison.Ordinal);
        Assert.Contains("Stroke\" Value=\"Transparent\"", primaryRailIcon, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction:pointerover", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction:pressed", styles, StringComparison.Ordinal);
        Assert.Equal(2, shell.Split("Classes=\"railAction primaryRailAction buildRailAction\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, shell.Split("Classes.reducedMotion=\"{Binding IsReducedMotionEnabled}\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("BuildActionTip", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildActionTip", replaceSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip.Tip=\"{Binding Text.BuildActionLabel}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderTransform", railAction + railPresenter + primaryAction + actionLabel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.LatestOutputActionLabel}\"", shell, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding Text.LatestOutputOpenFolderLabel}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.LatestOutputActionLabel}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Pick(\"View file\", \"檢視檔案\")", ReadPresentationFile("ViewModels/ShellTextResources.Localized.cs"), StringComparison.Ordinal);
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
        Assert.Equal("StackPanel", rail.Name.LocalName);
        Assert.Null(rail.Attribute("ZIndex"));
        Assert.Equal("Right", (string?)rail.Attribute("HorizontalAlignment"));
        Assert.Equal("Bottom", (string?)rail.Attribute("VerticalAlignment"));
        Assert.Equal("0,0,24,16", (string?)rail.Attribute("Margin"));
        Assert.Equal("Vertical", (string?)rail.Attribute("Orientation"));
        Assert.Equal("{DynamicResource NfcSpace8}", (string?)rail.Attribute("Spacing"));
        Assert.Equal("{Binding IsCompositionActionRailVisible}", (string?)rail.Attribute("IsHitTestVisible"));
        Assert.Equal(
            ["BuildMergeButton_OnClick", "BuildReplaceButton_OnClick"],
            handlers);
        Assert.Equal(
            2,
            shell.Descendants().Count(element =>
                ((string?)element.Attribute("Click"))?.StartsWith("Build", StringComparison.Ordinal) == true));

        XElement folder = Assert.Single(
            rail.Descendants(),
            element => (string?)element.Attribute("Command") == "{Binding RevealFileCommand}");
        Assert.Equal("railAction outputRailAction", (string?)folder.Attribute("Classes"));
        Assert.NotNull(folder.Attribute("AutomationProperties.Name"));
        Assert.NotNull(folder.Attribute("AutomationProperties.HelpText"));
        Assert.Null(folder.Attribute("ToolTip.Tip"));
        _ = AssertRailContentUsesFixedIconSlot(folder);

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
            XElement icon = AssertRailContentUsesFixedIconSlot(button);
            Assert.Equal("M4 8.5H11V5L16 10L11 15V11.5H4Z", (string?)icon.Attribute("Data"));
        });
    }

    private static XElement AssertRailContentUsesFixedIconSlot(XElement button)
    {
        XElement content = Assert.Single(button.Elements());
        Assert.Equal("Grid", content.Name.LocalName);
        Assert.Equal("*,44", (string?)content.Attribute("ColumnDefinitions"));
        XElement label = Assert.Single(content.Elements(), element => element.Name.LocalName == "TextBlock");
        Assert.Equal("0", (string?)label.Attribute("Grid.Column"));
        Assert.Equal("railActionLabel", (string?)label.Attribute("Classes"));
        XElement iconSlot = Assert.Single(content.Elements(), element => element.Name.LocalName == "Border");
        Assert.Equal("1", (string?)iconSlot.Attribute("Grid.Column"));
        Assert.Equal("railActionIconSlot", (string?)iconSlot.Attribute("Classes"));
        XElement icon = Assert.Single(iconSlot.Elements(), element => element.Name.LocalName == "Path");
        Assert.Equal("18", (string?)icon.Attribute("Width"));
        Assert.Equal("18", (string?)icon.Attribute("Height"));
        Assert.Equal("railActionIcon", (string?)icon.Attribute("Classes"));
        return icon;
    }
}
