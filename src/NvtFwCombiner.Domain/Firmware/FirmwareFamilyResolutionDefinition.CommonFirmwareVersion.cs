// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    /// <summary>The canonical version-selection artifact, or null for unconditioned families.</summary>
    public string? CommonFirmwareVersionArtifactBindingId => _imageMaps
        .Where(static map => map.Applicability.CommonFirmwareVersionFrom is not null)
        .Select(map => VersionStructure(map).ArtifactBindingId).Distinct(StringComparer.Ordinal).SingleOrDefault();

    private static IEnumerable<IGrouping<TopologyRequirement, FirmwareImageMap>> VersionGroups(
        IEnumerable<FirmwareImageMap> maps)
    {
        return maps.GroupBy(static map => map.Applicability.TopologyRequirement)
            .Where(static group => group.Any(static map => map.Applicability.CommonFirmwareVersionFrom is not null));
    }

    private static void ValidateCommonFirmwareVersionSlots(FirmwareImageMap[] maps)
    {
        IEnumerable<IGrouping<(string Member, string Mode, TopologyRequirement Topology, long Capacity), FirmwareImageMap>> groups =
            maps.SelectMany(map => map.Applicability.MemberIds.SelectMany(member =>
                map.Applicability.ModeIds.Select(mode => (Member: member, Mode: mode, Map: map))))
                .GroupBy(static item => (item.Member, item.Mode, item.Map.Applicability.TopologyRequirement, item.Map.CapacityBytes),
                    static item => item.Map);
        foreach (IGrouping<(string Member, string Mode, TopologyRequirement Topology, long Capacity), FirmwareImageMap> group in
            groups.Where(static group => group.Any(static map => map.Applicability.CommonFirmwareVersionFrom is not null)))
        {
            CommonFirmwareVersion[] slots = [.. group.Select(static map =>
                map.Applicability.CommonFirmwareVersionFrom ?? CommonFirmwareVersion.MinimumSupported)];
            DomainInvariant.Reject(slots[0] != CommonFirmwareVersion.MinimumSupported,
                "Common FW map slots must start at 1.0.0.");
            for (int index = 1; index < slots.Length; index++)
            {
                DomainInvariant.Reject(slots[index] <= slots[index - 1],
                    "Common FW map slots must be unique and declared in increasing order.");
            }
        }
    }

    private FirmwareMetadataStructure VersionStructure(FirmwareImageMap map)
    {
        return GetStructuresForMap(map.MapId).SingleOrDefault(static structure =>
            structure.StructureId == CommonFirmwareVersionMetadata.StructureId) ??
            throw new ArgumentException("Version-conditioned maps require canonical FWConfig metadata.");
    }

    private void ValidateCommonFirmwareVersionMetadata()
    {
        bool versionConditioned = _imageMaps.Any(static map => map.Applicability.CommonFirmwareVersionFrom is not null);
        bool hasAliases = _imageMaps.Any(static map =>
            map.RegionSetBindings.Any(static binding => binding.Provenance.AliasChain.Count != 0) ||
            map.MetadataSetBindings.Any(static binding => binding.Provenance.AliasChain.Count != 0)) ||
            _capabilityBindings.Any(static binding => binding.Provenance.AliasChain.Count != 0);
        DomainInvariant.Reject(versionConditioned && hasAliases,
            "Version-conditioned families do not admit fact aliases.");
        foreach (FirmwareImageMap map in _imageMaps.Where(static map => map.Applicability.CommonFirmwareVersionFrom is not null))
        {
            FirmwareMetadataStructure anchor = VersionStructure(map);
            string[] fields = [CommonFirmwareVersionMetadata.MajorFieldId,
                CommonFirmwareVersionMetadata.MinorFieldId, CommonFirmwareVersionMetadata.AdditionalFieldId];
            DomainInvariant.Reject(fields.Any(id => !anchor.Fields.Any(field => field.FieldId == id &&
                field.WidthBytes == 1 && field.Encoding == FirmwareMetadataEncoding.UnsignedInteger)),
                "Version-conditioned maps require the three canonical Common FW bytes.");
            foreach (FirmwareImageMap candidate in _imageMaps.Where(candidate =>
                candidate.CapacityBytes == map.CapacityBytes &&
                candidate.Applicability.TopologyRequirement == map.Applicability.TopologyRequirement &&
                candidate.Applicability.MemberIds.Intersect(map.Applicability.MemberIds, StringComparer.Ordinal).Any() &&
                candidate.Applicability.ModeIds.Intersect(map.Applicability.ModeIds, StringComparer.Ordinal).Any()))
            {
                DomainInvariant.Reject(!ReferenceEquals(anchor, VersionStructure(candidate)),
                    "Common FW slots must share the same canonical FWConfig binding and locator.");
            }
        }
    }

    private FirmwareMapResolutionRejectionKind? SelectVersionMaps(
        FirmwareMapResolutionInputs inputs, out IReadOnlySet<string> excluded, out CommonFirmwareVersion? version)
    {
        HashSet<string> rejected = new(StringComparer.Ordinal);
        version = null;
        excluded = rejected;
        // Select against the complete family group before applying a profile's map subset.
        foreach (IGrouping<TopologyRequirement, FirmwareImageMap> group in VersionGroups(_imageMaps.Where(map =>
            map.Applicability.MemberIds.Contains(inputs.MemberId, StringComparer.Ordinal) &&
            map.Applicability.ModeIds.Contains(inputs.ModeId, StringComparer.Ordinal) &&
            map.CapacityBytes == inputs.CapacityBytes &&
            (inputs.RequestedTopology is null || map.Applicability.TopologyRequirement.Matches(inputs.RequestedTopology)))))
        {
            FirmwareImageMap[] slots = [.. group.OrderBy(static map =>
                map.Applicability.CommonFirmwareVersionFrom ?? CommonFirmwareVersion.MinimumSupported)];
            FirmwareMetadataStructureResolution read = ResolveMetadataStructure(
                slots[0].MapId, CommonFirmwareVersionMetadata.StructureId, inputs);
            CommonFirmwareVersion? decoded = read.Resolved is { } resolved &&
                CommonFirmwareVersionMetadata.TryRead(resolved.DecodedStructure, out CommonFirmwareVersion parsed)
                    ? parsed : null;
            CommonFwVersionIntervalSelector.SelectionResult result = CommonFwVersionIntervalSelector.Select(
                slots, static map => map.Applicability.CommonFirmwareVersionFrom ?? CommonFirmwareVersion.MinimumSupported,
                decoded, out int selected);
            if (result == CommonFwVersionIntervalSelector.SelectionResult.BelowMinimum)
            { return FirmwareMapResolutionRejectionKind.CommonFirmwareVersionBelowMinimum; }
            if (result != CommonFwVersionIntervalSelector.SelectionResult.Selected)
            { return FirmwareMapResolutionRejectionKind.CommonFirmwareVersionRequired; }
            version = decoded;
            rejected.UnionWith(slots.Where((_, index) => index != selected).Select(static map => map.MapId));
        }
        return null;
    }
}
