namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The catalog is an Application read model and never a second firmware compiler.</summary>
    [Fact]
    public void CanonicalCapabilityCatalogStaysApplicationOwnedAndReferenceOnly()
    {
        string catalog = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalog.cs");
        string models = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityModels.cs");
        string applicationCapabilitySources = string.Join(Environment.NewLine, catalog, models);

        Assert.Contains("CompiledComposition", models, StringComparison.Ordinal);
        Assert.Contains("ICanonicalCapabilityCatalogSource", catalog, StringComparison.Ordinal);
        Assert.Contains("ResolutionToken", models, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Bootstrap", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.Json", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("new ByteRange(", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOperation.", applicationCapabilitySources, StringComparison.Ordinal);
    }

    /// <summary>Canonical routing is IC-neutral and delegates legacy facts through one named adapter.</summary>
    [Fact]
    public void CanonicalRouteUsesIcNeutralOneWayFactFreeMigrationAdapter()
    {
        string source = ReadText(
            "src/NvtFwCombiner.Bootstrap/CanonicalCapabilityCatalogMigrationSource.cs");
        string routing = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.BuiltInV2.cs");
        string canonicalRouting = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.CanonicalCapability.cs");
        string cli = ReadText(
            "src/NvtFwCombiner.Bootstrap/CliApplication.StandardMerge.cs");
        string presentation = ReadPresentationSources();

        Assert.Contains("BuiltInV2RegistrationRegistry.StandardMergeByIc", source, StringComparison.Ordinal);
        Assert.Contains("registration.TryCompile", source, StringComparison.Ordinal);
        Assert.Contains("composition.CompilationFingerprint", source, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMergeThroughMigrationAdapter", routing, StringComparison.Ordinal);
        Assert.Contains("CapabilityCatalogIssueCodes.RouteUnavailable", canonicalRouting, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51929", routing, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51929", canonicalRouting, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCanonicalStandardMergePilot", routing, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCanonicalStandardMergePilot", canonicalRouting, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.TryCompileStandardMerge(", cli, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.IsStandardMergeSupported", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("new ByteRange(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOperation.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("0x", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanonicalCapabilityCatalog", presentation, StringComparison.Ordinal);
    }
}
