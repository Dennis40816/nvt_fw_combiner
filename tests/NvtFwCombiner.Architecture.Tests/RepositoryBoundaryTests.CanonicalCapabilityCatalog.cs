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

    /// <summary>The pilot bridge delegates firmware facts to the existing V2 compiler.</summary>
    [Fact]
    public void Nt51929PilotUsesOneWayFactFreeMigrationAdapter()
    {
        string source = ReadText(
            "src/NvtFwCombiner.Bootstrap/CanonicalCapabilityCatalogMigrationSource.cs");
        string routing = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.BuiltInV2.cs");
        string presentation = ReadPresentationSources();

        Assert.Contains("BuiltInV2RegistrationRegistry.StandardMergeByIc", source, StringComparison.Ordinal);
        Assert.Contains("registration.TryCompile", source, StringComparison.Ordinal);
        Assert.Contains("composition.CompilationFingerprint", source, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMergeThroughMigrationAdapter", routing, StringComparison.Ordinal);
        Assert.DoesNotContain("new ByteRange(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOperation.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("0x", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanonicalCapabilityCatalog", presentation, StringComparison.Ordinal);
    }
}
