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

        Assert.Equal(22, profileCount);
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
}
