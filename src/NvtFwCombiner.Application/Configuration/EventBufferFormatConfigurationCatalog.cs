using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Configuration;

/// <summary>One declared map's TP B effect in its address space; not a currently selected output.</summary>
public sealed record EventBufferFormatOutputEffect(
    string UniqueId, string MemberId, string MapId, string AddressSpaceId, ByteRange TpBRange);

/// <summary>Read-only projection of an exact family's configuration policy and canonical regions.</summary>
public sealed class EventBufferFormatConfigurationCatalog
{
    internal EventBufferFormatConfigurationCatalog(
        string scopeId, IEnumerable<EventBufferFormatIdentity> identities, IEnumerable<EventBufferFormatOutputEffect> effects)
    {
        ScopeId = scopeId;
        Identities = Array.AsReadOnly(identities.ToArray());
        OutputEffects = Array.AsReadOnly(effects.ToArray());
    }

    /// <summary>Canonical scope to which the editable recognition values belong.</summary>
    public string ScopeId { get; }

    /// <summary>Owner-defined identities; aliases do not add or change these keys.</summary>
    public IReadOnlyList<EventBufferFormatIdentity> Identities { get; }

    /// <summary>All declared member/map effects for configurable identities, retaining applicability.</summary>
    public IReadOnlyList<EventBufferFormatOutputEffect> OutputEffects { get; }

    internal static EventBufferFormatConfigurationCatalog FromFamily(FirmwareFamilyResolutionDefinition family)
    {
        ArgumentNullException.ThrowIfNull(family);
        FirmwareAbFormatPolicy policy = family.AbFormatPolicy
            ?? throw new ArgumentException("Family has no Event Buffer configuration policy.", nameof(family));
        EventBufferFormatIdentity[] identities = [.. policy.Formats.Select(format =>
            new EventBufferFormatIdentity(format.UniqueId, format.DisplayName))];
        EventBufferFormatOutputEffect[] effects = [.. policy.Variants
            .Where(variant => variant.FormatId != policy.CommonFormatId)
            .Select(variant =>
            {
                FirmwareImageMap map = family.ImageMaps.Single(item => item.MapId == variant.MapId);
                FirmwareRegion tpB = map.Regions.Single(region => region.RegionId == "b-tp-code");
                return new EventBufferFormatOutputEffect(variant.FormatId, variant.MemberId, map.MapId, map.AddressSpaceId, tpB.Range);
            })];
        return new(policy.ScopeId, identities, effects);
    }
}
