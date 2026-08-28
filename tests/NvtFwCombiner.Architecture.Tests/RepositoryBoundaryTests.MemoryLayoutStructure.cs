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

        AssertContainsAll(models, "public FirmwareRegion? CanonicalRegion { get; }",
            "public string RegionId { get; }", "MemoryLayoutGeometryKind", "MemoryLayoutPendingItem");
        AssertContainsAll(projector, "ReferenceEquals(compiledOverlay, capability.CompiledComposition)",
            "case MapBoundV2CompilationContext mapContext:", "case LogicalOutputV2CompilationContext:",
            "MemoryContentRole.CustomerInformation", "MemoryContentRole.Unmapped",
            "ProjectCtrlRamDiscovery");
        AssertContainsAll(classifications, "CustomerInformation", "Unmapped");
        Assert.False(File.Exists(Path.Combine(
            memoryLayoutRoot,
            "CtrlRamInputSlotProjector.cs")));
        AssertDoesNotContainAny(ctrlRamAdapter, "new CtrlRamRegion(", "new ReplaceInputSlot(",
            "ResolveRegionGroup");
        AssertContainsAll(compositionPorts, "CtrlRamInspectionDisplay GetDiscoveryDisplay(");
        AssertDoesNotContainAny(compositionPorts, "GetRegions(", "GetInputSlots(");
        AssertDoesNotContainAny(ctrlRamRunner, "CtrlRamAuthoring.GetRegions", "CtrlRamAuthoring.GetInputSlots");
        AssertDoesNotContainAny(projector, "new FirmwareRegion(", "ResolveMap(", ".Compile(",
            ".Execute(", ".RegionId.Contains(", ".Role.Contains(");
        AssertDoesNotContainAny(combined, "System.IO", "Avalonia", "Brush", "Color", "Pixel");
        AssertDoesNotContainAny(memoryRunner, "NT51950", "NT51951", "0x37000", "0x37FFF");
        Assert.DoesNotContain("customer-info", memoryRunner, StringComparison.OrdinalIgnoreCase);
        AssertContainsAll(memoryRunner, "ShellTextResources text,");
        AssertDoesNotContainAny(memoryRunner, "text ??=");
        AssertDoesNotContainAny(memorySegmentViewModel, "NormalizeSourceLabel", "CreateCompactDetail");
    }
}
