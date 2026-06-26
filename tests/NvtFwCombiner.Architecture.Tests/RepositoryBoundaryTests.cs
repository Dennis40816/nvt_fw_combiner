namespace NvtFwCombiner.Architecture.Tests;

public sealed class RepositoryBoundaryTests
{
    private static readonly DirectoryInfo Root = LocateRepositoryRoot();

    [Fact]
    public void ArchitectureTestsRemainDependencyFree()
    {
        string project = ReadText("tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj");

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalCombinerVersionsAreDocumentedAsStringTokens()
    {
        string adr = ReadText("docs/adr/0006-external-combiner-tool-runner.md");

        Assert.Contains("`toolVersion` is always a string", adr, StringComparison.Ordinal);
        Assert.Contains("`1.10` and `1.9` are exact version tokens", adr, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoUiDocumentsForbidFirmwareSemanticsInViewModels()
    {
        string boundaries = ReadText("docs/ui/viewmodel-boundaries.md");

        Assert.Contains("byte range arithmetic", boundaries, StringComparison.Ordinal);
        Assert.Contains("CRC/Header calculation or `combiner.exe` invocation", boundaries, StringComparison.Ordinal);
        Assert.Contains("No `File.ReadAllBytes` or `Process.Start` in ViewModels", boundaries, StringComparison.Ordinal);
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
