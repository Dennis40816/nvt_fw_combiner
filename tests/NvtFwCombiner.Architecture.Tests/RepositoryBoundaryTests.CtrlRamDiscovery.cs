namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>CtrlRAM Base discovery is produced by Application and only translated by Presentation.</summary>
    [Fact]
    public void CtrlRamBaseDiscoveryHasOneTypedApplicationOwner()
    {
        string applicationContract = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionClientModels.cs");
        string applicationOwner = ReadText(
            "src/NvtFwCombiner.Application/Authoring/CtrlRamAuthoringExperience.Inspection.cs");
        string adapter = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.cs");
        string presentation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");

        Assert.Contains("enum CtrlRamBaseDiscoveryReadiness", applicationContract, StringComparison.Ordinal);
        Assert.Contains("CtrlRamBaseDiscoveryReadiness.Inspected", applicationOwner, StringComparison.Ordinal);
        Assert.Contains("CtrlRamBaseDiscoveryReadiness", adapter, StringComparison.Ordinal);
        Assert.Contains("CtrlRamBaseDiscoveryReadiness.Inspected", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCtrlRamBaseFactsOnly", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthoringSlotLifecycle.Verified", presentation, StringComparison.Ordinal);
    }
}
