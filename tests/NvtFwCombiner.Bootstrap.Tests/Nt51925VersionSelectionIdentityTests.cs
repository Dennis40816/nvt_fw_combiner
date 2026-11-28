// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Pins every unrelated policy/bundle row and all pre-existing family schemas to the task base.</summary>
public sealed class Nt51925VersionSelectionIdentityTests
{
    /// <summary>
    /// The new opt-in does not alter any other IC's bundle declaration or policy row. NT51925 bundles and routes are
    /// excluded on both sides: the owner decision of 2026-11-28 (9b, 9c) changes the NT51925 CtrlRAM Replace bundle.
    /// </summary>
    [Fact]
    public void OtherIcIdentitiesRemainExactlyAtTheTaskBase()
    {
        using JsonDocument baseline = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.FromTestData(
            "profiles/nt51925-version-selection-base-pins.json")));
        using JsonDocument bundles = Read("profiles/built-in/package-trust-index.json");
        using JsonDocument policy = Read("docs/contracts/canonical-capability-policy-v1.json");
        Dictionary<string, string> actualBundles = bundles.RootElement.GetProperty("bundles").EnumerateArray()
            .Where(static row => !IsNt51925Bundle(row.GetProperty("bundleDirectory").GetString()!))
            .ToDictionary(static row => row.GetProperty("bundleDirectory").GetString()!, HashRow, StringComparer.Ordinal);
        Dictionary<string, string> actualPolicy = policy.RootElement.GetProperty("routes").EnumerateArray()
            .Where(static row => row.GetProperty("icId").GetString() != "NT51925")
            .ToDictionary(static row => row.GetProperty("routeId").GetString()!, HashRow, StringComparer.Ordinal);

        Assert.Equal("d554add5e98eba11064dda83c47b4222b3209341", baseline.RootElement.GetProperty("sourceBase").GetString());
        AssertRows(baseline.RootElement.GetProperty("bundleRows"), actualBundles, IsNt51925Bundle);
        AssertRows(baseline.RootElement.GetProperty("policyRows"), actualPolicy, static _ => false);
    }

    /// <summary>All seven previous family schema files retain their exact base bytes.</summary>
    [Fact]
    public void ExistingFamilySchemaBytesRemainUnchanged()
    {
        using JsonDocument baseline = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.FromTestData(
            "profiles/nt51925-version-selection-base-pins.json")));
        Assert.All(baseline.RootElement.GetProperty("familySchemas").EnumerateObject(), entry =>
            Assert.Equal(entry.Value.GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(
                RepositoryPaths.FromRepositoryRoot("docs", "contracts", entry.Name))))));
    }

    private static void AssertRows(JsonElement expected, Dictionary<string, string> actual, Func<string, bool> excluded)
    {
        JsonProperty[] rows = [.. expected.EnumerateObject().Where(entry => !excluded(entry.Name))];
        Assert.Equal(rows.Length, actual.Count);
        Assert.All(rows, entry => Assert.Equal(entry.Value.GetString(), actual[entry.Name]));
    }

    private static bool IsNt51925Bundle(string bundleDirectory)
    {
        return bundleDirectory is "nt51925-standard-merge-candidate" or "nt51925-ctrlram-replace-candidate";
    }

    private static string HashRow(JsonElement row)
    {
        return Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(row.GetRawText().Replace("\r\n", "\n", StringComparison.Ordinal))));
    }

    private static JsonDocument Read(string path)
    {
        return JsonDocument.Parse(File.ReadAllText(RepositoryPaths.FromRepositoryRoot(path)));
    }
}
