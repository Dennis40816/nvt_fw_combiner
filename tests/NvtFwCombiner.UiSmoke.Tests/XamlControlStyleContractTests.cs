using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regression coverage for shared Avalonia visual-control contracts.</summary>
public sealed class XamlControlStyleContractTests
{
    /// <summary>Ensures badge alignment and raw-text scrolling remain centralized.</summary>
    [Fact]
    public void SharedControlStylesDefineTheBadgeAndReadOnlyRawContracts()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");

        Assert.Contains("Selector=\"Label.reportBadge\"", styles, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Center\"", styles, StringComparison.Ordinal);
        Assert.Contains("VerticalContentAlignment\" Value=\"Center\"", styles, StringComparison.Ordinal);
        Assert.Contains("MinHeight\" Value=\"22\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBox.readOnlyRaw\"", styles, StringComparison.Ordinal);
        Assert.Contains("IsReadOnly\" Value=\"True\"", styles, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Auto\"", styles, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", styles, StringComparison.Ordinal);
    }

    /// <summary>Ensures the Report raw payload uses the shared read-only text control.</summary>
    [Fact]
    public void ReportRawPayloadUsesTheSharedReadOnlyTextBox()
    {
        string rawTemplate = ReadPresentationFile("Resources/MainWindowReportAuditTemplates.axaml");
        Assert.Contains("<TextBox", rawTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes=\"readOnlyRaw\"", rawTemplate, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LoadedReportJson, Mode=OneWay}\"", rawTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer MaxHeight=\"320\"", rawTemplate, StringComparison.Ordinal);
    }

    /// <summary>Ensures application resources expose the shared control style library to all views.</summary>
    [Fact]
    public void SharedControlStyleLibraryIsIncludedByTheApplication()
    {
        string application = ReadPresentationFile("App.axaml");
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");

        Assert.Contains("Styles/MainWindowControlStyles.axaml", application, StringComparison.Ordinal);
        Assert.Contains("<Label", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes=\"slotBadge\"", slotCard, StringComparison.Ordinal);
    }

    private static string ReadPresentationFile(string relativePath)
    {
        return File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Presentation.Avalonia", relativePath));
    }
}
