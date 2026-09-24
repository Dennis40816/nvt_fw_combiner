using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal sealed record BankReferenceReplaceAdmission(BankReferenceReplaceDefinition Definition, string IcCountVariant);

internal sealed partial class TrustedProfileBundleCatalog
{
    private const string PerfectAbFamilyId = "nt51919-nt51929-nt51932-ab-merge";
    private const string PartialAbFamilyId = "nt51950-ab-merge";

    /// <summary>Only Profiles joins exact trusted AB layouts to an existing same-IC local CtrlRAM parent.</summary>
    internal BankReferenceReplaceAdmission? TryCreateBankReplaceAdmission(TrustedProfileBundleCatalog local,
        string memberId, string localMemberId, string layoutProfileId, string layoutProfileVersion,
        string layoutMapId, string localProfileId, string localProfileVersion, string localMapId)
    {
        ArgumentNullException.ThrowIfNull(local);
        if (!StringComparer.Ordinal.Equals(memberId, localMemberId)) { return null; }
        TrustedCompositionProfileCatalogEntry? layoutProfile = SelectProfile(layoutProfileId, layoutProfileVersion, out _);
        TrustedCompositionProfileCatalogEntry? localProfile = local.SelectProfile(localProfileId, localProfileVersion, out _);
        if (layoutProfile is null || localProfile is null ||
            layoutProfile.Profile.Header.ExperienceId != ExperienceIds.AbMerge ||
            localProfile.Profile.Header.ExperienceId != ExperienceIds.CtrlRamReplace ||
            !layoutProfile.Family.MemberIds.Contains(memberId, StringComparer.Ordinal) ||
            !localProfile.Family.MemberIds.Contains(memberId, StringComparer.Ordinal))
        {
            return null;
        }

        (BankReferenceDefinitionSource layoutSource, FirmwareImageMap layoutMap) =
            Source(this, layoutProfile, memberId, ExperienceIds.AbMerge, layoutMapId);
        (BankReferenceDefinitionSource localSource, FirmwareImageMap localMap) =
            Source(local, localProfile, memberId, ExperienceIds.CtrlRamReplace, localMapId);
        if (StringComparer.Ordinal.Equals(layoutProfile.Family.Family.FamilyId, PartialAbFamilyId))
        {
            return TryPartialAdmission(localProfile, layoutSource, localSource,
                layoutMap, localMap, memberId);
        }
        if (!StringComparer.Ordinal.Equals(layoutProfile.Family.Family.FamilyId, PerfectAbFamilyId) ||
            localProfile.Family.Family.FamilyId is not ("nt51929-ctrlram-replace" or "nt51932-ctrlram-replace"))
        {
            return null;
        }
        TopologyRequirement topology = localMap.Applicability.TopologyRequirement;
        string? countVariant = topology.Kind == TopologyRequirementKind.SingleChip ? "1-ic" :
            topology.Kind == TopologyRequirementKind.Cascade && topology.MinimumChipCount == 2 &&
            topology.MaximumChipCount == 8 ? "2-8-ic" : null;
        bool eligible = countVariant is not null && layoutMap.CapacityBytes == 0x80000 && localMap.CapacityBytes == 0x40000 &&
            layoutMap.Regions.Any(static region => region.RegionId == "ab-image") &&
            layoutMap.Regions.Any(static region => region.RegionId == "tpa-code") &&
            layoutMap.Regions.Any(static region => region.RegionId == "tpb-code") &&
            localMap.Regions.Any(static region => region.RegionId == "fw-config-source") &&
            localMap.Regions.Any(static region => region.RegionId == "nf-ctrlram") &&
            localMap.Regions.Any(static region => region.RegionId == "normal-ctrlram") &&
            localMap.Regions.Any(static region => region.RegionId == "vn-ctrlram") &&
            (countVariant != "1-ic" || localMap.Regions.Any(static region => region.RegionId == "fw-config-backup")) &&
            (countVariant != "2-8-ic" || localMap.Regions.Any(static region => region.RegionId == "diff-ctrlram"));
        return eligible ? new(new BankReferenceReplaceDefinition(layoutSource, localSource), countVariant!) : null;
    }

    /// <summary>Resolves an exact closed composite from trusted declarations, without firmware bytes.</summary>
    internal BankReferenceReplaceDefinition CreateBankReplaceDefinition(TrustedProfileBundleCatalog local,
        string memberId, string layoutProfileId, string layoutProfileVersion, string layoutMapId,
        string localProfileId, string localProfileVersion, string localMapId)
    {
        return TryCreateBankReplaceAdmission(local, memberId, memberId, layoutProfileId, layoutProfileVersion,
            layoutMapId, localProfileId, localProfileVersion, localMapId)?.Definition ??
            throw new ArgumentException("AB Replace parents have no trusted bank binding.");
    }

