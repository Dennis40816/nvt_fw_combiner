using System.Text.Json;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks Saved Rule v2 canonical semantic hashing.</summary>
public sealed class SavedCompositionRuleV2ContentHasherTests
{
    /// <summary>Path is external and display/property formatting is not semantic identity.</summary>
    [Fact]
    public void CanonicalHashIgnoresPathFormattingPropertyOrderAndDisplayName()
    {
        using var first = JsonDocument.Parse(
            """{"schemaVersion":"2.0","displayName":"First","ruleId":"rule","mappingFragments":[{"fragmentId":"map","targetOffset":1}]}""");
        using var second = JsonDocument.Parse(
            """
            {
              "mappingFragments": [ { "targetOffset": 1, "fragmentId": "map" } ],
              "ruleId": "rule",
              "displayName": "Renamed",
              "schemaVersion": "2.0"
            }
            """);

        string firstHash = SavedCompositionRuleV2ContentHasher.Calculate(first.RootElement);
        string secondHash = SavedCompositionRuleV2ContentHasher.Calculate(second.RootElement);

        Assert.Equal(firstHash, secondHash);
        Assert.Matches("^[0-9a-f]{64}$", firstHash);
    }

    /// <summary>Execution-affecting content changes the canonical identity.</summary>
    [Fact]
    public void CanonicalHashChangesForSemanticContent()
    {
        using var first = JsonDocument.Parse(
            """{"schemaVersion":"2.0","displayName":"Same","ruleId":"rule","mappingFragments":[{"targetOffset":1}]}""");
        using var second = JsonDocument.Parse(
            """{"schemaVersion":"2.0","displayName":"Same","ruleId":"rule","mappingFragments":[{"targetOffset":2}]}""");

        Assert.NotEqual(
            SavedCompositionRuleV2ContentHasher.Calculate(first.RootElement),
            SavedCompositionRuleV2ContentHasher.Calculate(second.RootElement));
    }

    /// <summary>Every supported JSON scalar and numeric representation has one deterministic path.</summary>
    [Fact]
    public void CanonicalHashSupportsNestedDisplayNameAndAllJsonScalars()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "nested": {
                "displayName": "Nested names remain semantic",
                "enabled": true,
                "disabled": false,
                "optional": null
              },
              "signed": -1,
              "unsigned": 18446744073709551615,
              "decimal": 1.25,
              "double": 1e100
            }
            """);

        string hash =
            SavedCompositionRuleV2ContentHasher.Calculate(
                document.RootElement);

        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    /// <summary>Only one complete JSON object can become a Saved Rule semantic identity.</summary>
    [Fact]
    public void CanonicalHashRejectsNonObjectRootAndUndefinedValue()
    {
        using var array = JsonDocument.Parse("[]");

        _ = Assert.Throws<ArgumentException>(
            () => SavedCompositionRuleV2ContentHasher.Calculate(
                array.RootElement));
        _ = Assert.Throws<ArgumentException>(
            () => SavedCompositionRuleV2ContentHasher.Calculate(
                default));
    }
}
