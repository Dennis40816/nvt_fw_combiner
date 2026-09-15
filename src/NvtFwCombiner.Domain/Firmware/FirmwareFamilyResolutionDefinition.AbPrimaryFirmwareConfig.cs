using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    /// <summary>
    /// Inspects the member's equivalent canonical primary bindings without selecting an output map.
    /// True means the policy declares the pair; each result may still be pending or rejected.
    /// Result map IDs identify locator context only and confer no execution authority.
    /// </summary>
    public bool TryResolveAbPrimaryFirmwareConfig(
        string memberId,
        IEnumerable<FirmwareArtifactPayload> artifacts,
        TopologySelection? topology,
        [NotNullWhen(true)] out FirmwareMetadataStructureResolution? tpA,
        [NotNullWhen(true)] out FirmwareMetadataStructureResolution? tpB)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(artifacts);
        tpA = null;
        tpB = null;
        FirmwareAbFormatVariant? context = AbFormatPolicy?.Variants
            .Where(variant => StringComparer.Ordinal.Equals(variant.MemberId, memberId))
            .OrderBy(static variant => variant.MapId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (context is null)
        {
            return false;
        }

        FirmwareImageMap map = _imageMaps.Single(candidate => candidate.MapId == context.MapId);
        var inputs = new FirmwareMapResolutionInputs(memberId, "ab-merge", map.CapacityBytes, topology, artifacts);
        FirmwareAbPrimaryBindings bindings = AbFormatPolicy!.PrimaryBindings;
        tpA = ResolveMetadataStructureCore(map, ResolveStructure(map.MapId, bindings.TpAStructureId), inputs);
        tpB = ResolveMetadataStructureCore(map, ResolveStructure(map.MapId, bindings.TpBStructureId), inputs);
        return true;
    }

    internal void ValidateAbPrimaryContext(FirmwareImageMap anchor, FirmwareImageMap candidate, string structureId)
    {
        FirmwareMetadataStructure source = ResolveStructure(anchor.MapId, structureId);
        FirmwareMetadataStructure target = ResolveStructure(candidate.MapId, structureId);
        DomainInvariant.Reject(
            !ReferenceEquals(source, target) || anchor.AddressSpaceId != candidate.AddressSpaceId ||
            GetMaximumMetadataReadEnd(anchor, source) != GetMaximumMetadataReadEnd(candidate, target),
            "A/B primary discovery requires equivalent canonical structures and read envelopes across variants.");

        RequireEquivalentRegion(source.Locator.AllowedResultRegionId);
        if (source.Locator is FirmwareRegionRelativeLocator relative)
        {
            RequireEquivalentRegion(relative.RegionId);
        }
        else if (source.Locator is FirmwareMetadataFieldSelectedLocator selected)
        {
            // Family construction already validates the prerequisite graph and rejects cycles.
            ValidateAbPrimaryContext(anchor, candidate, selected.PrerequisiteStructureId);
        }

        void RequireEquivalentRegion(string regionId)
        {
            DomainInvariant.Reject(
                anchor.Regions.Single(region => region.RegionId == regionId).Range !=
                candidate.Regions.Single(region => region.RegionId == regionId).Range,
                $"A/B primary discovery region '{regionId}' must have the same range across variants.");
        }
    }
}
