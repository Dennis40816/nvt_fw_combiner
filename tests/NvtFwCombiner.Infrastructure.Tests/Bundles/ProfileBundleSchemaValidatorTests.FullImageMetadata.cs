using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    private const string FullImageMetadataSchema = "firmware-family-v1.3-full-image-metadata.schema.json";

    /// <summary>The trusted schema admits explicit declarations and rejects undeclared authority at every level.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("absent", true)]
    [InlineData("empty-views", true)]
    [InlineData("empty-bindings", true)]
    [InlineData("null", false)]
    [InlineData("missing-map", false)]
    [InlineData("missing-bindings", false)]
    [InlineData("empty-members", false)]
    [InlineData("empty-targets", false)]
    [InlineData("duplicate-target", false)]
    [InlineData("unknown-kind", false)]
    [InlineData("view-workflow", false)]
    [InlineData("binding-offset", false)]
    [InlineData("binding-space", false)]
    [InlineData("binding-source", false)]
    [InlineData("binding-processor", false)]
    [InlineData("binding-policy", false)]
    [InlineData("target-range", false)]
    [InlineData("bank-extension", false)]
    [InlineData("ab-extension", false)]
    [InlineData("wrong-version", false)]
    public void FullImageMetadataSchemaEnforcesClosedShape(string mutation, bool valid)
    {
        JsonObject family = LoadFirmwareFamilyWithRelationships();
        JsonObject view = Assert.IsType<JsonObject>(JsonNode.Parse("""
            {"viewId":"full-image","mapId":"map","memberIds":["NT00001"],
             "metadataBindings":[{"bindingId":"config","structureId":"config",
              "targetReferences":[{"targetKind":"field","targetId":"value"}],"evidenceRefs":["evidence"]}],
             "evidenceRefs":["evidence"]}
            """));
        var views = new JsonArray(view);
        family["fullImageMetadataViews"] = views;
        JsonObject binding = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(view["metadataBindings"])[0]);
        JsonArray targets = Assert.IsType<JsonArray>(binding["targetReferences"]);
        switch (mutation)
        {
            case "valid": break;
            case "absent": _ = family.Remove("fullImageMetadataViews"); break;
            case "empty-views": family["fullImageMetadataViews"] = new JsonArray(); break;
            case "empty-bindings": view["metadataBindings"] = new JsonArray(); break;
            case "null": family["fullImageMetadataViews"] = null; break;
            case "missing-map": _ = view.Remove("mapId"); break;
            case "missing-bindings": _ = view.Remove("metadataBindings"); break;
            case "empty-members": view["memberIds"] = new JsonArray(); break;
            case "empty-targets": binding["targetReferences"] = new JsonArray(); break;
            case "duplicate-target": targets.Add(targets[0]!.DeepClone()); break;
            case "unknown-kind": targets[0]!["targetKind"] = "future"; break;
            case "view-workflow": view["workflow"] = "dp-replace"; break;
            case "binding-offset": binding["offset"] = 0; break;
            case "binding-space": binding["spaceId"] = "reference"; break;
            case "binding-source": binding["source"] = "other"; break;
            case "binding-processor": binding["processor"] = "crc"; break;
            case "binding-policy": binding["policy"] = "replace"; break;
            case "target-range": targets[0]!["offset"] = 0; break;
            case "bank-extension": family["bankInstances"] = new JsonArray(); break;
            case "ab-extension": family["abFormatPolicy"] = new JsonObject(); break;
            case "wrong-version": family["schemaVersion"] = "1.3"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        ProfileBundleEntrySnapshotCollection collection = CaptureFirmwareFamily(family.ToJsonString(), FullImageMetadataSchema);
        if (valid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
        }
        else
        {
            _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
        }
    }

    /// <summary>The successor retains exact accepted TP subjects and rejects new inferred subjects.</summary>
    [Theory]
    [InlineData("data", true)]
    [InlineData("firmware-config", true)]
    [InlineData("ctrlram", true)]
    [InlineData("mp-ctrlram", true)]
    [InlineData("future", false)]
    public void FullImageMetadataSchemaPreservesTpHeaderSubjects(string subject, bool valid)
    {
        JsonObject family = LoadFirmwareFamilyWithTpHeader();
        JsonObject structure = FirstMetadataStructure(family);
        JsonArray semantics = Assert.IsType<JsonArray>(structure["tpFlashHeader"]!["fieldSemantics"]);
        semantics[0]!["subject"] = subject;
        ProfileBundleEntrySnapshotCollection collection = CaptureFirmwareFamily(family.ToJsonString(), FullImageMetadataSchema);
        if (valid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
        }
        else
        {
            _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
        }
    }
}