    /// <summary>Decode the native chip count through the selected local family's existing NVT Backup definition.</summary>
    internal static int ReadPartialBankNativeCount(FirmwareFamilyResolutionDefinition family,
        BankReferenceDefinitionSource local, ReadOnlySpan<byte> bankLocalBytes)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(local);
        if (family.FamilyId != $"{local.MemberId.ToLowerInvariant()}-ctrlram-replace" ||
            bankLocalBytes.Length != local.CapacityBytes)
        {
            throw new ArgumentException("Native count requires the exact trusted local Reference slice.");
        }
        var input = new FirmwareMapResolutionInputs(local.MemberId, ExperienceIds.CtrlRamReplace,
            local.CapacityBytes, null, [new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, bankLocalBytes)]);
        FirmwareMetadataStructureResolution resolution = family.ResolveMetadataStructure(
            local.MapId, $"{local.MemberId.ToLowerInvariant()}-fwconfig-backup-envelope", input);
        ulong? count = resolution.Resolved?.DecodedStructure.Facts.SingleOrDefault(static fact =>
            fact.FieldId == "chip-count")?.Value.UnsignedIntegerValue;
        return count is >= 1 and <= 2 ? checked((int)count.Value) :
            throw new ArgumentException(resolution.Status == FirmwareMetadataStructureResolutionStatus.Resolved
                ? $"AB native IC count Read {(count is null ? "unavailable" : count.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))}; expected 1 or 2."
                : $"AB native IC count unreadable: {resolution.Failure}.");
    }

    private static BankReferenceReplaceAdmission? TryPartialAdmission(
        TrustedCompositionProfileCatalogEntry localProfile,
        BankReferenceDefinitionSource layoutSource,
        BankReferenceDefinitionSource localSource,
        FirmwareImageMap layoutMap,
        FirmwareImageMap localMap,
        string memberId)
    {
        string? countVariant = (memberId, layoutSource.ProfileId, layoutMap.MapId,
            localSource.ProfileId, localMap.MapId) switch
        {
            ("NT51950", "nt51950-ab-merge", "nt51950-ab-merge-512k",
                "nt51950-ctrlram-replace-fw200-single", "nt51950-ctrlram-fw200-single-full-flash") => "1-ic",
            ("NT51950", "nt51950-ab-merge-cascade", "nt51950-ab-merge-1024k",
                "nt51950-ctrlram-replace-fw1x-cascade", "nt51950-ctrlram-fw1x-cascade-full-flash") => "2-ic",
            ("NT51951", "nt51951-ab-merge", "nt51951-ab-merge-1024k",
                "nt51951-ctrlram-replace-fw200-single", "nt51951-ctrlram-fw200-single-full-flash") => "1-ic",
            ("NT51951", "nt51951-ab-merge", "nt51951-ab-merge-1024k",
                "nt51951-ctrlram-replace-fw1x-cascade", "nt51951-ctrlram-fw1x-cascade-full-flash") => "2-ic",
            _ => null,
        };
        bool eligible = countVariant is not null &&
            localProfile.Family.Family.FamilyId == $"{memberId.ToLowerInvariant()}-ctrlram-replace" &&
            layoutMap.CapacityBytes % 2 == 0 &&
            localMap.CapacityBytes <= layoutMap.CapacityBytes / 2 &&
            localMap.CapacityBytes == (memberId == "NT51950" ? 0x40000 : 0x80000) &&
            layoutMap.Regions.Any(static region => region.RegionId == "ab-image") &&
            layoutMap.Regions.Any(static region => region.RegionId == "a-tp-code") &&
            layoutMap.Regions.Any(static region => region.RegionId == "b-tp-code") &&
            localMap.Regions.Any(static region => region.RegionId == "fw-config-source" &&
                region.Range.Start == 0x22200 && region.Range.Length == 0x780) &&
            localMap.Regions.Any(static region => region.RegionId == "fw-config-backup" &&
                region.Range.Start == 0x36000 && region.Range.Length == 0x780) &&
            localMap.Regions.Any(static region => region.RegionId == "nf-ctrlram") &&
            localMap.Regions.Any(static region => region.RegionId == "normal-ctrlram") &&
            localMap.Regions.Any(static region => region.RegionId == "vn-ctrlram") &&
            (countVariant == "2-ic" ?
                localMap.Applicability.TopologyRequirement.Kind == TopologyRequirementKind.ExactCount &&
                localMap.Applicability.TopologyRequirement.ExactChipCount == 2 :
                localMap.Applicability.TopologyRequirement.Kind == TopologyRequirementKind.SingleChip);
        return eligible ? new(new BankReferenceReplaceDefinition(layoutSource, localSource,
            new ByteRange(0, localMap.CapacityBytes), BankReferenceFinalizationKind.RunAbHeaderProcessor), countVariant!) : null;
    }

    private static (BankReferenceDefinitionSource Definition, FirmwareImageMap Map) Source(
        TrustedProfileBundleCatalog catalog, TrustedCompositionProfileCatalogEntry profile,
        string memberId, string mode, string mapId)
    {
        string profileId = profile.Profile.ProfileId;
        string version = profile.Profile.ProfileVersion;
        IReadOnlyList<FirmwareImageMap> maps = catalog.GetMapVariants(profileId, version, memberId, mode,
            out _, out IReadOnlyList<CompositionIssue> issues);
        FirmwareImageMap map = issues.Count == 0
            ? maps.SingleOrDefault(candidate => candidate.MapId == mapId) ??
                throw new ArgumentException($"AB Replace parent map '{mapId}' is unavailable.")
            : throw new ArgumentException(string.Join("; ", issues.Select(static issue => issue.Message)));
        return (new BankReferenceDefinitionSource(profileId, version, catalog.BundleIdentity, profile.EntryIdentity,
            profile.Family.Family.FamilyContentHash, memberId, map.MapId, map.CapacityBytes), map);
    }
}
