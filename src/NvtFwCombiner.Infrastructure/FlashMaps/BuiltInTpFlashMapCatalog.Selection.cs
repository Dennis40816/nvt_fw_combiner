using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Infrastructure.FlashMaps;

internal static partial class BuiltInTpFlashMapCatalog
{
    /// <summary>Selects an IC map using the shared Common FW interval rule without guessing between entries.</summary>
    internal static bool TrySelect(
        string icId,
        string? commonFwVersion,
        out TpFlashMapProfile? profile,
        out string? issue)
    {
        return CatalogInstance.TrySelect(icId, commonFwVersion, out profile, out issue);
    }

    /// <summary>Catalog state shared by shipped queries and synthetic-data tests.</summary>
    internal sealed class Catalog
    {
        private readonly Dictionary<string, TpFlashMapProfile[]> _profilesByIc;

        /// <summary>Creates an isolated query catalog from loaded, validated entries.</summary>
        internal Catalog(IReadOnlyList<TpFlashMapProfile> profiles)
        {
            _profilesByIc = profiles.GroupBy(static profile => profile.IcId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.OrderBy(static profile => profile.EffectiveCommonFwVersion).ToArray(),
                    StringComparer.Ordinal);
            IcIds = Array.AsReadOnly(_profilesByIc.Keys.Order(StringComparer.Ordinal).ToArray());
        }

        /// <summary>Each declared IC id once in ordinal order, including ICs that require version selection.</summary>
        internal IReadOnlyList<string> IcIds { get; }

        /// <summary>Finds a map only when the IC has exactly one entry.</summary>
        internal bool TryFind(string icId, [NotNullWhen(true)] out TpFlashMapProfile? profile)
        {
            profile = _profilesByIc.TryGetValue(icId, out TpFlashMapProfile[]? profiles) && profiles.Length == 1
                ? profiles[0]
                : null;
            return profile is not null;
        }

        /// <summary>Selects a map by Common FW version, returning a diagnostic on every failure.</summary>
        internal bool TrySelect(
            string icId,
            string? commonFwVersion,
            out TpFlashMapProfile? profile,
            out string? issue)
        {
            TpFlashMapProfile[] profiles = _profilesByIc.GetValueOrDefault(icId) ?? [];
            CommonFwVersionIntervalSelector.SelectionResult result = CommonFwVersionIntervalSelector.Select(
                profiles, static candidate => candidate.EffectiveCommonFwVersion, commonFwVersion,
                out int index, out LegacyCombinerCommonFwVersion version);
            profile = index >= 0 ? profiles[index] : null;
            issue = result switch
            {
                CommonFwVersionIntervalSelector.SelectionResult.Selected => null,
                CommonFwVersionIntervalSelector.SelectionResult.NoEntries =>
                    $"No TP flash-map profile is registered for {icId}.",
                CommonFwVersionIntervalSelector.SelectionResult.BelowMinimum =>
                    $"{icId} Common FW {version} is below the minimum supported version " +
                    $"{LegacyCombinerCommonFwVersion.MinimumSupported}.",
                CommonFwVersionIntervalSelector.SelectionResult.VersionRequired =>
                    $"{icId} has multiple TP flash-map profiles; a valid three-component " +
                    "base FWConfig Common FW version is required.",
                CommonFwVersionIntervalSelector.SelectionResult.NoMatchingInterval =>
                    $"No TP flash-map profile for {icId} applies to Common FW {version}.",
                _ => throw new InvalidOperationException("Unsupported interval selection result."),
            };
            return result == CommonFwVersionIntervalSelector.SelectionResult.Selected;
        }

        /// <summary>Gets visible regions only for an IC whose map needs no version selection.</summary>
        internal IReadOnlyList<TpFlashMapRegion> GetRegions(
            string icId,
            IcNumberSelection? selection,
            LegacyCombinerPostbuildProfile? postbuildProfile,
            TpFlashMapRegionKind? kind = null)
        {
            return TryFind(icId, out TpFlashMapProfile? profile)
                ? BuiltInTpFlashMapCatalog.GetRegions(profile, selection, postbuildProfile, kind)
                : [];
        }

        /// <summary>Gets plan-adjusted regions only for an IC whose map needs no version selection.</summary>
        internal IReadOnlyList<TpFlashMapRegion> GetRegionsForPlan(
            string icId,
            LegacyCombinerPostbuildCommandPlan postbuildPlan,
            TpFlashMapRegionKind? kind = null)
        {
            ArgumentNullException.ThrowIfNull(postbuildPlan);
            return TryFind(icId, out TpFlashMapProfile? profile)
                ? BuiltInTpFlashMapCatalog.GetRegionsForPlan(profile, postbuildPlan, kind)
                : [];
        }
    }
}
