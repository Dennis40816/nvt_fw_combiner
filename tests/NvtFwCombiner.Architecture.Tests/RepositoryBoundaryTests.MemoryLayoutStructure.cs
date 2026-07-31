namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Keeps Memory Layout a pure reference-based Application projection.</summary>
    [Fact]
    public void MemoryLayoutProjectionDoesNotBecomeASecondFirmwareOrRenderingModel()
    {
        string models = ReadText(
            "src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutModels.cs");
        string projector = ReadText(
            "src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.cs");
        string combined = string.Concat(models, "\n", projector);

        Assert.Contains(
            "public FirmwareRegion CanonicalRegion { get; }",
            models,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReferenceEquals(compiledOverlay, capability.CompiledComposition)",
            projector,
            StringComparison.Ordinal);
        Assert.Contains(
            "details.Provenance.Context is MapBoundV2CompilationContext",
            projector,
            StringComparison.Ordinal);
        Assert.Contains(
            "MemoryLayoutPendingItem",
            models,
            StringComparison.Ordinal);
        Assert.DoesNotContain("new FirmwareRegion(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveMap(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain(".Compile(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain(".Execute(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Brush", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Color", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Pixel", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("RegionId.Contains", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("Role.Contains", projector, StringComparison.Ordinal);
    }
}
