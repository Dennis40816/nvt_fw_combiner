using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests strict JSON transport mapping for composition-profile-v2 DTOs.</summary>
public sealed class CompositionProfileDocumentTests
{
    /// <summary>Verifies a complete schema-shaped profile maps without inventing workflow semantics.</summary>
    [Fact]
    public void CompleteProfileJsonMapsToTransportDocument()
    {
        const string json = """
            {
              "schemaVersion": "2.0",
              "profileId": "synthetic-merge",
              "profileVersion": "1.2.3",
              "promotion": {
                "stage": "known",
                "blockers": [
                  {
                    "blockerId": "golden-missing",
                    "kind": "golden",
                    "reason": "Synthetic profile has no owner-approved golden.",
                    "evidenceRefs": []
                  }
                ]
              },
              "compositionKind": "merge",
              "experience": {
                "experienceId": "display-merge",
                "audience": "system",
                "layoutPolicy": "fixed",
                "inputPolicy": "fixed",
                "topologyAuthoring": "hidden",
                "displayNameKey": "profile.synthetic.merge"
              },
              "mapBinding": {
                "familyId": "synthetic-family",
                "familyVersion": "1.0.0",
                "familyContentHash": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                "mapIds": ["standard-map"],
                "requiredRegionIds": ["dp-code"],
                "requiredMetadataStructureIds": ["firmware-config"],
                "requiredCapabilityIds": []
              },
              "inputSlots": [
                {
                  "slotId": "tp-input",
                  "role": "tp",
                  "artifactClass": "tp-firmware",
                  "required": true,
                  "cardinality": "exactly-one",
                  "acceptedExtensions": [".bin"],
                  "acceptance": {
                    "lengthRule": { "kind": "tp-maximum-256k", "maximumBytes": 262144 },
                    "normalization": { "kind": "none" }
                  }
                }
              ],
              "spaces": [
                {
                  "spaceId": "tp-source",
                  "kind": "input-artifact",
                  "slotId": "tp-input",
                  "instancePolicy": "singleton"
                },
                {
                  "spaceId": "output",
                  "kind": "output-image",
                  "capacity": { "kind": "resolved-map" },
                  "initializer": { "kind": "blank", "fillByte": 255 }
                }
              ],
              "views": [
                {
                  "viewId": "tp-code",
                  "spaceId": "tp-source",
                  "selector": { "kind": "map-region", "regionId": "dp-code" }
                },
                {
                  "viewId": "output-code",
                  "spaceId": "output",
                  "selector": { "kind": "space-range", "range": { "start": 0, "length": 16 } }
                }
              ],
              "metadataBindings": [
                {
                  "bindingId": "fwconfig",
                  "spaceId": "tp-source",
                  "structureId": "firmware-config",
                  "fieldIds": ["pid"],
                  "purposes": ["validation"]
                }
              ],
              "regionAccessRules": [
                {
                  "regionId": "dp-code",
                  "access": "read-only",
                  "reason": "Synthetic source is immutable."
                }
              ],
              "operations": [
                {
                  "operationId": "copy-code",
                  "sequence": 0,
                  "overlapPolicy": "reject",
                  "reason": "Copy the declared source view.",
                  "kind": "copy-range",
                  "sourceViewId": "tp-code",
                  "targetViewId": "output-code"
                }
              ],
              "validations": [
                {
                  "ruleId": "pid-valid",
                  "stage": "input-load",
                  "severity": "error",
                  "issueCode": "PID_INVALID",
                  "kind": "pid-sanity",
                  "field": { "bindingId": "fwconfig", "fieldId": "pid" }
                }
              ],
              "processorStages": [],
              "output": {
                "fileNameTemplate": "{original-name}_merged.bin",
                "allowOverride": false,
                "invalidCharacterPolicy": "replace-underscore",
                "requiredTokenIds": ["original-name"]
              },
              "evidenceRefs": ["synthetic-evidence"]
            }
            """;

        CompositionProfileDocument profile = Assert.IsType<CompositionProfileDocument>(
            JsonSerializer.Deserialize<CompositionProfileDocument>(json, StrictOptions()));

        Assert.Equal("2.0", profile.SchemaVersion);
        Assert.Equal("synthetic-merge", profile.ProfileId);
        Assert.Equal("known", profile.Promotion.Stage);
        Assert.Equal("golden", Assert.Single(profile.Promotion.Blockers).Kind);
        Assert.Equal("merge", profile.CompositionKind);
        Assert.Equal("display-merge", profile.Experience.ExperienceId);
        Assert.Equal("standard-map", Assert.Single(profile.MapBinding.MapIds));
        Assert.Equal("tp-maximum-256k", Assert.Single(profile.InputSlots).Acceptance.LengthRule.Kind);
        Assert.Equal(262144, profile.InputSlots[0].Acceptance.LengthRule.MaximumBytes?.GetInt32());
        Assert.Equal("input-artifact", profile.Spaces[0].Kind);
        Assert.Equal("blank", profile.Spaces[1].Initializer?.Kind);
        Assert.Equal("map-region", profile.Views[0].Selector.Kind);
        Assert.Equal("space-range", profile.Views[1].Selector.Kind);
        Assert.Equal("firmware-config", Assert.Single(profile.MetadataBindings).StructureId);
        Assert.Equal("copy-range", Assert.Single(profile.Operations).Kind);
        Assert.Equal(0, profile.Operations[0].Sequence.GetInt32());
        Assert.Equal("pid-sanity", Assert.Single(profile.Validations).Kind);
        Assert.Empty(profile.ProcessorStages);
        Assert.Equal("{original-name}_merged.bin", profile.Output.FileNameTemplate);
    }

    /// <summary>Verifies schema integers beyond Int64 stay lossless until semantic normalization.</summary>
    [Fact]
    public void NumericTransportPreservesIntegerBeyondInt64()
    {
        const string json = """
            { "start": 9223372036854775808, "length": 18446744073709551616 }
            """;

        CompositionProfileRelativeRangeDocument range =
            Assert.IsType<CompositionProfileRelativeRangeDocument>(
                JsonSerializer.Deserialize<CompositionProfileRelativeRangeDocument>(json, StrictOptions()));
        using var roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(range, StrictOptions()));

        Assert.Equal("9223372036854775808", range.Start.GetRawText());
        Assert.Equal("18446744073709551616", range.Length.GetRawText());
        Assert.Equal(
            "9223372036854775808",
            roundTrip.RootElement.GetProperty("start").GetRawText());
    }

    /// <summary>Verifies strict transport settings reject unknown JSON members.</summary>
    [Fact]
    public void StrictTransportOptionsRejectUnknownMembers()
    {
        const string json = """
            {
              "experienceId": "display-merge",
              "audience": "system",
              "layoutPolicy": "fixed",
              "inputPolicy": "fixed",
              "topologyAuthoring": "hidden",
              "displayNameKey": "profile.synthetic.merge",
              "unexpected": true
            }
            """;

        _ = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CompositionProfileExperienceDocument>(json, StrictOptions()));
    }

    internal static JsonSerializerOptions StrictOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
    }
}
