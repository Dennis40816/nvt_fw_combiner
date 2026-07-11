using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regression coverage for the report's collapsed information hierarchy.</summary>
public sealed class ReportPresentationTemplateTests
{
    /// <summary>Inputs, physical change sections, flow steps, and runtime invocations start collapsed.</summary>
    [Fact]
    public void ReportTemplatesUseCollapsedProgressiveDisclosure()
    {
        string inputTemplates = ReadPresentationFile("Resources/MainWindowReportInputTemplates.axaml");
        string changeTemplates = ReadPresentationFile("Resources/MainWindowReportChangeTemplates.axaml");
        string operationTemplates = ReadPresentationFile("Resources/MainWindowReportOperationTemplates.axaml");
        string auditTemplates = ReadPresentationFile("Resources/MainWindowReportAuditTemplates.axaml");

        Assert.Contains("<Expander Margin=\"0,0,0,10\" IsExpanded=\"False\">", inputTemplates, StringComparison.Ordinal);
        Assert.Contains("ReportOutputDifferenceGroupTemplate", changeTemplates, StringComparison.Ordinal);
        Assert.Contains("<Expander Margin=\"0,0,0,10\" IsExpanded=\"False\">", changeTemplates, StringComparison.Ordinal);
        Assert.Contains("ReportOperationFlowNodeTemplate", operationTemplates, StringComparison.Ordinal);
        Assert.Contains("<Expander Grid.Column=\"1\" Margin=\"0,3,0,18\" IsExpanded=\"False\">", operationTemplates, StringComparison.Ordinal);
        Assert.Contains("ReportPostbuildInvocationTemplate", operationTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportCommandOperationTemplate", operationTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportRuntimeCommandTemplate", operationTemplates, StringComparison.Ordinal);
        Assert.Contains("<Expander IsExpanded=\"False\">", operationTemplates, StringComparison.Ordinal);
        Assert.Contains("PostbuildInvocations", auditTemplates, StringComparison.Ordinal);
        Assert.Contains("PostbuildInvocationCount", auditTemplates, StringComparison.Ordinal);
    }

    private static string ReadPresentationFile(string relativePath)
    {
        return File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Presentation.Avalonia", relativePath));
    }
}
