using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal sealed partial class TrustedProfileBundleCatalog
{
    private const string ProfileSelectionNotFound = "profile.v2.selection.not-found";

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
