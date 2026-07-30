using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    /// <summary>Normal Preview and Build reject v2 documents outside the complete reviewed contract.</summary>
    [Theory]
    [InlineData("preview", "missing-governance", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "missing-governance", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "invalid-rule-id", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "invalid-rule-id", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "invalid-rule-version", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "invalid-rule-version", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unsupported-promotion", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("build", "unsupported-promotion", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("preview", "blocking-promotion-debt", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("build", "blocking-promotion-debt", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("preview", "empty-reviewers", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("build", "empty-reviewers", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("preview", "empty-evidence", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "empty-evidence", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unknown-parent-binding-property", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "unknown-parent-binding-property", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unknown-slot-template-property", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "unknown-slot-template-property", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unknown-mapping-fragment-property", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "unknown-mapping-fragment-property", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unknown-source-slot-property", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "unknown-source-slot-property", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unknown-source-range-property", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "unknown-source-range-property", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unknown-access-envelope-property", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "unknown-access-envelope-property", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unknown-slot-role", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("build", "unknown-slot-role", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("preview", "unsupported-slot-cardinality", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("build", "unsupported-slot-cardinality", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("preview", "broadened-slot-extension", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("build", "broadened-slot-extension", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("preview", "unknown-validation-reference", "saved-rule.v2.parent-narrowing-invalid")]
    [InlineData("build", "unknown-validation-reference", "saved-rule.v2.parent-narrowing-invalid")]
    public async Task GeneralMergeRuleExecutionRequiresCompleteV2Admission(
        string action,
        string mutation,
        string expectedIssueCode)
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject();
        switch (mutation)
        {
            case "missing-governance":
                _ = json.Remove("owner");
                break;
            case "invalid-rule-id":
                json["ruleId"] = "Invalid Rule Id";
                break;
            case "invalid-rule-version":
                json["ruleVersion"] = "1";
                break;
            case "unsupported-promotion":
                json["promotion"]!["stage"] = "supported";
                break;
            case "blocking-promotion-debt":
                json["promotion"]!["blockers"] = new JsonArray(new JsonObject
                {
                    ["blockerId"] = "mapping-review",
                    ["kind"] = "mapping",
                    ["reason"] = "Mapping review remains open.",
                    ["evidenceRefs"] = new JsonArray("initializer-evidence"),
                });
                break;
            case "empty-reviewers":
                json["reviewers"] = new JsonArray();
                break;
            case "empty-evidence":
                json["evidenceRefs"] = new JsonArray();
                break;
            case "unknown-parent-binding-property":
                json["parentBinding"]!["unexpected"] = true;
                break;
            case "unknown-slot-template-property":
                json["slotTemplates"]![0]!["unexpected"] = true;
                break;
            case "unknown-mapping-fragment-property":
                json["mappingFragments"]![0]!["unexpected"] = true;
                break;
            case "unknown-source-slot-property":
                json["mappingFragments"]![0]!["sourceSlot"]!["unexpected"] = true;
                break;
            case "unknown-source-range-property":
                json["mappingFragments"]![0]!["sourceRange"]!["unexpected"] = true;
                break;
            case "unknown-access-envelope-property":
                json["accessEnvelope"]!["unexpected"] = true;
                break;
            case "unknown-slot-role":
                json["slotTemplates"]![0]!["role"] = "other-source";
                break;
            case "unsupported-slot-cardinality":
                json["slotTemplates"]![0]!["cardinality"] = "many";
                break;
            case "broadened-slot-extension":
                json["slotTemplates"]![0]!["acceptedExtensions"] =
                    new JsonArray(".bin", ".hex");
                break;
            case "unknown-validation-reference":
                json["validationRuleIds"] = new JsonArray("not-in-parent");
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown test mutation.");
        }

        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [0x10]);
        List<string> args =
        [
            "general-merge",
            action,
            "--profile",
            "NT51950",
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
        ];
        if (action == "build")
        {
            args.AddRange(["--output", workspace.PathFor("rejected.bin")]);
        }

        CliRunResult result = await RunCliAsync([.. args]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(expectedIssueCode, result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.PathFor("rejected.bin")));
    }

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

    /// <summary>
    /// Threads the reviewed Saved Rule resource policy into canonical admission,
    /// where an envelope broader than the exact Trusted Parent is rejected.
    /// </summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsSavedRuleResourceEnvelopeThatBroadensParent()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject();
        json["accessEnvelope"]!["maximumMappingCount"] = 4097;
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

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(
            "general.admission.saved-rule-broadens-parent",
            result.Error,
            StringComparison.Ordinal);
    }

    /// <summary>Rejects a schema-valid mapping ceiling that cannot be represented by runtime admission.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsMappingCeilingOutsideRuntimeIntegerRange()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject();
        json["accessEnvelope"]!["maximumMappingCount"] = 2147483648L;
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
        Assert.Contains(RangeOverflow, result.Error, StringComparison.Ordinal);
    }

    /// <summary>Requires a saved-rule compatibility envelope to match IC, derived profile id, and mode.</summary>
    [Theory]
    [InlineData("profileId", "nt51951-general-merge-logical-candidate", V2ParentNarrowingInvalid)]
    [InlineData("bundleContentHash", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", V2ParentNarrowingInvalid)]
    [InlineData("sourceExperienceId", "general-replace", V2ContractInvalid)]
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
        JsonElement admission =
            buildRoot.GetProperty("GeneralAdmission");
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
            1,
            effective.GetProperty("MaximumTotalWriteBytes").GetInt64());
        JsonElement resource = Assert.Single(
            admission.GetProperty("InputResources").EnumerateArray());
        Assert.Equal(
            "reviewed-copy-operation",
            resource.GetProperty("SlotId").GetString());
        Assert.Equal(
            1,
            resource.GetProperty("LengthBytes").GetInt64());
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
