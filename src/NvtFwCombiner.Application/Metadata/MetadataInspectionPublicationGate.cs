using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>Rejects metadata results produced from stale publication or authoring inputs.</summary>
public static class MetadataInspectionPublicationGate
{
    /// <summary>Checks exact resolution token, authoring revision, and artifact identities.</summary>
    public static bool IsCurrent(
        MetadataInspectionSnapshot snapshot,
        ResolvedMetadataPlan currentPlan,
        long currentAuthoringRevision,
        IEnumerable<FirmwareArtifactPayload> currentArtifacts)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(currentPlan);
        ArgumentOutOfRangeException.ThrowIfNegative(currentAuthoringRevision);
        ArgumentNullException.ThrowIfNull(currentArtifacts);
        FirmwareArtifactIdentity[] currentIdentities =
        [
            .. currentArtifacts
                .Select(static artifact => artifact.Identity)
                .OrderBy(static identity => identity.ArtifactId, StringComparer.Ordinal),
        ];
        return snapshot.ResolutionToken == currentPlan.ResolutionToken &&
               snapshot.AuthoringRevision == currentAuthoringRevision &&
               snapshot.ArtifactIdentities.SequenceEqual(currentIdentities);
    }
}
