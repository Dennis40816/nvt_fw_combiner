using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    /// <summary>Verifies a General Merge saved rule validates and projects CLI mapping fragments without executing firmware.</summary>
    [Fact]
    public async Task SavedRuleMappingsPrintsGeneralMergeMappingFragments()
    {
        using var workspace = TempWorkspace.Create();
        string rule = workspace.PathFor("rule.json");
        await File.WriteAllTextAsync(rule, ValidGeneralMergeRuleJson(), TestContext.Current.CancellationToken);

        CliRunResult result = await RunCliAsync(["saved-rule", "mappings", rule]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Rule: copy-display-window 1.0.0", result.Output, StringComparison.Ordinal);
        Assert.Contains("Composition: merge / general-merge", result.Output, StringComparison.Ordinal);
        Assert.Contains("Mappings: 1", result.Output, StringComparison.Ordinal);
        Assert.Contains("copy-fw-window: source-bin 0x10-0x2F (len 0x20) -> output-image 0x100-0x11F (len 0x20)", result.Output, StringComparison.Ordinal);
        Assert.Contains("--mapping 0x10+0x100+0x20=<source-bin>", result.Output, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.Error);
    }

    /// <summary>Rejects General Merge rows that saved-rule mappings cannot project without changing semantics.</summary>
    [Fact]
    public async Task SavedRuleMappingsRejectsUnsupportedGeneralMergeRows()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        MappingRows(json)[0]!.AsObject()["targetAddressSpaceId"] = "tp-image";
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "mappings", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(MappingRowTargetAddressSpaceUnsupported, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--mapping", result.Output, StringComparison.Ordinal);
    }

    /// <summary>Rejects target regions that the current General Merge saved-rule materializer cannot preserve.</summary>
    [Fact]
    public async Task SavedRuleMappingsRejectsUnsupportedGeneralMergeTargetRegions()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        MappingRows(json)[0]!.AsObject()["targetRegionId"] = "tp-payload";
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "mappings", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(MappingRowTargetRegionUnsupported, result.Error, StringComparison.Ordinal);
        Assert.Contains("$.mappingRows[0].targetRegionId", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--mapping", result.Output, StringComparison.Ordinal);
    }

    /// <summary>Rejects rows whose declared alignment would be lost by manual mapping projection.</summary>
    [Fact]
    public async Task SavedRuleMappingsRejectsUnalignedGeneralMergeRows()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        JsonObject row = MappingRows(json)[0]!.AsObject();
        row["alignment"] = 4;
        row["sourceRange"] = new JsonObject
        {
            ["start"] = 18,
            ["length"] = 32,
        };
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "mappings", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(MappingRowAlignment, result.Error, StringComparison.Ordinal);
        Assert.Contains("$.mappingRows[0].alignment", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--mapping", result.Output, StringComparison.Ordinal);
    }

    /// <summary>Rejects processor-dependent General Replace saved-rule projections until postbuild-aware rule projection exists.</summary>
    [Theory]
    [InlineData(true, ProcessorDependencyUnsupported)]
    [InlineData(false, OperationFragmentProcessorDependencyUnsupported)]
    public async Task SavedRuleMappingsRejectsProcessorDependentGeneralReplaceRules(
        bool rootDependency,
        string expectedIssueCode)
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralReplaceRuleObject();
        if (rootDependency)
        {
            json["processorDependencyIds"] = new JsonArray("legacy-combiner-postbuild");
        }
        else
        {
            OperationFragments(json)[0]!.AsObject()["processorDependencyIds"] =
                new JsonArray("legacy-combiner-postbuild");
        }

        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "mappings", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(expectedIssueCode, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--mapping", result.Output, StringComparison.Ordinal);
    }
}
