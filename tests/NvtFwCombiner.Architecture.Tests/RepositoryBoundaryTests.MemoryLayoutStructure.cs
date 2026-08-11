namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Keeps Memory Layout a pure reference-based Application projection.</summary>
    [Fact]
    public void MemoryLayoutProjectionDoesNotBecomeASecondFirmwareOrRenderingModel()
    {
        string memoryLayoutRoot = Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Application/MemoryLayout");
        string models = ReadText(
            "src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutModels.cs");
        string projector = string.Join(
            "\n",
            Directory.EnumerateFiles(
                    Path.Combine(
                        Root.FullName,
                        "src/NvtFwCombiner.Application/MemoryLayout"),
                    "MemoryLayoutProjector*.cs",
                    SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string combined = string.Concat(models, "\n", projector);
        string ctrlRamAdapter = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.cs");
        string compositionPorts = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs");
        string ctrlRamRunner = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");

        Assert.Contains(
            "public FirmwareRegion? CanonicalRegion { get; }",
            models,
            StringComparison.Ordinal);
        Assert.Contains("public string RegionId { get; }", models, StringComparison.Ordinal);
        Assert.Contains("MemoryLayoutGeometryKind", models, StringComparison.Ordinal);
        Assert.Contains(
            "ReferenceEquals(compiledOverlay, capability.CompiledComposition)",
            projector,
            StringComparison.Ordinal);
        Assert.Contains(
            "case MapBoundV2CompilationContext mapContext:",
            projector,
            StringComparison.Ordinal);
        Assert.Contains(
            "case LogicalOutputV2CompilationContext:",
            projector,
            StringComparison.Ordinal);
        Assert.Contains(
            "MemoryLayoutPendingItem",
            models,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            memoryLayoutRoot,
            "CtrlRamInputSlotProjector.cs")));
        Assert.Contains("ProjectCtrlRamDiscovery", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("new CtrlRamRegion(", ctrlRamAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("new ReplaceInputSlot(", ctrlRamAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveRegionGroup", ctrlRamAdapter, StringComparison.Ordinal);
        Assert.Contains(
            "CtrlRamInspectionDisplay GetDiscoveryDisplay(",
            compositionPorts,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetRegions(", compositionPorts, StringComparison.Ordinal);
        Assert.DoesNotContain("GetInputSlots(", compositionPorts, StringComparison.Ordinal);
        Assert.DoesNotContain("CtrlRamAuthoring.GetRegions", ctrlRamRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("CtrlRamAuthoring.GetInputSlots", ctrlRamRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("new FirmwareRegion(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveMap(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain(".Compile(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain(".Execute(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Brush", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Color", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Pixel", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(".RegionId.Contains(", projector, StringComparison.Ordinal);
        Assert.DoesNotContain(".Role.Contains(", projector, StringComparison.Ordinal);
    }
}
