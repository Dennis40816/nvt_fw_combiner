using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
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

    /// <summary>Rejects fragment-level processor dependencies until General Merge rule execution can run them.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsProcessorDependentGeneralMergeFragments()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        OperationFragments(json)[0]!.AsObject()["processorDependencyIds"] = new JsonArray("crc-v1");
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.operation-fragment.processor-dependency.unsupported", result.Error, StringComparison.Ordinal);
        Assert.Contains("$.operationFragments[0].processorDependencyIds", result.Error, StringComparison.Ordinal);
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

    /// <summary>Rejects General Merge fragments that authorize more than one executed mapping row.</summary>
    [Fact]
    public async Task SavedRuleValidateRejectsGeneralMergeFragmentsWithMultipleRows()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeRuleObject();
        JsonObject row = CloneObject(MappingRows(json)[0]!);
        row["rowId"] = "copy-second-window";
        row["sourceRange"] = new JsonObject
        {
            ["start"] = 32,
            ["length"] = 16,
        };
        row["targetRange"] = new JsonObject
        {
            ["start"] = 0,
            ["length"] = 16,
        };
        MappingRows(json).Add(row);
        OperationFragments(json)[0]!.AsObject()["mappingRowIds"] = new JsonArray("copy-fw-window", "copy-second-window");
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("saved-rule.operation-fragment.mapping-row-count", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("saved-rule.mapping-row.unreferenced", result.Error, StringComparison.Ordinal);
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
}
