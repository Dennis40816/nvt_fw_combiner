using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies required and union-shape failures are rejected by the production schema gateway.</summary>
    [Theory]
    [InlineData("root-promotion")]
    [InlineData("root-input-slots")]
    [InlineData("root-output")]
    [InlineData("root-evidence")]
    [InlineData("null-validation")]
    [InlineData("output-tokens")]
    [InlineData("operation-source")]
    [InlineData("space-slot")]
    [InlineData("space-capacity")]
    [InlineData("space-initializer")]
    [InlineData("clone-source")]
    [InlineData("view-region")]
    [InlineData("input-maximum")]
    [InlineData("validation-field")]
    public void ValidateEntriesRejectsMissingOrNullCompositionProfileShape(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        JsonArray inputSlots = Assert.IsType<JsonArray>(profile["inputSlots"]);
        JsonArray spaces = Assert.IsType<JsonArray>(profile["spaces"]);
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);

        switch (mutation)
        {
            case "space-initializer":
                spaces.Add(new JsonObject
                {
                    ["spaceId"] = "work",
                    ["kind"] = "work-buffer",
                    ["capacity"] = new JsonObject
                    {
                        ["kind"] = "fixed",
                        ["bytes"] = 16,
                    },
                    ["initializer"] = new JsonObject
                    {
                        ["kind"] = "blank",
                        ["fillByte"] = 0,
                    },
                });
                break;
            case "clone-source":
                profile["compositionKind"] = "replace";
                profile["icNumberInputMode"] = "single-selector";
                Assert.IsType<JsonObject>(spaces[1])["initializer"] = new JsonObject
                {
                    ["kind"] = "clone",
                    ["sourceSlotId"] = "tp-input",
                };
                break;
            case "validation-field":
                profile["validations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["ruleId"] = "pid-valid",
                        ["stage"] = "input-load",
                        ["severity"] = "error",
                        ["issueCode"] = "PID_INVALID",
                        ["kind"] = "pid-sanity",
                        ["field"] = new JsonObject
                        {
                            ["bindingId"] = "fwconfig",
                            ["fieldId"] = "pid",
                        },
                    },
                };
                break;
            default:
                break;
        }

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString()),
            32);

        switch (mutation)
        {
            case "root-promotion":
                _ = profile.Remove("promotion");
                break;
            case "root-input-slots":
                _ = profile.Remove("inputSlots");
                break;
            case "root-output":
                _ = profile.Remove("output");
                break;
            case "root-evidence":
                _ = profile.Remove("evidenceRefs");
                break;
            case "null-validation":
                profile["validations"] = new JsonArray { null };
                break;
            case "output-tokens":
                _ = Assert.IsType<JsonObject>(profile["output"]).Remove("requiredTokenIds");
                break;
            case "operation-source":
                _ = Assert.IsType<JsonObject>(operations[0]).Remove("sourceViewId");
                break;
            case "space-slot":
                _ = Assert.IsType<JsonObject>(spaces[0]).Remove("slotId");
                break;
            case "space-capacity":
                _ = Assert.IsType<JsonObject>(spaces[1]).Remove("capacity");
                break;
            case "space-initializer":
                _ = Assert.IsType<JsonObject>(spaces[spaces.Count - 1]).Remove("initializer");
                break;
            case "clone-source":
                _ = Assert.IsType<JsonObject>(
                    Assert.IsType<JsonObject>(spaces[1])["initializer"])
                    .Remove("sourceSlotId");
                break;
            case "view-region":
                _ = Assert.IsType<JsonObject>(
                    Assert.IsType<JsonObject>(views[0])["selector"]).Remove("regionId");
                break;
            case "input-maximum":
                _ = Assert.IsType<JsonObject>(
                    Assert.IsType<JsonObject>(
                        Assert.IsType<JsonObject>(inputSlots[0])["acceptance"])["lengthRule"])
                    .Remove("maximumBytes");
                break;
            case "validation-field":
                _ = Assert.IsType<JsonObject>(
                    Assert.IsType<JsonArray>(profile["validations"])[0])
                    .Remove("field");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown shape mutation.");
        }

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString()),
            32));
    }

    /// <summary>Verifies schema 2.11 exclusively owns typed metadata target/evidence shape.</summary>
    [Theory]
    [InlineData("mixed-authority")]
    [InlineData("missing-evidence")]
    public void ValidateEntriesRejectsInvalidTypedMetadataBindingShape(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.11";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        var binding = new JsonObject
        {
            ["bindingId"] = "tp-header",
            ["spaceId"] = "tp-source",
            ["metadataStructureId"] = "type-ab-tp-flash-header",
            ["purposes"] = new JsonArray("inspection"),
            ["targetReferences"] = new JsonArray
            {
                new JsonObject
                {
                    ["kind"] = "span",
                    ["targetId"] = "complete-header",
                },
            },
        };
        if (mutation == "mixed-authority")
        {
            binding["fieldIds"] = new JsonArray("header-crc");
            binding["evidenceRefs"] = new JsonArray("owner-table");
        }
        else if (!string.Equals(mutation, "missing-evidence", StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown metadata mutation.");
        }

        profile["metadataBindings"] = new JsonArray(binding);

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(
                profile.ToJsonString(),
                "composition-profile-v2.11.schema.json"),
            32));
    }

    /// <summary>Verifies the legacy TP-maximum declaration remains pinned to exactly 256 KiB.</summary>
    [Fact]
    public void ValidateEntriesRejectsDriftedTpMaximumCapacity()
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        JsonObject lengthRule = Assert.IsType<JsonObject>(
            Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"]);
        lengthRule["maximumBytes"] = 262143;

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString()),
            32));
    }

    /// <summary>Verifies required processor members and fixed policies are owned by the admitted schema.</summary>
    [Theory]
    [InlineData("legacy-valid", true)]
    [InlineData("legacy-authority", false)]
    [InlineData("legacy-failure", false)]
    [InlineData("legacy-missing-tool", false)]
    [InlineData("legacy-null-sources", false)]
    [InlineData("crc-valid", true)]
    [InlineData("crc-authority", false)]
    [InlineData("crc-purpose", false)]
    [InlineData("crc-integrity", false)]
    [InlineData("crc-failure", false)]
    [InlineData("crc-writes", false)]
    [InlineData("crc-missing-contract", false)]
    public void ValidateEntriesEnforcesClosedProcessorStageShapeInV22(string mutation, bool expectedValid)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.2";
        JsonObject stage = mutation.StartsWith("legacy", StringComparison.Ordinal)
            ? LegacyCombinerStage("legacy-combiner-1.13.0")
            : CrcWorkerStage();
        switch (mutation)
        {
            case "legacy-valid":
            case "crc-valid":
                break;
            case "legacy-authority":
            case "crc-authority":
                stage["authority"] = mutation == "legacy-authority" ? "calculate" : "transform";
                break;
            case "legacy-failure":
                stage["failurePolicy"] = "continue";
                break;
            case "crc-purpose":
                stage["purpose"] = "header";
                break;
            case "crc-integrity":
                stage["integrityDisposition"] = "none";
                break;
            case "crc-failure":
                stage["failurePolicy"] = "continue";
                break;
            case "legacy-missing-tool":
                _ = stage.Remove("toolBindingId");
                break;
            case "legacy-null-sources":
                stage["stagedSourceBindings"] = null;
                break;
            case "crc-writes":
                stage["allowedWriteViewIds"] = new JsonArray("output-code");
                break;
            case "crc-missing-contract":
                _ = stage.Remove("contractVersion");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown processor mutation.");
        }

        Assert.IsType<JsonArray>(profile["processorStages"]).Add(stage);
        ProfileBundleEntrySnapshotCollection collection = CaptureCompositionProfile(
            profile.ToJsonString(),
            "composition-profile-v2.2.schema.json");
        if (expectedValid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }

    private static JsonObject CrcWorkerStage()
    {
        return new JsonObject
        {
            ["processorStageId"] = "crc-check",
            ["kind"] = "crc-worker-v1",
            ["contractVersion"] = "1.0.0",
            ["calculationSetId"] = "display-crc",
            ["targetSpaceId"] = "output",
            ["authority"] = "calculate",
            ["purpose"] = "checksum",
            ["integrityDisposition"] = "verify-existing",
            ["allowedReadViewIds"] = new JsonArray("output-code"),
            ["allowedWriteViewIds"] = new JsonArray(),
            ["failurePolicy"] = "fail-closed",
        };
    }
}
