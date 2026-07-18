using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 2.9 retains the processor-free runtime General Replace shape.</summary>
    [Fact]
    public void ValidateEntriesAcceptsProcessorFreeRuntimeReferenceReplaceForV29()
    {
        JsonObject profile = RuntimeReferenceReplaceProfile();
        profile["schemaVersion"] = "2.9";

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.9.schema.json"),
            32);
    }

    /// <summary>Verifies schema 2.9 admits the closed CtrlRAM source/experience pair with one final processor.</summary>
    [Fact]
    public void ValidateEntriesAcceptsCtrlRamRuntimeReferenceReplaceForV29()
    {
        JsonObject profile = RuntimeReferenceReplaceProcessorProfile("2.9");
        JsonObject experience = Assert.IsType<JsonObject>(profile["experience"]);
        experience["experienceId"] = "ctrlram-replace";
        experience["layoutPolicy"] = "fixed";
        experience["inputPolicy"] = "fixed";
        JsonObject source = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[1]);
        source["artifactClass"] = "ctrlram-replacement";

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.9.schema.json"),
            32);
    }

    /// <summary>Verifies schema 2.9 never admits a CtrlRAM experience backed by the General auxiliary source class.</summary>
    [Fact]
    public void ValidateEntriesRejectsMismatchedCtrlRamRuntimeReferenceSourceForV29()
    {
        JsonObject profile = RuntimeReferenceReplaceProcessorProfile("2.9");
        JsonObject experience = Assert.IsType<JsonObject>(profile["experience"]);
        experience["experienceId"] = "ctrlram-replace";
        experience["layoutPolicy"] = "fixed";
        experience["inputPolicy"] = "fixed";

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.9.schema.json"),
            32));
    }

    /// <summary>Verifies only schema 2.9 admits the closed conditional Legacy Combiner pair.</summary>
    [Theory]
    [InlineData("2.8", "composition-profile-v2.8.schema.json", false)]
    [InlineData("2.9", "composition-profile-v2.9.schema.json", true)]
    public void ValidateEntriesAdmitsConditionalRuntimeReferenceProcessorOnlyInV29(
        string schemaVersion,
        string schemaFileName,
        bool expectedValid)
    {
        JsonObject profile = RuntimeReferenceReplaceProcessorProfile(schemaVersion);
        ProfileBundleEntrySnapshotCollection collection = CaptureCompositionProfile(
            profile.ToJsonString(),
            schemaFileName);

        if (expectedValid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }

    /// <summary>Verifies schema 2.9 keeps the conditional processor pair closed and final.</summary>
    [Theory]
    [InlineData("early-sequence")]
    [InlineData("staged-source")]
    [InlineData("second-stage")]
    public void ValidateEntriesRejectsExpandedRuntimeReferenceProcessorAuthorityInV29(string mutation)
    {
        JsonObject profile = RuntimeReferenceReplaceProcessorProfile("2.9");
        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);
        JsonArray stages = Assert.IsType<JsonArray>(profile["processorStages"]);
        switch (mutation)
        {
            case "early-sequence":
                Assert.IsType<JsonObject>(operations[0])["sequence"] = 100;
                break;
            case "staged-source":
                Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(stages[0])["stagedSourceBindings"]).Add(
                    new JsonObject
                    {
                        ["sourceViewId"] = "processor-image",
                        ["targetViewId"] = "processor-image",
                    });
                break;
            case "second-stage":
                stages.Add(stages[0]!.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown schema mutation.");
        }

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.9.schema.json"),
            32));
    }

    private static JsonObject RuntimeReferenceReplaceProcessorProfile(string schemaVersion)
    {
        JsonObject profile = RuntimeReferenceReplaceProfile();
        profile["schemaVersion"] = schemaVersion;
        profile["views"] = new JsonArray
        {
            new JsonObject
            {
                ["viewId"] = "processor-image",
                ["spaceId"] = "output-image",
                ["selector"] = new JsonObject
                {
                    ["kind"] = "space-range",
                    ["range"] = new JsonObject { ["start"] = 0, ["length"] = 16 },
                },
            },
            new JsonObject
            {
                ["viewId"] = "processor-write",
                ["spaceId"] = "output-image",
                ["selector"] = new JsonObject
                {
                    ["kind"] = "space-range",
                    ["range"] = new JsonObject { ["start"] = 12, ["length"] = 4 },
                },
            },
        };
        profile["operations"] = new JsonArray
        {
            new JsonObject
            {
                ["operationId"] = "refresh-tp-header",
                ["sequence"] = int.MaxValue,
                ["overlapPolicy"] = "replace-existing",
                ["reason"] = "Refresh TP header after runtime mappings.",
                ["kind"] = "run-processor",
                ["processorStageId"] = "tp-refresh",
            },
        };
        profile["processorStages"] = new JsonArray
        {
            new JsonObject
            {
                ["processorStageId"] = "tp-refresh",
                ["kind"] = "legacy-combiner-v1",
                ["toolBindingId"] = "legacy-combiner-1.13.0",
                ["invocationProfileId"] = "nfc.synthetic.general-replace",
                ["targetSpaceId"] = "output-image",
                ["targetViewId"] = "processor-image",
                ["authority"] = "transform",
                ["purpose"] = "header-and-integrity",
                ["integrityDisposition"] = "recalculate-and-write",
                ["allowedReadViewIds"] = new JsonArray("processor-image"),
                ["allowedWriteViewIds"] = new JsonArray("processor-write"),
                ["stagedSourceBindings"] = new JsonArray(),
                ["stagedArtifactBindings"] = new JsonArray(),
                ["evidenceRef"] = "synthetic-tp-refresh",
                ["failurePolicy"] = "fail-closed",
            },
        };
        return profile;
    }
}
