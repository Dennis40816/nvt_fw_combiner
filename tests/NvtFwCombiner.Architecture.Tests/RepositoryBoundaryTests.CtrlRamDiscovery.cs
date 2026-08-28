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

    /// <summary>CtrlRAM declaration display never reopens a caller path; accepted immutable bytes own refinement.</summary>
    [Fact]
    public void CtrlRamDiscoveryDisplayHasNoPathReadingCompatibilityOverload()
    {
        string applicationPort = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs")
            .ReplaceLineEndings("\n");
        string adapterPort = ReadText(
            "src/NvtFwCombiner.Application/Authoring/ICtrlRamAuthoringAdapter.cs")
            .ReplaceLineEndings("\n");
        string adapter = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.cs")
            .ReplaceLineEndings("\n");
        string metadata = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.Metadata.cs");

        const string declarationPortSignature =
            "CtrlRamInspectionDisplay GetDiscoveryDisplay(\n        string icId,\n        string number);";
        const string acceptedPortSignature =
            "CtrlRamInspectionDisplay GetDiscoveryDisplayFromAcceptedBase(\n        string icId,\n        string number,\n        ReadOnlyMemory<byte> acceptedBaseBytes);";
        const string declarationAdapterSignature =
            "public CtrlRamInspectionDisplay GetDiscoveryDisplay(\n        string icId,\n        string number)";
        const string acceptedAdapterSignature =
            "public CtrlRamInspectionDisplay GetDiscoveryDisplayFromAcceptedBase(\n        string icId,\n        string number,\n        ReadOnlyMemory<byte> acceptedBaseBytes)";

        Assert.Equal(
            1,
            applicationPort.Split("GetDiscoveryDisplay(", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            1,
            adapterPort.Split("GetDiscoveryDisplay(", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            1,
            adapter.Split("GetDiscoveryDisplay(", StringSplitOptions.None).Length - 1);
        Assert.Contains(declarationPortSignature, applicationPort, StringComparison.Ordinal);
        Assert.Contains(declarationPortSignature, adapterPort, StringComparison.Ordinal);
        Assert.Contains(acceptedPortSignature, applicationPort, StringComparison.Ordinal);
        Assert.Contains(acceptedPortSignature, adapterPort, StringComparison.Ordinal);
        Assert.Contains(declarationAdapterSignature, adapter, StringComparison.Ordinal);
        Assert.Contains(acceptedAdapterSignature, adapter, StringComparison.Ordinal);
        Assert.Contains("acceptedBaseBytes.Span", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveDisplay(", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryResolvePostbuildProfileFromBasePathForDisplay",
            metadata,
            StringComparison.Ordinal);
    }
}
