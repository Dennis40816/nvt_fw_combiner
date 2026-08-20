using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Composition;

/// <summary>One host path that execution must never overwrite.</summary>
internal sealed record CompositionExecutionProtectedPath(
    string Path,
    string Description);

/// <summary>Caller-selected target for one compiled additional delivery.</summary>
internal sealed record CompositionExecutionDeliveryTarget(
    string DeliveryKind,
    string OutputPath,
    bool UsesAutomaticFileName,
    IReadOnlyList<CompositionExecutionProtectedPath> ProtectedPaths);

/// <summary>Host destination request derived by the Application execution owner.</summary>
internal sealed record CompositionExecutionDestinationRequest(
    bool Build,
    string OutputDirectory,
    string OutputFileName,
    bool OutputPathUsesAutomaticName,
    IReadOnlyList<InputArtifactBinding> Bindings,
    IReadOnlyList<CompositionExecutionProtectedPath> AdditionalProtectedPaths,
    CompositionExecutionDeliveryTarget? AdditionalDelivery,
    CompositionExecutionBundleDelivery? BundleDelivery = null);

/// <summary>Platform writers admitted for one Preview or Build operation.</summary>
internal sealed record CompositionExecutionDestination(
    ICompositionOutputWriter? OutputWriter,
    ICompositionDeliveryWriter? DeliveryWriter);

/// <summary>Creates platform output adapters without owning workflow or byte semantics.</summary>
internal interface ICompositionExecutionDestinationProvider
{
    CompositionExecutionDestination Prepare(
        CompositionExecutionDestinationRequest request);
}

/// <summary>One exact external-processor generation captured for execution.</summary>
internal sealed record CompositionExternalProcessorLease(
    long Generation,
    IExternalProcessor? Processor);
