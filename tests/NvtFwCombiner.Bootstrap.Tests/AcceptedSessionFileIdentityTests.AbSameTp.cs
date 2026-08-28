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

        ActiveSessionSnapshot sharedSession = AcceptAbSession(sharedPaths);
        ActiveSessionSnapshot distinctSession = AcceptAbSession(distinctPaths);
        CompositionRunResult shared = await ExecuteAsync(sharedSession, sharedPaths);
        CompositionRunResult distinct = await ExecuteAsync(distinctSession, distinctPaths);

        Assert.True(shared.Succeeded, CompositionRunReportJson.Serialize(shared));
        Assert.True(distinct.Succeeded, CompositionRunReportJson.Serialize(distinct));
        Assert.Equal(distinct.OutputSha256, shared.OutputSha256);
        Assert.Equal(distinct.OutputBytes.ToArray(), shared.OutputBytes.ToArray());
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
