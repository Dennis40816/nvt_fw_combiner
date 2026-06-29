namespace NvtFwCombiner.Domain.Composition;

/// <summary>Application-owned hook used by the pure engine when an external processor operation is reached.</summary>
public delegate ValueTask<CompositionExternalProcessorResult> CompositionExternalProcessor(
    CompositionOperation operation,
    ReadOnlyMemory<byte> inputBytes,
    CancellationToken cancellationToken);
