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

    /// <summary>Ensures Hex Editor uses the shared safe-save and immutable-reference interaction contracts.</summary>
    [Fact]
    public void HexEditorUsesConfirmedSaveAndReadOnlyReferenceRows()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");

        Assert.Contains("Gesture=\"Ctrl+S\"", shell, StringComparison.Ordinal);
        Assert.Contains("RequestHexEditorSaveCommand", shell, StringComparison.Ordinal);
        Assert.Contains("Classes.editing=\"{Binding IsEditing}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"hexReferenceCell\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.hexReferenceCell\"", styles, StringComparison.Ordinal);
        Assert.Contains("TextChanged=\"HexByteEditBox_OnTextChanged\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("TextChanged=\"HexRangeValue_OnTextChanged\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("input:InputMethod.IsInputMethodEnabled=\"False\"", hexEditor, StringComparison.Ordinal);
    }

    /// <summary>Ensures the Hex Editor inspector remains compact and keeps its range selector usable.</summary>
    [Fact]
    public void HexEditorInspectorUsesCompactTopAlignedLayout()
    {
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");

        Assert.Contains("ColumnDefinitions=\"*,312\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"1\" Classes=\"compactSurface\" VerticalAlignment=\"Top\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,*\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Top\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved region", hexEditor, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Keeps each byte value centered on the same column center as its hexadecimal header.</summary>
    [Fact]
    public void HexEditorByteCellsCenterTheirContentUnderTheHeader()
    {
        string visualStyles = ReadPresentationFile("Styles/MainWindowVisualStyles.axaml");

        int hexCellStyle = visualStyles.IndexOf("Selector=\"Border.hexCell TextBlock\"", StringComparison.Ordinal);
        Assert.NotEqual(-1, hexCellStyle);
        string hexCellSection = visualStyles[hexCellStyle..visualStyles.IndexOf("</Style>", hexCellStyle, StringComparison.Ordinal)];
        Assert.Contains("HorizontalAlignment\" Value=\"Center\"", hexCellSection, StringComparison.Ordinal);
        Assert.Contains("TextAlignment\" Value=\"Center\"", hexCellSection, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment\" Value=\"Center\"", hexCellSection, StringComparison.Ordinal);
    }

    /// <summary>Prevents a read-mostly viewport from recreating one menu and editor per visible byte.</summary>
    [Fact]
    public void HexEditorUsesReadMostlyCellsAndOneSharedContextMenu()
    {
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string codeBehind = ReadPresentationFile("Views/HexEditorPanel.axaml.cs");

        Assert.Contains("<ContentControl", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"hexByteCell\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("PointerPressed=\"HexByte_OnPointerPressed\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button.ContextMenu>", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAuthoringDisplayVisible", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEditorVisible", hexEditor, StringComparison.Ordinal);
        Assert.Contains("new ContextMenu()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_hexByteContextMenu.Open(target)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("KeepAsciiHexOnly", codeBehind, StringComparison.Ordinal);
        Assert.Contains("IsPageActive", codeBehind, StringComparison.Ordinal);
        Assert.Contains("!_workspace.IsPageActive", codeBehind, StringComparison.Ordinal);
        Assert.Contains("IsPageActive: true, HasMoreRows: true", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>Ensures the desktop shell and distributed executable use the dedicated compact app icon.</summary>
    [Fact]
    public void PresentationUsesTheDedicatedApplicationIcon()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string project = ReadPresentationFile("NvtFwCombiner.Presentation.Avalonia.csproj");
        string icon = RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "Assets",
            "AppIcon.ico");

        Assert.Contains("Icon=\"/Assets/AppIcon.ico\"", shell, StringComparison.Ordinal);
        Assert.Contains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.True(File.Exists(icon));
        Assert.NotEqual(0, new FileInfo(icon).Length);
    }

    /// <summary>Ensures the local fixture is Debug-only and never changes the default landing page.</summary>
    [Fact]
    public void DebugHexFixturePreloadsWithoutForcingHexEditorNavigation()
    {
        string debugDemo = ReadPresentationFile("MainWindow.DebugDemo.cs");
        string startup = ReadPresentationFile("MainWindow.Report.cs");

        Assert.StartsWith("#if DEBUG", debugDemo, StringComparison.Ordinal);
        Assert.Contains("SetSlotFile(WorkbenchSlotIds.ReplaceBase, basePath)", debugDemo, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowHexEditorCommand.Execute", debugDemo, StringComparison.Ordinal);
        Assert.Contains("#if DEBUG", startup, StringComparison.Ordinal);
    }

    /// <summary>Prevents repeated shell, panel, row, and text property bundles from drifting back into templates.</summary>
    [Fact]
    public void SharedControlStylesOwnTheCommonXamlRoles()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        foreach (string selector in new[]
        {
            "Border.shellBar",
            "Border.homePreviewSurface",
            "Border.roomyPanel",
            "Border.contentPanel",
            "Border.listRow",
            "Border.settingRow",
            "TextBlock.panelTitle",
            "TextBlock.supportingText",
            "TextBlock.infoText",
            "TextBlock.technicalValue",
        })
        {
            Assert.Contains($"Selector=\"{selector}\"", styles, StringComparison.Ordinal);
        }

        string[] legacyPropertyBundles =
        [
            "FontSize=\"13\" FontWeight=\"SemiBold\" Foreground=\"#0F172A\"",
            "FontSize=\"12\" Foreground=\"#64748B\"",
            "FontSize=\"11\" Foreground=\"#64748B\"",
            "FontSize=\"10\" FontWeight=\"SemiBold\" Foreground=\"#64748B\"",
            "FontFamily=\"Cascadia Mono, Consolas\" FontSize=\"12\" Foreground=\"#0F172A\"",
        ];

        foreach (string xaml in ReadPresentationXamlFiles())
        {
            foreach (string bundle in legacyPropertyBundles)
            {
                Assert.DoesNotContain(bundle, xaml, StringComparison.Ordinal);
            }
        }
    }

    private static string ReadPresentationFile(string relativePath)
    {
        return File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Presentation.Avalonia", relativePath));
    }

    private static IEnumerable<string> ReadPresentationXamlFiles()
    {
        string presentationRoot = RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Presentation.Avalonia");
        return Directory.EnumerateFiles(presentationRoot, "*.axaml", SearchOption.AllDirectories)
            .Select(File.ReadAllText);
    }
}
