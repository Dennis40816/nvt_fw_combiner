using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Message Center follows the approved wide activity-history reference and stays accessible.</summary>
    [Fact]
    public void MessageCenterAndBuildBlockerAffordancesAreAccessible()
    {
        string shellText = ReadPresentationFile("MainWindow.axaml");
        var shell = XDocument.Parse(shellText);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        _ = Assert.Single(shell.Descendants(), element =>
            element.Attribute(x + "Name")?.Value == "MessageCenterModalHost");
        Assert.Contains("Command=\"{Binding MessageCenter.OpenCommand}\"", shellText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Command=\"{Binding MessageCenter.OpenSystemInformationCommand}\"",
            shellText,
            StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(
            shellText,
            "behaviors:FocusToolTipBehavior.IsEnabled=\"True\""));
        Assert.Equal(2, CountOccurrences(shellText, "Classes=\"warningSurface buildBlockerBadge\""));
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding MessageCenter.MessageCenterAccessibleName}\"",
            shellText,
            StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding MergeBuildBlockerText}\"", shellText, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding ReplaceBuildBlockerText}\"", shellText, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding MergeBuildBlockerText}\"", shellText, StringComparison.Ordinal);
        Assert.DoesNotContain("IsChecked=\"{Binding MessageCenter", shellText, StringComparison.Ordinal);

        string modal = ReadPresentationFile("Views/MessageCenterModal.axaml");
        Assert.Contains("KeyboardNavigation.TabNavigation=\"Cycle\"", modal, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"MessageCenterModal_OnKeyDown\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MessageCenterSurface\"", modal, StringComparison.Ordinal);
        Assert.Contains("Width=\"1420\"", modal, StringComparison.Ordinal);
        Assert.Contains("Height=\"824\"", modal, StringComparison.Ordinal);
        var modalDocument = XDocument.Parse(modal);
        XElement body = Assert.Single(modalDocument.Descendants(), element =>
            element.Attribute(x + "Name")?.Value == "MessageCenterBody");
        Assert.Equal("238,*", body.Attribute("ColumnDefinitions")?.Value);
        _ = Assert.Single(modalDocument.Descendants(), element =>
            element.Attribute(x + "Name")?.Value == "MessageCenterNavigationRail");
        _ = Assert.Single(modalDocument.Descendants(), element =>
            element.Attribute(x + "Name")?.Value == "SystemActivityRoot");
        _ = Assert.Single(modalDocument.Descendants(), element =>
            element.Attribute(x + "Name")?.Value == "SystemActivityTimelineViewport");

        XElement[] reportCards =
        [
            .. modalDocument.Descendants()
                .Where(element => HasClass(element, "messageCenterReportCard")),
        ];
        Assert.Equal(2, reportCards.Length);
        Assert.All(reportCards, card =>
            _ = Assert.Single(card.Descendants(), element => element.Name.LocalName == "Button"));
        Assert.Contains("IsVisible=\"{Binding IsRunReportsSelected}\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsSystemInformationSelected}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Click=\"ExportDiagnosticsButton_OnClick\"", modal, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ActivityItems}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:DataType=\"vm:MessageCenterActivityItem\"", modal, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleText}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ToggleDebugActivityCommand}\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsImportantActivitySelected, Mode=OneWay}\"", modal,
            StringComparison.Ordinal);
        Assert.DoesNotContain("systemFactCard", modal, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(modal, "Command=\"{Binding CloseCommand}\""));
        Assert.Contains("Content=\"{Binding RefreshActionLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", modal, StringComparison.Ordinal);
        string activityStyles = ReadPresentationFile("Styles/MessageCenterStyles.axaml");
        string controlStyles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        Assert.Contains("Border.activityRow:pointerover", activityStyles, StringComparison.Ordinal);
        Assert.Contains("Border.activityRow:focus-visible", activityStyles, StringComparison.Ordinal);
        Assert.Contains("Border.buildBlockerBadge:focus-visible", controlStyles, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBrush", activityStyles, StringComparison.Ordinal);

        string codeBehind = ReadPresentationFile("Views/MessageCenterModal.axaml.cs");
        Assert.Contains("PropertyChanged += MessageCenterModal_OnPropertyChanged", codeBehind,
            StringComparison.Ordinal);
        Assert.Contains("e.Property == IsVisibleProperty", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FocusInitialControl();", codeBehind, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        return source.Split(value, StringSplitOptions.None).Length - 1;
    }

    private static bool HasClass(XElement element, string className)
    {
        return element.Attribute("Classes")?.Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(className, StringComparer.Ordinal) == true;
    }
}
