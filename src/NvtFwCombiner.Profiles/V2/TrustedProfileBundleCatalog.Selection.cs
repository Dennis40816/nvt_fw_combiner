using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>One exact trusted profile/map declaration, without an input snapshot or executable plan.</summary>
internal sealed record TrustedMapBoundProfileDeclaration(
    TrustedCompositionProfileCatalogEntry ProfileEntry,
    FirmwareImageMap Map,
    string MemberId,
    string ModeId)
{
    internal SourceEnvelopeProfileBinding? SourceEnvelopeBinding =>
        ProfileEntry.Profile.Header.SourceEnvelopeBinding;
}

internal sealed partial class TrustedProfileBundleCatalog
{
    private const string ProfileSelectionNotFound = "profile.v2.selection.not-found";

    /// <summary>Selects trusted fixed map facts without compiling or accepting any firmware artifact.</summary>
    internal TrustedMapBoundProfileDeclaration GetMapBoundDeclaration(
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        if (!TryResolveMapCandidates(
                profileId, profileVersion, memberId, modeId,
                out TrustedCompositionProfileCatalogEntry? profileEntry,
                out FirmwareImageMap[] candidates,
                out IReadOnlyList<CompositionIssue> issues))
        {
            throw new InvalidDataException(
                $"Trusted declaration is unavailable: {string.Join(", ", issues.Select(static issue => issue.Code))}.");
        }

        FirmwareImageMap map = candidates.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.MapId, mapId)) ??
            throw new InvalidDataException($"Trusted declaration has no exact map '{mapId}'.");
        SourceEnvelopeProfileBinding? envelope = profileEntry.Profile.Header.SourceEnvelopeBinding;
        return envelope is not null && !candidates.Any(candidate =>
                StringComparer.Ordinal.Equals(candidate.MapId, envelope.LayoutTemplateMapId))
            ? throw new InvalidDataException(
                "Trusted source-envelope declaration names a template outside its exact map set.")
            : new TrustedMapBoundProfileDeclaration(profileEntry, map, memberId, modeId);
    }

    /// <summary>Returns the exact immutable catalog entry without version or declaration-order fallback.</summary>
    internal TrustedCompositionProfileCatalogEntry? SelectProfile(
        string profileId,
        string profileVersion,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        TrustedCompositionProfileCatalogEntry? profile = Profiles.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.Profile.ProfileId, profileId) &&
            StringComparer.Ordinal.Equals(candidate.Profile.ProfileVersion, profileVersion));
        issues = profile is null
            ? [new CompositionIssue(
                ProfileSelectionNotFound,
                $"Trusted catalog does not contain profile '{profileId}' version '{profileVersion}'.")]
            : [];
        return profile;
    }

    internal bool OwnsProfile(TrustedCompositionProfileCatalogEntry profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Profiles.Any(candidate => ReferenceEquals(candidate, profile));
    }
}
