using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    /// <summary>A normal saved-rule run cannot accept an out-of-band fill override.</summary>
    [Fact]
    public async Task GeneralMergeSavedRuleRejectsFillOverride()
    {
        using var workspace = TempWorkspace.Create();
        string rule = await WriteRuleAsync(workspace, ValidGeneralMergeV2RuleObject());
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--fill",
            "0xFF",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            "--fill cannot override a saved-rule initializer",
            result.Error,
            StringComparison.Ordinal);
    }

    /// <summary>A normal v2 saved-rule run cannot accept an out-of-band capacity override.</summary>
    [Fact]
    public async Task GeneralMergeSavedRuleRejectsSizeOverride()
    {
        using var workspace = TempWorkspace.Create();
        string rule = await WriteRuleAsync(workspace, ValidGeneralMergeV2RuleObject());
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--size",
            "0x20",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            "--size cannot override a saved-rule initializer",
            result.Error,
            StringComparison.Ordinal);
    }

    /// <summary>Rejects processor-dependent General Merge saved rules until processor fragments are actually supported.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsProcessorDependentSavedRule()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject();
        json["processorStageIds"] = new JsonArray("crc-v1");
        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(ProcessorDependencyUnsupported, result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects a v2 access envelope that broadens the logical General output.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsBroadenedV2AccessEnvelope()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject();
        json["accessEnvelope"]!["allowedRegionIds"] =
            new JsonArray("general-output", "other-output");
        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            MappingRowTargetRegionUnsupported,
            result.Error,
            StringComparison.Ordinal);
    }

    /// <summary>Requires a saved-rule compatibility envelope to match IC, derived profile id, and mode.</summary>
    [Theory]
    [InlineData("profileId", "nt51951-general-merge-logical-candidate", "not compatible")]
    [InlineData("bundleContentHash", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "not compatible")]
    [InlineData("sourceExperienceId", "general-replace", ExperienceKindMismatch)]
    public async Task GeneralMergePreviewRequiresFullSavedRuleCompatibilityEnvelope(
        string propertyName,
        string incompatibleId,
        string expectedError)
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject();
        if (propertyName is "profileId" or "bundleContentHash")
        {
            json["parentBinding"]![propertyName] = incompatibleId;
        }
        else
        {
            json[propertyName] = incompatibleId;
        }

        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(expectedError, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Documents that manual initializer flags are absent from both v2 rule forms.</summary>
    [Fact]
    public async Task GeneralMergeHelpKeepsV2RuleInitializerClosed()
    {
        CliRunResult result = await RunCliAsync(["general-merge", "--help"]);

        Assert.Equal(0, result.ExitCode);
        string[] ruleLines =
        [
            .. result.Output.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(static line => line.Contains("--rule", StringComparison.Ordinal)),
        ];
        Assert.Equal(2, ruleLines.Length);
        Assert.All(ruleLines, static line =>
        {
            Assert.Contains("<v2-rule.json>", line, StringComparison.Ordinal);
            Assert.DoesNotContain("--size", line, StringComparison.Ordinal);
            Assert.DoesNotContain("--fill", line, StringComparison.Ordinal);
        });
    }

    /// <summary>Normal v2 rule consumption closes over output bytes, report values, and Preview/Build identity.</summary>
    [Fact]
    public async Task GeneralMergeBuildConsumesV2InitializerAndPreservesPreviewIdentity()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject(capacity: 4, fillByte: 0xA5);
        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [0x10]);
        string output = workspace.PathFor("out.bin");
        string previewReport = workspace.PathFor("preview-report.json");
        string buildReport = workspace.PathFor("build-report.json");

        CliRunResult preview = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
            "--report",
            previewReport,
        ]);

        CliRunResult build = await RunCliAsync([
            "general-merge",
            "build",
            "--profile",
            "NT51950",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
            "--output",
            output,
            "--report",
            buildReport,
        ]);

        Assert.Equal(0, preview.ExitCode);
        Assert.Equal(0, build.ExitCode);
        byte[] outputBytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0xA5, 0x10, 0xA5, 0xA5], outputBytes);

        using var previewDocument = JsonDocument.Parse(await File.ReadAllTextAsync(
            previewReport,
            TestContext.Current.CancellationToken));
        using var buildDocument = JsonDocument.Parse(await File.ReadAllTextAsync(
            buildReport,
            TestContext.Current.CancellationToken));
        JsonElement previewRoot = previewDocument.RootElement;
        JsonElement buildRoot = buildDocument.RootElement;
        Assert.Equal(
            previewRoot.GetProperty("CompilationFingerprint").GetString(),
            buildRoot.GetProperty("CompilationFingerprint").GetString());
        Assert.Contains("PreviewToken: ", preview.Output, StringComparison.Ordinal);
        JsonElement initialization = buildRoot.GetProperty("ImageInitialization");
        Assert.Equal(4, initialization.GetProperty("Capacity").GetInt64());
        Assert.Equal(0xA5, initialization.GetProperty("FillByte").GetInt32());
        JsonElement operation = Assert.Single(buildRoot.GetProperty("Operations").EnumerateArray());
        Assert.Equal("reviewed-copy-operation", operation.GetProperty("OperationId").GetString());
        JsonElement provenance = operation.GetProperty("Provenance");
        Assert.Equal("saved-rule", provenance.GetProperty("Kind").GetString());
        Assert.Equal("copy-display-window", provenance.GetProperty("SourceId").GetString());
        Assert.Equal("1.0.0", provenance.GetProperty("SourceVersion").GetString());
    }

    /// <summary>Rejects saved-rule report paths that would overwrite the reviewed rule JSON.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsReportPathAliasingSavedRule()
    {
        using var workspace = TempWorkspace.Create();
        string rule = await WriteRuleAsync(workspace, ValidGeneralMergeV2RuleObject());
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
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
