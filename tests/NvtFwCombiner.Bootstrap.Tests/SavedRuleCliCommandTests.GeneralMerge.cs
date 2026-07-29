using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
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
        Assert.Contains(ProcessorDependencyUnsupported, result.Error, StringComparison.Ordinal);
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
        JsonObject json = ValidGeneralMergeRuleObject();
        OperationFragments(json)[0]!.AsObject()["operationId"] = "reviewed-copy-operation";
        string rule = await WriteRuleAsync(workspace, json);
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
        Assert.Equal("reviewed-copy-operation", operation.GetProperty("OperationId").GetString());
        JsonElement provenance = operation.GetProperty("Provenance");
        Assert.Equal("saved-rule", provenance.GetProperty("Kind").GetString());
        Assert.Equal("copy-display-window", provenance.GetProperty("SourceId").GetString());
        Assert.Equal("1.0.0", provenance.GetProperty("SourceVersion").GetString());
        JsonElement admission =
            document.RootElement.GetProperty("GeneralAdmission");
        Assert.Equal(
            "nt51950-general-merge-logical-candidate",
            admission.GetProperty("TrustedParentId").GetString());
        Assert.Equal(
            "copy-display-window",
            admission.GetProperty("SavedRuleId").GetString());
        JsonElement effective =
            admission.GetProperty("EffectiveLimits");
        Assert.Equal(
            1,
            effective.GetProperty("MaximumMappingCount").GetInt32());
        Assert.Equal(
            0x20,
            effective.GetProperty("MaximumTotalWriteBytes").GetInt64());
        JsonElement resource = Assert.Single(
            admission.GetProperty("InputResources").EnumerateArray());
        Assert.Equal(
            "reviewed-copy-operation",
            resource.GetProperty("SlotId").GetString());
        Assert.Equal(
            64,
            resource.GetProperty("LengthBytes").GetInt64());
    }

    /// <summary>Rejects saved-rule report paths that would overwrite the reviewed rule JSON.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsReportPathAliasingSavedRule()
    {
        using var workspace = TempWorkspace.Create();
        string rule = await WriteRuleAsync(workspace, ValidGeneralMergeRuleObject());
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
            "--report",
            rule,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Report path must not overwrite saved-rule input", result.Error, StringComparison.Ordinal);
        Assert.Contains("\"ruleId\":\"copy-display-window\"", await File.ReadAllTextAsync(
            rule,
            TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
