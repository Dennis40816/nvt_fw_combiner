using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 2.4 accepts only the map-independent logical General Merge declaration shape.</summary>
    [Fact]
    public void ValidateEntriesAcceptsLogicalOutputProfileForV24()
    {
        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(LogicalOutputProfile().ToJsonString(), "composition-profile-v2.4.schema.json"),
            32);
    }

    /// <summary>Verifies schema 2.4 keeps the resolved-map path explicit instead of reinterpreting it as logical output.</summary>
    [Fact]
    public void ValidateEntriesAcceptsResolvedMapProfileForV24()
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.4";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.4.schema.json"),
            32);
    }

    /// <summary>Verifies a logical-output declaration cannot carry a misleading physical map binding.</summary>
    [Fact]
    public void ValidateEntriesRejectsLogicalOutputProfileWithMapBindingForV24()
    {
        JsonObject profile = LogicalOutputProfile();
        profile["mapBinding"] = new JsonObject
        {
            ["familyId"] = "family",
            ["familyVersion"] = "1.0.0",
            ["familyContentHash"] = new string('c', 64),
            ["mapIds"] = new JsonArray("map"),
            ["requiredRegionIds"] = new JsonArray("region"),
            ["requiredMetadataStructureIds"] = new JsonArray(),
            ["requiredCapabilityIds"] = new JsonArray(),
        };

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.4.schema.json"),
            32));
    }

    private static JsonObject LogicalOutputProfile()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "2.4",
            ["profileId"] = "logical-general-merge",
            ["profileVersion"] = "1.0.0",
            ["promotion"] = new JsonObject
            {
                ["stage"] = "compilable",
                ["blockers"] = new JsonArray(),
            },
            ["compositionKind"] = "merge",
            ["experience"] = new JsonObject
            {
                ["experienceId"] = "general-merge",
                ["audience"] = "advanced",
                ["layoutPolicy"] = "user-defined",
                ["inputPolicy"] = "extensible",
                ["topologyAuthoring"] = "hidden",
                ["displayNameKey"] = "logical-general-merge",
            },
            ["compilationContext"] = new JsonObject { ["kind"] = "logical-output" },
            ["logicalOutputBinding"] = new JsonObject
            {
                ["familyId"] = "family",
                ["familyVersion"] = "1.0.0",
                ["familyContentHash"] = new string('c', 64),
                ["memberIds"] = new JsonArray("member"),
            },
            ["inputSlots"] = new JsonArray
            {
                new JsonObject
                {
                    ["slotId"] = "source",
                    ["role"] = "source",
                    ["artifactClass"] = "auxiliary",
                    ["required"] = true,
                    ["cardinality"] = "one-or-more",
                    ["acceptedExtensions"] = new JsonArray(".bin"),
                    ["acceptance"] = new JsonObject
                    {
                        ["lengthRule"] = new JsonObject
                        {
                            ["kind"] = "bounded",
                            ["minimumBytes"] = 1,
                            ["maximumBytes"] = int.MaxValue,
                        },
                        ["normalization"] = new JsonObject { ["kind"] = "none" },
                    },
                },
            },
            ["spaces"] = new JsonArray
            {
                new JsonObject
                {
                    ["spaceId"] = "source-template",
                    ["kind"] = "input-artifact",
                    ["slotId"] = "source",
                    ["instancePolicy"] = "per-binding",
                },
                new JsonObject
                {
                    ["spaceId"] = "output-image",
                    ["kind"] = "output-image",
                    ["capacity"] = new JsonObject { ["kind"] = "runtime-request" },
                    ["initializer"] = new JsonObject
                    {
                        ["kind"] = "blank",
                        ["fillByte"] = 0,
                    },
                },
            },
            ["views"] = new JsonArray(),
            ["metadataBindings"] = new JsonArray(),
            ["regionAccessRules"] = new JsonArray(),
            ["operations"] = new JsonArray(),
            ["validations"] = new JsonArray(),
            ["processorStages"] = new JsonArray(),
            ["output"] = new JsonObject
            {
                ["fileNameTemplate"] = "member-general-merge.bin",
                ["allowOverride"] = true,
                ["invalidCharacterPolicy"] = "reject",
                ["requiredTokenIds"] = new JsonArray(),
            },
            ["evidenceRefs"] = new JsonArray("logical-output-contract"),
        };
    }
}
