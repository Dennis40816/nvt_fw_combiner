using System.Text.Json.Nodes;

namespace NvtFwCombiner.TestSupport;

/// <summary>Non-confidential JSON fixtures for map-bound runtime reference-replace tests.</summary>
public static class RuntimeReferenceReplaceTestDocuments
{
    /// <summary>Builds one synthetic family with exact-capacity General Replace maps.</summary>
    public static string FamilyJson(
        IEnumerable<RuntimeReferenceReplaceMapDocument> mapDefinitions,
        string writeConstraint)
    {
        ArgumentNullException.ThrowIfNull(mapDefinitions);
        ArgumentException.ThrowIfNullOrWhiteSpace(writeConstraint);
        RuntimeReferenceReplaceMapDocument[] maps = [.. mapDefinitions];
        if (maps.Length == 0 ||
            maps.Any(static map => string.IsNullOrWhiteSpace(map.MapId) || map.CapacityBytes <= 0) ||
            maps.Select(static map => map.MapId).Distinct(StringComparer.Ordinal).Count() != maps.Length ||
            writeConstraint is not "explicit-range" and not "forbidden")
        {
            throw new ArgumentException(
                "Runtime reference-replace fixtures require unique positive maps and explicit or forbidden write authority.",
                nameof(mapDefinitions));
        }

        JsonObject family = JsonNode.Parse(TrustedV2BundleTestDocuments.FamilyJson())!.AsObject();
        JsonArray regionSets = family["regionSets"]!.AsArray();
        JsonArray imageMaps = family["imageMaps"]!.AsArray();
        JsonObject sourceRegionSet = regionSets[0]!.DeepClone().AsObject();
        JsonObject sourceMap = imageMaps[0]!.DeepClone().AsObject();
        regionSets.Clear();
        imageMaps.Clear();

        foreach (RuntimeReferenceReplaceMapDocument definition in maps)
        {
            JsonObject regionSet = sourceRegionSet.DeepClone().AsObject();
            string regionSetId = $"{definition.MapId}-regions";
            regionSet["regionSetId"] = regionSetId;
            JsonObject root = regionSet["regions"]!.AsArray()[0]!.AsObject();
            root["range"] = new JsonObject { ["start"] = 0, ["length"] = definition.CapacityBytes };
            root["writeConstraint"] = writeConstraint;

            JsonObject map = sourceMap.DeepClone().AsObject();
            map["mapId"] = definition.MapId;
            map["applicability"]!.AsObject()["modeIds"] = new JsonArray("general-replace");
            map["applicability"]!.AsObject()["capacityBytes"] = definition.CapacityBytes;
            map["regionSetIds"] = new JsonArray(regionSetId);
            regionSets.Add(regionSet);
            imageMaps.Add(map);
        }

        return family.ToJsonString();
    }

    /// <summary>Builds one synthetic closed General Replace runtime-reference profile.</summary>
    public static string ProfileJson(
        string familyContentHash,
        string promotionStage,
        IEnumerable<string> mapIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyContentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(promotionStage);
        ArgumentNullException.ThrowIfNull(mapIds);
        string[] maps = [.. mapIds];
        if (maps.Length == 0 || maps.Any(string.IsNullOrWhiteSpace) ||
            maps.Distinct(StringComparer.Ordinal).Count() != maps.Length)
        {
            throw new ArgumentException("Runtime reference-replace profiles require unique map ids.", nameof(mapIds));
        }

        var mapIdNodes = new JsonArray();
        foreach (string mapId in maps)
        {
            mapIdNodes.Add(mapId);
        }

        var profile = new JsonObject
        {
            ["schemaVersion"] = "2.6",
            ["profileId"] = "runtime-general-replace",
            ["profileVersion"] = "1.0.0",
            ["promotion"] = new JsonObject
            {
                ["stage"] = promotionStage,
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
                ["familyContentHash"] = familyContentHash,
                ["mapIds"] = mapIdNodes,
                ["requiredRegionIds"] = new JsonArray("root"),
                ["requiredMetadataStructureIds"] = new JsonArray(),
                ["requiredCapabilityIds"] = new JsonArray(),
            },
            ["inputSlots"] = new JsonArray
            {
                Slot("reference", "reference-image", "exactly-one", new JsonObject
                {
                    ["kind"] = "exact-resolved-map-capacity",
                }),
                Slot("source", "auxiliary", "one-or-more", new JsonObject
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
                    ["regionId"] = "root",
                    ["access"] = "explicit-range",
                    ["reason"] = "Synthetic map-bound General Replace target.",
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
        return profile.ToJsonString();
    }

    private static JsonObject Slot(
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

/// <summary>One exact-capacity map fixture for runtime reference-replace tests.</summary>
public sealed record RuntimeReferenceReplaceMapDocument(string MapId, int CapacityBytes);
