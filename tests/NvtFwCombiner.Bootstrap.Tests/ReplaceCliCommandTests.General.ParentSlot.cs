using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Parent-slot mappings read from the immutable --base artifact without a second binding.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceParentSlotUsesExactBase()
    {
        using var workspace = TempWorkspace.Create();
        byte[] baseBytes = CreatePattern(0x40000, 0x36);
        string reference = workspace.Write("reference.bin", baseBytes);
        JsonObject json = ValidGeneralReplaceV2RuleObject();
        json["slotTemplates"] = new JsonArray();
        JsonObject sourceSlot = json["mappingFragments"]![0]!["sourceSlot"]!.AsObject();
        sourceSlot.Clear();
        sourceSlot["kind"] = "parent-slot";
        sourceSlot["slotId"] = "reference";
        string rule = await WriteGeneralReplaceRuleAsync(workspace, json);
        string output = workspace.PathFor("parent-slot.bin");
        (GeneralMappingDraftState draft, GeneralSavedRuleResourcePolicy policy) =
            LoadTrustedGeneralReplaceRule(rule, reference, sourcePath: null);
        WorkbenchRunResult result =
            await CompositionExecutionAdapter.BuildGeneralReplaceEphemeralDraftAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = reference,
                },
                draft,
                output,
                policy,
                TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] outputBytes = await File.ReadAllBytesAsync(
            output,
            TestContext.Current.CancellationToken);
        Assert.Equal(baseBytes[..2], outputBytes[0x3E020..0x3E022]);
    }

    /// <summary>The Parent reference slot cannot be rebound independently from --base.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceRejectsExplicitParentSlotRebinding()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x46));
        string conflicting = workspace.Write("conflicting.bin", [0xA5, 0x5A]);
        JsonObject json = ValidGeneralReplaceV2RuleObject();
        json["slotTemplates"] = new JsonArray();
        JsonObject sourceSlot = json["mappingFragments"]![0]!["sourceSlot"]!.AsObject();
        sourceSlot.Clear();
        sourceSlot["kind"] = "parent-slot";
        sourceSlot["slotId"] = "reference";
        string rule = await WriteGeneralReplaceRuleAsync(workspace, json);
        string output = workspace.PathFor("must-not-exist.bin");

        CliRunResult result = await RunCliAsync([
            "general-replace", "build",
            "--profile", "NT51926",
            "--ic-num", "single",
            "--base", reference,
            "--rule", rule,
            "--slot", $"reference={conflicting}",
            "--output", output,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("reference is reserved for --base", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>Rule slots cannot alias an exact Parent slot identifier.</summary>
    [Fact]
    public async Task Nt51926GeneralReplaceRejectsParentAndRuleSlotCollision()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x56));
        JsonObject json = ValidGeneralReplaceV2RuleObject();
        json["slotTemplates"]![0]!["slotTemplateId"] = "reference";
        json["mappingFragments"]![0]!["sourceSlot"]!["slotTemplateId"] =
            "reference";
        string rule = await WriteGeneralReplaceRuleAsync(workspace, json);
        string output = workspace.PathFor("must-not-exist.bin");

        CliRunResult result = await RunCliAsync([
            "general-replace", "build",
            "--profile", "NT51926",
            "--ic-num", "single",
            "--base", reference,
            "--rule", rule,
            "--output", output,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("collides with exact Parent slot", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }
}
