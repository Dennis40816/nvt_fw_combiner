using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal sealed partial class TrustedProfileBundleCatalog
{
    private const string ProfileSelectionNotFound = "profile.v2.selection.not-found";
    private static readonly object ProfileSelectionMintingKey = new();

    /// <summary>Creates one exact catalog-owned profile selection without version or declaration-order fallback.</summary>
    internal ProfileSelectionResult SelectProfile(string profileId, string profileVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        TrustedCompositionProfileCatalogEntry? profile = Profiles.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.Profile.ProfileId, profileId) &&
            StringComparer.Ordinal.Equals(candidate.Profile.ProfileVersion, profileVersion));
        return profile is null
            ? ProfileSelectionResult.Failed(new CompositionIssue(
                ProfileSelectionNotFound,
                $"Trusted catalog does not contain profile '{profileId}' version '{profileVersion}'."))
            : ProfileSelectionResult.Succeeded(new ProfileSelection(
                ProfileSelectionMintingKey,
                BundleIdentity,
                new ProfileBundleEntryIdentity(profile.Identity.EntryId, profile.Identity.ContentHash),
                profile.Profile.ProfileId,
                profile.Profile.ProfileVersion));
    }

    internal bool TryResolveSelection(
        ProfileSelection selection,
        [NotNullWhen(true)] out TrustedCompositionProfileCatalogEntry? profile)
    {
        ArgumentNullException.ThrowIfNull(selection);
        profile = null;
        if (!HasExactBundleIdentity(selection.BundleIdentity))
        {
            return false;
        }

        profile = Profiles.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.Identity.EntryId, selection.ProfileEntryIdentity.EntryId) &&
            StringComparer.Ordinal.Equals(candidate.Identity.ContentHash, selection.ProfileEntryIdentity.ContentHash) &&
            StringComparer.Ordinal.Equals(candidate.Profile.ProfileId, selection.ProfileId) &&
            StringComparer.Ordinal.Equals(candidate.Profile.ProfileVersion, selection.ProfileVersion));
        return profile is not null;
    }

    private bool HasExactBundleIdentity(ProfileBundleIdentity identity)
    {
        return StringComparer.Ordinal.Equals(identity.BundleId, BundleIdentity.BundleId) &&
            StringComparer.Ordinal.Equals(identity.BundleVersion, BundleIdentity.BundleVersion) &&
            StringComparer.Ordinal.Equals(identity.ContentHash, BundleIdentity.ContentHash) &&
            StringComparer.Ordinal.Equals(identity.TrustAnchorBindingId, BundleIdentity.TrustAnchorBindingId);
    }

    /// <summary>Opaque exact profile token minted only by a trusted catalog selection.</summary>
    internal sealed class ProfileSelection
    {
        internal ProfileSelection(
            object mintingKey,
            ProfileBundleIdentity bundleIdentity,
            ProfileBundleEntryIdentity profileEntryIdentity,
            string profileId,
            string profileVersion)
        {
            if (!ReferenceEquals(mintingKey, ProfileSelectionMintingKey))
            {
                throw new ArgumentException("Profile selections may be minted only by their trusted catalog.", nameof(mintingKey));
            }

            ArgumentNullException.ThrowIfNull(bundleIdentity);
            ArgumentNullException.ThrowIfNull(profileEntryIdentity);
            ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
            ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
            BundleIdentity = bundleIdentity;
            ProfileEntryIdentity = profileEntryIdentity;
            ProfileId = profileId;
            ProfileVersion = profileVersion;
        }

        /// <summary>Exact trusted bundle identity from which this selection was minted.</summary>
        internal ProfileBundleIdentity BundleIdentity { get; }

        /// <summary>Exact allowlisted profile entry identity.</summary>
        internal ProfileBundleEntryIdentity ProfileEntryIdentity { get; }

        /// <summary>Selected normalized profile id.</summary>
        internal string ProfileId { get; }

        /// <summary>Selected normalized profile version.</summary>
        internal string ProfileVersion { get; }
    }

    /// <summary>Structured outcome of an exact trusted profile selection.</summary>
    internal sealed class ProfileSelectionResult
    {
        private readonly CompositionIssue[] _issues;

        private ProfileSelectionResult(ProfileSelection? selection, IEnumerable<CompositionIssue> issues)
        {
            _issues = ImmutableReferenceSnapshot.Create(
                issues,
                "A profile selection must contain either one token or unique failure issues.");
            if (_issues.Select(static issue => issue.Code).Distinct(StringComparer.Ordinal).Count() != _issues.Length ||
                (selection is null) != (_issues.Length != 0))
            {
                throw new ArgumentException("A profile selection must contain either one token or unique failure issues.");
            }

            Array.Sort(_issues, static (left, right) => StringComparer.Ordinal.Compare(left.Code, right.Code));
            Selection = selection;
            Issues = Array.AsReadOnly(_issues);
        }

        /// <summary>Catalog-minted selection token when exact lookup succeeded; otherwise null.</summary>
        internal ProfileSelection? Selection { get; }

        /// <summary>Deterministic exact-selection diagnostics.</summary>
        internal IReadOnlyList<CompositionIssue> Issues { get; }

        /// <summary>True only when one exact profile token was selected.</summary>
        internal bool IsSelected => Selection is not null;

        internal static ProfileSelectionResult Succeeded(ProfileSelection selection)
        {
            ArgumentNullException.ThrowIfNull(selection);
            return new ProfileSelectionResult(selection, []);
        }

        internal static ProfileSelectionResult Failed(CompositionIssue issue)
        {
            ArgumentNullException.ThrowIfNull(issue);
            return new ProfileSelectionResult(selection: null, [issue]);
        }
    }
}
