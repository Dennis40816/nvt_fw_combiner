using System.Text.Json;
using System.Xml.Linq;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>ADR 0083's per-project allowlist covers every resolved lock-file target and dependency.</summary>
public sealed class CorePackageClosureTests
{
    private static readonly string[] ProjectDirectories = ["src", "tests", "eng"];
    private static readonly string[] TrustAnchors = ["NvtFwCombiner.Bootstrap", "NvtFwCombiner.LauncherBootstrap"];
    private static readonly IReadOnlyDictionary<string, string[]> AllowedPackages =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["NvtFwCombiner.Domain"] = [],
            ["NvtFwCombiner.Contracts"] = [],
            ["NvtFwCombiner.Application"] = [],
            ["NvtFwCombiner.VersionManagement.Application"] = [],
            ["NvtFwCombiner.Profiles"] = [],
            // These potential Core consumers are currently reached by a Bootstrap trust anchor.
            ["NvtFwCombiner.Platform"] = [],
            ["NvtFwCombiner.Infrastructure"] = [],
            ["NvtFwCombiner.VersionManagement.Infrastructure"] = [],
            ["NvtFwCombiner.Bootstrap"] = [],
            ["NvtFwCombiner.LauncherBootstrap"] = [],
            ["NvtFwCombiner.PrebuiltProfileCatalogGenerator"] = [],
            ["NvtFwCombiner.Cli"] = ["Nvt.Core"],
            ["NvtFwCombiner.Launcher"] = ["Nvt.Core"],
            ["NvtFwCombiner.DistributionLauncher"] = ["Nvt.Core", "Nvt.Core.Avalonia"],
            ["NvtFwCombiner.Presentation.Avalonia"] = ["Nvt.Core", "Nvt.Core.Avalonia"],
            ["NvtFwCombiner.Desktop"] = ["Nvt.Core", "Nvt.Core.Avalonia"],
            ["NvtFwCombiner.Domain.Tests"] = [],
            ["NvtFwCombiner.Application.Tests"] = [],
            ["NvtFwCombiner.Infrastructure.Tests"] = [],
            ["NvtFwCombiner.ProfileContract.Tests"] = [],
            ["NvtFwCombiner.GoldenRegression.Tests"] = [],
            ["NvtFwCombiner.Bootstrap.Tests"] = [],
            ["NvtFwCombiner.Architecture.Tests"] = [],
            ["NvtFwCombiner.TestSupport"] = [],
            ["NvtFwCombiner.ReadyProbe"] = [],
            ["NvtFwCombiner.CatalogProbe"] = [],
            // This test host exercises Presentation and Desktop's resolved closure.
            ["NvtFwCombiner.UiSmoke.Tests"] = ["Nvt.Core", "Nvt.Core.Avalonia"],
        };

    /// <summary>Every project has an explicit policy and its entire resolved closure stays within it.</summary>
    [Fact]
    public void EveryProjectLockFileClosureUsesOnlyItsAllowedCorePackages()
    {
        string[] projects = ProjectPaths();
        Assert.Equal(AllowedPackages.Keys.Order(StringComparer.Ordinal),
            projects.Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal));
        foreach (string project in projects)
        {
            string name = Path.GetFileNameWithoutExtension(project);
            string lockPath = Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json");
            Assert.True(File.Exists(lockPath), $"Missing lock file: {name}");
            using var lockFile = JsonDocument.Parse(File.ReadAllText(lockPath));
            string[] rejected = RejectedPackages(lockFile.RootElement, AllowedPackages[name]);
            Assert.True(rejected.Length == 0, $"{name} resolves forbidden Core packages: {string.Join(", ", rejected)}");
        }
    }

    /// <summary>Neither trust anchor nor any project it reaches may allow Core before launcher adoption.</summary>
    [Fact]
    public void BootstrapProjectReferenceClosuresAllowNoCorePackage()
    {
        Dictionary<string, string> projects = ProjectPaths().ToDictionary(
            static path => Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(TrustAnchors.Select(name => projects[name]));
        while (pending.TryPop(out string? projectPath))
        {
            if (!visited.Add(projectPath))
            {
                continue;
            }
            Assert.Empty(AllowedPackages[Path.GetFileNameWithoutExtension(projectPath)]);
            var project = XDocument.Load(projectPath);
            foreach (XElement reference in project.Descendants("ProjectReference"))
            {
                string relative = reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar);
                pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, relative)));
            }
        }
    }

    /// <summary>Both direct and transitive Core packages are checked, including runtime-specific targets.</summary>
    [Theory]
    [InlineData("Direct")]
    [InlineData("Transitive")]
    public void AllowlistChecksAllResolvedTargetsRegardlessOfReferenceKind(string kind)
    {
        using var lockFile = JsonDocument.Parse($$$"""
            {"version":2,"dependencies":{
              "net10.0":{"Nvt.Core":{"type":"{{{kind}}}","resolved":"0.2.0"}},
              "net10.0/win-x64":{"Nvt.Core.Avalonia":{"type":"Transitive","resolved":"0.2.0"}}
            }}
            """);
        Assert.Empty(RejectedPackages(lockFile.RootElement, AllowedPackages["NvtFwCombiner.Presentation.Avalonia"]));
        Assert.Empty(RejectedPackages(lockFile.RootElement, AllowedPackages["NvtFwCombiner.Desktop"]));
        Assert.Equal<string>(["Nvt.Core", "Nvt.Core.Avalonia"],
            RejectedPackages(lockFile.RootElement, AllowedPackages["NvtFwCombiner.Domain"]));
        Assert.Equal<string>(["Nvt.Core", "Nvt.Core.Avalonia"],
            RejectedPackages(lockFile.RootElement, AllowedPackages["NvtFwCombiner.Bootstrap"]));
        Assert.Equal<string>(["Nvt.Core.Avalonia"],
            RejectedPackages(lockFile.RootElement, AllowedPackages["NvtFwCombiner.Cli"]));
    }

    /// <summary>New Core-family packages cannot enter via a transitive dependency without explicit approval.</summary>
    [Fact]
    public void UnlistedCoreFamilyPackageIsRejectedEvenForAvaloniaConsumers()
    {
        using var lockFile = JsonDocument.Parse("""
            {"dependencies":{"net10.0":{"Nvt.Core.Future":{"type":"Transitive","resolved":"0.2.0"}}}}
            """);
        Assert.Equal<string>(["Nvt.Core.Future"],
            RejectedPackages(lockFile.RootElement, AllowedPackages["NvtFwCombiner.Presentation.Avalonia"]));
    }

    private static string[] RejectedPackages(JsonElement lockFile, IReadOnlyList<string> allowed)
    {
        return [.. lockFile.GetProperty("dependencies").EnumerateObject()
            .SelectMany(static target => target.Value.EnumerateObject())
            .Select(static dependency => dependency.Name)
            .Where(name => name.StartsWith("Nvt.Core", StringComparison.OrdinalIgnoreCase) &&
                !allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)];
    }

    private static string[] ProjectPaths()
    {
        string root = RepositoryPaths.FindRepositoryRoot();
        return [.. ProjectDirectories.SelectMany(directory => Directory.EnumerateFiles(
                Path.Combine(root, directory), "*.csproj", SearchOption.AllDirectories))
            .Where(static path => !path.Split(Path.DirectorySeparatorChar).Any(static part => part is "bin" or "obj"))
            .Order(StringComparer.Ordinal)];
    }
}
