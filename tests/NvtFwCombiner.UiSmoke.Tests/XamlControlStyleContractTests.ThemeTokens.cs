using System.Text.RegularExpressions;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps shared shell visual tokens defined once and resolves every migrated resource reference.</summary>
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
        var definedKeys = colorDefinitions
            .Concat(shadowDefinitions)
            .Concat(cornerRadiusDefinitions)
            .Concat(spacingDefinitions)
            .Concat(fontSizeDefinitions)
            .Concat(fontFamilyDefinitions)
            .Select(static definition => definition.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var definedColors = colorDefinitions
            .Select(static definition => definition.Groups["color"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("<Application.Resources>", application, StringComparison.Ordinal);
        Assert.Contains(
            "<ResourceInclude Source=\"avares://NvtFwCombiner.Presentation.Avalonia/Styles/ThemeTokens.axaml\" />",
            application,
            StringComparison.Ordinal);
        Assert.NotEmpty(colorDefinitions);
        Assert.NotEmpty(shadowDefinitions);
        Assert.NotEmpty(cornerRadiusDefinitions);
        Assert.Equal(5, spacingDefinitions.Length);
        Assert.Equal(5, fontSizeDefinitions.Length);
        Assert.Equal(2, fontFamilyDefinitions.Length);
        Assert.Equal(colorDefinitions.Length, tokens.Split("<SolidColorBrush", StringSplitOptions.None).Length - 1);
        Assert.Equal(shadowDefinitions.Length, tokens.Split("<BoxShadows", StringSplitOptions.None).Length - 1);
        Assert.Equal(cornerRadiusDefinitions.Length, tokens.Split("<CornerRadius", StringSplitOptions.None).Length - 1);
        Assert.Equal(spacingDefinitions.Length + fontSizeDefinitions.Length, tokens.Split("<x:Double", StringSplitOptions.None).Length - 1);
        Assert.Equal(fontFamilyDefinitions.Length, tokens.Split("<FontFamily", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            colorDefinitions.Length + shadowDefinitions.Length + cornerRadiusDefinitions.Length + spacingDefinitions.Length + fontSizeDefinitions.Length + fontFamilyDefinitions.Length,
            definedKeys.Count);
        Assert.Equal(colorDefinitions.Length, definedColors.Count);
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
            "Styles/MainWindowStyles.axaml",
            "Styles/MainWindowButtonStyles.axaml",
            "Styles/MainWindowVisualStyles.axaml",
            "Views/CtrlRamFirmwareVersionModal.axaml",
            "Views/FirmwareIcMismatchModal.axaml",
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
            "Views/GeneralReplaceMappingRow.axaml",
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
}
