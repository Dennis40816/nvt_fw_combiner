using System.Text.Json;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks Saved Rule v2 canonical semantic hashing.</summary>
public sealed class SavedCompositionRuleV2ContentHasherTests
{
    /// <summary>Path is external and display/property formatting is not semantic identity.</summary>
    [Fact]
    public void CanonicalHashIgnoresPathFormattingPropertyOrderAndDisplayName()
    {
        using JsonDocument first = JsonDocument.Parse(
            """{"schemaVersion":"2.0","displayName":"First","ruleId":"rule","mappingFragments":[{"fragmentId":"map","targetOffset":1}]}""");
        using JsonDocument second = JsonDocument.Parse(
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
        using JsonDocument first = JsonDocument.Parse(
            """{"schemaVersion":"2.0","displayName":"Same","ruleId":"rule","mappingFragments":[{"targetOffset":1}]}""");
        using JsonDocument second = JsonDocument.Parse(
            """{"schemaVersion":"2.0","displayName":"Same","ruleId":"rule","mappingFragments":[{"targetOffset":2}]}""");

        Assert.NotEqual(
            SavedCompositionRuleV2ContentHasher.Calculate(first.RootElement),
            SavedCompositionRuleV2ContentHasher.Calculate(second.RootElement));
    }
}
