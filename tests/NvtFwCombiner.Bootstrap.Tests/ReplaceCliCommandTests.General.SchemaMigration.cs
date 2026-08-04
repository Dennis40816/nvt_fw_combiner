using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Normal General Replace execution reports the shared v1 migration gate.</summary>
    [Fact]
    public async Task GeneralReplaceRuleExecutionRejectsV1WithMigrationGuidance()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", CreatePattern(0x40000, 0x24));
        string rule = workspace.PathFor("rule-v1.json");
        await File.WriteAllTextAsync(
            rule,
            /*lang=json,strict*/ """{"schemaVersion":"1.0"}""",
            TestContext.Current.CancellationToken);

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
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
