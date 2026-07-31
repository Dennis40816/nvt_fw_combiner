using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Additional DP Replace authoring payloads approved before executable per-IC DP Replace profiles exist.</summary>
public sealed record DpReplaceAdditionalPayloadRule(
    string IcId,
    string RegionId,
    string SlotId,
    string Title,
    string Description,
    string AddressSpaceId);

/// <summary>Catalog-owned DP Replace authoring policy for pending per-IC DP/LD evidence.</summary>
public static class DpReplaceAuthoringCatalog
{
    private static readonly DpReplaceAdditionalPayloadRule[] AdditionalPayloadRules =
    [
        new(
            "NT51928",
            "dp-ldc-51928",
            "replace-ldc",
            "LDC replacement BIN",
            "NT51928-only LDC payload under DP Replace.",
            CompositionAddressSpaceIds.LdcReplacement),
    ];

    private static readonly Dictionary<string, DpReplaceAdditionalPayloadRule[]> AdditionalPayloadRulesByIc =
        AdditionalPayloadRules
            .GroupBy(rule => rule.IcId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

    /// <summary>Gets additional payload rules for one IC.</summary>
    public static IReadOnlyList<DpReplaceAdditionalPayloadRule> GetAdditionalPayloads(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return AdditionalPayloadRulesByIc.TryGetValue(IcSupportCatalog.NormalizeIcId(icId), out DpReplaceAdditionalPayloadRule[]? rules)
            ? rules
            : [];
    }

    /// <summary>Returns true when the DP-kind region is approved for an additional DP Replace payload slot.</summary>
    public static bool IsAdditionalPayloadRegion(string icId, string regionId)
    {
        return TryGetAdditionalPayload(icId, regionId, out _);
    }

    /// <summary>Finds the additional payload rule for one IC and TP Overview region id.</summary>
    public static bool TryGetAdditionalPayload(
        string icId,
        string regionId,
        [NotNullWhen(true)] out DpReplaceAdditionalPayloadRule? rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        rule = GetAdditionalPayloads(icId).FirstOrDefault(candidate =>
            string.Equals(candidate.RegionId, regionId, StringComparison.Ordinal));
        return rule is not null;
    }
}
