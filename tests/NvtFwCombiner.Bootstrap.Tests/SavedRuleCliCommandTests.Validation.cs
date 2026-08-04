using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    /// <summary>Saved Rule v1 is retained only as historical evidence and cannot enter inspection.</summary>
    [Theory]
    [InlineData("validate")]
    [InlineData("mappings")]
    public async Task SavedRuleInspectionRejectsV1WithMigrationGuidance(string action)
    {
        using var workspace = TempWorkspace.Create();
        string rule = workspace.PathFor("rule-v1.json");
        await File.WriteAllTextAsync(
            rule,
            ValidGeneralMergeRuleV1Json(),
            TestContext.Current.CancellationToken);

        CliRunResult result = await RunCliAsync(["saved-rule", action, rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(SchemaVersionUnsupported, result.Error, StringComparison.Ordinal);
        Assert.Contains(
            "Saved Rule v1 is retired; migrate the document to Saved Rule v2",
            result.Error,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Rule:", result.Output, StringComparison.Ordinal);
    }

    /// <summary>An unknown future schema is rejected without being mislabeled as v1.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsUnknownSchemaVersionAccurately()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject();
        json["schemaVersion"] = "3.0";
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(SchemaVersionUnsupported, result.Error, StringComparison.Ordinal);
        Assert.Contains("schemaVersion must be '2.0'", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("v1 is retired", result.Error, StringComparison.Ordinal);
    }

    /// <summary>V2-only inspection preserves actionable storage and JSON failure categories.</summary>
    [Theory]
    [InlineData("missing", FileNotFound)]
    [InlineData("invalid-json", JsonInvalid)]
    public async Task SavedRuleValidateClassifiesDocumentReadFailures(
        string scenario,
        string expectedCode)
    {
        using var workspace = TempWorkspace.Create();
        string rule = workspace.PathFor("rule.json");
        if (scenario == "invalid-json")
        {
            await File.WriteAllTextAsync(
                rule,
                "{ not-json",
                TestContext.Current.CancellationToken);
        }

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(expectedCode, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Rule:", result.Output, StringComparison.Ordinal);
    }
}
