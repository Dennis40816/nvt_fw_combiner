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
        string classifications = ReadText(
            "src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutClassifications.cs");
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
        string memoryRunner = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs");
        string memorySegmentViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MemoryCoverageSegmentViewModel.cs");

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
        Assert.Contains(
            "MemoryContentRole.CustomerInformation",
            projector,
            StringComparison.Ordinal);
        Assert.Contains(
            "MemoryContentRole.Unmapped",
            projector,
            StringComparison.Ordinal);
        Assert.Contains("CustomerInformation", classifications, StringComparison.Ordinal);
        Assert.Contains("Unmapped", classifications, StringComparison.Ordinal);
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
        Assert.DoesNotContain("NT51950", memoryRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51951", memoryRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("0x37000", memoryRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("0x37FFF", memoryRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-info", memoryRunner, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShellTextResources text,", memoryRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("text ??=", memoryRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizeSourceLabel", memorySegmentViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateCompactDetail", memorySegmentViewModel, StringComparison.Ordinal);
    }
}
