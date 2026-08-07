using System.Text.Json;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Reports one deterministic semantic failure while creating a normalized trusted profile catalog.</summary>
internal sealed class TrustedProfileBundleCatalogException : Exception
{
    internal TrustedProfileBundleCatalogException(
        string code,
        string message,
        string? entryId = null,
        string? entryPath = null,
        string? semanticPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        EntryId = entryId;
        EntryPath = entryPath;
        SemanticPath = semanticPath;
    }

    internal string Code { get; }

    internal string? EntryId { get; }

    internal string? EntryPath { get; }

    internal string? SemanticPath { get; }
}

/// <summary>Creates the sole immutable Profiles-owned semantic catalog from trusted bundle JSON trees.</summary>
internal static class TrustedProfileBundleCatalogFactory
{
    private const string DuplicateSourceEntryId = "profile-bundle.catalog.source-entry-id-duplicate";
    private const string FamilyNormalizationFailed = "profile-bundle.catalog.family-normalization-failed";
    private const string FamilyIdentityDuplicate = "profile-bundle.catalog.family-identity-duplicate";
    private const string ProfileNormalizationFailed = "profile-bundle.catalog.profile-normalization-failed";
    private const string ProfileIdentityDuplicate = "profile-bundle.catalog.profile-identity-duplicate";
    private const string ProfileFamilyMissing = "profile-bundle.catalog.profile-family-missing";
    private const string ProfileFamilyAmbiguous = "profile-bundle.catalog.profile-family-ambiguous";
    private const string ProfileMapMissing = "profile-bundle.catalog.profile-map-missing";
    private const string ProfileRequiredRegionMissing = "profile-bundle.catalog.profile-required-region-missing";
    private const string ProfileRequiredMetadataMissing = "profile-bundle.catalog.profile-required-metadata-missing";
    private const string ProfileMetadataTargetMissing = "profile-bundle.catalog.profile-metadata-target-missing";
    private const string LogicalProfileMemberMissing = "profile-bundle.catalog.logical-member-missing";

    /// <summary>Normalizes one complete trusted source atomically without map resolution or plan compilation.</summary>
    internal static TrustedProfileBundleCatalog Create(
        TrustedProfileBundleCatalogSource source,
        IFirmwareMetadataStructureDefinitionResolver? metadataDefinitionResolver = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateUniqueSourceEntryIds(source);
        TrustedFirmwareFamilyCatalogEntry[] families = NormalizeFamilies(
            source.Families,
            metadataDefinitionResolver);
        ValidateUniqueFamilyIdentities(families);
        TrustedCompositionProfileCatalogEntry[] profiles = NormalizeProfiles(source.Profiles, families);
        ValidateUniqueProfileIdentities(profiles);
        return new TrustedProfileBundleCatalog(source.BundleIdentity, source.ManifestSha256, families, profiles);
    }

