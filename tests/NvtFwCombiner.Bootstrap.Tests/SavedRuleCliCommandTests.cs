using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI tests for saved composition rule validation and mapping projection.</summary>
public sealed class SavedRuleCliCommandTests
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

    /// <summary>Verifies command-like or unknown saved-rule fields are rejected instead of becoming hidden execution hooks.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsUnknownCommandFields()
    {
        using var workspace = TempWorkspace.Create();
        string rule = workspace.PathFor("rule.json");
        string json = ValidGeneralMergeRuleJson().Replace(
            "\"owner\": \"firmware-owner\"",
            "\"shellCommand\": \"Combiner.exe /danger\",\n  \"owner\": \"firmware-owner\"",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(rule, json, TestContext.Current.CancellationToken);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.property.unknown", result.Error, StringComparison.Ordinal);
        Assert.Contains("$.shellCommand", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies General Merge can consume a saved rule through slot bindings and preserve report provenance.</summary>
    [Fact]
    public async Task GeneralMergeBuildConsumesSavedRuleMappings()
    {
        using var workspace = TempWorkspace.Create();
        string rule = workspace.PathFor("rule.json");
        await File.WriteAllTextAsync(rule, ValidGeneralMergeRuleJson(), TestContext.Current.CancellationToken);
        string source = workspace.Write("source.bin", [.. Enumerable.Range(0, 64).Select(value => (byte)value)]);
        string output = workspace.PathFor("out.bin");
        string report = workspace.PathFor("report.json");

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "build",
            "--profile",
            "NT51950",
            "--size",
            "0x120",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
            "--output",
            output,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        byte[] outputBytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(0x120, outputBytes.Length);
        Assert.Equal(
            [.. Enumerable.Range(0x10, 0x20).Select(value => (byte)value)],
            outputBytes[0x100..0x120]);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement operation = Assert.Single(document.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal("copy-fw-window", operation.GetProperty("OperationId").GetString());
        JsonElement provenance = operation.GetProperty("Provenance");
        Assert.Equal("saved-rule", provenance.GetProperty("Kind").GetString());
        Assert.Equal("copy-display-window", provenance.GetProperty("SourceId").GetString());
        Assert.Equal("1.0.0", provenance.GetProperty("SourceVersion").GetString());
    }

    private static string ValidGeneralMergeRuleJson()
    {
        return /*lang=json,strict*/ """
            {
              "schemaVersion": "1.0",
              "ruleId": "copy-display-window",
              "ruleVersion": "1.0.0",
              "displayName": "Copy display window",
              "compositionKind": "merge",
              "sourceExperience": "general-merge",
              "supportStatus": "draft",
              "compatibility": {
                "profileIds": ["nt51950-general-merge-workbench"],
                "icIds": ["NT51950"],
                "modeIds": ["general-merge"]
              },
              "inputSlotTemplates": [
                {
                  "slotTemplateId": "source-bin",
                  "role": "Source BIN",
                  "cardinality": "one",
                  "acceptedExtensions": [".bin"]
                }
              ],
              "mappingRows": [
                {
                  "rowId": "copy-fw-window",
                  "sourceSlotTemplateId": "source-bin",
                  "sourceRange": { "start": 16, "length": 32 },
                  "targetAddressSpaceId": "output-image",
                  "targetRange": { "start": 256, "length": 32 },
                  "overlapPolicy": "reject",
                  "reason": "Reviewed General Merge mapping."
                }
              ],
              "operationFragments": [
                {
                  "operationId": "copy-fw-window",
                  "kind": "copy-range",
                  "reason": "Compile mapping row to a copy operation.",
                  "mappingRowIds": ["copy-fw-window"]
                }
              ],
              "validationRuleIds": ["reviewed-bounds"],
              "owner": "firmware-owner",
              "evidenceRefs": []
            }
            """;
    }

    private static async Task<CliRunResult> RunCliAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await CliApplication
            .RunAsync(args, output, error, TestContext.Current.CancellationToken);
        return new CliRunResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CliRunResult(int ExitCode, string Output, string Error);
}
