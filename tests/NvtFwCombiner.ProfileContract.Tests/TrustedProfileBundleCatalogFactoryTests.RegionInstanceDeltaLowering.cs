using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies relocation addends are derived from the selected canonical instance bases.</summary>
    [Fact]
    public void BlankOutputLoweringResolvesRegionInstanceDeltaAddend()
    {
        string familyJson = FamilyWithTwoBankInstances();
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithRegionInstanceDeltaTransform(SupportedProfileJson(familyHash)),
            familyJson,
            capacityBytes: 32));

        CompositionOperation operation = Assert.Single(
            result.CompiledComposition!.Plan.OrderedOperations,
            static candidate => candidate.Kind == CompositionOperationKind.TransformScalar);
        ScalarTransform transform = Assert.IsType<ScalarTransform>(operation.ScalarTransform);
        Assert.Equal(16, transform.Addend);
        Assert.Equal(ScalarTransformAddendSourceKind.RegionInstanceDelta, transform.AddendSource.Kind);
        Assert.Equal("a-bank", transform.AddendSource.SourceRegionInstanceId);
        Assert.Equal("b-bank", transform.AddendSource.TargetRegionInstanceId);
    }

    /// <summary>Verifies unresolved instance identities fail without falling back to a numeric relocation.</summary>
    [Theory]
    [InlineData("sourceRegionInstanceId")]
    [InlineData("targetRegionInstanceId")]
    public void BlankOutputLoweringRejectsUnknownRegionInstanceDeltaEndpoint(string propertyName)
    {
        string familyJson = FamilyWithTwoBankInstances();
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithInvalidRegionInstanceDelta(
                SupportedProfileJson(familyHash),
                propertyName,
                "missing-bank"),
            familyJson,
            capacityBytes: 32));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.invalid-scalar-transform", issue.Code);
        Assert.Contains("unknown region instance", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies structurally equal but separately declared templates cannot define one placement delta.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsDeltaAcrossDifferentCanonicalTemplates()
    {
        string familyJson = FamilyWithSplitBankTemplateAuthority();
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithRegionInstanceDeltaTransform(SupportedProfileJson(familyHash)),
            familyJson,
            capacityBytes: 32));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.invalid-scalar-transform", issue.Code);
        Assert.Contains("same canonical template", issue.Message, StringComparison.Ordinal);
    }

    private static string ProfileWithRegionInstanceDeltaTransform(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            ProfileWithTemplateRangeCopyFlow(profileJson)));
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        views.Add(new JsonObject
        {
            ["viewId"] = "relocation-field",
            ["spaceId"] = "scratch",
            ["selector"] = new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject { ["start"] = 0, ["length"] = 4 },
            },
        });
        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);
        Assert.IsType<JsonObject>(operations[1])["sequence"] = 2;
        operations.Insert(1, new JsonObject
        {
            ["operationId"] = "relocate-b",
            ["sequence"] = 1,
            ["overlapPolicy"] = "reject",
            ["reason"] = "Relocate one stored address by the resolved B-minus-A instance delta.",
            ["kind"] = "transform-scalar",
            ["sourceViewId"] = "relocation-field",
            ["targetViewId"] = "relocation-field",
            ["widthBytes"] = 4,
            ["byteOrder"] = "little",
            ["valueInterpretation"] = "unsigned",
            ["addend"] = new JsonObject
            {
                ["kind"] = "region-instance-delta",
                ["sourceRegionInstanceId"] = "a-bank",
                ["targetRegionInstanceId"] = "b-bank",
            },
            ["overflowPolicy"] = "reject",
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithInvalidRegionInstanceDelta(
        string profileJson,
        string propertyName,
        string value)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            ProfileWithRegionInstanceDeltaTransform(profileJson)));
        JsonObject addend = Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(profile["operations"])[1])["addend"]);
        addend[propertyName] = value;
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string FamilyWithSplitBankTemplateAuthority()
    {
        JsonObject family = Assert.IsType<JsonObject>(JsonNode.Parse(FamilyWithTwoBankInstances()));
        JsonObject regionSet = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["regionSets"])[0]);
        JsonArray templates = Assert.IsType<JsonArray>(regionSet["regionTemplates"]);
        JsonObject otherTemplate = Assert.IsType<JsonObject>(templates[0]!.DeepClone());
        otherTemplate["templateId"] = "other-bank";
        templates.Add(otherTemplate);
        JsonObject targetInstance = Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(regionSet["regionInstances"])[1]);
        targetInstance["templateId"] = "other-bank";
        return family.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
