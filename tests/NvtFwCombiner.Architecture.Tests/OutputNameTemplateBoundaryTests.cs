// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Guards one template owner across production projects, including partial declarations.</summary>
public sealed partial class OutputNameTemplateBoundaryTests
{
    /// <summary>Accepted-session and run-service facades cannot acquire a second naming implementation.</summary>
    [Fact]
    public void SolutionHasOneOutputNameTemplateResolverType()
    {
        string[] sources =
        [
            .. Directory.EnumerateFiles(Path.Combine(Root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(static path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
                .Select(File.ReadAllText),
        ];

        Assert.Equal(
            ["NvtFwCombiner.Application.Composition.CompiledOutputNameResolver"],
            FindTemplateOwners(sources));
        Assert.Equal(1, sources.Sum(static source => TemplateEntryPoint().Count(source)));
    }

    /// <summary>The guard detects a second resolver even when it retains the older entry-point shape.</summary>
    [Fact]
    public void TemplateOwnerGuardDetectsAnIndependentLegacyResolver()
    {
        string[] owners = FindTemplateOwners(
        [
            """
            namespace Example;
            internal static class TemplateOwner
            {
                internal static string ResolveTemplate(string template) { return template; }
            }
            """,
            """
            namespace Example;
            internal static class IndependentLegacyOwner
            {
                internal static OutputNameResolution Resolve(Request request) { return default; }
            }
            """,
        ]);

        Assert.Equal(["Example.IndependentLegacyOwner", "Example.TemplateOwner"], owners);
    }

    private static string[] FindTemplateOwners(IEnumerable<string> sources)
    {
        return
        [
            .. sources.Where(static source => NamingEntryPoint().IsMatch(source))
                .Select(static source =>
                    NamespaceDeclaration().Match(source).Groups["name"].Value + "." +
                    TypeDeclaration().Match(source).Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    [GeneratedRegex(@"\b(?:OutputNameResolution\s+Resolve\w*|string\s+ResolveTemplate)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex NamingEntryPoint();

    [GeneratedRegex(@"\bstring\s+ResolveTemplate\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateEntryPoint();

    [GeneratedRegex(@"\bnamespace\s+(?<name>[\w.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex NamespaceDeclaration();

    [GeneratedRegex(@"^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly)\s+)*(?:class|struct|record)\s+(?<name>\w+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TypeDeclaration();

    /// <summary>Delivery file names and the Standard Merge bundle folder expand tokens through the one owner.</summary>
    [Fact]
    public void DeliveryAndBundleNamesUseTheOneTokenExpander()
    {
        string composition = Path.Combine(Root.FullName, "src", "NvtFwCombiner.Application", "Composition");
        string[] files = ["CompositionAdditionalDeliveryPlan.cs", "CompositionOutputBundleProposal.cs"];
        foreach (string file in files)
        {
            string source = File.ReadAllText(Path.Combine(composition, file));
            Assert.Contains("CompiledOutputNameResolver.ExpandTemplate(", source, StringComparison.Ordinal);
        }

        string[] replaceLoops =
        [
            .. Directory.EnumerateFiles(Path.Combine(Root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(static path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
                .Where(static path => File.ReadAllText(path).Contains(".Replace($\"{{{", StringComparison.Ordinal)),
        ];
        Assert.Empty(replaceLoops);
    }
}
