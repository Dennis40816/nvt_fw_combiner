using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    /// <summary>V2 General Replace validation and mapping inspection use the same exact Parent as execution.</summary>
    [Fact]
    public async Task SavedRuleV2GeneralReplaceInspectionUsesExactParent()
    {
        using var workspace = TempWorkspace.Create();
        string rule = await WriteRuleAsync(
            workspace,
            ReplaceCliCommandTests.ValidGeneralReplaceV2RuleObject());

        CliRunResult validation = await RunCliAsync(
            ["saved-rule", "validate", rule]);
        CliRunResult mappings = await RunCliAsync(
            ["saved-rule", "mappings", rule]);

        Assert.Equal(0, validation.ExitCode);
        Assert.Equal(string.Empty, validation.Error);
        Assert.Contains(
            "Parent: nt51926-ctrlram-replace-candidate / nt51926-general-replace-dp-single-candidate / nt51926-ctrlram-replace / nt51926-general-replace-full-flash-256k",
            validation.Output,
            StringComparison.Ordinal);
        Assert.Equal(0, mappings.ExitCode);
        Assert.Equal(string.Empty, mappings.Error);
        Assert.Contains("Mappings: 1", mappings.Output, StringComparison.Ordinal);
        Assert.Contains(
            "--mapping 0x3E020+0x2=<source-bin>",
            mappings.Output,
            StringComparison.Ordinal);
    }

    /// <summary>General Replace inspection fails closed before projection when routing or Parent identity is invalid.</summary>
    [Theory]
    [InlineData("mismatched-parent", "not uniquely installed")]
    [InlineData("mismatched-workflow", "supported compositionKind/sourceExperienceId pair")]
    [InlineData("missing-parent", "requires one exact parentBinding.profileId")]
    public async Task SavedRuleV2GeneralReplaceInspectionRejectsInvalidAuthority(
        string mutation,
        string expectedMessage)
    {
        using var workspace = TempWorkspace.Create();
        JsonObject json = ReplaceCliCommandTests.ValidGeneralReplaceV2RuleObject();
        switch (mutation)
        {
            case "mismatched-parent":
                json["parentBinding"]!["profileId"] = "not-installed";
                break;
            case "mismatched-workflow":
                json["compositionKind"] = "merge";
                break;
            case "missing-parent":
                _ = json["parentBinding"]!.AsObject().Remove("profileId");
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown Saved Rule inspection mutation: {mutation}");
        }

        string rule = await WriteRuleAsync(workspace, json);

        CliRunResult result = await RunCliAsync(
            ["saved-rule", "validate", rule]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(expectedMessage, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Mappings:", result.Output, StringComparison.Ordinal);
    }
}
