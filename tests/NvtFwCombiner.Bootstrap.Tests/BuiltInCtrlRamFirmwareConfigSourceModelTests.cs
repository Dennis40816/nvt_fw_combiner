using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Cross-checks every built-in CtrlRAM V2 profile against its IC's declared FWConfig source.</summary>
public sealed class BuiltInCtrlRamFirmwareConfigSourceModelTests
{
    private static readonly string[] CtrlRamBundleDirectories =
    [
        "nt51917-ctrlram-replace-alias-candidate",
        "nt51920-ctrlram-replace-candidate",
        "nt51923-ctrlram-replace-candidate",
        "nt51926-ctrlram-replace-candidate",
        "nt51927-ctrlram-replace-candidate",
        "nt51928-ctrlram-replace-candidate",
        "nt51929-ctrlram-replace-candidate",
        "nt51930-ctrlram-replace-candidate",
        "nt51931-ctrlram-replace-candidate",
        "nt51932-ctrlram-replace-candidate",
        "nt51950-ctrlram-replace-candidate",
        "nt51951-ctrlram-replace-candidate",
    ];

    private static readonly IReadOnlyDictionary<string, string> ExpectedCtrlRamMapTopologies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nt51920-ctrlram-fw120-single-full-flash"] = "single",
            ["nt51920-ctrlram-fw120-cascade2-full-flash"] = "cascade:2-*",
            ["nt51923-ctrlram-fw141-single-full-flash"] = "single",
            ["nt51923-ctrlram-fw141-cascade3-full-flash"] = "cascade:2-*",
            ["nt51926-ctrlram-fw141-tp-work-240k"] = "none",
            ["nt51926-ctrlram-fw141-full-flash-256k"] = "none",
            ["nt51926-ctrlram-fw200-tp-work-240k"] = "none",
            ["nt51926-ctrlram-fw200-full-flash-256k"] = "none",
            ["nt51927-ctrlram-fw132-twochip-full-flash"] = "exact-count:2",
            ["nt51927-ctrlram-fw140-threechip-full-flash"] = "exact-count:3",
            ["nt51927-ctrlram-fw141-single-full-flash"] = "single",
            ["nt51928-ctrlram-fw141-single-full-flash"] = "single",
            ["nt51928-ctrlram-fw132-twochip-full-flash"] = "exact-count:2",
            ["nt51928-ctrlram-fw140-threechip-full-flash"] = "exact-count:3",
            ["nt51929-ctrlram-fw200-single-full-flash"] = "single",
            ["nt51929-ctrlram-fw1x-cascade-full-flash"] = "cascade:2-*",
            ["nt51930-ctrlram-fw130-cascade3-full-flash"] = "none",
            ["nt51931-ctrlram-fw1x-single-full-flash"] = "single",
            ["nt51931-ctrlram-fw130-cascade6-full-flash"] = "cascade:2-*",
            ["nt51932-ctrlram-fw1x-single-full-flash"] = "single",
            ["nt51932-ctrlram-fw200-cascade3-full-flash"] = "cascade:2-*",
            ["nt51950-ctrlram-fw200-single-tp-work"] = "single",
            ["nt51950-ctrlram-fw200-single-full-flash"] = "single",
            ["nt51950-ctrlram-fw1x-cascade-tp-work"] = "cascade:2-*",
            ["nt51950-ctrlram-fw1x-cascade-full-flash"] = "cascade:2-*",
            ["nt51951-ctrlram-fw200-single-tp-work"] = "single",
            ["nt51951-ctrlram-fw200-single-full-flash"] = "single",
            ["nt51951-ctrlram-fw1x-cascade-tp-work"] = "cascade:2-*",
            ["nt51951-ctrlram-fw1x-cascade-full-flash"] = "cascade:2-*",
        };

    /// <summary>Production CtrlRAM map admission uses requested topology, never golden fixture metadata.</summary>
    [Fact]
    public void CtrlRamProductionMapsDoNotUseGoldenMetadataAdmission()
    {
        var actualTopologies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string familyPath in EnumerateCheckedInCtrlRamFamilies())
        {
            using var familyDocument = JsonDocument.Parse(File.ReadAllBytes(familyPath));
            foreach (JsonElement imageMap in familyDocument.RootElement.GetProperty("imageMaps").EnumerateArray())
            {
                JsonElement applicability = imageMap.GetProperty("applicability");
                if (!applicability.GetProperty("modeIds").EnumerateArray()
                        .Any(static mode => mode.GetString() == "ctrlram-replace"))
                {
                    continue;
                }

                string mapId = imageMap.GetProperty("mapId").GetString()!;
                Assert.False(
                    applicability.TryGetProperty("metadataPredicates", out JsonElement predicates) &&
                    predicates.GetArrayLength() != 0,
                    $"{mapId} must not use golden fixture metadata for production admission.");
                Assert.True(
                    actualTopologies.TryAdd(mapId, FormatTopology(applicability.GetProperty("topologyRequirement"))),
                    $"Duplicate CtrlRAM map id '{mapId}'.");
            }
        }

        Assert.Equal(ExpectedCtrlRamMapTopologies, actualTopologies);
    }

    /// <summary>Every CtrlRAM profile requires one TP firmware-config region at the cataloged Primary start.</summary>
    [Fact]
    public void EveryCtrlRamProfileRequiresItsCatalogedFirmwareConfigSource()
    {
        var coveredIcIds = new HashSet<string>(StringComparer.Ordinal);
        int profileCount = 0;

        foreach (string bundleDirectory in CtrlRamBundleDirectories)
        {
            string bundleRoot = RepositoryPaths.FromRepositoryRoot("profiles", "built-in", bundleDirectory);
            foreach (string profilePath in Directory.EnumerateFiles(
                         Path.Combine(bundleRoot, "profiles"), "*.json", SearchOption.TopDirectoryOnly))
            {
                using var profileDocument = JsonDocument.Parse(File.ReadAllBytes(profilePath));
                JsonElement profile = profileDocument.RootElement;
                if (!StringComparer.Ordinal.Equals(
                        profile.GetProperty("experience").GetProperty("experienceId").GetString(),
                        "ctrlram-replace"))
                {
                    continue;
                }

                profileCount++;
                string profileId = profile.GetProperty("profileId").GetString()!;
                string icId = profileId[..7].ToUpperInvariant();
                _ = coveredIcIds.Add(icId);
                Assert.True(BuiltInTpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? flashMap),
                    $"{profileId} has no TP flash-map profile.");

                JsonElement mapBinding = profile.GetProperty("mapBinding");
                Assert.Contains(
                    mapBinding.GetProperty("requiredRegionIds").EnumerateArray(),
                    static regionId => regionId.GetString() == "fw-config-source");

                string familyHash = mapBinding.GetProperty("familyContentHash").GetString()!;
                string familyPath = FindFamilyPath(
                    mapBinding.GetProperty("familyId").GetString()!,
                    familyHash);
                using var familyDocument = JsonDocument.Parse(File.ReadAllBytes(familyPath));
                JsonElement family = familyDocument.RootElement;
                Assert.Equal(mapBinding.GetProperty("familyVersion").GetString(),
                    family.GetProperty("familyVersion").GetString());

                foreach (JsonElement mapIdElement in mapBinding.GetProperty("mapIds").EnumerateArray())
                {
                    string mapId = mapIdElement.GetString()!;
                    JsonElement imageMap = Assert.Single(
                        family.GetProperty("imageMaps").EnumerateArray(),
                        candidate => candidate.GetProperty("mapId").GetString() == mapId);
                    AssertFirmwareConfigSource(
                        family,
                        imageMap,
                        flashMap!.FirmwareConfigPrimaryStart,
                        profileId);
                }
            }
        }

        Assert.Equal(32, profileCount);
        Assert.Equal(
            [
                "NT51917", "NT51919", "NT51920", "NT51923", "NT51926", "NT51927", "NT51928",
                "NT51929", "NT51930", "NT51931", "NT51932", "NT51950", "NT51951",
            ],
            [.. coveredIcIds.Order(StringComparer.Ordinal)]);
    }

    private static void AssertFirmwareConfigSource(
        JsonElement family,
        JsonElement imageMap,
        long expectedStart,
        string profileId)
    {
        string[] regionSetIds =
        [
            .. imageMap.GetProperty("regionSetIds").EnumerateArray().Select(static id => id.GetString()!),
        ];
        JsonElement[] sources =
        [
            .. family.GetProperty("regionSets").EnumerateArray()
                .Where(regionSet => regionSetIds.Contains(
                    regionSet.GetProperty("regionSetId").GetString()!,
                    StringComparer.Ordinal))
                .SelectMany(static regionSet => regionSet.GetProperty("regions").EnumerateArray())
                .Where(static region => region.GetProperty("regionId").GetString() == "fw-config-source"),
        ];
        JsonElement source = Assert.Single(sources);
        JsonElement range = source.GetProperty("range");

        Assert.Equal(expectedStart, range.GetProperty("start").GetInt64());
        Assert.True(
            range.GetProperty("length").GetInt64() >= FirmwareConfigLayout.RequiredLength,
            $"{profileId}/{imageMap.GetProperty("mapId").GetString()} does not cover the complete FWConfig read contract.");
        Assert.Equal("tp", source.GetProperty("owner").GetString());
        Assert.Equal("firmware-config", source.GetProperty("kind").GetString());
        Assert.Equal("forbidden", source.GetProperty("writeConstraint").GetString());
    }

    private static string FindFamilyPath(string familyId, string expectedHash)
    {
        string builtInRoot = RepositoryPaths.FromRepositoryRoot("profiles", "built-in");
        string[] candidates =
        [
            .. Directory.EnumerateFiles(
                builtInRoot,
                $"{familyId}.json",
                SearchOption.AllDirectories)
                .Where(static path => path.Contains($"{Path.DirectorySeparatorChar}families{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)),
        ];
        return Assert.Single(candidates, path => Hash(path) == expectedHash);
    }

    private static string Hash(string path)
    {
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static IEnumerable<string> EnumerateCheckedInCtrlRamFamilies()
    {
        string builtInRoot = RepositoryPaths.FromRepositoryRoot("profiles", "built-in");
        return CtrlRamBundleDirectories
            .Select(bundle => Path.Combine(builtInRoot, bundle, "families"))
            .Where(Directory.Exists)
            .SelectMany(static directory => Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly));
    }

    private static string FormatTopology(JsonElement topology)
    {
        string kind = topology.GetProperty("kind").GetString()!;
        return kind switch
        {
            "cascade" => $"cascade:{topology.GetProperty("minimumChipCount").GetInt32()}-{(topology.TryGetProperty("maximumChipCount", out JsonElement maximum) ? maximum.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture) : "*")}",
            "exact-count" => $"exact-count:{topology.GetProperty("chipCount").GetInt32()}",
            _ => kind,
        };
    }
}
