namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies shell copy is routed through bilingual text resources.</summary>
    [Fact]
    public void ShellUsesBilingualTextResources()
    {
        string resources = ReadShellTextResourcesPartials();
        string factory = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellViewModelFactory.cs");
        string viewModel = ReadViewModelPartials();
        string shell = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");
        string pageTemplates = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml");
        string workflowTemplates = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowWorkflowTemplates.axaml");
        string reportTemplates = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportTemplates.axaml");
        string reportAuditTemplates = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportAuditTemplates.axaml");
        string reportPanels = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportPanels.axaml");
        string shellPanels = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowShellPanels.axaml");
        string shellSurface = string.Join(
            Environment.NewLine,
            shell,
            pageTemplates,
            workflowTemplates,
            reportTemplates,
            reportAuditTemplates,
            reportPanels,
            shellPanels);

        Assert.Contains("ShellLanguage.ChineseTraditional", resources, StringComparison.Ordinal);
        Assert.Contains("合併", resources, StringComparison.Ordinal);
        Assert.Contains("Device context", resources, StringComparison.Ordinal);
        Assert.Contains("ShellTextResources.For(language)", viewModel, StringComparison.Ordinal);
        Assert.Contains("ApplyTextResources(ShellTextResources.LanguageFromPreference(value))", viewModel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.SettingsPreferencesTitle}\"", shellSurface, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.LanguageLabel}\"", shellSurface, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding Text.ReportTabInputs}\"", reportAuditTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Merge preview\"", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved rules", resources, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demo", resources, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CapabilityEvidenceStatus.SyntheticOracle", resources, StringComparison.Ordinal);
        Assert.Contains("合成 oracle", resources, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic demo", resources, StringComparison.OrdinalIgnoreCase);

        foreach (string retiredName in new[]
                 {
                     "FooterStatus",
                     "ConfigureKicker",
                     "OpenSettingsLabel",
                     "TpReplacementBinTitle",
                     "TpReplacementBinDescription",
                     "LdReplacementBinTitle",
                     "LdReplacementBinDescription",
                     "CtrlRamReplacementBinDescription",
                     "HexEditorSaveLabel",
                     "HexEditorMemoryOnlyDetail",
                     "DiffLabel",
                     "ExplanationLabel",
                     "public string ReasonLabel",
                     "public string StepLabel",
                     "public string TargetLabel",
                     "public string ProcessorLabel",
                     "HexEditorOverwriteRangeLabel",
                     "HexEditorFillRangeLabel",
                 })
        {
            Assert.DoesNotContain(retiredName, resources, StringComparison.Ordinal);
        }
    }
}
