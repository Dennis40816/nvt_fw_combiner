using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>v1.2.1 Saved Rule parent identities remain executable after NT51925 was isolated (owner decision 334).</summary>
public sealed class SavedRuleV121ParentBindingCompatibilityTests
{
    /// <summary>Literal v1.2.1 General Merge parent bindings remain trusted and executable.</summary>
    [Theory]
    [InlineData("NT51923", Nt51923GeneralMergeParent)]
    [InlineData("NT51926", Nt51926GeneralMergeParent)]
    public async Task V121GeneralMergeRuleIsAdmittedAndAcceptedAsTrustedPublishedRule(
        string icId,
        string parentBindingJson)
    {
        using var workspace = TempWorkspace.Create();
        JsonObject rule = SavedRuleCliCommandTests.ValidGeneralMergeV2RuleObject();
        rule["parentBinding"] = JsonNode.Parse(parentBindingJson);
        string rulePath = workspace.PathFor("v121-general-merge-rule.json");
        await File.WriteAllTextAsync(
            rulePath,
            rule.ToJsonString(),
            TestContext.Current.CancellationToken);
        string source = workspace.Write("source.bin", [0x10]);
        GeneralMergeV2CandidateRegistration registration =
            BuiltInV2RegistrationRegistry.GeneralMergeByIc[icId];

        SavedRuleV2DraftLoadResult<GeneralMergeDraftState> loaded =
            SavedRuleV2GeneralMergeDraftLoader.Load(
                rulePath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source-bin"] = source,
                },
                registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                    registration.ProfileId));

        Assert.True(loaded.IsValid, DescribeIssues(loaded.Issues));
        GeneralSavedRuleResourcePolicy policy = TrustedPublishedPolicy(loaded);
        CompositionRunResult result = await GeneralWorkflowTestSupport.RunGeneralMergeAsync(
            BootstrapTestHost.Canonical,
            icId,
            loaded.Draft!,
            policy,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal(
            registration.ProfileId,
            result.Report.GeneralAdmission?.SavedRule?.Parent.ProfileId);
    }

    /// <summary>The literal v1.2.1 NT51926 General Replace parent binding remains trusted and executable.</summary>
    [Fact]
    public async Task V121Nt51926GeneralReplaceRuleIsAdmittedAndAcceptedAsTrustedPublishedRule()
    {
        using var workspace = TempWorkspace.Create();
        JsonObject rule = ReplaceCliCommandTests.ValidGeneralReplaceV2RuleObject();
        rule["parentBinding"] = JsonNode.Parse(Nt51926GeneralReplaceParent);
        string rulePath = workspace.PathFor("v121-general-replace-rule.json");
        await File.WriteAllTextAsync(
            rulePath,
            rule.ToJsonString(),
            TestContext.Current.CancellationToken);
        string reference = workspace.Write("reference.bin", new byte[0x40000]);
        string source = workspace.Write("source.bin", [0xA5, 0x5A]);
        string output = workspace.PathFor("out.bin");
        GeneralReplaceV2Registration registration =
            BuiltInV2RegistrationRegistry.GeneralReplaceByIc["NT51926"];

        SavedRuleV2DraftLoadResult<GeneralMappingDraftState> loaded =
            SavedRuleV2GeneralMergeDraftLoader.LoadGeneralReplace(
                rulePath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [registration.ReferenceSlotId] = reference,
                    ["source-bin"] = source,
                },
                registration.SavedRuleAdmissionContext);

        Assert.True(loaded.IsValid, DescribeIssues(loaded.Issues));
        GeneralSavedRuleResourcePolicy policy = TrustedPublishedPolicy(loaded);
        CompositionRunResult result = await GeneralWorkflowTestSupport.BuildGeneralReplaceAsync(
            BootstrapTestHost.Canonical,
            "NT51926",
            "single",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionSlotIds.ReplaceBase] = reference,
            },
            loaded.Draft!,
            output,
            policy,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal(
            "nt51926-general-replace-dp-single-candidate",
            result.Report.GeneralAdmission?.SavedRule?.Parent.ProfileId);
        byte[] outputBytes = await File.ReadAllBytesAsync(
            output,
            TestContext.Current.CancellationToken);
        Assert.Equal([0xA5, 0x5A], outputBytes[0x3E020..0x3E022]);
    }

    /// <summary>
    /// 0cb57b907:profiles/built-in/package-trust-index.json has only CtrlRAM Replace
    /// registrations for NT51923; GeneralReplaceByIc has no NT51923 parent.
    /// </summary>
    [Fact]
    public async Task V121Nt51923GeneralReplaceHasNoRegisteredParentOrCatalogRoute()
    {
        Assert.False(BuiltInV2RegistrationRegistry.GeneralReplaceByIc.ContainsKey("NT51923"));

        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", new byte[0x40000]);
        string source = workspace.Write("source.bin", [0xA5, 0x5A]);
        string report = workspace.PathFor("unavailable-report.json");
        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "general-replace",
                "preview",
                "--profile",
                "NT51923",
                "--ic-num",
                "single",
                "--base",
                reference,
                "--mapping",
                $"0x3E020+0x2={source}",
                "--report",
                report,
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("replace.workflow.not-supported", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(report));
    }

    private static GeneralSavedRuleResourcePolicy TrustedPublishedPolicy<TDraft>(
        SavedRuleV2DraftLoadResult<TDraft> loaded)
        where TDraft : class
    {
        var lifecycle = new SavedRuleLifecycleSnapshot(
            loaded.ExecutionIdentity!,
            SavedRuleStorageKind.TrustedCatalog,
            SavedRuleLifecycleState.Published,
            hasApproval: true,
            hasEvidence: true,
            isTrusted: true);
        return new GeneralSavedRuleResourcePolicy(
            lifecycle,
            loaded.ResourcePolicy!.Limits);
    }

    private static string DescribeIssues(IReadOnlyList<SavedRuleValidationIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => issue.Message));
    }

    // From 0cb57b907:profiles/built-in/nt51923-nt51926-general-merge-logical-candidate/
    // profile-bundle.json and profiles/nt51923-general-merge-logical-candidate.json.
    private const string Nt51923GeneralMergeParent = /*lang=json,strict*/ """
        {
          "bundleId": "nt51923-nt51926-general-merge-logical-candidate",
          "bundleVersion": "1.1.13-tp-svn.1",
          "bundleContentHash": "d9769ec15efe2f5ed47a8414bae9cdf167769187e4ee108fe0c4653484ea6f64",
          "profileId": "nt51923-general-merge-logical-candidate",
          "profileVersion": "0.1.0",
          "profileContentHash": "2a264b120056b0941ea973d11a7b42a5096b22fc6be18cc2b3c55a0d1bace397",
          "familyId": "nt51923-nt51926",
          "familyVersion": "1.2.1",
          "familyContentHash": "50e9918cea0d414af4aeb15e06bc441f7a060342a5a9493521a2f24b0676a738",
          "mapId": "logical-output"
        }
        """;

    // From the same 0cb57b907 bundle and profiles/nt51926-general-merge-logical-candidate.json.
    private const string Nt51926GeneralMergeParent = /*lang=json,strict*/ """
        {
          "bundleId": "nt51923-nt51926-general-merge-logical-candidate",
          "bundleVersion": "1.1.13-tp-svn.1",
          "bundleContentHash": "d9769ec15efe2f5ed47a8414bae9cdf167769187e4ee108fe0c4653484ea6f64",
          "profileId": "nt51926-general-merge-logical-candidate",
          "profileVersion": "0.1.0",
          "profileContentHash": "8b1c8bbcfeb4390d0431913d7d858cebb14620eb4688a20b7bf92e18ce14aa40",
          "familyId": "nt51923-nt51926",
          "familyVersion": "1.2.1",
          "familyContentHash": "50e9918cea0d414af4aeb15e06bc441f7a060342a5a9493521a2f24b0676a738",
          "mapId": "logical-output"
        }
        """;

    // From 0cb57b907:profiles/built-in/nt51926-ctrlram-replace-candidate/
    // profile-bundle.json and profiles/nt51926-general-replace-dp-single-candidate.json.
    private const string Nt51926GeneralReplaceParent = /*lang=json,strict*/ """
        {
          "bundleId": "nt51926-ctrlram-replace-candidate",
          "bundleVersion": "1.1.13-tp-svn.1",
          "bundleContentHash": "241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa",
          "profileId": "nt51926-general-replace-dp-single-candidate",
          "profileVersion": "0.1.0",
          "profileContentHash": "61a14ef72e7e9ea6877ae96610fce1eba1f3f8a16f6657549ce50fc44900a500",
          "familyId": "nt51926-ctrlram-replace",
          "familyVersion": "0.7.1",
          "familyContentHash": "ee315aa3f713ea0a7ea748bae6f3c8ee9aa60cda7cdf38adeef05cf9943ced0f",
          "mapId": "nt51926-general-replace-full-flash-256k"
        }
        """;
}
