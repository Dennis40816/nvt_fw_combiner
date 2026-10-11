using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AcceptedSessionFileIdentityTests
{
    /// <summary>Linked authoring preserves the bytes and operations of independent equal-content TP bindings.</summary>
    [Fact]
    public async Task AbMergeSharedTpPathMatchesDistinctIdenticalTpFiles()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-ab-accepted-shared-tp-parity");
        Dictionary<string, string> sharedPaths = CreateAbInputs(workspace);
        sharedPaths[CompositionAddressSpaceIds.TpBInput] =
            sharedPaths[CompositionAddressSpaceIds.TpAInput];
        var distinctPaths = new Dictionary<string, string>(sharedPaths, StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write(
                "distinct-equal-tp-b.bin",
                File.ReadAllBytes(sharedPaths[CompositionAddressSpaceIds.TpAInput])),
        };

        ActiveSessionSnapshot sharedSession = await AcceptAbSessionAsync(sharedPaths);
        ActiveSessionSnapshot distinctSession = await AcceptAbSessionAsync(distinctPaths);
        CompositionRunResult shared = await ExecuteAsync(sharedSession, sharedPaths);
        CompositionRunResult distinct = await ExecuteAsync(distinctSession, distinctPaths);

        Assert.True(shared.Succeeded, $"Composition result: status={shared.Status}, succeeded={shared.Succeeded}.");
        Assert.True(distinct.Succeeded, $"Composition result: status={distinct.Status}, succeeded={distinct.Succeeded}.");
        Assert.Equal(distinct.OutputSha256, shared.OutputSha256);
        GoldenAssert.AssertEqualsGolden(distinct.OutputBytes.ToArray(), shared.OutputBytes.ToArray(), "firmware image");
        Assert.Equal(
            distinct.Report.Operations.Select(static operation => (
                operation.Kind,
                operation.SourceSpaceId,
                operation.TargetSpaceId,
                operation.SourceRange,
                operation.TargetRange)),
            shared.Report.Operations.Select(static operation => (
                operation.Kind,
                operation.SourceSpaceId,
                operation.TargetSpaceId,
                operation.SourceRange,
                operation.TargetRange)));
    }
}
