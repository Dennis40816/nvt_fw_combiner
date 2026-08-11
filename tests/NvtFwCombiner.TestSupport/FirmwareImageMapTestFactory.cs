using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.TestSupport;

/// <summary>Builds alias-free physical maps used only by synthetic tests.</summary>
public static class FirmwareImageMapTestFactory
{
    /// <summary>Creates direct member/map bindings without adding a production construction seam.</summary>
    public static FirmwareImageMap CreateDirect(
        string mapId,
        string addressSpaceId,
        FirmwareMapApplicability applicability,
        FirmwareImageMapCoveragePolicy coveragePolicy,
        IEnumerable<FirmwareRegionSet> regionSets,
        IEnumerable<FirmwareMetadataSet> metadataSets,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(applicability);
        ArgumentNullException.ThrowIfNull(regionSets);
        ArgumentNullException.ThrowIfNull(metadataSets);
        var factApplicability = FirmwareFactApplicability.FromMap(applicability);
        return new FirmwareImageMap(
            mapId,
            addressSpaceId,
            applicability,
            coveragePolicy,
            CreateBindings(mapId, applicability.MemberIds, regionSets, factApplicability),
            CreateBindings(mapId, applicability.MemberIds, metadataSets, factApplicability),
            evidenceRefs);
    }

    private static IEnumerable<FirmwareMapFactBinding<TFact>> CreateBindings<TFact>(
        string mapId,
        IEnumerable<string> memberIds,
        IEnumerable<TFact> facts,
        FirmwareFactApplicability applicability)
        where TFact : class, IFirmwareMapFact
    {
        TFact[] snapshot = [.. facts];
        return snapshot.Any(static fact => fact is null)
            ? throw new ArgumentException("Direct map facts cannot contain null.", nameof(facts))
            : memberIds.SelectMany(memberId => snapshot.Select(fact =>
        {
            FirmwareMapFactKey key = new(memberId, mapId, fact.FactKind, fact.CanonicalFactId);
            return new FirmwareMapFactBinding<TFact>(
                applicability,
                new FirmwareFactProvenance(key, fact, []));
        }));
    }
}
