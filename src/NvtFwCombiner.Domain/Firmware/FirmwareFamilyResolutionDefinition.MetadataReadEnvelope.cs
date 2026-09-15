namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    /// <summary>Returns the complete source prefix needed to locate and decode one canonical metadata structure.</summary>
    internal long GetMaximumMetadataReadEnd(
        ResolvedFirmwareImageMap resolvedMap,
        FirmwareMetadataStructure structure)
    {
        ArgumentNullException.ThrowIfNull(resolvedMap);
        return GetMaximumMetadataReadEnd(resolvedMap.ImageMap, structure);
    }

    internal long GetMaximumMetadataReadEnd(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(structure);
        return structure.Locator switch
        {
            FirmwareAbsoluteRangeLocator absolute => absolute.Range.Range.EndExclusive,
            FirmwareRegionRelativeLocator relative => checked(
                map.Regions.Single(region => StringComparer.Ordinal.Equals(
                    region.RegionId,
                    relative.RegionId)).Range.Start + relative.Offset + structure.LengthBytes),
            FirmwareMarkerRelativeLocator marker => Math.Max(
                marker.SearchRange.Range.EndExclusive,
                checked(marker.SearchRange.Range.EndExclusive - marker.MarkerBytes.Length +
                    marker.ResultOffset + structure.LengthBytes)),
            FirmwareMetadataFieldSelectedLocator selected => Math.Max(
                GetMaximumMetadataReadEnd(
                    map,
                    ResolveStructure(map.MapId, selected.PrerequisiteStructureId)),
                selected.Branches.Max(branch => checked(
                    branch.AnchorRange.Range.Start + selected.ResultOffset + structure.LengthBytes))),
            _ => throw new InvalidOperationException(
                $"Unknown locator for canonical metadata structure '{structure.StructureId}'."),
        };
    }

    private FirmwareMetadataStructure ResolveStructure(string mapId, string structureId)
    {
        return TryResolveStructure(mapId, structureId, out FirmwareMetadataStructure? structure)
            ? structure
            : throw new InvalidOperationException(
                $"Canonical map '{mapId}' does not contain metadata structure '{structureId}'.");
    }
}
