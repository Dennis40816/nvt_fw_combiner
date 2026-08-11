using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Application-owned acceptance boundary that binds output naming to one compiled
/// capability publication and one immutable metadata inspection.
/// </summary>
public sealed class AcceptedOutputNamingInspection
{
    internal AcceptedOutputNamingInspection(
        string routeId,
        string compilationFingerprint,
        ResolvedMetadataPlan plan,
        MetadataInspectionSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ValidateFingerprint(compilationFingerprint, nameof(compilationFingerprint));
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.ResolutionToken != plan.ResolutionToken)
        {
            throw new ArgumentException(
                "Output naming inspection must use the metadata plan publication token.",
                nameof(snapshot));
        }

        if (snapshot.Results.Count != plan.Entries.Count ||
            snapshot.Results.Where((result, index) =>
                !ReferenceEquals(result.PlanEntry, plan.Entries[index])).Any())
        {
            throw new ArgumentException(
                "Output naming inspection must retain the exact resolved metadata plan entries.",
                nameof(snapshot));
        }

        RouteId = routeId;
        CompilationFingerprint = compilationFingerprint;
        Plan = plan;
        Snapshot = snapshot;
    }

    /// <summary>
    /// Accepts an inspection only when it belongs to the exact resolved capability
    /// publication and metadata plan.
    /// </summary>
    public static AcceptedOutputNamingInspection Accept(
        ResolvedCapability capability,
        MetadataInspectionSnapshot snapshot,
        long currentAuthoringRevision,
        IEnumerable<FirmwareArtifactPayload> currentArtifacts)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(currentAuthoringRevision);
        ArgumentNullException.ThrowIfNull(currentArtifacts);
        CapabilityPublicationCoherence.ValidateResolved(capability);
        FirmwareArtifactPayload[] artifacts = [.. currentArtifacts];
        return !MetadataInspectionPublicationGate.IsCurrent(
                   snapshot,
                   capability.MetadataPlan,
                   currentAuthoringRevision,
                   artifacts)
            ? throw new ArgumentException(
                "Output naming inspection must match the current capability publication, authoring revision, and immutable artifacts.",
                nameof(snapshot))
            : new AcceptedOutputNamingInspection(
                capability.Identity.RouteId,
                capability.CompiledComposition.CompilationFingerprint,
                capability.MetadataPlan,
                snapshot);
    }

    /// <summary>
    /// Accepts the canonical metadata publication already retained by one exact
    /// authoring session without reopening paths or decoding firmware again.
    /// </summary>
    public static AcceptedOutputNamingInspection Accept(ActiveSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ResolvedCapability capability = session.HasCurrentInputInspection
            ? session.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection) ??
                throw new InvalidOperationException(
                    "Output naming requires the exact inspected session capability.")
            : throw new InvalidOperationException(
                "Output naming requires one current complete input inspection.");
        MetadataInspectionSnapshot snapshot = session.MetadataInspection ??
            throw new InvalidOperationException(
                "Output naming requires the canonical session metadata inspection.");
        FirmwareArtifactPayload[] artifacts =
        [
            .. session.InputSlotStatuses.Select(status =>
            {
                ReadOnlyMemory<byte> bytes = status.AcceptedBytes ??
                    throw new InvalidOperationException(
                        $"Output naming input '{status.AddressSpaceId}' has no accepted bytes.");
                return new FirmwareArtifactPayload(status.AddressSpaceId, bytes.Span);
            }),
        ];
        return Accept(
            capability,
            snapshot,
            session.AuthoringRevision.Value,
            artifacts);
    }

    /// <summary>
    /// Captures the exact inspection and publication identity only for a compiled
    /// renderer that consumes canonical metadata.
    /// </summary>
    public static AcceptedOutputNamingPublication? TryAcceptForCompiledRenderer(
        ActiveSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ResolvedCapability capability = session.ExactCapability ??
            throw new InvalidOperationException(
                "Output naming requires one exact session capability.");
        CompiledOutputNameRendererKind renderer = capability.CompiledComposition
            .V2Details.OutputNamingRequirement.RendererKind;
        return renderer is not (
            CompiledOutputNameRendererKind.NormalFlashCodeV1 or
            CompiledOutputNameRendererKind.TpFirmwareV1)
                ? null
                : new AcceptedOutputNamingPublication(
                    Accept(session),
                    OutputNamingAdmissionIdentity.Capture(
                        capability,
                        session.AuthoringRevision.Value));
    }

    /// <summary>Stable exact route of the accepted capability publication.</summary>
    public string RouteId { get; }

    /// <summary>Fingerprint of the exact compiled composition used for naming.</summary>
    public string CompilationFingerprint { get; }

    /// <summary>Publication token shared by the accepted plan and inspection.</summary>
    public ResolutionToken ResolutionToken => Plan.ResolutionToken;

    /// <summary>Authoring revision inspected by the accepted snapshot.</summary>
    public long AuthoringRevision => Snapshot.AuthoringRevision;

    internal ResolvedMetadataPlan Plan { get; }

    internal MetadataInspectionSnapshot Snapshot { get; }

    /// <summary>
    /// Confirms that every output-naming artifact identity is the same full input
    /// or accepted execution prefix that the composition run read.
    /// </summary>
    internal bool IsCurrent(IReadOnlyList<InputArtifactSummary> inputSummaries)
    {
        ArgumentNullException.ThrowIfNull(inputSummaries);
        Dictionary<string, InputArtifactSummary> summaries;
        try
        {
            summaries = inputSummaries.ToDictionary(
                static summary => summary.AddressSpaceId,
                StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }

        Dictionary<string, FirmwareArtifactIdentity> identities;
        try
        {
            identities = Snapshot.ArtifactIdentities.ToDictionary(
                static identity => identity.ArtifactId,
                StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }

        string[] namingSpaceIds =
        [
            .. Plan.Entries
                .Where(static entry => entry.Definition.Purposes.Contains(
                    MetadataReferencePurpose.OutputNaming))
                .Select(static entry => entry.Definition.SpaceId)
                .Distinct(StringComparer.Ordinal),
        ];
        foreach (string spaceId in namingSpaceIds)
        {
            if (!identities.TryGetValue(spaceId, out FirmwareArtifactIdentity? identity) ||
                !summaries.TryGetValue(spaceId, out InputArtifactSummary? summary) ||
                !MatchesAcceptedExecutionInput(identity, summary))
            {
                return false;
            }
        }

        foreach (MetadataInspectionResult result in Snapshot.Results.Where(static result =>
                     result.PlanEntry.Definition.Purposes.Contains(
                         MetadataReferencePurpose.OutputNaming)))
        {
            string spaceId = result.PlanEntry.Definition.SpaceId;
            InputArtifactExecutionSnapshotSummary? executionSnapshot =
                summaries[spaceId].ExecutionSnapshot;
            if (executionSnapshot is null ||
                result.State != MetadataInspectionState.Value)
            {
                continue;
            }

            if (result.Resolution?.Resolved is not { } resolved ||
                !executionSnapshot.AcceptedRange.Contains(
                    resolved.LocatorOutcome.ResolvedRange.Range))
            {
                return false;
            }
        }

        return true;
    }

    internal bool TryGetOutputNamingSource(
        string metadataBindingId,
        string metadataSpaceId,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        out string? spaceId,
        out string? acceptedSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataSpaceId);
        ArgumentNullException.ThrowIfNull(inputSummaries);
        spaceId = null;
        acceptedSha256 = null;
        ResolvedMetadataPlanEntry[] matches =
        [
            .. Plan.Entries.Where(entry =>
                entry.Definition.Purposes.Contains(MetadataReferencePurpose.OutputNaming) &&
                StringComparer.Ordinal.Equals(
                    entry.Definition.BindingId,
                    metadataBindingId)),
        ];
        if (matches.Length != 1)
        {
            return false;
        }

        string selectedSpaceId = matches[0].Definition.SpaceId;
        if (!StringComparer.Ordinal.Equals(selectedSpaceId, metadataSpaceId))
        {
            return false;
        }

        spaceId = selectedSpaceId;
        FirmwareArtifactIdentity? identity = Snapshot.ArtifactIdentities.SingleOrDefault(
            candidate => StringComparer.Ordinal.Equals(candidate.ArtifactId, selectedSpaceId));
        InputArtifactSummary? inputSummary = inputSummaries.SingleOrDefault(
            candidate => StringComparer.Ordinal.Equals(
                candidate.AddressSpaceId,
                selectedSpaceId));
        acceptedSha256 = inputSummary?.ExecutionSnapshot?.AcceptedSha256 ??
            identity?.Sha256;
        return identity is not null;
    }

    private static bool MatchesAcceptedExecutionInput(
        FirmwareArtifactIdentity identity,
        InputArtifactSummary summary)
    {
        return (identity.LengthBytes == summary.Size &&
                StringComparer.Ordinal.Equals(identity.Sha256, summary.Sha256)) ||
            (summary.ExecutionSnapshot is { } executionSnapshot &&
             identity.LengthBytes == executionSnapshot.AcceptedSize &&
             StringComparer.Ordinal.Equals(identity.Sha256, executionSnapshot.AcceptedSha256));
    }

    private static void ValidateFingerprint(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 64 || value.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Compilation fingerprint must be 64 lowercase hexadecimal characters.",
                parameterName);
        }
    }
}

/// <summary>Exact canonical inspection and publication identity used by one naming run.</summary>
public sealed record AcceptedOutputNamingPublication(
    AcceptedOutputNamingInspection Inspection,
    OutputNamingAdmissionIdentity Admission);
