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
    private readonly Func<ActiveSessionSnapshot, CancellationToken,
        ValueTask<(AbMergeFormatSelection? Selection, IReadOnlyList<CompositionIssue> Issues)>>? _assessFormat;

    internal CompositionOutputNamingExperience(
        ICanonicalCapabilityQuery catalog,
        ISystemClock clock,
        ICompositionArtifactIdentityPolicy artifactIdentityPolicy,
        ICompositionOutputBundleDestinationValidator bundleDestinationValidator,
        Func<ActiveSessionSnapshot, CancellationToken,
            ValueTask<(AbMergeFormatSelection? Selection, IReadOnlyList<CompositionIssue> Issues)>>? assessFormat = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _artifactIdentityPolicy = artifactIdentityPolicy ??
            throw new ArgumentNullException(nameof(artifactIdentityPolicy));
        _bundleDestinationValidator = bundleDestinationValidator ??
            throw new ArgumentNullException(nameof(bundleDestinationValidator));
        _assessFormat = assessFormat;
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

    public async ValueTask<CompositionOutputBundleProposal> PrepareBundleProposalAsync(
        ActiveSessionSnapshot acceptedSession, CancellationToken cancellationToken,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AbMergeFormatSelection? format = await CaptureFormatAsync(acceptedSession, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        CompositionOutputBundleProposal proposal = ResolveAcceptedBundleProposal(acceptedSession, ctrlRamVersionEdit);
        return new(proposal.FolderName, proposal.OutputPreparation, proposal.ResolvedAtUtc, proposal.Admission)
        {
            Confirmation = CompositionOutputConfirmationProjector.Create(acceptedSession, _artifactIdentityPolicy, format, proposal.OutputPreparation),
            ConfirmationSession = acceptedSession,
        };
    }

    public async ValueTask<bool> IsProposalCurrentAsync(CompositionOutputBundleProposal proposal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        cancellationToken.ThrowIfCancellationRequested();
        if (proposal.ConfirmationSession is not { ExactCapability: { } capability } session ||
            _catalog.ResolveCurrentCompilation(capability.CompiledComposition, capability) is null) { return false; }
        if (session.WorkflowId != ExperienceIds.AbMerge) { return true; }
        if (_assessFormat is null) { return false; }
        (AbMergeFormatSelection? current, IReadOnlyList<CompositionIssue> issues) =
            await _assessFormat(session, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        CompositionOutputFormatSummary? captured = proposal.Confirmation?.Format;
        return issues.Count == 0 && (current is null ? captured is null : captured is not null &&
            current.FormatId == captured.FormatId && current.DisplayName == captured.DisplayName &&
            current.ConfigurationSourceSha256 == captured.ConfigurationSourceSha256);
    }

    private async ValueTask<AbMergeFormatSelection?> CaptureFormatAsync(ActiveSessionSnapshot session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.WorkflowId != ExperienceIds.AbMerge) { return null; }
        if (_assessFormat is null) { throw new InvalidOperationException("AB confirmation requires the canonical format assessor."); }
        (AbMergeFormatSelection? format, IReadOnlyList<CompositionIssue> issues) =
            await _assessFormat(session, cancellationToken).ConfigureAwait(false);
        return issues.Count != 0 ? throw new CompositionPreRunRefusalException(issues) : format;
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
