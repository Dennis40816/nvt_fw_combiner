using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>The complete DP AB container remains exact and rejects a non-authoritative extra byte.</summary>
    [Fact]
    public async Task CompleteDpAbRejectsTrailingBytesAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-dp-oversize");
        Dictionary<string, string> paths = WriteInputs(workspace);
        byte[] oversized = [.. await File.ReadAllBytesAsync(
            paths[CompositionAddressSpaceIds.DpAbInput],
            TestContext.Current.CancellationToken), 0xD7];
        paths[CompositionAddressSpaceIds.DpAbInput] =
            workspace.Write("oversized/dp-ab-input.bin", oversized);

        CompiledAuthoringSessionPreparation prepared = AbMergeTestSupport.Prepare(
            BootstrapTestHost.Services,
            "NT51929",
            paths);

        Assert.False(prepared.Succeeded);
        Assert.Contains(
            prepared.Issues,
            static issue => issue.Code == CompositionIssueCodes.InputAddressSpaceLengthMismatch);
        Assert.Equal(oversized, await File.ReadAllBytesAsync(
            paths[CompositionAddressSpaceIds.DpAbInput],
            TestContext.Current.CancellationToken));
    }
}
