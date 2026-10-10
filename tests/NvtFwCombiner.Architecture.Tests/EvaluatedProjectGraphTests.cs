using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Checks the evaluated graph, including imported, conditional and build-only edges.</summary>
public sealed class EvaluatedProjectGraphTests
{
    // dependency-rules.md establishes direction; ADR 0083 establishes Core ownership.
    // Exact project edges are the accepted csproj snapshot, including Desktop's managed-version adapter.
    internal static ImmutableDictionary<string, ImmutableArray<string>> AllowedEdges { get; } =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            ["Domain"] = [],
            ["Contracts"] = [],
            ["Platform"] = [],
            ["Application"] = ["Contracts", "Domain"],
            ["Profiles"] = ["Contracts", "Domain"],
            ["VersionManagement.Application"] = ["Contracts"],
            ["VersionManagement.Infrastructure"] = ["Contracts", "Platform", "VersionManagement.Application"],
            ["Infrastructure"] = ["Application", "Contracts", "Domain", "Platform", "Profiles"],
            ["Bootstrap"] = ["Application", "Infrastructure", "PrebuiltProfileCatalogGenerator", "VersionManagement.Application", "VersionManagement.Infrastructure"],
            ["Presentation.Avalonia"] = ["Application", "VersionManagement.Application"],
            ["Cli"] = ["Bootstrap", "VersionManagement.Application"],
            ["Desktop"] = ["Bootstrap", "Presentation.Avalonia", "VersionManagement.Application", "VersionManagement.Infrastructure"],
            ["Launcher"] = ["VersionManagement.Application", "VersionManagement.Infrastructure"],
            ["LauncherBootstrap"] = ["VersionManagement.Application", "VersionManagement.Infrastructure"],
            ["DistributionLauncher"] = ["Bootstrap", "VersionManagement.Application"],
            ["PrebuiltProfileCatalogGenerator"] = ["Infrastructure"],
        }.ToImmutableDictionary(StringComparer.Ordinal);
    private static readonly string[] _configurations = ["Debug", "Release"];
    private static readonly string[] _runtimes = ["", "win-x64"];
    // All ordinary references use SDK runtime defaults. Bootstrap's generator is build-only.
    private static readonly ImmutableDictionary<string, string> _generatorMetadata =
        new Dictionary<string, string> { ["ReferenceOutputAssembly"] = "false", ["PrivateAssets"] = "all", ["Private"] = "false", ["OutputItemType"] = "_NfcCatalogTool" }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    internal static IEnumerable<string> ProductionProjects => AllowedEdges.Keys.Where(name => name != "PrebuiltProfileCatalogGenerator");
    internal static string ProjectPath(string name)
    {
        return Path.GetFullPath(Path.Combine(Root.FullName,
        name == "PrebuiltProfileCatalogGenerator" ? "eng/prebuilt-profile-catalog" : $"src/NvtFwCombiner.{name}", $"NvtFwCombiner.{name}.csproj"));
    }

    internal static XElement ReadGraph()
    {
        return XElement.Load(Path.Combine(AppContext.BaseDirectory, "architecture-project-graph.xml"));
    }

    internal static string Value(XElement element, string attribute)
    {
        return (string?)element.Attribute(attribute) ?? throw new InvalidDataException($"Graph.Manifest: missing {attribute}");
    }

    internal static void AssertArchitectureReferences(XDocument project)
    {
        XElement[] references = [.. project.Descendants("ProjectReference")];
        Assert.Equal(ProductionProjects.Select(name => $"../../src/NvtFwCombiner.{name}/NvtFwCombiner.{name}.csproj").Order(StringComparer.Ordinal),
            references.Select(item => Value(item, "Include").Replace('\\', '/')).Order(StringComparer.Ordinal));
        Assert.All(references, item =>
        {
            Assert.Equal("false", Value(item, "ReferenceOutputAssembly"));
            Assert.Equal("all", Value(item, "PrivateAssets"));
            Assert.Equal("false", Value(item, "Private"));
        });
    }
    internal static void ValidateEdges(XElement graph)
    {
        foreach (XElement project in graph.Elements("project"))
        {
            string path = Value(project, "path");
            string name = AllowedEdges.Keys.Single(key => StringComparer.OrdinalIgnoreCase.Equals(ProjectPath(key), path));
            string[] expected = [.. AllowedEdges[name].Select(ProjectPath).Order(StringComparer.OrdinalIgnoreCase)];
            string[] actual = [.. project.Elements("reference").Select(edge => Value(edge, "path")).Order(StringComparer.OrdinalIgnoreCase)];
            Assert.True(expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase), $"Graph.Edges: {name} [{Value(project, "configuration")}/{Value(project, "runtime")}] -> {string.Join(", ", actual)}");
            foreach (XElement edge in project.Elements("reference"))
            {
                var metadata = edge.Elements("metadata").ToDictionary(item => Value(item, "name"), item => item.Value, StringComparer.OrdinalIgnoreCase);
                bool generator = name == "Bootstrap" && Value(edge, "path") == ProjectPath("PrebuiltProfileCatalogGenerator");
                foreach (string key in _generatorMetadata.Keys)
                {
                    string value = metadata.GetValueOrDefault(key, string.Empty);
                    string allowed = generator ? _generatorMetadata[key] : key == "ReferenceOutputAssembly" ? "true" : string.Empty;
                    Assert.True(value == allowed || (!generator && key == "ReferenceOutputAssembly" && value.Length == 0), $"Graph.ReferenceMetadata: {name} -> {Value(edge, "path")} {key}={value}");
                }
            }
        }
    }
    internal static void ValidateCoverage(XElement graph)
    {
        string[] expected = [.. AllowedEdges.Keys.SelectMany(name => _configurations.SelectMany(configuration => _runtimes.Select(runtime => $"{ProjectPath(name)}|{configuration}|net10.0|{runtime}"))).Order(StringComparer.Ordinal)];
        string[] actual = [.. graph.Elements("project").Select(item => $"{Value(item, "path")}|{Value(item, "configuration")}|{Value(item, "framework")}|{Value(item, "runtime")}").Order(StringComparer.Ordinal)];
        Assert.True(expected.SequenceEqual(actual, StringComparer.Ordinal), $"Graph.Coverage: missing [{string.Join(", ", expected.Except(actual))}], duplicate [{string.Join(", ", actual.GroupBy(item => item).Where(group => group.Count() > 1).Select(group => group.Key))}], unlisted [{string.Join(", ", actual.Except(expected))}]");
    }
    private static XElement InvalidCoverage(string scenario)
    {
        XElement graph = new("inputs", _configurations.SelectMany(configuration => _runtimes.SelectMany(runtime => Fixture(runtime, configuration).Elements())));
        XElement entry = graph.Elements().First(item => Value(item, "path") == ProjectPath("Domain"));
        return scenario switch { "Missing" => new("inputs", graph.Elements().Where(item => item != entry)), "Duplicate" => new("inputs", graph.Elements(), entry), _ => new("inputs") };
    }
    /// <summary>Coverage rejects missing, duplicated and empty evaluation evidence.</summary>
    [Theory]
    [InlineData("Missing")]
    [InlineData("Duplicate")]
    [InlineData("Empty")]
    public void Graph_InvalidCoverage_NamesRuleAndOffender(string scenario)
    {
        string message = Assert.ThrowsAny<Exception>(() => ValidateCoverage(InvalidCoverage(scenario))).Message;
        Assert.Contains("Graph.Coverage:", message, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Domain", message, StringComparison.Ordinal);
    }
    internal static void ValidateCycles(XElement graph)
    {
        var edges = graph.Elements("project").ToDictionary(item => Value(item, "path"),
            item => item.Elements("reference").Select(edge => Value(edge, "path")).ToArray(), StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var complete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string project)
        {
            if (complete.Contains(project))
            {
                return;
            }

            Assert.True(active.Add(project), $"Graph.Cycle: {string.Join(" -> ", active.Append(project))}");
            foreach (string reference in edges[project])
            {
                Visit(reference);
            }

            _ = active.Remove(project);
            _ = complete.Add(project);
        }
        foreach (string project in edges.Keys)
        {
            Visit(project);
        }
    }
    private static XElement Fixture(string runtime = "win-x64", string configuration = "Release")
    {
        return new("inputs", AllowedEdges.Select(pair =>
        new XElement("project", new XAttribute("path", ProjectPath(pair.Key)), new XAttribute("configuration", configuration),
            new XAttribute("framework", "net10.0"), new XAttribute("runtime", runtime),
            pair.Value.Select(name => new XElement("reference", new XAttribute("path", ProjectPath(name)),
                name == "PrebuiltProfileCatalogGenerator" ? _generatorMetadata.Select(item => new XElement("metadata", new XAttribute("name", item.Key), item.Value)) : [])))));
    }

    /// <summary>All shipped projects have all four required evaluations.</summary>
    [Fact]
    public void Graph_ShippedProjects_CoversRequiredTargetTuples()
    {
        XElement graph = ReadGraph();
        Assert.Equal(ProductionProjects.Select(ProjectPath).Order(StringComparer.OrdinalIgnoreCase), XDocument.Load(Path.Combine(Root.FullName, "NvtFwCombiner.slnx"))
            .Descendants("Project").Select(item => Value(item, "Path")).Where(path => path.StartsWith("src/", StringComparison.Ordinal)).Select(path => Path.GetFullPath(Path.Combine(Root.FullName, path))).Order(StringComparer.OrdinalIgnoreCase));
        ValidateCoverage(graph);
    }
    /// <summary>Evaluated edges match the exact accepted policy.</summary>
    [Fact]
    public void Graph_EvaluatedReferences_MatchesAllowedEdges()
    {
        ValidateEdges(ReadGraph());
    }

    /// <summary>Each evaluated target graph is acyclic.</summary>
    [Theory]
    [InlineData("Debug", "")]
    [InlineData("Debug", "win-x64")]
    [InlineData("Release", "")]
    [InlineData("Release", "win-x64")]
    public void Graph_EvaluatedTarget_HasNoCycles(string configuration, string runtime)
    {
        ValidateCycles(new XElement("inputs",
        ReadGraph().Elements("project").Where(item => Value(item, "configuration") == configuration && Value(item, "runtime") == runtime)));
    }

    /// <summary>A forbidden edge confined to win-x64 is rejected.</summary>
    [Fact]
    public void Graph_WindowsOnlyForbiddenEdge_NamesRuleAndOffender()
    {
        XElement graph = Fixture();
        graph.Elements().Single(item => Value(item, "path") == ProjectPath("Domain")).Add(new XElement("reference", new XAttribute("path", ProjectPath("Application"))));
        Assert.Contains("Graph.Edges: Domain [Release/win-x64]", Assert.ThrowsAny<Exception>(() => ValidateEdges(graph)).Message, StringComparison.Ordinal);
    }
    /// <summary>A Domain to Application cycle is rejected independently of edge policy.</summary>
    [Fact]
    public void Graph_DomainApplicationCycle_NamesRuleAndOffender()
    {
        XElement graph = Fixture();
        graph.Elements().Single(item => Value(item, "path") == ProjectPath("Domain")).Add(new XElement("reference", new XAttribute("path", ProjectPath("Application"))));
        string message = Assert.ThrowsAny<Exception>(() => ValidateCycles(graph)).Message;
        Assert.Contains("Graph.Cycle:", message, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Domain", message, StringComparison.Ordinal);
    }
    /// <summary>The accepted graph is a positive control.</summary>
    [Fact]
    public void Graph_AcceptedEdges_Passes() { ValidateEdges(Fixture()); }
    /// <summary>The accepted graph is an independent cycle positive control.</summary>
    [Fact]
    public void Graph_AcceptedCycles_Passes() { ValidateCycles(Fixture()); }
    /// <summary>The same generator path cannot become a runtime reference.</summary>
    [Fact]
    public void Graph_GeneratorRuntimeReference_NamesRuleAndOffender()
    {
        XElement graph = Fixture();
        graph.Descendants("reference").Single(item => Value(item, "path") == ProjectPath("PrebuiltProfileCatalogGenerator"))
            .Elements("metadata").Single(item => Value(item, "name") == "ReferenceOutputAssembly").Value = "true";
        Assert.Contains("Graph.ReferenceMetadata: Bootstrap", Assert.ThrowsAny<Exception>(() => ValidateEdges(graph)).Message, StringComparison.Ordinal);
    }
    /// <summary>Build-only inputs never become runtime assembly references.</summary>
    [Fact]
    public void ArchitectureAssembly_BuildOnlyInputs_HasNoProductionAssemblyReferences()
    {
        using FileStream stream = File.OpenRead(typeof(EvaluatedProjectGraphTests).Assembly.Location);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        Assert.DoesNotContain(metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)),
            name => name.StartsWith("NvtFwCombiner.", StringComparison.Ordinal));
    }
}
