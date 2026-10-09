using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.FlashMaps;

internal static partial class BuiltInTpFlashMapCatalog
{
    private const string RelativePath = "profiles/built-in/ctrlram-postbuild-v2/flash-map.json";
    private const string ExpectedSha256 = "60b61fecca8fbab189ebd250c8beb6370e0960f2598ccf500e1a777747e9b4b4";
    private static IReadOnlyList<TpFlashMapProfile> LoadProfiles()
    {
        string path = Path.Combine(AppContext.BaseDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return Load(File.ReadAllBytes(path), ExpectedSha256);
    }

    /// <summary>Loads hash-checked schema 1.0 entries ordered by effective version within each IC.</summary>
    internal static IReadOnlyList<TpFlashMapProfile> Load(ReadOnlySpan<byte> bytes, string expectedSha256)
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
        if (profiles.Select(static profile => (profile.IcId, profile.EffectiveCommonFwVersion))
                .Distinct().Count() != profiles.Length)
        {
            throw Invalid("duplicate IC id / effectiveCommonFwVersion");
        }

        IGrouping<string, TpFlashMapProfile>[] groups =
            [.. profiles.GroupBy(static profile => profile.IcId, StringComparer.Ordinal)];
        foreach (IGrouping<string, TpFlashMapProfile> group in groups)
        {
            if (group.Min(static profile => profile.EffectiveCommonFwVersion) !=
                LegacyCombinerCommonFwVersion.MinimumSupported)
            {
                throw Invalid($"{group.Key} first effectiveCommonFwVersion");
            }
        }

        return Array.AsReadOnly(groups
            .SelectMany(static group => group.OrderBy(static profile => profile.EffectiveCommonFwVersion))
            .ToArray());
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
        [property: JsonPropertyName("profiles")] IReadOnlyList<ProfileDocument>? Profiles);

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
}
