using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    /// <summary>V2 General Replace validation and mapping inspection use the same exact Parent as execution.</summary>
    [Fact]
    public async Task SavedRuleV2GeneralReplaceInspectionUsesExactParent()
    {
        using var workspace = TempWorkspace.Create();
        string rule = await WriteRuleAsync(
            workspace,
            ReplaceCliCommandTests.ValidGeneralReplaceV2RuleObject());

        CliRunResult validation = await RunCliAsync(
            ["saved-rule", "validate", rule]);
        CliRunResult mappings = await RunCliAsync(
            ["saved-rule", "mappings", rule]);

        Assert.Equal(0, validation.ExitCode);
        Assert.Equal(string.Empty, validation.Error);
        Assert.Contains(
            "Parent: nt51926-ctrlram-replace-candidate / nt51926-general-replace-dp-single-candidate / nt51926-ctrlram-replace / nt51926-general-replace-full-flash-256k",
            validation.Output,
            StringComparison.Ordinal);
        Assert.Equal(0, mappings.ExitCode);
        Assert.Equal(string.Empty, mappings.Error);
        Assert.Contains("Mappings: 1", mappings.Output, StringComparison.Ordinal);
        Assert.Contains(
            "--mapping 0x3E020+0x2=<source-bin>",
            mappings.Output,
            StringComparison.Ordinal);
    }

    /// <summary>General Replace inspection fails closed before projection when routing or Parent identity is invalid.</summary>
    [Theory]
    [InlineData("mismatched-parent", "not uniquely installed")]
    [InlineData("mismatched-workflow", "supported compositionKind/sourceExperienceId pair")]
    [InlineData("missing-parent", "requires one exact parentBinding.profileId")]
    public async Task SavedRuleV2GeneralReplaceInspectionRejectsInvalidAuthority(
        string mutation,
        string expectedMessage)
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ReplaceCliCommandTests.ValidGeneralReplaceV2RuleObject();
        switch (mutation)
        {
            case "mismatched-parent":
                json["parentBinding"]!["profileId"] = "not-installed";
                break;
            case "mismatched-workflow":
                json["compositionKind"] = "merge";
                break;
            case "missing-parent":
                _ = json["parentBinding"]!.AsObject().Remove("profileId");
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown Saved Rule inspection mutation: {mutation}");
        }

        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(
            ["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(expectedMessage, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Mappings:", result.Output, StringComparison.Ordinal);
    }

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

    /// <summary>
    /// Projects a processor-free General Replace rule through the canonical
    /// typed draft without executing or writing firmware.
    /// </summary>
    [Fact]
    public async Task SavedRuleMappingsProjectsGeneralReplaceThroughTypedDraftWithoutExecution()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralReplaceRuleObject();
        JsonObject rowJson = MappingRows(json)[0]!.AsObject();
        _ = rowJson.Remove("sourceRange");
        rowJson["targetRegionId"] = "dp-code";
        rowJson["alignment"] = 4;
        rowJson["reason"] = "Reviewed General Replace mapping.";
        OperationFragments(json)[0]!.AsObject()["operationId"] =
            "reviewed-replace-operation";
        string rulePath = await WriteRuleAsync(workspace, json);
        string unexpectedOutput = workspace.PathFor("must-not-exist.bin");

        SavedCompositionRuleLoadResult load =
            SavedCompositionRuleLoader.Load(rulePath);
        Assert.True(load.IsValid, string.Join(
            Environment.NewLine,
            load.Issues.Select(static issue => issue.Message)));
        bool projected = SavedRuleGeneralMappingDraftAdapter.TryCreate(
            load.Rule!,
            static row => $"resolved-{row.SourceReference}.bin",
            out GeneralMappingDraftState? draft,
            out IReadOnlyList<SavedRuleValidationIssue> issues);

        Assert.True(projected, string.Join(
            Environment.NewLine,
            issues.Select(static issue => issue.Message)));
        GeneralMappingDraftRow row = Assert.Single(draft!.Rows);
        Assert.Equal("reviewed-replace-operation", row.MappingId);
        Assert.Equal(ExplicitMappingOperationKind.ReplaceRange, row.OperationKind);
        Assert.Equal(GeneralMappingSourceKind.FileArtifact, row.Source.Kind);
        Assert.Equal("resolved-source-bin.bin", row.Source.Reference);
        Assert.Equal(new ByteRange(0, 0x20), row.SourceRange);
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, row.TargetAddressSpaceId);
        Assert.Equal("dp-code", row.TargetRegionId);
        Assert.Equal(new ByteRange(0x100, 0x20), row.TargetRange);
        Assert.Equal(OverlapPolicy.Reject, row.OverlapPolicy);
        Assert.Equal(4, row.Alignment);
        Assert.Equal("Reviewed General Replace mapping.", row.Reason);
        Assert.Equal("saved-rule", row.Provenance.Kind);
        Assert.Equal("copy-display-window", row.Provenance.SourceId);
        Assert.Equal("1.0.0", row.Provenance.SourceVersion);

        CliRunResult result = await RunCliAsync(
            ["saved-rule", "mappings", rulePath]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "reviewed-replace-operation: source-bin 0x0-0x1F (len 0x20) -> output-image 0x100-0x11F (len 0x20)",
            result.Output,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "(entire replacement file)",
            result.Output,
            StringComparison.Ordinal);
        Assert.Contains(
            "--mapping 0x100+0x20=<source-bin>",
            result.Output,
            StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.Error);
        Assert.False(File.Exists(unexpectedOutput));
    }

    /// <summary>Rejects a Saved Rule projection that cannot preserve overlap semantics.</summary>
    [Fact]
    public async Task SavedRuleMappingsRejectsNonProjectableGeneralReplaceOverlap()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralReplaceRuleObject();
        JsonObject row = MappingRows(json)[0]!.AsObject();
        _ = row.Remove("sourceRange");
        row["overlapPolicy"] = "allow-declared";
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result =
            await RunCliAsync(["saved-rule", "mappings", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(
            MappingRowOverlapPolicyUnsupported,
            result.Error,
            StringComparison.Ordinal);
        Assert.DoesNotContain("--mapping", result.Output, StringComparison.Ordinal);
    }
}
