using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps shared shell visual tokens complete per theme and resolves every migrated resource reference.</summary>
    [Fact]
    public void SharedThemeTokensHaveUniqueDefinitionsAndOwnMigratedViews()
    {
        string application = ReadPresentationFile("App.axaml");
        string tokens = ReadPresentationFile("Styles/ThemeTokens.axaml");
        Match[] colorDefinitions = ReadThemeTokenDefinitions();
        Match[] shadowDefinitions = ReadThemeShadowTokenDefinitions();
        Match[] cornerRadiusDefinitions = ReadThemeCornerRadiusTokenDefinitions();
        Match[] spacingDefinitions = ReadThemeSpacingTokenDefinitions();
        Match[] fontSizeDefinitions = ReadThemeFontSizeTokenDefinitions();
        Match[] fontFamilyDefinitions = ReadThemeFontFamilyTokenDefinitions();
        var visualKeys = colorDefinitions
            .Concat(shadowDefinitions)
            .Select(static definition => definition.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var definedKeys = colorDefinitions
            .Concat(shadowDefinitions)
            .Concat(cornerRadiusDefinitions)
            .Concat(spacingDefinitions)
            .Concat(fontSizeDefinitions)
            .Concat(fontFamilyDefinitions)
            .Select(static definition => definition.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var themeDocument = XDocument.Parse(tokens);
        var presentation = (XNamespace)"https://github.com/avaloniaui";
        var x = (XNamespace)"http://schemas.microsoft.com/winfx/2006/xaml";
        var themeKeys = themeDocument
            .Root!
            .Element(presentation + "ResourceDictionary.ThemeDictionaries")!
            .Elements(presentation + "ResourceDictionary")
            .ToDictionary(
                dictionary => dictionary.Attribute(x + "Key")!.Value,
                dictionary => dictionary.Elements()
                    .Select(resource => resource.Attribute(x + "Key")!.Value)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        Assert.Contains("<Application.Resources>", application, StringComparison.Ordinal);
        Assert.Contains(
            "<ResourceInclude Source=\"avares://NvtFwCombiner.Presentation.Avalonia/Styles/ThemeTokens.axaml\" />",
            application,
            StringComparison.Ordinal);
        Assert.NotEmpty(colorDefinitions);
        Assert.NotEmpty(shadowDefinitions);
        Assert.NotEmpty(cornerRadiusDefinitions);
        Assert.Equal(["Dark", "Light"], themeKeys.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(themeKeys["Light"].Order(StringComparer.Ordinal), themeKeys["Dark"].Order(StringComparer.Ordinal));
        Assert.Equal(visualKeys.Order(StringComparer.Ordinal), themeKeys["Light"].Order(StringComparer.Ordinal));
        Assert.All(
            colorDefinitions.GroupBy(static definition => definition.Groups["key"].Value, StringComparer.Ordinal),
            static definitions => Assert.Equal(2, definitions.Count()));
        Assert.All(
            shadowDefinitions.GroupBy(static definition => definition.Groups["key"].Value, StringComparer.Ordinal),
            static definitions => Assert.Equal(2, definitions.Count()));
        Assert.Equal(6, spacingDefinitions.Length);
        Assert.Equal(6, fontSizeDefinitions.Length);
        Assert.Equal(2, fontFamilyDefinitions.Length);
        Assert.Equal(colorDefinitions.Length, tokens.Split("<SolidColorBrush", StringSplitOptions.None).Length - 1);
        Assert.Equal(shadowDefinitions.Length, tokens.Split("<BoxShadows", StringSplitOptions.None).Length - 1);
        Assert.Equal(cornerRadiusDefinitions.Length, tokens.Split("<CornerRadius", StringSplitOptions.None).Length - 1);
        Assert.Equal(spacingDefinitions.Length + fontSizeDefinitions.Length, tokens.Split("<x:Double", StringSplitOptions.None).Length - 1);
        Assert.Equal(fontFamilyDefinitions.Length, tokens.Split("<FontFamily", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            visualKeys.Count + cornerRadiusDefinitions.Length + spacingDefinitions.Length + fontSizeDefinitions.Length + fontFamilyDefinitions.Length,
            definedKeys.Count);
        Assert.Contains(
            fontFamilyDefinitions,
            static definition => StringComparer.Ordinal.Equals("NfcUiFontFamily", definition.Groups["key"].Value) &&
                                 StringComparer.Ordinal.Equals(
                                     "fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, Segoe UI",
                                     definition.Groups["value"].Value));
        Assert.Contains(
            fontFamilyDefinitions,
            static definition => StringComparer.Ordinal.Equals("NfcTechnicalFontFamily", definition.Groups["key"].Value) &&
                                 StringComparer.Ordinal.Equals("Cascadia Mono, Consolas", definition.Groups["value"].Value));
        foreach ((string key, string value) in new[]
                 {
                     ("NfcSpace2", "2"),
                     ("NfcSpace4", "4"),
                     ("NfcSpace8", "8"),
                     ("NfcSpace12", "12"),
                     ("NfcSpace16", "16"),
                     ("NfcSpace24", "24"),
                 })
        {
            Assert.Contains(
                spacingDefinitions,
                definition => StringComparer.Ordinal.Equals(key, definition.Groups["key"].Value) &&
                              StringComparer.Ordinal.Equals(value, definition.Groups["value"].Value));
        }
        foreach ((string key, string value) in new[]
                 {
                     ("NfcFontSize10", "10"),
                     ("NfcFontSize11", "11"),
                     ("NfcFontSize12", "12"),
                     ("NfcFontSize13", "13"),
                     ("NfcFontSize14", "14"),
                     ("NfcFontSize16", "16"),
                 })
        {
            Assert.Contains(
                fontSizeDefinitions,
                definition => StringComparer.Ordinal.Equals(key, definition.Groups["key"].Value) &&
                              StringComparer.Ordinal.Equals(value, definition.Groups["value"].Value));
        }

        string[] migratedPaths =
        [
            "MainWindow.axaml",
            "Styles/MainWindowControlStyles.axaml",
            "Styles/MemoryCoverageStyles.axaml",
            "Styles/MainWindowStyles.axaml",
            "Styles/MainWindowButtonStyles.axaml",
            "Styles/MainWindowVisualStyles.axaml",
            "Views/OutputDeliveryConfirmationModal.axaml",
            "Views/FirmwareIcMismatchModal.axaml",
            "Views/ForegroundLoadingSurface.axaml",
            "Views/HexEditorInsertBytesModal.axaml",
            "Views/HexEditorSaveModal.axaml",
            "Views/ReplaceSelectionModal.axaml",
            "Views/WorkflowContextSetupModal.axaml",
            "Resources/MainWindowPageTemplates.axaml",
            "Resources/MainWindowReportAuditTemplates.axaml",
            "Resources/MainWindowReportChangeTemplates.axaml",
            "Resources/MainWindowReportHistoryTemplates.axaml",
            "Resources/MainWindowReportInputTemplates.axaml",
            "Resources/MainWindowReportOperationTemplates.axaml",
            "Resources/MainWindowReportPanels.axaml",
            "Resources/MainWindowReportTemplates.axaml",
            "Resources/MainWindowSharedTemplates.axaml",
            "Resources/MainWindowShellPanels.axaml",
            "Views/FirmwareSlotCard.axaml",
            "Views/GeneralMappingRow.axaml",
            "Views/HexEditorPanel.axaml",
            "Views/ReportCodeBlockView.axaml",
            "Views/ReportModal.axaml",
        ];

        foreach (string path in migratedPaths)
        {
            string content = ReadPresentationFile(path);
            Match[] references = [.. DynamicThemeReferencePattern.Matches(content).Cast<Match>()];
            string[] colorLiterals = [.. ColorLiteralPattern.Matches(content).Select(static literal => literal.Value)];

            Assert.NotEmpty(references);
            Assert.Empty(references
                .Select(static reference => reference.Groups["key"].Value)
                .Except(definedKeys, StringComparer.Ordinal));
            Assert.Empty(colorLiterals);
        }

        Assert.All(
            ReadPresentationXamlFiles().Where(content => !StringComparer.Ordinal.Equals(content, tokens)),
            static content =>
            {
                Assert.DoesNotContain("fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, Segoe UI", content, StringComparison.Ordinal);
                Assert.DoesNotContain("Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC", content, StringComparison.Ordinal);
                Assert.DoesNotContain("Cascadia Mono, Consolas", content, StringComparison.Ordinal);
            });

        var referencedKeys = ReadPresentationXamlFiles()
            .SelectMany(content => DynamicThemeReferencePattern.Matches(content)
                .Cast<Match>()
                .Select(static reference => reference.Groups["key"].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(referencedKeys.Except(definedKeys, StringComparer.Ordinal));
        Assert.Empty(definedKeys.Except(referencedKeys, StringComparer.Ordinal));
    }

    /// <summary>Prevents the common layout scale from drifting into repeated raw XAML literals.</summary>
    [Fact]
    public void CommonLayoutSpacingAndTypographyUseSharedScaleTokens()
    {
        Assert.All(
            ReadPresentationXamlFiles().Where(content => !StringComparer.Ordinal.Equals(
                content,
                ReadPresentationFile("Styles/ThemeTokens.axaml"))),
            static content => Assert.False(
                RawCommonSpacingPattern.IsMatch(content),
                "Common spacing literals must use the shared NfcSpace tokens."));
        Assert.All(
            ReadPresentationXamlFiles().Where(content => !StringComparer.Ordinal.Equals(
                content,
                ReadPresentationFile("Styles/ThemeTokens.axaml"))),
            static content => Assert.False(
                RawCommonFontSizePattern.IsMatch(content),
                "Common font-size literals must use the shared NfcFontSize tokens."));
    }

    /// <summary>Keeps the three Home preview headings on their shared semantic visual role.</summary>
    [Fact]
    public void HomePreviewTitlesUseSharedPreviewTitleRole()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string pages = ReadPresentationFile("Resources/MainWindowPageTemplates.axaml");

        Assert.Contains("Selector=\"TextBlock.previewTitle\"", styles, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"24\" />", ExtractStyle(styles, "TextBlock.previewTitle"), StringComparison.Ordinal);
        Assert.Equal(3, pages.Split("Classes=\"previewTitle\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("FontSize=\"25\"", pages, StringComparison.Ordinal);
    }

    /// <summary>Home typography uses named roles while preserving the approved geometry.</summary>
    [Fact]
    public void HomeTypographyUsesSharedSemanticRolesWithoutLocalOverrides()
    {
        var pages = XDocument.Parse(ReadPresentationFile("Resources/MainWindowPageTemplates.axaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement home = Assert.Single(pages.Descendants(), element =>
            element.Name.LocalName == "DataTemplate" &&
            (string?)element.Attribute(x + "Key") == "HomePageTemplate");
        XElement[] textBlocks = [.. home.Descendants().Where(element => element.Name.LocalName == "TextBlock")];

        Assert.Equal(3, textBlocks.Count(element => HasClass(element, "workflowKicker")));
        Assert.Equal(3, textBlocks.Count(element => HasClass(element, "pageSubtitle")));
        Assert.Equal(7, textBlocks.Count(element => HasClass(element, "workflowActionText")));
        Assert.All(textBlocks, static textBlock =>
        {
            Assert.Null(textBlock.Attribute("FontFamily"));
            Assert.Null(textBlock.Attribute("FontSize"));
            Assert.Null(textBlock.Attribute("FontWeight"));
            Assert.Null(textBlock.Attribute("Foreground"));
        });
    }

    /// <summary>Only dynamic workflow readiness remains visible; Standard keeps slot-owned status and drop behavior.</summary>
    [Fact]
    public void StandardMergeOmitsDuplicatedReadinessStripWhileDynamicModesRetainIt()
    {
        var templates = XDocument.Parse(ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement mergeTemplate = Assert.Single(templates.Descendants(), element =>
            element.Name.LocalName == "DataTemplate" &&
            (string?)element.Attribute(x + "Key") == "MergeModeContentTemplate");

        XElement standard = Assert.Single(mergeTemplate.Descendants(), element =>
            (string?)element.Attribute("IsVisible") == "{Binding IsNormalMergeModeSelected}");
        XElement general = Assert.Single(mergeTemplate.Descendants(), element =>
            (string?)element.Attribute("IsVisible") == "{Binding IsGeneralMergeModeSelected}");
        XElement ab = Assert.Single(mergeTemplate.Descendants(), element =>
            (string?)element.Attribute("IsVisible") == "{Binding IsAbCodeMergeModeSelected}");

        Assert.DoesNotContain(standard.Descendants(), IsMergeReadinessProjection);
        Assert.Contains(general.Descendants(), IsMergeReadinessProjection);
        Assert.Contains(ab.Descendants(), IsMergeReadinessProjection);
        Assert.Contains(
            "DragDrop.AllowDrop=\"{Binding CanSelectFile}\"",
            ReadPresentationFile("Views/FirmwareSlotCard.axaml"),
            StringComparison.Ordinal);
        var shell = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        XElement mergeBlocker = Assert.Single(shell.Descendants(), element =>
            element.Name.LocalName == "Border" &&
            (string?)element.Attribute("IsVisible") == "{Binding HasMergeBuildBlocker}");
        Assert.Equal(
            "{Binding MergeBuildBlockerText}",
            mergeBlocker.Attributes().Single(attribute =>
                attribute.Name.LocalName == "AutomationProperties.HelpText").Value);
        Assert.Equal(
            "{Binding MergeBuildBlockerText}",
            mergeBlocker.Attributes().Single(attribute =>
                attribute.Name.LocalName == "ToolTip.Tip").Value);
        Assert.Equal(
            "True",
            mergeBlocker.Attributes().Single(attribute =>
                attribute.Name.LocalName == "FocusToolTipBehavior.IsEnabled").Value);
    }

    /// <summary>Replace output avoids a repeated status row while Build owns an accessible blocker.</summary>
    [Fact]
    public void ReplaceOutputOmitsDuplicatedReadinessStripAndBuildOwnsAccessibleBlocker()
    {
        string workflowSource = ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml");
        var templates = XDocument.Parse(workflowSource);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement replaceOutput = Assert.Single(templates.Descendants(), element =>
            element.Name.LocalName == "DataTemplate" &&
            (string?)element.Attribute(x + "Key") == "ReplaceOutputLayoutPanelTemplate");
        XElement replaceMode = Assert.Single(templates.Descendants(), element =>
            element.Name.LocalName == "DataTemplate" &&
            (string?)element.Attribute(x + "Key") == "ReplaceModeContentTemplate");

        Assert.DoesNotContain(replaceOutput.Descendants(), IsReplaceReadinessProjection);
        Assert.Contains(replaceMode.Descendants(), IsReplaceReadinessProjection);

        var shell = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        XElement replaceBlocker = Assert.Single(shell.Descendants(), element =>
            element.Name.LocalName == "Border" &&
            (string?)element.Attribute("IsVisible") == "{Binding HasReplaceBuildBlocker}");
        Assert.Equal(
            "{Binding ReplaceBuildBlockerText}",
            replaceBlocker.Attributes().Single(attribute =>
                attribute.Name.LocalName == "AutomationProperties.HelpText").Value);
        Assert.Equal(
            "{Binding ReplaceBuildBlockerText}",
            replaceBlocker.Attributes().Single(attribute =>
                attribute.Name.LocalName == "AutomationProperties.Name").Value);
        Assert.Equal(
            "{Binding ReplaceBuildBlockerText}",
            replaceBlocker.Attributes().Single(attribute =>
                attribute.Name.LocalName == "ToolTip.Tip").Value);
        Assert.Equal(
            "True",
            replaceBlocker.Attributes().Single(attribute =>
                attribute.Name.LocalName == "FocusToolTipBehavior.IsEnabled").Value);
    }

    private static bool IsMergeReadinessProjection(XElement element)
    {
        return element.Name.LocalName == "TextBlock" &&
            (string?)element.Attribute("Text") == "{Binding MergeReadinessStatus}";
    }

    private static bool IsReplaceReadinessProjection(XElement element)
    {
        return element.Name.LocalName == "TextBlock" &&
            (string?)element.Attribute("Text") == "{Binding ReplaceReadinessStatus}";
    }
}
