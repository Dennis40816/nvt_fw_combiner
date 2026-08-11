using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

internal sealed class CompositionOutputNamingExperience : ICompositionOutputNaming
{
    private readonly ICanonicalCapabilityQuery _catalog;
    private readonly ISystemClock _clock;

    internal CompositionOutputNamingExperience(
        ICanonicalCapabilityQuery catalog,
        ISystemClock clock)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CompositionOutputPreparation ResolveAcceptedOutput(
        ActiveSessionSnapshot acceptedSession,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null)
    {
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ResolvedCapability acceptedCapability = acceptedSession.ExactCapability ??
            throw new InvalidOperationException(
                "Output naming requires one exact accepted capability.");
        ResolvedCapability currentCapability = _catalog.ResolveCurrentCompilation(
                acceptedCapability.CompiledComposition,
                acceptedCapability) ??
            throw new InvalidOperationException(
                "Output naming requires the current accepted capability publication.");
        TopologySelection? topology = StringComparer.Ordinal.Equals(
                acceptedSession.WorkflowId,
                ExperienceIds.AbMerge)
            ? CapabilityPublicationCoherence.GetAcceptedAbMergeTopologySelection(
                currentCapability)
            : null;
        return AcceptedSessionOutputNameResolver.Resolve(
            acceptedSession,
            currentCapability,
            _clock.UtcNow,
            topology,
            ctrlRamVersionEdit);
    }

    public ValueTask<CompositionOutputPreparation> PrepareAutomaticOutputAsync(
        ActiveSessionSnapshot acceptedSession,
        CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<CompositionOutputPreparation>(cancellationToken)
            : ValueTask.FromResult(ResolveAcceptedOutput(acceptedSession));
    }
}
