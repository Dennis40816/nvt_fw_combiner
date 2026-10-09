using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.FlashMaps;

internal static partial class BuiltInTpFlashMapCatalog
{
    private const string RelativePath = "profiles/built-in/ctrlram-postbuild-v2/flash-map.json";
    private const string ExpectedSha256 = "1ba6eee0a139266121cda96e95f5e1f3dc1f0d317508a92ea71d9fc1970e67ed";
    private static LoadedCatalog LoadShippedCatalog()
    {
        string path = Path.Combine(AppContext.BaseDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return LoadCatalog(File.ReadAllBytes(path), ExpectedSha256);
    }

    /// <summary>Loads hash-checked schema 1.0 entries ordered by effective version within each IC.</summary>
    internal static IReadOnlyList<TpFlashMapProfile> Load(ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        return LoadCatalog(bytes, expectedSha256).Profiles;
    }

    /// <summary>Loads entries and the declared pending maps of one hash-checked catalog document.</summary>
    internal static LoadedCatalog LoadCatalog(ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        CatalogDocument document = PinnedJsonCatalogLoader.Load<CatalogDocument>(
            bytes,
            expectedSha256,
            "Built-in TP flash-map catalog",
            "Built-in TP flash-map catalog has invalid empty document.");

        if (document.SchemaVersion != "1.0" || document.Profiles is null)
        {
            throw Invalid($"schemaVersion '{document.SchemaVersion}' / profiles '{document.Profiles?.Count}'");
        }

        TpFlashMapProfile[] profiles = [.. document.Profiles.Select(CreateProfile)];
        PendingTpFlashMap[] pending = CreatePendingMaps(document.PendingMaps ?? []);
        ValidateSlots(profiles, pending);

        TpFlashMapProfile[] ordered = [.. profiles
            .GroupBy(static profile => profile.IcId, StringComparer.Ordinal)
            .SelectMany(static group => group.OrderBy(static profile => profile.EffectiveCommonFwVersion))];
        return new LoadedCatalog(Array.AsReadOnly(ordered), Array.AsReadOnly(pending));
    }

    private static PendingTpFlashMap[] CreatePendingMaps(IReadOnlyList<PendingMapDocument> sources)
    {
        return [.. sources.Select(static source =>
            !string.IsNullOrWhiteSpace(source.IcId) &&
            !string.IsNullOrWhiteSpace(source.Reason) &&
            LegacyCombinerCommonFwVersion.TryParse(source.FromCommonFwVersion, out LegacyCombinerCommonFwVersion from)
                ? new PendingTpFlashMap(source.IcId, from, source.Reason)
                : throw Invalid("pendingMaps entry"))];
    }

    /// <summary>
    /// Checks each IC's slots, the entries and the pending markers together: a version is used once and the first
    /// slot starts at the minimum supported version. An IC may have only pending markers.
    /// </summary>
    private static void ValidateSlots(IReadOnlyList<TpFlashMapProfile> profiles, IReadOnlyList<PendingTpFlashMap> pending)
    {
        (string IcId, LegacyCombinerCommonFwVersion Version)[] slots =
        [
            .. profiles.Select(static profile => (profile.IcId, profile.EffectiveCommonFwVersion)),
            .. pending.Select(static map => (map.IcId, map.FromCommonFwVersion)),
        ];
        if (slots.Distinct().Count() != slots.Length)
        {
            throw Invalid("duplicate IC id / effectiveCommonFwVersion");
        }

        foreach (IGrouping<string, (string IcId, LegacyCombinerCommonFwVersion Version)> group in
                 slots.GroupBy(static slot => slot.IcId, StringComparer.Ordinal))
        {
            if (group.Min(static slot => slot.Version) != LegacyCombinerCommonFwVersion.MinimumSupported)
            {
                throw Invalid($"{group.Key} first effectiveCommonFwVersion");
            }
        }
    }

    private static TpFlashMapProfile CreateProfile(ProfileDocument source)
    {
        IReadOnlyList<RegionDocument> regions = source.Regions ?? throw Invalid("profile.regions");
        return regions.Select(static region => region.RegionId).Distinct(StringComparer.Ordinal).Count() == regions.Count
            ? new TpFlashMapProfile(
                source.IcId,
                source.OverviewSource,
                source.FirmwareConfigPrimaryStart,
                source.TpPrefixLength,
                source.FullFlashCapacities ?? throw Invalid("profile.fullFlashCapacities"),
                source.BaseShapeEvidence,
                regions.Select(CreateRegion),
                source.Evidence,
                ParseEffectiveCommonFwVersion(source.EffectiveCommonFwVersion))
            : throw Invalid($"duplicate region id for {source.IcId}");
    }

    private static LegacyCombinerCommonFwVersion ParseEffectiveCommonFwVersion(JsonElement value)
    {
        // An omitted field is Undefined and defaults to the minimum; an explicit null or any non-string is malformed.
        return value.ValueKind == JsonValueKind.Undefined
            ? LegacyCombinerCommonFwVersion.MinimumSupported
            : value.ValueKind == JsonValueKind.String &&
              LegacyCombinerCommonFwVersion.TryParse(value.GetString(), out LegacyCombinerCommonFwVersion version)
                ? version
                : throw Invalid("effectiveCommonFwVersion");
    }

    private static TpFlashMapRegion CreateRegion(RegionDocument source)
    {
        return new TpFlashMapRegion(
            source.RegionId,
            source.DisplayName,
            source.Kind switch
            {
                "dp" => TpFlashMapRegionKind.Dp,
                "ctrlram" => TpFlashMapRegionKind.CtrlRam,
                "customer-info" => TpFlashMapRegionKind.CustomerInfo,
                "project-id" => TpFlashMapRegionKind.ProjectId,
                "other" => TpFlashMapRegionKind.Other,
                _ => throw Invalid("region.kind"),
            },
            new ByteRange(source.Start, source.Length),
            source.Visibility switch
            {
                "always" => TpFlashMapRegionVisibility.Always,
                "multi-chip-only" => TpFlashMapRegionVisibility.MultiChipOnly,
                "two-chip-and-above" => TpFlashMapRegionVisibility.TwoChipAndAbove,
                "three-chip-and-above" => TpFlashMapRegionVisibility.ThreeChipAndAbove,
                _ => throw Invalid("region.visibility"),
            },
            source.PostbuildFileName,
            source.Tags);
    }

    private static InvalidDataException Invalid(string name)
    {
        return new InvalidDataException($"Built-in TP flash-map catalog has invalid {name}.");
    }

    private sealed record CatalogDocument(
        [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
        [property: JsonPropertyName("profiles")] IReadOnlyList<ProfileDocument>? Profiles,
        [property: JsonPropertyName("pendingMaps")] IReadOnlyList<PendingMapDocument>? PendingMaps = null);

    private sealed record PendingMapDocument(
        [property: JsonPropertyName("icId")] string IcId,
        [property: JsonPropertyName("fromCommonFwVersion")] string FromCommonFwVersion,
        [property: JsonPropertyName("reason")] string Reason);

    private sealed record ProfileDocument(
        [property: JsonPropertyName("icId")] string IcId,
        [property: JsonPropertyName("overviewSource")] string OverviewSource,
        [property: JsonPropertyName("firmwareConfigPrimaryStart")] long FirmwareConfigPrimaryStart,
        [property: JsonPropertyName("tpPrefixLength")] long TpPrefixLength,
        [property: JsonPropertyName("fullFlashCapacities")] IReadOnlyList<long>? FullFlashCapacities,
        [property: JsonPropertyName("baseShapeEvidence")] string BaseShapeEvidence,
        [property: JsonPropertyName("evidence")] string Evidence,
        [property: JsonPropertyName("regions")] IReadOnlyList<RegionDocument>? Regions,
        [property: JsonPropertyName("effectiveCommonFwVersion")] JsonElement EffectiveCommonFwVersion);

    private sealed record RegionDocument(
        [property: JsonPropertyName("regionId")] string RegionId,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("start")] long Start,
        [property: JsonPropertyName("length")] long Length,
        [property: JsonPropertyName("visibility")] string Visibility,
        [property: JsonPropertyName("postbuildFileName")] string? PostbuildFileName,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags);

    /// <summary>Entries and pending-map declarations loaded from one catalog document.</summary>
    internal sealed record LoadedCatalog(
        IReadOnlyList<TpFlashMapProfile> Profiles,
        IReadOnlyList<PendingTpFlashMap> PendingMaps);

    /// <summary>
    /// Declares that an IC has no map from a Common FW version on, so no earlier slot may be used there.
    /// </summary>
    internal sealed record PendingTpFlashMap(string IcId, LegacyCombinerCommonFwVersion FromCommonFwVersion, string Reason);
}
