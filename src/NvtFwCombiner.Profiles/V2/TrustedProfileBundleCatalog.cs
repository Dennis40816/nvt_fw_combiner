using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>One immutable normalized firmware family and its exact trusted source entry.</summary>
internal sealed class TrustedFirmwareFamilyCatalogEntry
{
    internal TrustedFirmwareFamilyCatalogEntry(
        TrustedProfileBundleCatalogEntryIdentity identity,
        FirmwareFamilyResolutionDefinition family,
        IEnumerable<string> memberIds)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(memberIds);
        string[] members = [.. memberIds];
        if (members.Length == 0 ||
            members.Any(string.IsNullOrWhiteSpace) ||
            members.Distinct(StringComparer.Ordinal).Count() != members.Length)
        {
            throw new ArgumentException("Trusted family members must be nonempty and unique.", nameof(memberIds));
        }

        Identity = identity;
        Family = family;
        MemberIds = Array.AsReadOnly(members);
    }

    internal TrustedProfileBundleCatalogEntryIdentity Identity { get; }

    internal FirmwareFamilyResolutionDefinition Family { get; }

    /// <summary>Exact declared family members retained for non-map logical-profile admission.</summary>
    internal IReadOnlyList<string> MemberIds { get; }
}

/// <summary>One immutable normalized profile and the exact normalized family it binds.</summary>
internal sealed class TrustedCompositionProfileCatalogEntry
{
    internal TrustedCompositionProfileCatalogEntry(
        TrustedProfileBundleCatalogEntryIdentity identity,
        CompositionProfileDefinition profile,
        TrustedFirmwareFamilyCatalogEntry family)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(family);
        Identity = identity;
        EntryIdentity = new ProfileBundleEntryIdentity(identity.EntryId, identity.ContentHash);
        Profile = profile;
        Family = family;
    }

    internal TrustedProfileBundleCatalogEntryIdentity Identity { get; }

    internal ProfileBundleEntryIdentity EntryIdentity { get; }

    internal CompositionProfileDefinition Profile { get; }

    internal TrustedFirmwareFamilyCatalogEntry Family { get; }
}

/// <summary>Atomic immutable catalog of normalized family/profile facts from one trusted bundle projection.</summary>
internal sealed partial class TrustedProfileBundleCatalog
{
    private readonly TrustedFirmwareFamilyCatalogEntry[] _families;
    private readonly TrustedCompositionProfileCatalogEntry[] _profiles;

    internal TrustedProfileBundleCatalog(
        ProfileBundleIdentity bundleIdentity,
        string manifestSha256,
        IEnumerable<TrustedFirmwareFamilyCatalogEntry> families,
        IEnumerable<TrustedCompositionProfileCatalogEntry> profiles)
    {
        ArgumentNullException.ThrowIfNull(bundleIdentity);
        ProfileBundleIdentity.ValidateSha256(manifestSha256, nameof(manifestSha256));
        _families = ImmutableReferenceSnapshot.Create(
            families,
            "Trusted catalog entries cannot contain null values.");
        _profiles = ImmutableReferenceSnapshot.Create(
            profiles,
            "Trusted catalog entries cannot contain null values.");
        Array.Sort(_families, static (left, right) => CompareFamily(left, right));
        Array.Sort(_profiles, static (left, right) => CompareProfile(left, right));

        BundleIdentity = bundleIdentity;
        ManifestSha256 = manifestSha256;
        Families = Array.AsReadOnly(_families);
        Profiles = Array.AsReadOnly(_profiles);
    }

    internal ProfileBundleIdentity BundleIdentity { get; }

    internal string ManifestSha256 { get; }

    internal IReadOnlyList<TrustedFirmwareFamilyCatalogEntry> Families { get; }

    internal IReadOnlyList<TrustedCompositionProfileCatalogEntry> Profiles { get; }

    private static int CompareFamily(
        TrustedFirmwareFamilyCatalogEntry left,
        TrustedFirmwareFamilyCatalogEntry right)
    {
        int familyId = StringComparer.Ordinal.Compare(left.Family.FamilyId, right.Family.FamilyId);
        if (familyId != 0)
        {
            return familyId;
        }

        int version = StringComparer.Ordinal.Compare(left.Family.FamilyVersion, right.Family.FamilyVersion);
        return version != 0
            ? version
            : StringComparer.Ordinal.Compare(left.Identity.EntryId, right.Identity.EntryId);
    }

    private static int CompareProfile(
        TrustedCompositionProfileCatalogEntry left,
        TrustedCompositionProfileCatalogEntry right)
    {
        int profileId = StringComparer.Ordinal.Compare(left.Profile.ProfileId, right.Profile.ProfileId);
        if (profileId != 0)
        {
            return profileId;
        }

        int version = StringComparer.Ordinal.Compare(left.Profile.ProfileVersion, right.Profile.ProfileVersion);
        return version != 0
            ? version
            : StringComparer.Ordinal.Compare(left.Identity.EntryId, right.Identity.EntryId);
    }
}
