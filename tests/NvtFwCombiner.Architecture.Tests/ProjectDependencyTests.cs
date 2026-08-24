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

    /// <summary>Verifies that only Bootstrap directly composes the managed-version bounded context.</summary>
    [Fact]
    public void BootstrapOwnsDirectVersionManagementProjectReferences()
    {
        Assert.Equal(
            ["NvtFwCombiner.Contracts", "NvtFwCombiner.Domain"],
            ProjectReferences("NvtFwCombiner.Application"));
        Assert.Equal(
            [
                "NvtFwCombiner.Application",
                "NvtFwCombiner.Contracts",
                "NvtFwCombiner.Domain",
                "NvtFwCombiner.Profiles",
            ],
            ProjectReferences("NvtFwCombiner.Infrastructure"));
        Assert.Equal(
            [
                "NvtFwCombiner.Application",
                "NvtFwCombiner.Infrastructure",
                "NvtFwCombiner.VersionManagement.Application",
                "NvtFwCombiner.VersionManagement.Infrastructure",
            ],
            ProjectReferences("NvtFwCombiner.Bootstrap"));
    }

    /// <summary>Verifies lower semantic projects expose internals only to their proven production consumers.</summary>
    [Fact]
    public void LowerSemanticProjectsDoNotFriendBootstrapWithoutCallers()
    {
        Assert.Equal(
            [
                "NvtFwCombiner.Application.Tests",
                "NvtFwCombiner.Bootstrap.Tests",
                "NvtFwCombiner.Domain.Tests",
                "NvtFwCombiner.Infrastructure",
                "NvtFwCombiner.ProfileContract.Tests",
                "NvtFwCombiner.Profiles",
                "NvtFwCombiner.TestSupport",
            ],
            FriendAssemblies("NvtFwCombiner.Domain"));
        Assert.Equal(
            [
                "NvtFwCombiner.Bootstrap.Tests",
                "NvtFwCombiner.Infrastructure",
                "NvtFwCombiner.ProfileContract.Tests",
            ],
            FriendAssemblies("NvtFwCombiner.Profiles"));
    }

    /// <summary>Infrastructure consumes Application through focused public adapter ports, not implementation internals.</summary>
    [Fact]
    public void ApplicationDoesNotFriendSiblingInfrastructure()
    {
        Assert.Equal(
            [
                "NvtFwCombiner.Application.Tests",
                "NvtFwCombiner.Bootstrap",
                "NvtFwCombiner.Bootstrap.Tests",
                "NvtFwCombiner.UiSmoke.Tests",
            ],
            FriendAssemblies("NvtFwCombiner.Application"));
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

    private static string[] ProjectReferences(string projectName)
    {
        DirectoryInfo root = FindRepositoryRoot();
        string projectPath = Path.Combine(root.FullName, "src", projectName, $"{projectName}.csproj");
        var project = XDocument.Load(projectPath);
        return
        [
            .. project.Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string[] FriendAssemblies(string projectName)
    {
        DirectoryInfo root = FindRepositoryRoot();
        string projectPath = Path.Combine(root.FullName, "src", projectName, $"{projectName}.csproj");
        var project = XDocument.Load(projectPath);
        return
        [
            .. project.Descendants("InternalsVisibleTo")
                .Select(friend => friend.Attribute("Include")!.Value)
                .Order(StringComparer.Ordinal),
        ];
    }
}
