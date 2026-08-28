using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

internal sealed class CompositionOutputNamingExperience : ICompositionOutputNaming
{
    private readonly ICanonicalCapabilityQuery _catalog;
    private readonly ICompositionArtifactIdentityPolicy _artifactIdentityPolicy;
    private readonly ICompositionOutputBundleDestinationValidator _bundleDestinationValidator;
    private readonly ISystemClock _clock;

    internal CompositionOutputNamingExperience(
        ICanonicalCapabilityQuery catalog,
        ISystemClock clock,
        ICompositionArtifactIdentityPolicy artifactIdentityPolicy,
        ICompositionOutputBundleDestinationValidator bundleDestinationValidator)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _artifactIdentityPolicy = artifactIdentityPolicy ??
            throw new ArgumentNullException(nameof(artifactIdentityPolicy));
        _bundleDestinationValidator = bundleDestinationValidator ??
            throw new ArgumentNullException(nameof(bundleDestinationValidator));
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

    public CompositionOutputBundleProposal ResolveAcceptedBundleProposal(
        ActiveSessionSnapshot acceptedSession,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null)
    {
        CompositionOutputPreparation outputPreparation = ResolveAcceptedOutput(
            acceptedSession,
            ctrlRamVersionEdit);
        return CompositionOutputBundleProposer.Create(
            acceptedSession,
            outputPreparation,
            _artifactIdentityPolicy);
    }

    public CompositionOutputBundleDestinationValidation ValidateBundleDestination(
        CompositionOutputBundleIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return _bundleDestinationValidator.Validate(intent);
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
