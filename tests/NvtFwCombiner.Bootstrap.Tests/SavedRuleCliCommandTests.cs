using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>Rejects protectedRangePolicy objects instead of allowing hidden nested rule data.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsNestedProtectedRangePolicy()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        json["protectedRangePolicy"] = new JsonObject
        {
            ["shellCommand"] = "Combiner.exe /danger",
        };
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("$.protectedRangePolicy", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Rule: copy-display-window", result.Output, StringComparison.Ordinal);
    }

    /// <summary>Rejects optional saved-rule fields whose shapes are not part of the reviewed schema.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsUnsupportedOptionalFieldShapes()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        json["description"] = new JsonObject { ["shellCommand"] = "Combiner.exe /danger" };
        json["reviewers"] = new JsonArray(new JsonObject { ["name"] = "firmware-owner" });
        json["inputSlotTemplates"]!.AsArray()[0]!.AsObject()["acceptedExtensions"] =
            new JsonObject { ["shellCommand"] = "Combiner.exe /danger" };
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("$.description", result.Error, StringComparison.Ordinal);
        Assert.Contains("$.reviewers[0]", result.Error, StringComparison.Ordinal);
        Assert.Contains("$.inputSlotTemplates[0].acceptedExtensions", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects duplicate row identifiers so operation fragments cannot ambiguously bind rows.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsDuplicateMappingRowIds()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        JsonObject row = CloneObject(MappingRows(json)[0]!);
        MappingRows(json).Add(row);
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.mapping-row.duplicate", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects mapping rows that bind to undeclared slot templates.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsDanglingSourceSlotTemplateIds()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        MappingRows(json)[0]!.AsObject()["sourceSlotTemplateId"] = "missing-source";
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.mapping-row.source-slot-template-unknown", result.Error, StringComparison.Ordinal);
        Assert.Contains("missing-source", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Current General Merge rule consumption supports only copy-range operation fragments.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsUnsupportedGeneralMergeFragmentKinds()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        OperationFragments(json)[0]!["kind"] = "run-external-processor";
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.operation-fragment.kind-unsupported", result.Error, StringComparison.Ordinal);
        Assert.Contains("$.operationFragments[0].kind", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects rows that are present in mappingRows but absent from reviewed operation fragments.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsUnfragmentedGeneralMergeRows()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        JsonObject row = CloneObject(MappingRows(json)[0]!);
        row["rowId"] = "copy-second-window";
        row["sourceRange"] = new JsonObject
        {
            ["start"] = 0,
            ["length"] = 16,
        };
        row["targetRange"] = new JsonObject
        {
            ["start"] = 0,
            ["length"] = 16,
        };
        MappingRows(json).Add(row);
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.mapping-row.unreferenced", result.Error, StringComparison.Ordinal);
        Assert.Contains("copy-second-window", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects reviewed fragment sets that reference the same mapping row more than once.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsMappingRowsReferencedByMultipleFragments()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        JsonObject fragment = CloneObject(OperationFragments(json)[0]!);
        fragment["operationId"] = "copy-fw-window-again";
        OperationFragments(json).Add(fragment);
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.operation-fragment.mapping-row-duplicate-reference", result.Error, StringComparison.Ordinal);
        Assert.Contains("copy-fw-window", result.Error, StringComparison.Ordinal);
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
        Assert.Contains("saved-rule.mapping-row.target-address-space-unsupported", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--mapping", result.Output, StringComparison.Ordinal);
    }

    /// <summary>Rejects General Merge saved-rule overlap policies not supported by CLI consumption.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsUnsupportedGeneralMergeOverlapPolicies()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        MappingRows(json)[0]!.AsObject()["overlapPolicy"] = "replace-existing";
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.mapping-row.overlap-policy-unsupported", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects processor-dependent General Merge saved rules until processor fragments are actually supported.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsProcessorDependentSavedRule()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        json["processorDependencyIds"] = new JsonArray("crc-v1");
        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [.. Enumerable.Range(0, 64).Select(value => (byte)value)]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--size",
            "0x120",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("saved-rule.processor-dependency.unsupported", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Requires a saved-rule compatibility envelope to match IC, derived profile id, and mode.</summary>
    [Theory]
    [InlineData("profileIds", "nt51951-general-merge-workbench")]
    [InlineData("modeIds", "standard-merge")]
    public async Task GeneralMergePreviewRequiresFullSavedRuleCompatibilityEnvelope(string propertyName, string incompatibleId)
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        json["compatibility"]![propertyName] = new JsonArray(incompatibleId);
        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [.. Enumerable.Range(0, 64).Select(value => (byte)value)]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--size",
            "0x120",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            "not compatible with NT51950 / nt51950-general-merge-workbench / general-merge",
            result.Error,
            StringComparison.Ordinal);
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

    /// <summary>Allows the reviewed scalar protected-range policy values from the saved-rule schema.</summary>
    [Fact]
    public async Task SavedRuleValidateAcceptsScalarProtectedRangePolicy()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        json["protectedRangePolicy"] = "profile-defined";
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
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

    private static JsonObject ValidGeneralMergeRuleObject()
    {
        return JsonNode.Parse(ValidGeneralMergeRuleJson())!.AsObject();
    }

    private static JsonArray MappingRows(JsonObject json)
    {
        return json["mappingRows"]!.AsArray();
    }

    private static JsonArray OperationFragments(JsonObject json)
    {
        return json["operationFragments"]!.AsArray();
    }

    private static JsonObject CloneObject(JsonNode node)
    {
        return JsonNode.Parse(node.ToJsonString())!.AsObject();
    }

    private static async Task<string> WriteRuleAsync(TempWorkspace workspace, JsonObject json)
    {
        string rule = workspace.PathFor("rule.json");
        await File.WriteAllTextAsync(rule, json.ToJsonString(), TestContext.Current.CancellationToken);
        return rule;
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
