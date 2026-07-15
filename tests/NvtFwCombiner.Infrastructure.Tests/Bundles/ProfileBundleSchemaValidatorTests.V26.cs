using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 2.6 admits only the closed map-bound runtime General Replace declaration shape.</summary>
    [Fact]
    public void ValidateEntriesAcceptsRuntimeReferenceReplaceProfileForV26()
    {
        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(RuntimeReferenceReplaceProfile().ToJsonString(), "composition-profile-v2.6.schema.json"),
            32);
    }

    /// <summary>Verifies schema 2.6 rejects static operations from the typed runtime mapping declaration.</summary>
    [Fact]
    public void ValidateEntriesRejectsStaticOperationFromRuntimeReferenceReplaceForV26()
    {
        JsonObject profile = RuntimeReferenceReplaceProfile();
        Assert.IsType<JsonArray>(profile["operations"]).Add(new JsonObject
        {
            ["operationId"] = "static-write",
            ["sequence"] = 0,
            ["kind"] = "replace-range",
            ["sourceViewId"] = "source",
            ["targetViewId"] = "output",
            ["overlapPolicy"] = "reject",
            ["reason"] = "Runtime mappings must remain request-scoped.",
        });

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.6.schema.json"),
            32));
    }

    private static JsonObject RuntimeReferenceReplaceProfile()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "2.6",
            ["profileId"] = "runtime-general-replace",
            ["profileVersion"] = "1.0.0",
            ["promotion"] = new JsonObject
            {
                ["stage"] = "compilable",
                ["blockers"] = new JsonArray(),
            },
            ["compositionKind"] = "replace",
            ["icNumberInputMode"] = "single-selector",
            ["experience"] = new JsonObject
            {
                ["experienceId"] = "general-replace",
                ["audience"] = "advanced",
                ["layoutPolicy"] = "user-defined",
                ["inputPolicy"] = "extensible",
                ["topologyAuthoring"] = "hidden",
                ["displayNameKey"] = "runtime-general-replace",
            },
            ["compilationContext"] = new JsonObject { ["kind"] = "runtime-reference-replace" },
            ["mapBinding"] = new JsonObject
            {
                ["familyId"] = "family",
                ["familyVersion"] = "1.0.0",
                ["familyContentHash"] = new string('a', 64),
                ["mapIds"] = new JsonArray("map"),
                ["requiredRegionIds"] = new JsonArray("target"),
                ["requiredMetadataStructureIds"] = new JsonArray(),
                ["requiredCapabilityIds"] = new JsonArray(),
            },
            ["inputSlots"] = new JsonArray
            {
                InputSlot("reference", "reference-image", "exactly-one", new JsonObject
                {
                    ["kind"] = "exact-resolved-map-capacity",
                }),
                InputSlot("source", "auxiliary", "one-or-more", new JsonObject
                {
                    ["kind"] = "bounded",
                    ["minimumBytes"] = 1,
                    ["maximumBytes"] = int.MaxValue,
                }),
            },
            ["spaces"] = new JsonArray
            {
                new JsonObject
                {
                    ["spaceId"] = "reference-image",
                    ["kind"] = "input-artifact",
                    ["slotId"] = "reference",
                    ["instancePolicy"] = "singleton",
                },
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
                        ["kind"] = "clone",
                        ["sourceSlotId"] = "reference",
                    },
                },
            },
            ["views"] = new JsonArray(),
            ["metadataBindings"] = new JsonArray(),
            ["regionAccessRules"] = new JsonArray
            {
                new JsonObject
                {
                    ["regionId"] = "target",
                    ["access"] = "explicit-range",
                    ["reason"] = "Map-bound runtime replacement target.",
                },
            },
            ["operations"] = new JsonArray(),
            ["validations"] = new JsonArray(),
            ["processorStages"] = new JsonArray(),
            ["output"] = new JsonObject
            {
                ["fileNameTemplate"] = "runtime-general-replace.bin",
                ["allowOverride"] = true,
                ["invalidCharacterPolicy"] = "reject",
                ["requiredTokenIds"] = new JsonArray(),
            },
            ["evidenceRefs"] = new JsonArray("runtime-reference-replace-contract"),
        };
    }

    private static JsonObject InputSlot(
        string slotId,
        string artifactClass,
        string cardinality,
        JsonObject lengthRule)
    {
        return new JsonObject
        {
            ["slotId"] = slotId,
            ["role"] = slotId,
            ["artifactClass"] = artifactClass,
            ["required"] = true,
            ["cardinality"] = cardinality,
            ["acceptedExtensions"] = new JsonArray(".bin"),
            ["acceptance"] = new JsonObject
            {
                ["lengthRule"] = lengthRule,
                ["normalization"] = new JsonObject { ["kind"] = "none" },
            },
        };
    }
}
