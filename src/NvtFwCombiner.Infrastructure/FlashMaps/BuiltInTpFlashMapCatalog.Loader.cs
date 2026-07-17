using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.FlashMaps;

internal static partial class BuiltInTpFlashMapCatalog
{
    private const string RelativePath = "profiles/built-in/ctrlram-postbuild-v2/flash-map.json";
    private const string ExpectedSha256 = "db4f9c2bcb07085c4995eef8ec846ba33b3d0bb2a5298cd961a659e75e6d3afa";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static IReadOnlyList<TpFlashMapProfile> LoadProfiles()
    {
        string path = Path.Combine(AppContext.BaseDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return Load(File.ReadAllBytes(path), ExpectedSha256);
    }

    internal static IReadOnlyList<TpFlashMapProfile> Load(ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!StringComparer.Ordinal.Equals(actualHash, expectedSha256))
        {
            throw new InvalidDataException($"Built-in TP flash-map catalog hash mismatch: {actualHash}.");
        }

        CatalogDocument document;
        try
        {
            document = JsonSerializer.Deserialize<CatalogDocument>(bytes, JsonOptions) ??
                throw Invalid("empty document");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Built-in TP flash-map catalog JSON is invalid.", exception);
        }

        if (document.SchemaVersion != "1.0" || document.Profiles is null)
        {
            throw Invalid($"schemaVersion '{document.SchemaVersion}' / profiles '{document.Profiles?.Count}'");
        }

        TpFlashMapProfile[] profiles = [.. document.Profiles.Select(CreateProfile)];
        return profiles.Select(static profile => profile.IcId).Distinct(StringComparer.Ordinal).Count() == profiles.Length
            ? Array.AsReadOnly(profiles)
            : throw Invalid("duplicate IC id");
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
                source.Evidence)
            : throw Invalid($"duplicate region id for {source.IcId}");
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
        [property: JsonPropertyName("regions")] IReadOnlyList<RegionDocument>? Regions);

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