    private static void ValidateUniqueSourceEntryIds(TrustedProfileBundleCatalogSource source)
    {
        TrustedProfileBundleCatalogEntryIdentity[] identities =
        [
            .. source.Families.Select(static family => family.Identity),
            .. source.Profiles.Select(static profile => profile.Identity),
        ];
        foreach (IGrouping<string, TrustedProfileBundleCatalogEntryIdentity> group in identities
                     .GroupBy(static identity => identity.EntryId, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            if (group.Skip(1).Any())
            {
                TrustedProfileBundleCatalogEntryIdentity first = group.First();
                throw Error(
                    DuplicateSourceEntryId,
                    $"Trusted bundle source repeats entry id '{group.Key}'.",
                    first);
            }
        }
    }

    private static TrustedFirmwareFamilyCatalogEntry[] NormalizeFamilies(
        IReadOnlyList<TrustedFirmwareFamilyJsonSource> sources,
        IFirmwareMetadataStructureDefinitionResolver? metadataDefinitionResolver)
    {
        TrustedFirmwareFamilyJsonSource[] ordered = [.. sources];
        Array.Sort(ordered, static (left, right) =>
            StringComparer.Ordinal.Compare(left.Identity.EntryId, right.Identity.EntryId));
        var families = new TrustedFirmwareFamilyCatalogEntry[ordered.Length];
        for (int index = 0; index < ordered.Length; index++)
        {
            TrustedFirmwareFamilyJsonSource source = ordered[index];
            FirmwareFamilyDocument document = DeserializeFamily(source);
            try
            {
                families[index] = new TrustedFirmwareFamilyCatalogEntry(
                    source.Identity,
                    FirmwareFamilyResolutionNormalizer.Normalize(
                        document,
                        source.Identity.ContentHash,
                        metadataDefinitionResolver),
                    document.Members.Select(static member => member.MemberId));
            }
            catch (FirmwareFamilyNormalizationException exception)
            {
                throw Error(FamilyNormalizationFailed, exception.Message, source.Identity, exception.Path, exception);
            }
            catch (ArgumentException exception)
            {
                throw Error(FamilyNormalizationFailed, exception.Message, source.Identity, innerException: exception);
            }
        }

        return families;
    }

    private static void ValidateUniqueFamilyIdentities(
        IReadOnlyList<TrustedFirmwareFamilyCatalogEntry> families)
    {
        foreach (IGrouping<(string FamilyId, string FamilyVersion), TrustedFirmwareFamilyCatalogEntry> group in families
                     .GroupBy(static entry => (entry.Family.FamilyId, entry.Family.FamilyVersion))
                     .OrderBy(static group => group.Key.FamilyId, StringComparer.Ordinal)
                     .ThenBy(static group => group.Key.FamilyVersion, StringComparer.Ordinal))
        {
            if (group.Skip(1).Any())
            {
                TrustedFirmwareFamilyCatalogEntry first = group.OrderBy(
                    static entry => entry.Identity.EntryId,
                    StringComparer.Ordinal).First();
                throw Error(
                    FamilyIdentityDuplicate,
                    $"Trusted bundle declares family '{group.Key.FamilyId}' version '{group.Key.FamilyVersion}' more than once.",
                    first.Identity);
            }
        }
    }

    private static TrustedCompositionProfileCatalogEntry[] NormalizeProfiles(
        IReadOnlyList<TrustedCompositionProfileJsonSource> sources,
        IReadOnlyList<TrustedFirmwareFamilyCatalogEntry> families)
    {
        TrustedCompositionProfileJsonSource[] ordered = [.. sources];
        Array.Sort(ordered, static (left, right) =>
            StringComparer.Ordinal.Compare(left.Identity.EntryId, right.Identity.EntryId));
        var profiles = new TrustedCompositionProfileCatalogEntry[ordered.Length];
        for (int index = 0; index < ordered.Length; index++)
        {
            TrustedCompositionProfileJsonSource source = ordered[index];
            CompositionProfileDefinition profile = DeserializeAndNormalizeProfile(source);
            TrustedFirmwareFamilyCatalogEntry family = FindBoundFamily(profile, families, source.Identity);
            switch (profile.Header.CompilationContextKind)
            {
                case V2CompilationContextKind.ResolvedMap:
                case V2CompilationContextKind.RuntimeReferenceReplace:
                    ValidateDeclaredMaps(profile, family, source.Identity);
                    break;
                case V2CompilationContextKind.LogicalOutput:
                    ValidateLogicalMembers(profile, family, source.Identity);
                    break;
                default:
                    throw new InvalidOperationException("Unknown composition-profile compilation context.");
            }
            profiles[index] = new TrustedCompositionProfileCatalogEntry(source.Identity, profile, family);
        }

        return profiles;
    }

    private static void ValidateUniqueProfileIdentities(
        IReadOnlyList<TrustedCompositionProfileCatalogEntry> profiles)
    {
        foreach (IGrouping<(string ProfileId, string ProfileVersion), TrustedCompositionProfileCatalogEntry> group in profiles
                     .GroupBy(static entry => (entry.Profile.ProfileId, entry.Profile.ProfileVersion))
                     .OrderBy(static group => group.Key.ProfileId, StringComparer.Ordinal)
                     .ThenBy(static group => group.Key.ProfileVersion, StringComparer.Ordinal))
        {
            if (group.Skip(1).Any())
            {
                TrustedCompositionProfileCatalogEntry first = group.OrderBy(
                    static entry => entry.Identity.EntryId,
                    StringComparer.Ordinal).First();
                throw Error(
                    ProfileIdentityDuplicate,
                    $"Trusted bundle declares profile '{group.Key.ProfileId}' version '{group.Key.ProfileVersion}' more than once.",
                    first.Identity);
            }
        }
    }

    private static FirmwareFamilyDocument DeserializeFamily(TrustedFirmwareFamilyJsonSource source)
    {
        try
        {
            return JsonSerializer.Deserialize(
                source.Document,
                ProfileBundleSemanticJsonContext.Default.FirmwareFamilyDocument) ?? throw Error(
                FamilyNormalizationFailed,
                "Trusted family JSON could not deserialize to a canonical document.",
                source.Identity);
        }
        catch (JsonException exception)
        {
            throw Error(FamilyNormalizationFailed, exception.Message, source.Identity, innerException: exception);
        }
    }

    private static CompositionProfileDefinition DeserializeAndNormalizeProfile(
        TrustedCompositionProfileJsonSource source)
    {
        try
        {
            CompositionProfileDocument document = JsonSerializer.Deserialize(
                source.Document,
                ProfileBundleSemanticJsonContext.Default.CompositionProfileDocument) ?? throw Error(
                ProfileNormalizationFailed,
                "Trusted composition-profile JSON could not deserialize to a canonical document.",
                source.Identity);
            return CompositionProfileNormalizer.Normalize(document);
        }
        catch (CompositionProfileNormalizationException exception)
        {
            throw Error(ProfileNormalizationFailed, exception.Message, source.Identity, exception.Path, exception);
        }
        catch (JsonException exception)
        {
            throw Error(ProfileNormalizationFailed, exception.Message, source.Identity, innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw Error(ProfileNormalizationFailed, exception.Message, source.Identity, innerException: exception);
        }
    }

    private static TrustedFirmwareFamilyCatalogEntry FindBoundFamily(
        CompositionProfileDefinition profile,
        IReadOnlyList<TrustedFirmwareFamilyCatalogEntry> families,
        TrustedProfileBundleCatalogEntryIdentity profileIdentity)
    {
        TrustedFirmwareFamilyCatalogEntry[] matches =
        [
            .. families.Where(candidate =>
                StringComparer.Ordinal.Equals(candidate.Family.FamilyId, profile.Header.FamilyId) &&
                StringComparer.Ordinal.Equals(candidate.Family.FamilyVersion, profile.Header.FamilyVersion) &&
                StringComparer.Ordinal.Equals(candidate.Family.FamilyContentHash, profile.Header.FamilyContentHash)),
        ];
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw Error(
                ProfileFamilyMissing,
                $"Profile '{profile.ProfileId}' does not bind an exact trusted firmware family.",
                profileIdentity),
            _ => throw Error(
                ProfileFamilyAmbiguous,
                $"Profile '{profile.ProfileId}' binds multiple trusted firmware families.",
                profileIdentity),
        };
    }

    private static void ValidateDeclaredMaps(
        CompositionProfileDefinition profile,
        TrustedFirmwareFamilyCatalogEntry family,
        TrustedProfileBundleCatalogEntryIdentity profileIdentity)
    {
        foreach (string mapId in profile.MapBinding.MapIds)
        {
            FirmwareImageMap map = family.Family.ImageMaps.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.MapId, mapId)) ?? throw Error(
                    ProfileMapMissing,
                    $"Profile '{profile.ProfileId}' names unknown map '{mapId}' in its exact trusted family.",
                    profileIdentity);
            ValidateStaticMapContract(profile, family.Family, map, profileIdentity);
        }
    }

    internal static void ValidateStaticMapContract(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareImageMap map,
        TrustedProfileBundleCatalogEntryIdentity profileIdentity)
    {
        var regionIds = map.Regions.Select(static region => region.RegionId).ToHashSet(StringComparer.Ordinal);
        string? missingRegion = profile.MapBinding.RequiredRegionIds.FirstOrDefault(id => !regionIds.Contains(id));
        if (missingRegion is not null)
        {
            throw Error(
                ProfileRequiredRegionMissing,
                $"Profile '{profile.ProfileId}' requires region '{missingRegion}' missing from map '{map.MapId}'.",
                profileIdentity);
        }

        string? missingStructure = profile.MapBinding.RequiredMetadataStructureIds.FirstOrDefault(id =>
            !family.TryResolveStructure(map.MapId, id, out _));
        if (missingStructure is not null)
        {
            throw Error(
                ProfileRequiredMetadataMissing,
                $"Profile '{profile.ProfileId}' requires metadata structure '{missingStructure}' missing from map '{map.MapId}'.",
                profileIdentity);
        }

        foreach (CompositionProfileMetadataBinding binding in profile.MetadataBindings)
        {
            _ = family.TryResolveStructure(map.MapId, binding.StructureId, out FirmwareMetadataStructure? structure);
            FirmwareMetadataReferenceTarget? missingTarget = binding.TargetReferences.FirstOrDefault(target =>
                !structure!.Definition.ContainsReferenceTarget(target));
            if (missingTarget is not null)
            {
                throw Error(
                    ProfileMetadataTargetMissing,
                    $"Profile metadata binding '{binding.BindingId}' references unknown {missingTarget.Kind} target '{missingTarget.TargetId}' in map '{map.MapId}'.",
                    profileIdentity);
            }
        }
    }

    private static void ValidateLogicalMembers(
        CompositionProfileDefinition profile,
        TrustedFirmwareFamilyCatalogEntry family,
        TrustedProfileBundleCatalogEntryIdentity profileIdentity)
    {
        foreach (string memberId in profile.LogicalOutputMemberIds)
        {
            if (!family.MemberIds.Contains(memberId, StringComparer.Ordinal))
            {
                throw Error(
                    LogicalProfileMemberMissing,
                    $"Logical-output profile '{profile.ProfileId}' names member '{memberId}' outside its exact trusted family.",
                    profileIdentity);
            }
        }
    }

    private static TrustedProfileBundleCatalogException Error(
        string code,
        string message,
        TrustedProfileBundleCatalogEntryIdentity identity,
        string? semanticPath = null,
        Exception? innerException = null)
    {
        return new TrustedProfileBundleCatalogException(
            code,
            message,
            identity.EntryId,
            identity.Path,
            semanticPath,
            innerException);
    }

    private static TrustedProfileBundleCatalogException Error(
        string code,
        string message,
        TrustedProfileBundleCatalogEntryIdentity identity,
        Exception innerException)
    {
        return Error(code, message, identity, semanticPath: null, innerException);
    }

}
