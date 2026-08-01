using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Saved Rule v2 DP mappings execute through the exact Parent and shared Replace engine.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceSavedRuleBuildUsesExactParent()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = CreatePattern(0x40000, 0x26);
        string reference = workspace.Write("reference.bin", baseBytes);
        string source = workspace.Write("dp-source.bin", [0xA5, 0x5A]);
        string rule = await WriteGeneralReplaceRuleAsync(
            workspace,
            ValidGeneralReplaceV2RuleObject());
        string output = workspace.PathFor("saved-rule-replace.bin");
        string report = workspace.PathFor("saved-rule-replace-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
            "--output",
            output,
            "--report",
            report,
        ]);

        Assert.True(
            result.ExitCode == 0,
            $"{result.Error}{Environment.NewLine}{result.Output}");
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(
            reference,
            TestContext.Current.CancellationToken));
        byte[] outputBytes = await File.ReadAllBytesAsync(
            output,
            TestContext.Current.CancellationToken);
        Assert.Equal([0xA5, 0x5A], outputBytes[0x3E020..0x3E022]);
        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                report,
                TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal(
            "nt51926-general-replace-dp-single-candidate",
            root.GetProperty("ProfileId").GetString());
        JsonElement operation = Assert.Single(
            root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("saved-rule", operation.GetProperty("Provenance")
            .GetProperty("Kind").GetString());
        Assert.Equal(JsonValueKind.Null, operation.GetProperty("ProcessorId").ValueKind);
        JsonElement savedRule = root.GetProperty("GeneralAdmission")
            .GetProperty("SavedRule");
        Assert.Equal("replace-dp-window", savedRule.GetProperty("RuleId").GetString());
        Assert.Equal(
            "nt51926-general-replace-dp-single-candidate",
            savedRule.GetProperty("Parent").GetProperty("ProfileId").GetString());
    }

    /// <summary>Missing or stale exact Parent identity fails closed before output creation.</summary>
    [Theory]
    [InlineData("bundleId")]
    [InlineData("bundleVersion")]
    [InlineData("bundleContentHash")]
    [InlineData("profileId")]
    [InlineData("profileVersion")]
    [InlineData("profileContentHash")]
    [InlineData("familyId")]
    [InlineData("familyVersion")]
    [InlineData("familyContentHash")]
    [InlineData("mapId")]
    public async Task Nt51926GeneralReplaceSavedRuleRejectsStaleParent(
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralReplaceV2RuleObject();
        json["parentBinding"]![propertyName] =
            propertyName.EndsWith("ContentHash", StringComparison.Ordinal)
                ? new string('0', 64)
                : propertyName.EndsWith("Version", StringComparison.Ordinal)
                    ? "9.9.9"
                    : "missing-parent-fact";
        string rule = await WriteGeneralReplaceRuleAsync(workspace, json);
        string reference = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x27));
        string source = workspace.Write("dp-source.bin", [0xA5, 0x5A]);
        string output = workspace.PathFor("must-not-exist.bin");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
            "--output",
            output,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            "saved-rule.v2.parent-narrowing-invalid",
            result.Error,
            StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>Rule JSON cannot omit or reorder Parent-owned processor stages.</summary>
    [Fact]
    public void GeneralReplaceSavedRuleRequiresExactOrderedParentStages()
    {
        SavedRuleV2GeneralReplaceAdmissionContext exact =
            WorkbenchCompositionService
                .GetNt51926GeneralReplaceSavedRuleAdmissionContext() with
            {
                ProcessorStageIds = ["stage-a", "stage-b"],
            };
        JsonObject omittedJson = ValidGeneralReplaceV2RuleObject();
        JsonObject reorderedJson = ValidGeneralReplaceV2RuleObject();
        reorderedJson["processorStageIds"] =
            new JsonArray("stage-b", "stage-a");

        SavedCompositionRuleV2AdmissionResult omitted =
            SavedCompositionRuleV2Admission.ValidateGeneralReplace(
                JsonSerializer.SerializeToElement(omittedJson),
                exact);
        SavedCompositionRuleV2AdmissionResult reordered =
            SavedCompositionRuleV2Admission.ValidateGeneralReplace(
                JsonSerializer.SerializeToElement(reorderedJson),
                exact);

        Assert.Contains(
            omitted.Issues,
            issue =>
                issue.Code == SavedRuleIssueCodes.V2ParentNarrowingInvalid &&
                issue.Path == "$.processorStageIds");
        Assert.Contains(
            reordered.Issues,
            issue =>
                issue.Code == SavedRuleIssueCodes.V2ParentNarrowingInvalid &&
                issue.Path == "$.processorStageIds");
    }

    /// <summary>Canonical target offsets cannot escape the Parent's DP region.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceSavedRuleRejectsRangeOutsideParentRegion()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ValidGeneralReplaceV2RuleObject();
        json["mappingFragments"]![0]!["targetOffset"] = 0x1FFF;
        string rule = await WriteGeneralReplaceRuleAsync(workspace, json);
        string reference = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x28));
        string source = workspace.Write("dp-source.bin", [0xA5, 0x5A]);
        string output = workspace.PathFor("must-not-exist.bin");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--rule",
            rule,
            "--slot",
            $"source-bin={source}",
            "--output",
            output,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(
            "mapping-row.target-region-unsupported",
            result.Error,
            StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>Locks NT51926 single full-Flash DP-only General Replace to the reviewed V2 candidate.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceDpOnlyBuildUsesV2Candidate()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = await File.ReadAllBytesAsync(
            GoldenArtifactPath("51926", "expected-output"),
            TestContext.Current.CancellationToken);
        string reference = workspace.Write("reference.bin", baseBytes);
        string source = workspace.Write("dp-source.bin", [0xA5, 0x5A]);
        string output = workspace.PathFor("general-replace.bin");
        string report = workspace.PathFor("general-replace-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x3E020+0x2={source}",
            "--output",
            output,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
        byte[] outputBytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0xA5, 0x5A], outputBytes[0x3E020..0x3E022]);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("nt51926-general-replace-dp-single-candidate", root.GetProperty("ProfileId").GetString());
        JsonElement operation = Assert.Single(root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString());
        Assert.Equal(JsonValueKind.Null, operation.GetProperty("ProcessorId").ValueKind);
    }

    /// <summary>Verifies retired real-IC General Replace mappings fail closed without output.</summary>
    [Fact]
    public async Task GeneralReplaceBuildWithRepeatedWorkbenchMappingsFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = CreatePattern(0x40000, 0x40);
        string reference = workspace.Write("reference.bin", baseBytes);
        string firstInput = workspace.Write("first.bin", [0xA5, 0x5A]);
        string secondInput = workspace.Write("second.bin", [0xC3]);
        string output = workspace.PathFor("general-replace.bin");
        string report = workspace.PathFor("general-replace-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x100+0x2={firstInput}",
            "--mapping",
            $"0x38000+0x1={secondInput}",
            "--output",
            output,
            "--report",
            report,
        ]);

        await AssertGeneralReplaceWorkflowNotSupportedAsync(result, report, output);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies retired TP-touching General Replace preview fails closed.</summary>
    [Fact]
    public async Task GeneralReplacePreviewWithWorkbenchTpMappingFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string reference = GoldenArtifactPath("51950", "expected-output", "dp-256k");
        byte[] baseBytes = await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken);
        string input = workspace.Write("input.bin", baseBytes[0x22C00..0x22C02]);
        string report = workspace.PathFor("general-replace-tp-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x22C00+0x2={input}",
            "--report",
            report,
        ]);

        await AssertGeneralReplaceWorkflowNotSupportedAsync(result, report, outputPath: null);
    }

    /// <summary>NT51926 TP mappings without an exact compilation fail before a diagnostic Preview.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceTpPreviewRequiresExactCompilation()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write(
            "reference.bin",
            CreatePostbuildReference(0x29));
        string input = workspace.Write("input.bin", [0xA5, 0x5A]);
        string report = workspace.PathFor("diagnostic-plan.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x22800+0x2={input}",
            "--report",
            report,
        ]);

        await AssertGeneralReplaceWorkflowNotSupportedAsync(
            result,
            report,
            outputPath: null);
    }

    /// <summary>Build also rejects a TP target before runtime readiness or output.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceTpBuildRequiresExactCompilation()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write(
            "reference.bin",
            CreatePostbuildReference(0x2A));
        string input = workspace.Write("input.bin", [0xA5, 0x5A]);
        string output = workspace.PathFor("must-not-exist.bin");
        string report = workspace.PathFor("blocked-build.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x22800+0x2={input}",
            "--output",
            output,
            "--report",
            report,
        ]);

        await AssertGeneralReplaceWorkflowNotSupportedAsync(result, report, output);
    }

    /// <summary>Verifies malformed real IC General Replace mapping paths are rejected before planning.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRejectsEmptyWorkbenchMappingPath()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", CreatePattern(0x40000, 0x30));

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            "0x100+0x2=  ",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--mapping path must not be empty", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies valid CLI patches fail closed after legacy General Replace retirement.</summary>
    [Fact]
    public async Task GeneralReplaceBuildWithVirtualPatchAndFillFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = CreatePattern(0x40000, 0x60);
        string reference = workspace.Write("reference.bin", baseBytes);
        string output = workspace.PathFor("general-replace-patch.bin");
        string report = workspace.PathFor("general-replace-patch-report.json");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--patch",
            "0x100+0x2=A55A",
            "--fill",
            "0x110+0x3=FF",
            "--output",
            output,
            "--report",
            report,
        ]);

        await AssertGeneralReplaceWorkflowNotSupportedAsync(result, report, output);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a real IC General Replace build cannot overwrite its immutable base BIN.</summary>
    [Fact]
    public async Task GeneralReplaceBuildRejectsOutputPathThatAliasesBase()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = CreatePattern(0x40000, 0x63);
        string reference = workspace.Write("reference.bin", baseBytes);

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--patch",
            "0x100+0x1=A5",
            "--output",
            reference,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Output path must not overwrite input artifact", result.Error, StringComparison.Ordinal);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies malformed CLI patch bytes receive the shared workbench validation issue.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRejectsMalformedVirtualPatch()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", CreatePattern(0x40000, 0x65));

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--patch",
            "0x100+0x2=ABC",
        ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ui.general-replace.patch-hex-invalid", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects retired fixed-profile range options at the workflow allowlist.</summary>
    [Theory]
    [InlineData("--input", "ignored.bin")]
    [InlineData("--source-start", "0")]
    [InlineData("--target-start", "0x100")]
    [InlineData("--length", "1")]
    public async Task GeneralReplaceRejectsRetiredFixedProfileOptions(string option, string value)
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", CreatePattern(0x40000, 0x66));

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--patch",
            "0x100+0x1=A5",
            option,
            value,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains($"unknown option '{option}'", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects General Replace-only mapping and patch options in other Replace command groups.</summary>
    [Theory]
    [InlineData("dp-replace", "--mapping")]
    [InlineData("dp-replace", "--patch")]
    [InlineData("ctrlram-replace", "--fill")]
    public async Task NonGeneralReplaceRejectsGeneralAuthoringOptions(string command, string option)
    {
        CliRunResult result = await RunCliAsync([
            command,
            "preview",
            option,
            "0x100+0x1=FF",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains($"unknown option '{option}'", result.Error, StringComparison.Ordinal);
    }

    private static async Task AssertGeneralReplaceWorkflowNotSupportedAsync(
        CliRunResult result,
        string reportPath,
        string? outputPath)
    {
        Assert.Equal(1, result.ExitCode);
        if (outputPath is not null)
        {
            Assert.False(File.Exists(outputPath));
        }

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            reportPath,
            TestContext.Current.CancellationToken));
        Assert.Contains(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == "replace.workflow.not-supported");
        Assert.False(document.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }

    private static byte[] CreatePostbuildReference(byte seed)
    {
        byte[] image = CreatePattern(0x40000, seed);
        const int backupStart = 0x3F000;
        const int markerStart = 0x3FFFC;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = 0x20;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = 0xDF;
        image[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = 2;
        image[markerStart] = 0x00;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }

    private static async Task<string> WriteGeneralReplaceRuleAsync(
        TempWorkspace workspace,
        JsonObject json)
    {
        string path = workspace.PathFor("replace-rule.json");
        await File.WriteAllTextAsync(
            path,
            json.ToJsonString(),
            TestContext.Current.CancellationToken);
        return path;
    }

    internal static JsonObject ValidGeneralReplaceV2RuleObject()
    {
        return JsonNode.Parse(
            /*lang=json,strict*/ """
            {
              "schemaVersion": "2.0",
              "ruleId": "replace-dp-window",
              "ruleVersion": "1.0.0",
              "displayName": "Replace DP window",
              "compositionKind": "replace",
              "sourceExperienceId": "general-replace",
              "parentBinding": {
                "bundleId": "nt51926-ctrlram-replace-candidate",
                "bundleVersion": "0.9.16-candidate.1",
                "bundleContentHash": "25d5adc9697eacedcf238835da197b0359c41f8cc6d82110c181496038469529",
                "profileId": "nt51926-general-replace-dp-single-candidate",
                "profileVersion": "0.1.0",
                "profileContentHash": "14fa7a02f86a2b7d8702fc2ff66e01c8857c7a909f7389c28f5f04d1e41c6ccc",
                "familyId": "nt51926-ctrlram-replace",
                "familyVersion": "0.7.0",
                "familyContentHash": "7d67ad155846a88545b28273e1233bd5dcb7ba7e766782d34a0c20fbb485956a",
                "mapId": "nt51926-general-replace-full-flash-256k"
              },
              "promotion": {
                "stage": "executable-candidate",
                "blockers": []
              },
              "slotTemplates": [
                {
                  "slotTemplateId": "source-bin",
                  "role": "source",
                  "cardinality": "one",
                  "acceptedExtensions": [".bin"]
                }
              ],
              "mappingFragments": [
                {
                  "fragmentId": "replace-dp-range",
                  "operationKind": "replace-range",
                  "sourceSlot": {
                    "kind": "rule-slot",
                    "slotTemplateId": "source-bin"
                  },
                  "sourceRange": { "start": 0, "length": 2 },
                  "targetRegionId": "general-replace-full-flash-dp-code",
                  "targetOffset": 32,
                  "overlapPolicy": "reject",
                  "reason": "Reviewed DP-only Saved Rule mapping."
                }
              ],
              "accessEnvelope": {
                "allowedRegionIds": ["general-replace-full-flash-dp-code"],
                "maximumMappingCount": 1,
                "maximumTotalWriteBytes": 2,
                "protectedRangePolicy": "parent-profile"
              },
              "validationRuleIds": [],
              "processorStageIds": [],
              "owner": "firmware-owner",
              "reviewers": ["architecture-reviewer"],
              "evidenceRefs": ["general-replace-v2-candidate-parity"]
            }
            """)!.AsObject();
    }
}
