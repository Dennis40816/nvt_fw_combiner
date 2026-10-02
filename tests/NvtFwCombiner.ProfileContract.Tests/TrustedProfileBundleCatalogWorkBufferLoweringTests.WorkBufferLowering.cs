using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests trusted profile bundle catalog work buffer lowering.</summary>
[Collection(nameof(TrustedProfileBundleCatalogFactorySerialGroup))]
public sealed partial class TrustedProfileBundleCatalogWorkBufferLoweringTests
{
    /// <summary>Verifies a work buffer is a virtual intermediate space, not an ungoverned physical map write.</summary>
    [Fact]
    public void BlankOutputLoweringUsesWorkBufferAsEngineOwnedIntermediate()
    {
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithWorkBufferCopyFlow(SupportedProfileJson(familyHash))));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal(
            ["copy-to-scratch", "copy-to-output"],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.Equal(
            ["scratch", "output"],
            composition.Plan.OrderedOperations.Select(static operation => operation.TargetSpaceId));
        Assert.DoesNotContain(
            composition.V2Details.RegionAccessContract.ResolvedViews,
            static view => view.AddressSpaceId == "scratch");
    }

    /// <summary>Verifies work buffers reject map-relative selectors because they have no physical map authority.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsMapRelativeWorkBufferView()
    {
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithMapRelativeWorkBufferView(SupportedProfileJson(familyHash))));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.invalid-view", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies A/B instances select one native source range in both input and work-buffer spaces.</summary>
    [Fact]
    public void TemplateRangeSelectorsPreserveNativeCoordinatesAcrossInputAndWorkBuffer()
    {
        string familyJson = FamilyWithTwoBankInstances();
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTemplateRangeCopyFlow(SupportedProfileJson(familyHash)),
            familyJson,
            capacityBytes: 32));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal(
            [new ByteRange(4, 8), new ByteRange(4, 8)],
            composition.Plan.OrderedOperations.Select(static operation => operation.SourceRange!.Value));
        Assert.Equal(
            [new ByteRange(4, 8), new ByteRange(20, 8)],
            composition.Plan.OrderedOperations.Select(static operation => operation.TargetRange));
    }

    /// <summary>Verifies template selectors fail closed when either identity is absent from the resolved map.</summary>
    [Theory]
    [InlineData("regionInstanceId", "missing-bank")]
    [InlineData("templateRegionId", "missing-code")]
    public void TemplateRangeSelectorRejectsUnknownResolvedIdentity(
        string propertyName,
        string value)
    {
        string familyJson = FamilyWithTwoBankInstances();
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithInvalidTemplateReference(
                SupportedProfileJson(familyHash),
                propertyName,
                value),
            familyJson,
            capacityBytes: 32));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.invalid-input-geometry", issue.Code);
        Assert.Contains("unknown", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies relative source coordinates cannot be used as an output-image target basis.</summary>
    [Fact]
    public void TemplateRangeSelectorRejectsOutputImageView()
    {
        string familyJson = FamilyWithTwoBankInstances();
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTemplateRangeOutput(SupportedProfileJson(familyHash)),
            familyJson,
            capacityBytes: 32));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.invalid-view", issue.Code);
        Assert.Contains("immutable input or work-buffer source", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a template-relative source selector cannot become mutable target authority.</summary>
    [Fact]
    public void TemplateRangeSelectorRejectsTargetUse()
    {
        string familyJson = FamilyWithTwoBankInstances();
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTemplateRangeTarget(SupportedProfileJson(familyHash)),
            familyJson,
            capacityBytes: 32));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.invalid-view", issue.Code);
        Assert.Contains("source-only view 'b-source'", issue.Message, StringComparison.Ordinal);
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

    private static string ProfileWithInvalidTemplateReference(
        string profileJson,
        string propertyName,
        string value)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            ProfileWithTemplateRangeCopyFlow(profileJson)));
        JsonObject selector = Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(profile["views"])[0])["selector"]);
        selector[propertyName] = value;
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithTemplateRangeOutput(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            ProfileWithTemplateRangeCopyFlow(profileJson)));
        JsonObject outputView = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["views"])[3]);
        outputView["selector"] = new JsonObject
        {
            ["kind"] = "region-template-range",
            ["regionInstanceId"] = "b-bank",
            ["templateRegionId"] = "tp-code",
        };
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithTemplateRangeTarget(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            ProfileWithTemplateRangeCopyFlow(profileJson)));
        JsonObject copyB = Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(profile["operations"])[1]);
        copyB["targetViewId"] = "b-source";
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
