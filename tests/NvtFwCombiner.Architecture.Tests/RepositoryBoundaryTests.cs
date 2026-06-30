namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Repository-level architecture boundary checks that do not depend on production assemblies.</summary>
public sealed class RepositoryBoundaryTests
{
    private static readonly DirectoryInfo Root = LocateRepositoryRoot();

    /// <summary>Verifies architecture tests do not introduce production project references.</summary>
    [Fact]
    public void ArchitectureTestsRemainDependencyFree()
    {
        string project = ReadText("tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj");

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
    }

    /// <summary>Verifies external combiner versions are documented as exact string tokens.</summary>
    [Fact]
    public void ExternalCombinerVersionsAreDocumentedAsStringTokens()
    {
        string adr = ReadText("docs/adr/0006-external-combiner-tool-runner.md");

        Assert.Contains("`toolVersion` is always a string", adr, StringComparison.Ordinal);
        Assert.Contains("`1.10` and `1.9` are exact version tokens", adr, StringComparison.Ordinal);
    }

    /// <summary>Verifies UI planning documents keep firmware behavior out of ViewModels.</summary>
    [Fact]
    public void DemoUiDocumentsForbidFirmwareSemanticsInViewModels()
    {
        string boundaries = ReadText("docs/ui/viewmodel-boundaries.md");

        Assert.Contains("byte range arithmetic", boundaries, StringComparison.Ordinal);
        Assert.Contains("CRC/Header calculation or `combiner.exe` invocation", boundaries, StringComparison.Ordinal);
        Assert.Contains("No `File.ReadAllBytes` or `Process.Start` in ViewModels", boundaries, StringComparison.Ordinal);
    }

    /// <summary>Verifies the demo shell follows the owner-approved top-tab and report-modal direction.</summary>
    [Fact]
    public void DemoShellUsesTopTabsAndReportModalPreview()
    {
        string shell = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");

        Assert.Contains("ItemsPanelTemplate", shell, StringComparison.Ordinal);
        Assert.Contains("StackPanel Orientation=\"Horizontal\"", shell, StringComparison.Ordinal);
        Assert.Contains("ReportModalPreview", shell, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, Segoe UI\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"220,*\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnostics.", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SavedRulesAndReports", shell, StringComparison.Ordinal);
    }

    /// <summary>Verifies demo-shell copy is routed through bilingual text resources.</summary>
    [Fact]
    public void DemoShellUsesBilingualTextResources()
    {
        string resources = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/DemoShellTextResources.cs");
        string sampleData = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/DemoShellSampleData.cs");

        Assert.Contains("DemoShellLanguage.ChineseTraditional", resources, StringComparison.Ordinal);
        Assert.Contains("合併", resources, StringComparison.Ordinal);
        Assert.Contains("DemoShellTextResources.For(language)", sampleData, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Merge preview\"", sampleData, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved rules", resources, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadText(string relativePath)
    {
        string fullPath = Path.Combine(Root.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(fullPath);
    }

    private static DirectoryInfo LocateRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NvtFwCombiner.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
