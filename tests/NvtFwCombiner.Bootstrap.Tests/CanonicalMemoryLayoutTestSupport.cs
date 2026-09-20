using NvtFwCombiner.TestSupport;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class CanonicalMemoryLayoutTestSupport
{
    internal static async Task<(MemoryLayoutSnapshot Layout, string ReplacementAddressSpace)> PrepareGeneralReplaceAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-general-memory-layout");
        string reference = workspace.Write("reference.bin", File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output")));
        string replacement = workspace.Write("replacement.bin", [0x31, 0x42]);
        GeneralAuthoringSessionPreparation prepared = await BootstrapTestHost.Services.GeneralAuthoring
            .PrepareReplaceSessionAsync(
                new AuthoringSessionState(ExperienceIds.GeneralReplace),
                "NT51926", "single", reference,
                GeneralTestDraftFactory.CreateReplaceDraft([
                    GeneralTestDraftFactory.ReplaceFile("mapping-1", replacement, "0x3E020", "0x2"),
                ]), TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot accepted = prepared.AcceptedSession!;
        return (MemoryLayoutProjector.Project(accepted.ExactCapability!, accepted, accepted.ExactCapability!.CompiledComposition),
            accepted.ExactCapability.CompiledComposition.Plan.OrderedOperations.Single(operation =>
                operation.SourceSpaceId is not null &&
                operation.SourceSpaceId != accepted.ExactCapability.CompiledComposition.Plan.OutputInitialization.ReferenceSpaceId).SourceSpaceId!);
    }
}
