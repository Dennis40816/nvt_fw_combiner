using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class GeneralMergeCliCommandTests
{
    /// <summary>The Saved Rule runner cannot claim a Parent other than the resolved V2 profile.</summary>
    [Fact]
    public async Task GeneralMergeRunnerRejectsForgedSavedRuleParent()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0xA5]);
        string output = workspace.PathFor("must-not-exist.bin");
        var draft = new GeneralMergeDraftState(
            new GeneralMergeOutputInitializer(1, 0),
            new GeneralMappingDraftState(
            [
                new GeneralMappingDraftRow(
                    "map",
                    ExplicitMappingOperationKind.CopyRange,
                    GeneralMappingSource.File(source),
                    new ByteRange(0, 1),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 1),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    "Forged Parent regression."),
            ]));
        GeneralResourceLimits limits = new(
            1,
            1,
            1,
            1,
            [new GeneralSlotLengthLimits("map", 1, 1)]);
        var identity = new SavedRuleExecutionIdentity(
            "forged",
            "1.0.0",
            new string('a', 64),
            new SavedRuleParentIdentity(
                "bundle",
                "1.0.0",
                new string('b', 64),
                "wrong-profile",
                "1.0.0",
                new string('c', 64),
                "family",
                "1.0.0",
                new string('d', 64),
                "map"));

        GeneralAuthoringSessionPreparation prepared =
            await GeneralWorkflowTestSupport.PrepareGeneralMergeAsync(BootstrapTestHost.Canonical,
                "NT51950",
                draft,
                new GeneralSavedRuleResourcePolicy(
                    new SavedRuleLifecycleSnapshot(
                        identity,
                        SavedRuleStorageKind.TrustedCatalog,
                        SavedRuleLifecycleState.Published,
                        hasApproval: true,
                        hasEvidence: true,
                        isTrusted: true),
                    limits),
                TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        Assert.False(File.Exists(output));
        Assert.Contains(
            prepared.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.SavedRuleParentMismatch);
    }
}
