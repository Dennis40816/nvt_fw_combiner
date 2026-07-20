using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regression coverage for the owner-required spatial layout contract.</summary>
public sealed class SpaciousPanelTests
{
    /// <summary>Locks output and report blocks to one padded, spaced control contract.</summary>
    [Fact]
    public void OutputAndReportBlocksUseTheSpaciousPanelContract()
    {
        _ = new SpaciousPanel();
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string sharedTemplates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string reportPanels = ReadPresentationFile("Resources/MainWindowReportPanels.axaml");

        Assert.Contains("Selector=\"views|SpaciousPanel\"", styles, StringComparison.Ordinal);
        Assert.Contains("Property=\"Padding\" Value=\"18,16,20,18\"", styles, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Spacing=\"{DynamicResource NfcSpace16}\" />", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"views|SpaciousPanel.compact\"", styles, StringComparison.Ordinal);
        Assert.Contains("Property=\"Padding\" Value=\"4,4,8,4\"", styles, StringComparison.Ordinal);
        Assert.Contains("NfcSpace8", styles, StringComparison.Ordinal);
        Assert.Equal(4, sharedTemplates.Split("<views:SpaciousPanel", StringSplitOptions.None).Length - 1);
        Assert.Contains("<views:SpaciousPanel Classes=\"compact\"", reportPanels, StringComparison.Ordinal);
        Assert.DoesNotContain("Padding=\"18,16\"", sharedTemplates, StringComparison.Ordinal);
    }

    private static string ReadPresentationFile(string relativePath)
    {
        return File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Presentation.Avalonia", relativePath));
    }
}
