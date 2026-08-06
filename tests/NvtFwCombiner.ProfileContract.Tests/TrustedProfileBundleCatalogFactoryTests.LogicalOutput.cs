using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies map-bound lowering cannot silently treat a logical runtime capacity as a physical map capacity.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsRuntimeRequestCapacityWithoutLogicalOutputRoute()
    {
        PreparedProfile preparation = PrepareSupportedBlankCopy(familyHash =>
        {
            JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(SupportedProfileJson(familyHash)));
            profile["schemaVersion"] = "2.3";
            Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["spaces"])[1])["capacity"] = new JsonObject
            {
                ["kind"] = "runtime-request",
            };
            return profile.ToJsonString();
        });

        V2CompositionPlanCompileResult result = Compile(preparation);

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.unsupported-declaration", issue.Code);
        Assert.Contains("runtime-request output capacity", issue.Message, StringComparison.Ordinal);
    }
}
