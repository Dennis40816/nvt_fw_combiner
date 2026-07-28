using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Application-owned acceptance boundary that binds output naming to one compiled
/// capability publication and one immutable metadata inspection.
/// </summary>
public sealed class AcceptedOutputNamingInspection
{
    internal AcceptedOutputNamingInspection(
        string capabilityFingerprint,
        ResolvedMetadataPlan plan,
        MetadataInspectionSnapshot snapshot)
    {
        ValidateFingerprint(capabilityFingerprint, nameof(capabilityFingerprint));
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

        CapabilityFingerprint = capabilityFingerprint;
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
        FirmwareArtifactPayload[] artifacts = [.. currentArtifacts];
        return !StringComparer.Ordinal.Equals(
                   capability.CapabilityFingerprint,
                   capability.CompiledComposition.CompilationFingerprint) ||
               capability.ResolutionToken != capability.MetadataPlan.ResolutionToken ||
               !MetadataInspectionPublicationGate.IsCurrent(
                   snapshot,
                   capability.MetadataPlan,
                   currentAuthoringRevision,
                   artifacts)
            ? throw new ArgumentException(
                "Output naming inspection must match the current capability publication, authoring revision, and immutable artifacts.",
                nameof(snapshot))
            : new AcceptedOutputNamingInspection(
                capability.CapabilityFingerprint,
                capability.MetadataPlan,
                snapshot);
    }

    /// <summary>Fingerprint of the exact compiled capability used for naming.</summary>
    public string CapabilityFingerprint { get; }

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
        string structureId,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        out string? spaceId,
        out string? acceptedSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(structureId);
        ArgumentNullException.ThrowIfNull(inputSummaries);
        spaceId = null;
        acceptedSha256 = null;
        ResolvedMetadataPlanEntry[] matches =
        [
            .. Plan.Entries.Where(entry =>
                entry.Definition.Purposes.Contains(MetadataReferencePurpose.OutputNaming) &&
                StringComparer.Ordinal.Equals(
                    entry.Definition.StructureDefinition.StructureId,
                    structureId)),
        ];
        if (matches.Length != 1)
        {
            return false;
        }

        string selectedSpaceId = matches[0].Definition.SpaceId;
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
                "Capability fingerprint must be 64 lowercase hexadecimal characters.",
                parameterName);
        }
    }
}
