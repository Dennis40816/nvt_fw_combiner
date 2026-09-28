using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>Direct execution enforces count admission without trusting authoring or a selector.</summary>
    [Theory]
    [InlineData("NT51929", "different", "AB_TP_TOPOLOGY_MISMATCH")]
    [InlineData("NT51929", "zero", "firmware-config.chip-count-required")]
    [InlineData("NT51929", "unreadable", "firmware-config.chip-count-unreadable")]
    [InlineData("NT51951", "different", "AB_TP_TOPOLOGY_MISMATCH")]
    [InlineData("NT51951", "zero", "firmware-config.chip-count-required")]
    [InlineData("NT51951", "unreadable", "firmware-config.chip-count-unreadable")]
    public async Task DirectAbExecutionRejectsInvalidCountsAsync(string icId, string defect, string issueCode)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-direct-count-admission");
        byte[] tp = CreateFormatTpImage(0x81, 0, chipCount: 2);
        if (icId == "NT51929") { Array.Resize(ref tp, 0x40000); }
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("dp.bin", new byte[icId == "NT51951" ? 0x100000 : 0x80000]),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write("a.bin", tp),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write("b.bin", tp),
        };
        CompositionHostServices host = await CreateFormatTestHostAsync(workspace);
        CompiledAuthoringSessionPreparation prepared = await AbMergeTestSupport.PrepareAsync(host, icId, paths,
            TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, CompositionExecutionTestSupport.FormatIssues(prepared.Issues));
        CompiledComposition composition = prepared.Snapshot!.ExactCapability!.CompiledComposition;
        var artifacts = paths.ToDictionary(static pair => pair.Value, static pair => File.ReadAllBytes(pair.Value));
        byte[] badTp = artifacts[paths[CompositionAddressSpaceIds.TpBInput]];
        if (defect == "unreadable") { badTp[TpBackupStart + 0xFFC] = 0xFF; }
        else { badTp[TpBackupStart + 0x17] = defect == "zero" ? (byte)0 : (byte)3; }
        InputArtifactBinding[] bindings = [.. paths.Select(pair =>
            AcceptedSessionExecutionInputs.CreateCompiledBinding(composition, pair.Key, pair.Value))];
        var request = new CompositionRunRequest("ab-count-direct", composition, bindings,
            composition.V2Details.OutputNamingRequirement.FileNameTemplate,
            resolvedCapability: prepared.Snapshot.ExactCapability);
        var effects = new ForbiddenCountExecutionEffects();
        var service = new CompositionRunService(new FakeArtifactReader(artifacts),
            new FakeClock(Enumerable.Repeat(DateTimeOffset.UnixEpoch, 20)), effects, effects);
        CompositionRunResult preview = await service.PreviewAsync(request, TestContext.Current.CancellationToken);
        CompositionRunResult build = await service.PreviewOrBuildAsync(request, true, TestContext.Current.CancellationToken);
        foreach (CompositionRunResult result in new[] { preview, build })
        {
            Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
            Assert.Contains(result.Report.Issues, issue => issue.Code == issueCode && issue.Severity == CompositionIssueSeverity.Error);
            Assert.Empty(result.OutputBytes.ToArray());
            Assert.Null(result.CommittedOutputId);
            Assert.Empty(result.Report.Mutations);
        }
        if (icId == "NT51929")
        {
            File.WriteAllBytes(paths[CompositionAddressSpaceIds.TpBInput], badTp);
            Assert.Contains(AbMergeTestSupport.Prepare(host, icId, paths).Issues, issue => issue.Code == issueCode);
        }
    }

    private sealed class ForbiddenCountExecutionEffects : ICompositionOutputWriter, IExternalProcessor
    {
        public ValueTask<CompositionOutputCommitReceipt> CommitAsync(string fileName, ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid TP count must never publish output.");
        }

        public ValueTask<ExternalProcessorResult> TransformAsync(ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid TP count must never invoke a processor.");
        }
    }

    /// <summary>Selector-free NT51951 rejects different positive counts without inventing a selector.</summary>
    [Fact]
    public async Task Nt51951RejectsDifferentTpCountsWithoutASelectorAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-observed-topology");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("inputs/dp-ab.bin", new byte[0x100000]),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write(
                "inputs/tp-a.bin",
                CreateFormatTpImage(0x81, 0x00, chipCount: 1)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write(
                "inputs/tp-b.bin",
                CreateFormatTpImage(0x82, 0x01, chipCount: 2)),
        };

        CompositionHostServices host = await CreateFormatTestHostAsync(workspace);
        CompiledAuthoringSessionPreparation result = await AbMergeTestSupport.PrepareAsync(host,
            "NT51951",
            paths,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Code == "AB_TP_TOPOLOGY_MISMATCH" && issue.Severity == CompositionIssueSeverity.Error);
    }

    /// <summary>Selector-free AB still requires a readable count in both TP inputs.</summary>
    [Fact]
    public async Task Nt51951BlocksUnreadableTpCountsAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-unreadable-topology");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("inputs/dp-ab.bin", new byte[0x100000]),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write("inputs/tp-a.bin", CreateFormatTpImage(0x81, 0, withBackup: false)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write("inputs/tp-b.bin", CreateFormatTpImage(0x82, 1, withBackup: false)),
        };

        CompositionHostServices host = await CreateFormatTestHostAsync(workspace);
        CompiledAuthoringSessionPreparation result = await AbMergeTestSupport.PrepareAsync(host,
            "NT51951",
            paths,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.All(result.Issues, issue =>
        {
            Assert.Equal("firmware-config.chip-count-unreadable", issue.Code);
            Assert.Contains("unreadable", issue.Message, StringComparison.Ordinal);
        });
        Assert.Equal(2, result.Issues.Count);
    }
}
