// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class HostInfrastructureBoundaryTests
{
    /// <summary>External diff ownership excludes the existing CompositionEngine and CompositionRunService owners.</summary>
    [Fact]
    public void ExternalProcessorStagingDiffHasOneHostOwnerExcludingCompositionEngineAndCompositionRunService()
    {
        (string TypeName, string Source)[] sources = [
            .. Directory.EnumerateFiles(Path.Combine(Root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
                .Select(path => (Path.GetFileName(path).Split('.')[0], File.ReadAllText(path))),
        ];

        Assert.Equal(["StagedExternalProcessorHost"], FindExternalDiffOwners(sources));
    }

    /// <summary>The two compatibility wrappers remain test entry points until the g.2 wiring wave deletes them.</summary>
    [Fact]
    public void ProductionSourcesCannotConstructLegacyProcessorTestEntryPoints()
    {
        string[] productionSources = [
            .. Directory.EnumerateFiles(Path.Combine(Root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
                .Where(path => Path.GetFileName(path) is not ("ExternalCombinerProcessor.cs" or "LegacyCombinerPostbuildProcessor.cs"))
                .Select(File.ReadAllText),
        ];

        // Banning all references also catches qualified names and target-typed construction.
        Assert.All(productionSources, source => Assert.False(ReferencesLegacyProcessorTestEntryPoint(source)));
    }

    /// <summary>The forbidden-reference rule detects explicit, qualified and target-typed wrapper construction.</summary>
    [Theory]
    [InlineData("new ExternalCombinerProcessor(registry, tools, staging, runner, profiles)")]
    [InlineData("new LegacyCombinerPostbuildProcessor(registry, tools, staging, runner)")]
    [InlineData("new NvtFwCombiner.Infrastructure.ExternalTools.ExternalCombinerProcessor(registry, tools, staging, runner, profiles)")]
    [InlineData("LegacyCombinerPostbuildProcessor processor = new(registry, tools, staging, runner)")]
    public void LegacyProcessorTestEntryPointRuleDetectsProductionConstruction(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Assert.True(ReferencesLegacyProcessorTestEntryPoint(source));
    }

    /// <summary>Legacy launch requests cannot replace the inherited process environment.</summary>
    [Fact]
    public void LegacyLaunchRequestHasNoEnvironmentOverride()
    {
        string request = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalProcessStartInfo.cs");
        string runner = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/SystemExternalProcessRunner.cs");

        Assert.DoesNotContain("Environment", request, StringComparison.Ordinal);
        Assert.DoesNotContain(".Environment", runner, StringComparison.Ordinal);
    }

    /// <summary>The forbidden-call rule detects a second processor owner even outside Infrastructure/ExternalTools.</summary>
    [Fact]
    public void ExternalProcessorDiffOwnerRuleDetectsSecondImplementation()
    {
        (string TypeName, string Source)[] sources = [
            ("StagedExternalProcessorHost", "ByteDiff.FindChangedRanges(before, after)"),
            ("AnotherProcessor", "ByteDiff.FindChangedRanges(before, after)"),
        ];

        Assert.Equal(["AnotherProcessor", "StagedExternalProcessorHost"], FindExternalDiffOwners(sources));
    }

    /// <summary>The production composition root builds the shared host directly.</summary>
    [Fact]
    public void ExternalEnvironmentCreatesOneStagedHost()
    {
        string loader = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalProcessorEnvironmentLoader.cs");
        string adapters = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerProcessorAdapter.Manifest.cs") +
            ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerProcessorAdapter.Postbuild.cs");

        Assert.Equal(1, CountOccurrences(loader, "new StagedExternalProcessorHost("));
        Assert.DoesNotContain("new ExternalProcessorRouter(", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("new ExternalCombinerProcessor(", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("new LegacyCombinerPostbuildProcessor(", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("ByteDiff", adapters, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangedRangePolicy", adapters, StringComparison.Ordinal);
        Assert.DoesNotContain("_processRunner.RunAsync", adapters, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalStagingDirectory.TryAcquire", adapters, StringComparison.Ordinal);
    }

    private static string[] FindExternalDiffOwners(IEnumerable<(string TypeName, string Source)> sources)
    {
        // These existing generic trace/report owners also diff built-in operations or final output references.
        // CompositionEngine still recomputes the external diff until the g.2 wiring wave reuses the host result.
        return [
            .. sources
                .Where(source => source.Source.Contains("ByteDiff.FindChangedRanges(", StringComparison.Ordinal))
                .Where(source => source.TypeName is not ("CompositionEngine" or "CompositionRunService"))
                .Select(source => source.TypeName)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static bool ReferencesLegacyProcessorTestEntryPoint(string source)
    {
        return source.Contains("ExternalCombinerProcessor", StringComparison.Ordinal) ||
            source.Contains("LegacyCombinerPostbuildProcessor", StringComparison.Ordinal);
    }
}
