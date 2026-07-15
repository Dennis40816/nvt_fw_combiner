using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies a work buffer is a virtual intermediate space, not an ungoverned physical map write.</summary>
    [Fact]
    public void BlankOutputLoweringUsesWorkBufferAsEngineOwnedIntermediate()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithWorkBufferCopyFlow(SupportedProfileJson(familyHash))));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal(
            ["copy-to-scratch", "copy-to-output"],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.Equal(
            ["scratch", "output"],
            composition.Plan.OrderedOperations.Select(static operation => operation.TargetSpaceId));
        Assert.DoesNotContain(
            composition.V2Details!.RegionAccessContract.ResolvedViews,
            static view => view.AddressSpaceId == "scratch");
    }

    /// <summary>Verifies work buffers reject map-relative selectors because they have no physical map authority.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsMapRelativeWorkBufferView()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithMapRelativeWorkBufferView(SupportedProfileJson(familyHash))));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.invalid-view", Assert.Single(result.Issues).Code);
    }

    private static string ProfileWithWorkBufferCopyFlow(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(ProfileWithWorkBuffer(profileJson)));
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        views.Add(new JsonObject
        {
            ["viewId"] = "scratch-code",
            ["spaceId"] = "scratch",
            ["selector"] = new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject { ["start"] = 0, ["length"] = 16 },
            },
        });
        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);
        JsonObject first = Assert.IsType<JsonObject>(operations[0]);
        first["operationId"] = "copy-to-scratch";
        first["targetViewId"] = "scratch-code";
        operations.Add(new JsonObject
        {
            ["operationId"] = "copy-to-output",
            ["sequence"] = 1,
            ["overlapPolicy"] = "reject",
            ["reason"] = "Copy the engine-owned scratch buffer into the physical output view.",
            ["kind"] = "copy-range",
            ["sourceViewId"] = "scratch-code",
            ["targetViewId"] = "output-code",
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithMapRelativeWorkBufferView(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(ProfileWithWorkBuffer(profileJson)));
        Assert.IsType<JsonArray>(profile["views"]).Add(new JsonObject
        {
            ["viewId"] = "scratch-code",
            ["spaceId"] = "scratch",
            ["selector"] = new JsonObject
            {
                ["kind"] = "map-region",
                ["regionId"] = "root",
            },
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
