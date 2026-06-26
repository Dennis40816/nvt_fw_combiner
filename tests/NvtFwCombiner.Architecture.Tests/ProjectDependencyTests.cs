using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Architecture boundary tests for project references and source inclusion.</summary>
public sealed class ProjectDependencyTests
{
    /// <summary>Verifies that the domain project remains free of project references.</summary>
    [Fact]
    public void DomainProjectHasNoProjectReferences()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string projectPath = Path.Combine(
            root.FullName,
            "src",
            "NvtFwCombiner.Domain",
            "NvtFwCombiner.Domain.csproj");
        var project = XDocument.Load(projectPath);

        Assert.Empty(project.Descendants("ProjectReference"));
    }

    /// <summary>Verifies that immutable reference code is never compiled by production projects.</summary>
    [Fact]
    public void ReferenceCodeIsNeverIncludedByProductionProjects()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string sourceRoot = Path.Combine(root.FullName, "src");
        IEnumerable<string> productionProjects = Directory.EnumerateFiles(
            sourceRoot,
            "*.csproj",
            SearchOption.AllDirectories);

        foreach (string projectPath in productionProjects)
        {
            var project = XDocument.Load(projectPath);
            string[] includes = [.. project.Descendants()
                .Attributes("Include")
                .Select(attribute => attribute.Value)];
            Assert.DoesNotContain(
                includes,
                include => include.Contains("refcode", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null &&
               !File.Exists(Path.Combine(current.FullName, "NvtFwCombiner.slnx")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
