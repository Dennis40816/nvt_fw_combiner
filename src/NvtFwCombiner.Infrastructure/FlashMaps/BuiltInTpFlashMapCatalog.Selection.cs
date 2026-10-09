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
        return ShippedCatalog.TrySelect(icId, commonFwVersion, out profile, out issue);
    }

    /// <summary>
    /// Reports the "not provided" reasons of an IC whose slots are all markers, so a caller can refuse it
    /// without a readable Common FW version.
    /// </summary>
    internal static bool TryGetNotProvidedIssue(string icId, out string? issue)
    {
        return ShippedCatalog.TryGetNotProvidedIssue(icId, out issue);
    }

    /// <summary>Catalog state shared by shipped queries and synthetic-data tests.</summary>
    internal sealed class Catalog
    {
        private readonly Dictionary<string, Slot[]> _slotsByIc;

        /// <summary>Creates an isolated query catalog from loaded, validated entries and pending-map declarations.</summary>
        internal Catalog(
            IReadOnlyList<TpFlashMapProfile> profiles,
            IReadOnlyList<PendingTpFlashMap>? pendingMaps = null)
        {
            _slotsByIc = profiles
                .Select(static profile => new Slot(profile.IcId, profile.EffectiveCommonFwVersion, profile, null))
                .Concat((pendingMaps ?? []).Select(static map =>
                    new Slot(map.IcId, map.FromCommonFwVersion, null, map.Reason)))
                .GroupBy(static slot => slot.IcId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.OrderBy(static slot => slot.Version).ToArray(),
                    StringComparer.Ordinal);
            IcIds = Array.AsReadOnly(_slotsByIc.Keys.Order(StringComparer.Ordinal).ToArray());
        }

        /// <summary>Each declared IC id once in ordinal order, including ICs that require version selection.</summary>
        internal IReadOnlyList<string> IcIds { get; }

        /// <summary>Finds a map only when the IC has exactly one slot and that slot is a map.</summary>
        internal bool TryFind(string icId, [NotNullWhen(true)] out TpFlashMapProfile? profile)
        {
            profile = _slotsByIc.TryGetValue(icId, out Slot[]? slots) && slots.Length == 1
                ? slots[0].Profile
                : null;
            return profile is not null;
        }

        /// <summary>Joins the reasons of an IC that has markers and no map at all.</summary>
        internal bool TryGetNotProvidedIssue(string icId, out string? issue)
        {
            bool markersOnly = _slotsByIc.TryGetValue(icId, out Slot[]? slots) &&
                slots.All(static slot => slot.Profile is null);
            issue = markersOnly ? string.Join("; ", slots!.Select(static slot => slot.Reason)) : null;
            return markersOnly;
        }

        /// <summary>Selects a map by Common FW version, returning a diagnostic on every failure.</summary>
        internal bool TrySelect(
            string icId,
            string? commonFwVersion,
            out TpFlashMapProfile? profile,
            out string? issue)
        {
            // A "not provided" marker is a slot like a map: the greatest slot at or below the version decides.
            Slot[] slots = _slotsByIc.GetValueOrDefault(icId) ?? [];
            CommonFwVersionIntervalSelector.SelectionResult result = CommonFwVersionIntervalSelector.Select(
                slots, static slot => slot.Version, commonFwVersion,
                out int index, out LegacyCombinerCommonFwVersion version);
            profile = index >= 0 ? slots[index].Profile : null;
            issue = result switch
            {
                CommonFwVersionIntervalSelector.SelectionResult.Selected => slots[index].Reason,
                CommonFwVersionIntervalSelector.SelectionResult.NoEntries =>
                    $"No TP flash-map profile is registered for {icId}.",
                CommonFwVersionIntervalSelector.SelectionResult.BelowMinimum =>
                    $"{icId} Common FW {version} is below the minimum supported version " +
                    $"{LegacyCombinerCommonFwVersion.MinimumSupported}.",
                CommonFwVersionIntervalSelector.SelectionResult.VersionRequired =>
                    $"{icId} has several TP flash-map slots; a valid three-component " +
                    "base FWConfig Common FW version is required.",
                CommonFwVersionIntervalSelector.SelectionResult.NoMatchingInterval =>
                    $"No TP flash-map profile for {icId} applies to Common FW {version}.",
                _ => throw new InvalidOperationException("Unsupported interval selection result."),
            };
            return profile is not null;
        }

        /// <summary>Uses the slot selection issue for display, retaining the existing sole-map fallback.</summary>
        internal TpFlashMapProfile? FindForDisplay(string icId, string? version, out string? issue)
        {
            if (TrySelect(icId, version, out TpFlashMapProfile? selected, out issue))
            {
                return selected;
            }
            if (TryFind(icId, out TpFlashMapProfile? sole))
            {
                issue = null;
                return sole;
            }
            return null;
        }

        /// <summary>Gets visible regions only for an IC whose sole slot is a map.</summary>
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

        /// <summary>Gets plan-adjusted regions only for an IC whose sole slot is a map.</summary>
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

        private sealed record Slot(
            string IcId,
            LegacyCombinerCommonFwVersion Version,
            TpFlashMapProfile? Profile,
            string? Reason);
    }
}
