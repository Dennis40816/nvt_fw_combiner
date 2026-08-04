using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    /// <summary>Normal General Merge execution reports the same explicit v1 migration gate.</summary>
    [Fact]
    public async Task GeneralMergeRuleExecutionRejectsV1WithMigrationGuidance()
    {
        using var workspace = TempWorkspace.Create();
        string rule = workspace.PathFor("rule-v1.json");
        await File.WriteAllTextAsync(
            rule,
            ValidGeneralMergeRuleV1Json(),
            TestContext.Current.CancellationToken);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--rule",
            rule,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(SchemaVersionUnsupported, result.Error, StringComparison.Ordinal);
        Assert.Contains(
            "Saved Rule v1 is retired; migrate the document to Saved Rule v2",
            result.Error,
            StringComparison.Ordinal);
    }
}
