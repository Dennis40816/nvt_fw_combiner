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

    /// <summary>Verifies A/B instances select one native source range in both input and work-buffer spaces.</summary>
    [Fact]
    public void TemplateRangeSelectorsPreserveNativeCoordinatesAcrossInputAndWorkBuffer()
    {
        string familyJson = FamilyWithTwoBankInstances();
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
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
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
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
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
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
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTemplateRangeTarget(SupportedProfileJson(familyHash)),
            familyJson,
            capacityBytes: 32));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.invalid-view", issue.Code);
        Assert.Contains("source-only view 'b-source'", issue.Message, StringComparison.Ordinal);
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

    private static string ProfileWithTemplateRangeCopyFlow(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        profile["schemaVersion"] = "2.14";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        JsonObject input = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        input["artifactClass"] = "tp-firmware";
        Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(input["acceptance"])["lengthRule"])
            ["kind"] = "source-view-coverage";
        JsonArray requiredRegions = Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(profile["mapBinding"])["requiredRegionIds"]);
        requiredRegions.Clear();
        requiredRegions.Add("a-code");
        requiredRegions.Add("b-code");

        Assert.IsType<JsonArray>(profile["spaces"]).Add(new JsonObject
        {
            ["spaceId"] = "scratch",
            ["kind"] = "work-buffer",
            ["capacity"] = new JsonObject { ["kind"] = "fixed", ["bytes"] = 16 },
            ["initializer"] = new JsonObject { ["kind"] = "clone", ["sourceSlotId"] = "tp-input" },
        });

        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        views.Clear();
        views.Add(TemplateRangeView("a-source", "tp-source", "a-bank"));
        views.Add(MapRegionView("a-output", "output", "a-code"));
        views.Add(TemplateRangeView("b-source", "scratch", "b-bank"));
        views.Add(MapRegionView("b-output", "output", "b-code"));

        JsonArray accessRules = Assert.IsType<JsonArray>(profile["regionAccessRules"]);
        accessRules.Clear();
        accessRules.Add(WholeRegionAccess("a-code"));
        accessRules.Add(WholeRegionAccess("b-code"));

        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);
        operations.Clear();
        operations.Add(Copy("copy-a", 0, "a-source", "a-output"));
        operations.Add(Copy("copy-b", 1, "b-source", "b-output"));
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        static JsonObject TemplateRangeView(
            string viewId,
            string spaceId,
            string regionInstanceId)
        {
            return new JsonObject
            {
                ["viewId"] = viewId,
                ["spaceId"] = spaceId,
                ["selector"] = new JsonObject
                {
                    ["kind"] = "region-template-range",
                    ["regionInstanceId"] = regionInstanceId,
                    ["templateRegionId"] = "tp-code",
                },
            };
        }

        static JsonObject MapRegionView(string viewId, string spaceId, string regionId)
        {
            return new JsonObject
            {
                ["viewId"] = viewId,
                ["spaceId"] = spaceId,
                ["selector"] = new JsonObject
                {
                    ["kind"] = "map-region",
                    ["regionId"] = regionId,
                },
            };
        }

        static JsonObject WholeRegionAccess(string regionId)
        {
            return new JsonObject
            {
                ["regionId"] = regionId,
                ["access"] = "whole",
                ["reason"] = "Synthetic template projection is writable only as one complete region.",
            };
        }

        static JsonObject Copy(
            string operationId,
            int sequence,
            string sourceViewId,
            string targetViewId)
        {
            return new JsonObject
            {
                ["operationId"] = operationId,
                ["sequence"] = sequence,
                ["overlapPolicy"] = "reject",
                ["reason"] = "Copy one canonical template-relative source range.",
                ["kind"] = "copy-range",
                ["sourceViewId"] = sourceViewId,
                ["targetViewId"] = targetViewId,
            };
        }
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

    private static string FamilyWithTwoBankInstances()
    {
        JsonObject family = ParseFamily(FamilyJsonWithRootWriteConstraint(
            "explicit-range",
            capacity: 32));
        family["schemaVersion"] = "1.2";
        JsonObject regionSet = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["regionSets"])[0]);
        regionSet["regionTemplates"] = new JsonArray
        {
            new JsonObject
            {
                ["templateId"] = "ab-bank",
                ["capacityBytes"] = 16,
                ["regions"] = new JsonArray
                {
                    RelativeRegion("bank", null, 0, 16, "image"),
                    RelativeRegion("dp-before", "bank", 0, 4, "code"),
                    RelativeRegion("tp-code", "bank", 4, 8, "code"),
                    RelativeRegion("dp-after", "bank", 12, 4, "code"),
                },
            },
        };
        regionSet["regionInstances"] = new JsonArray
        {
            Instance("a-bank", 0, "a-bank", "a-code"),
            Instance("b-bank", 16, "b-bank", "b-code"),
        };
        return family.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        static JsonObject RelativeRegion(
            string regionId,
            string? parentRegionId,
            int start,
            int length,
            string kind)
        {
            var region = new JsonObject
            {
                ["regionId"] = regionId,
                ["owner"] = regionId == "tp-code" ? "tp" : "system",
                ["kind"] = kind,
                ["range"] = new JsonObject { ["start"] = start, ["length"] = length },
                ["writeConstraint"] = regionId == "tp-code" ? "whole-region" : "explicit-range",
                ["alignment"] = 1,
            };
            if (parentRegionId is not null)
            {
                region["parentRegionId"] = parentRegionId;
            }

            return region;
        }

        static JsonObject Instance(
            string instanceId,
            int baseOffset,
            string bankRegionId,
            string codeRegionId)
        {
            return new JsonObject
            {
                ["instanceId"] = instanceId,
                ["templateId"] = "ab-bank",
                ["baseOffset"] = baseOffset,
                ["parentRegionId"] = "root",
                ["resolvedRegionIds"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["templateRegionId"] = "bank",
                        ["resolvedRegionId"] = bankRegionId,
                    },
                    new JsonObject
                    {
                        ["templateRegionId"] = "dp-before",
                        ["resolvedRegionId"] = $"{instanceId}-before",
                    },
                    new JsonObject
                    {
                        ["templateRegionId"] = "tp-code",
                        ["resolvedRegionId"] = codeRegionId,
                    },
                    new JsonObject
                    {
                        ["templateRegionId"] = "dp-after",
                        ["resolvedRegionId"] = $"{instanceId}-after",
                    },
                },
            };
        }
    }
}
