using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keep two-pixel defaults and only the approved compact Report, memory and support outlines.</summary>
    [Fact]
    public void FullPerimeterThinOutlinesUseTwoPixels()
    {
        XElement[] onePixelOutlines =
        [
            .. ReadPresentationXamlFiles()
                .Select(XDocument.Parse)
                .SelectMany(static document => document.Descendants())
                .Where(static element =>
                    (string?)element.Attribute("BorderThickness") == "1" ||
                    (element.Name.LocalName == "Setter" &&
                     (string?)element.Attribute("Property") == "BorderThickness" &&
                     (string?)element.Attribute("Value") == "1")),
        ];

        // v1.1.4 Report/issue-card references approve thin compact outlines;
        // do not exempt whole files or all future one-pixel borders.
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement[] approvedOutlines =
        [
            ApprovedOutline("Resources/MainWindowReportTemplates.axaml", "Setter", "Selector", "Label.reportBadge"),
            ApprovedOutline("Resources/MainWindowReportPanels.axaml", "Border", "IsVisible", "{Binding LoadedReport.HasPrimaryIssue}"),
            ApprovedOutline("Resources/MainWindowReportHistoryTemplates.axaml", "Border", "Classes", "reportResult"),
            ApprovedOutline("Resources/MainWindowReportAuditTemplates.axaml", "Button", xaml + "Name", "RawReportCopyButton"),
            ApprovedOutline("Views/FirmwareSlotCard.axaml", "Border", "IsVisible", "{Binding !HasIssueCard}"),
            ApprovedOutline("Views/RunReportsTable.axaml", "Border", xaml + "Name", "RunReportsTableSurface"),
            ApprovedOutline("Views/OutputDeliveryConfirmationModal.axaml", "Border", xaml + "Name", "BuildWarningsPanel"),
            ApprovedOutline("Views/IssueDetailsCard.axaml", "Setter", "Selector", "Border.icDetailCard"),
            ApprovedOutline("Views/IssueDetailsCard.axaml", "Setter", "Selector", "Border.icDetailHeroIcon"),
            ApprovedOutline("Views/IssueDetailsCard.axaml", "Label", "Classes", "compactBadge neutralBadge"),
            ApprovedOutline("Resources/MainWindowReportChangeTemplates.axaml", "Setter", xaml + "Key", "ReportHexDiffRangeCardTheme"),
            ApprovedOutline("Resources/MainWindowPageTemplates.axaml", "Border", "IsVisible", "{Binding Settings.SupportMatrix.HasRows}"),
            Assert.IsType<XElement>(Assert.Single(XDocument.Parse(ReadPresentationFile("Resources/MainWindowPageTemplates.axaml")).Descendants(),
                element => (string?)element.Attribute(xaml + "Name") == "SupportMatrixCatalogDetails").Parent),
            ApprovedTemplateOutline("MemoryCoverageSegmentListTemplate"),
            ApprovedTemplateOutline("MemoryCoveragePlainSegmentListTemplate"),
        ];
        Assert.Equal(
            approvedOutlines.Select(static outline => outline.ToString()).Order(StringComparer.Ordinal),
            onePixelOutlines.Select(static outline => outline.ToString()).Order(StringComparer.Ordinal));

        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        XElement semanticActionStyle = Assert.Single(
            XDocument.Parse(buttonStyles).Descendants(),
            static element =>
                element.Name.LocalName == "Style" &&
                (string?)element.Attribute("Selector") == "Button.semanticAction, RadioButton.settingsNavItem");
        Assert.Contains(
            semanticActionStyle.Elements(),
            static element =>
                (string?)element.Attribute("Property") == "BorderThickness" &&
                (string?)element.Attribute("Value") == "2");
        Assert.Contains("BorderThickness=\"0,1,0,0\"", string.Concat(ReadPresentationXamlFiles()), StringComparison.Ordinal);
    }

    private static XElement ApprovedOutline(string path, string kind, XName attribute, string value)
    {
        return Assert.Single(XDocument.Parse(ReadPresentationFile(path)).Descendants(), element =>
            element.Name.LocalName == kind &&
            (kind == "Setter"
                ? (string?)element.Attribute("Property") == "BorderThickness" &&
                  (string?)element.Attribute("Value") == "1" &&
                  (string?)element.Parent?.Attribute(attribute) == value
                : (string?)element.Attribute("BorderThickness") == "1" &&
                  (string?)element.Attribute(attribute) == value));
    }

    private static XElement ApprovedTemplateOutline(string key)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement template = Assert.Single(XDocument.Parse(ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml")).Descendants(),
            element => element.Name.LocalName == "DataTemplate" && (string?)element.Attribute(xaml + "Key") == key);
        return Assert.Single(template.Elements(), element => element.Name.LocalName == "Border" &&
            (string?)element.Attribute("BorderThickness") == "1" &&
            (string?)element.Attribute("Classes") == "surface memoryInfoRow memoryCoverageLinkedRow");
    }

    /// <summary>Every shared dropdown keeps a stable two-pixel outline before and during keyboard focus.</summary>
    [AvaloniaFact]
    public void ComboBoxRestingAndKeyboardFocusUseTwoPixelOutline()
    {
        var comboBox = new ComboBox
        {
            Width = 180,
            ItemsSource = new[] { "Standard", "AB Code" },
            SelectedIndex = 0,
        };
        var host = new Window
        {
            Width = 240,
            Height = 100,
            Content = comboBox,
        };
        host.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });

        host.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AssertComboBoxAndTemplateUseTwoPixelOutline(comboBox);

            _ = comboBox.Focus(NavigationMethod.Tab, KeyModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(comboBox.IsKeyboardFocusWithin);
            AssertComboBoxAndTemplateUseTwoPixelOutline(comboBox);
        }
        finally
        {
            host.Close();
        }
    }

    private static void AssertComboBoxAndTemplateUseTwoPixelOutline(ComboBox comboBox)
    {
        Assert.Equal(new Thickness(2), comboBox.BorderThickness);
        Assert.Contains(
            comboBox.GetVisualDescendants().OfType<Border>(),
            border => border.Bounds is { Width: > 0, Height: > 0 } &&
                border.BorderThickness == new Thickness(2));
    }
}
