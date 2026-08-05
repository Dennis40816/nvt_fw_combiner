using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    /// <summary>Headless v2 validation reports canonical identity without granting import trust.</summary>
    [Fact]
    public async Task SavedRuleValidateV2ReportsCanonicalUntrustedDraftIdentity()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject();
        string expectedHash = SavedCompositionRuleV2ContentHasher.Calculate(
            JsonSerializer.SerializeToElement(json));
        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(["saved-rule", "validate", rule]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Rule: copy-display-window 1.0.0", result.Output, StringComparison.Ordinal);
        Assert.Contains($"Content SHA256: {expectedHash}", result.Output, StringComparison.Ordinal);
        Assert.Contains("Lifecycle: Draft", result.Output, StringComparison.Ordinal);
        Assert.Contains("Trusted: False", result.Output, StringComparison.Ordinal);
        Assert.Contains(
            "Parent: nt51950-nt51951-general-merge-logical-candidate / nt51950-general-merge-logical-candidate",
            result.Output,
            StringComparison.Ordinal);
    }

    /// <summary>Headless v2 mapping projection reuses the canonical General mapping draft.</summary>
    [Fact]
    public async Task SavedRuleMappingsV2PrintsCanonicalGeneralMergeFragment()
    {
        using var workspace = TempWorkspace.Create();
        string rule = await WriteRuleAsync(workspace, ValidGeneralMergeV2RuleObject());

        CliRunResult result = await RunCliAsync(["saved-rule", "mappings", rule]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "--mapping 0x0+0x1+0x1=<source-bin>",
            result.Output,
            StringComparison.Ordinal);
    }

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
    [InlineData("preview", "missing-initializer", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "missing-initializer", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "invalid-fill", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "invalid-fill", "saved-rule.v2.contract-invalid")]
    [InlineData("preview", "unknown-initializer-property", "saved-rule.v2.contract-invalid")]
    [InlineData("build", "unknown-initializer-property", "saved-rule.v2.contract-invalid")]
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
            case "missing-initializer":
                _ = json.Remove("imageInitialization");
                break;
            case "invalid-fill":
                json["imageInitialization"]!["fillByte"] = 256;
                break;
            case "unknown-initializer-property":
                json["imageInitialization"]!["unexpected"] = true;
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
    public async Task GeneralMergePreviewRejectsImportedDraftRuleExecution()
    {
        using var workspace = TempWorkspace.Create();
        string rule = await WriteRuleAsync(
            workspace,
            ValidGeneralMergeV2RuleObject());
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
            "saved-rule.lifecycle.execution-not-trusted-published",
            result.Error,
            StringComparison.Ordinal);
    }

    /// <summary>Normal v2 rule consumption closes over output bytes, report values, and Preview/Build identity.</summary>
    [Fact]
    public async Task GeneralMergeBuildConsumesV2InitializerAndPreservesPreviewIdentity()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject(capacity: 4, fillByte: 0xA5);
        string expectedRuleHash = SavedCompositionRuleV2ContentHasher.Calculate(
            JsonSerializer.SerializeToElement(json));
        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [0x10]);
        string output = workspace.PathFor("out.bin");
        (GeneralMergeDraftState draft, GeneralSavedRuleResourcePolicy policy) =
            LoadTrustedGeneralMergeRule(rule, source);

        WorkbenchRunResult preview =
            await CompositionExecutionAdapter.RunGeneralMergeEphemeralDraftAsync(
                "NT51950",
                draft,
                policy,
                build: false,
                TestContext.Current.CancellationToken);
        WorkbenchRunResult build =
            await CompositionExecutionAdapter.RunGeneralMergeEphemeralDraftAsync(
                "NT51950",
                draft,
                policy,
                build: true,
                TestContext.Current.CancellationToken,
                output);

        Assert.True(preview.Succeeded, preview.ReportJson);
        Assert.True(build.Succeeded, build.ReportJson);
        byte[] outputBytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0xA5, 0x10, 0xA5, 0xA5], outputBytes);

        using var previewDocument = JsonDocument.Parse(preview.ReportJson);
        using var buildDocument = JsonDocument.Parse(build.ReportJson);
        JsonElement previewRoot = previewDocument.RootElement;
        JsonElement buildRoot = buildDocument.RootElement;
        Assert.Equal(
            previewRoot.GetProperty("CompilationFingerprint").GetString(),
            buildRoot.GetProperty("CompilationFingerprint").GetString());
        Assert.NotNull(preview.PreviewToken);
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
        JsonElement savedRule = admission.GetProperty("SavedRule");
        Assert.Equal("copy-display-window", savedRule.GetProperty("RuleId").GetString());
        Assert.Equal("1.0.0", savedRule.GetProperty("RuleVersion").GetString());
        Assert.Equal(expectedRuleHash, savedRule.GetProperty("ContentHash").GetString());
        JsonElement parent = savedRule.GetProperty("Parent");
        Assert.Equal(
            "nt51950-nt51951-general-merge-logical-candidate",
            parent.GetProperty("BundleId").GetString());
        Assert.Equal(
            "nt51950-general-merge-logical-candidate",
            parent.GetProperty("ProfileId").GetString());
        Assert.Equal(
            "nt51950-nt51951-dp-perspective",
            parent.GetProperty("FamilyId").GetString());
        Assert.Equal("logical-output", parent.GetProperty("MapId").GetString());
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

    /// <summary>Omitted v2 fill reaches compilation as the canonical zero default.</summary>
    [Fact]
    public async Task GeneralMergeSavedRuleDefaultsOmittedFillToZero()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralMergeV2RuleObject(capacity: 3);
        _ = json["imageInitialization"]!.AsObject().Remove("fillByte");
        string rule = await WriteRuleAsync(workspace, json);
        string source = workspace.Write("source.bin", [0x10]);
        string output = workspace.PathFor("out.bin");
        (GeneralMergeDraftState draft, GeneralSavedRuleResourcePolicy policy) =
            LoadTrustedGeneralMergeRule(rule, source);

        WorkbenchRunResult result =
            await CompositionExecutionAdapter.RunGeneralMergeEphemeralDraftAsync(
                "NT51950",
                draft,
                policy,
                build: true,
                TestContext.Current.CancellationToken,
                output);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(
            [0x00, 0x10, 0x00],
            await File.ReadAllBytesAsync(
                output,
                TestContext.Current.CancellationToken));
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

    /// <summary>Preview identity binds canonical Saved Rule content beyond id/version and path.</summary>
    [Fact]
    public async Task GeneralMergePreviewTokenChangesWhenRuleSemanticHashChanges()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject firstJson = ValidGeneralMergeV2RuleObject();
        JsonObject secondJson = ValidGeneralMergeV2RuleObject();
        firstJson["description"] = "First reviewed meaning.";
        secondJson["description"] = "Changed reviewed meaning.";
        string firstRule = await WriteRuleAsync(workspace, firstJson, "first-rule.json");
        string secondRule = await WriteRuleAsync(workspace, secondJson, "second-rule.json");
        string source = workspace.Write("source.bin", [0x10]);

        (GeneralMergeDraftState firstDraft, GeneralSavedRuleResourcePolicy firstPolicy) =
            LoadTrustedGeneralMergeRule(firstRule, source);
        (GeneralMergeDraftState secondDraft, GeneralSavedRuleResourcePolicy secondPolicy) =
            LoadTrustedGeneralMergeRule(secondRule, source);

        WorkbenchRunResult first =
            await CompositionExecutionAdapter.RunGeneralMergeEphemeralDraftAsync(
                "NT51950",
                firstDraft,
                firstPolicy,
                build: false,
                TestContext.Current.CancellationToken);
        WorkbenchRunResult second =
            await CompositionExecutionAdapter.RunGeneralMergeEphemeralDraftAsync(
                "NT51950",
                secondDraft,
                secondPolicy,
                build: false,
                TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded, first.ReportJson);
        Assert.True(second.Succeeded, second.ReportJson);
        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
    }

    private static (
        GeneralMergeDraftState Draft,
        GeneralSavedRuleResourcePolicy Policy) LoadTrustedGeneralMergeRule(
            string rulePath,
            string sourcePath)
    {
        GeneralMergeV2CandidateRegistration registration =
            BuiltInV2RegistrationRegistry.GeneralMergeByIc["NT51950"];
        SavedRuleV2DraftLoadResult<GeneralMergeDraftState> load =
            SavedRuleV2GeneralMergeDraftLoader.Load(
                rulePath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source-bin"] = sourcePath,
                },
                registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                    registration.ProfileId));
        Assert.True(
            load.IsValid,
            string.Join(
                Environment.NewLine,
                load.Issues.Select(static issue => issue.Message)));
        var lifecycle = new SavedRuleLifecycleSnapshot(
            load.ExecutionIdentity!,
            SavedRuleStorageKind.TrustedCatalog,
            SavedRuleLifecycleState.Published,
            hasApproval: true,
            hasEvidence: true,
            isTrusted: true);
        return (
            load.Draft!,
            new GeneralSavedRuleResourcePolicy(
                lifecycle,
                load.ResourcePolicy!.Limits));
    }
}
