using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Single-TP admission is shared by authoring/runtime and never compares an AB peer.</summary>
public sealed class TpCountAdmissionTests
{
    /// <summary>Each malformed TP is blocked with its actual cause before any execution effects.</summary>
    [Theory]
    [InlineData("zero", "firmware-config.chip-count-required")]
    [InlineData("missing", "firmware-config.chip-count-unreadable")]
    [InlineData("duplicate", "firmware-config.chip-count-unreadable")]
    [InlineData("complement", "firmware-config.chip-count-unreadable")]
    [InlineData("tail-only", "firmware-config.chip-count-unreadable")]
    public async Task StandardTpCountBlocksAuthoringAndDirectExecution(string defect, string code)
    {
        CompositionHostServices host = BootstrapTestHost.Services;
        byte[] good = Tp();
        CompiledAuthoringSessionPreparation baseline = Prepare(host, good);
        Assert.True(baseline.Succeeded);
        CompiledComposition composition = baseline.Snapshot!.ExactCapability!.CompiledComposition;
        byte[] bad = [.. good];
        switch (defect)
        {
            case "zero": bad[0x1017] = 0; break;
            case "missing": bad[0x1FFC] = 0xFF; break;
            case "duplicate": "\0NVT"u8.CopyTo(bad.AsSpan(0x2FFC)); break;
            case "complement": bad[0x1001] = 0; break;
            case "tail-only":
                Array.Resize(ref bad, good.Length + 0x1000);
                good.AsSpan(0x1000, 0x1000).CopyTo(bad.AsSpan(good.Length));
                bad[0x1FFC] = 0xFF;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
        CompiledInputArtifactInspectionResult inspection = CompiledInputArtifactInspectionService.Inspect(composition, CompositionAddressSpaceIds.TpInput, bad);
        Assert.True(inspection.BlocksBuild);
        Assert.Equal(code, inspection.IssueCode);
        Assert.Equal(new ByteRange(0, good.Length), inspection.AcceptedSnapshotRange);
        Assert.Equal(FileStamp.FromBytes(bad.AsSpan(0, good.Length)).Sha256, inspection.AcceptedSnapshotSha256);
        CompiledAuthoringSessionPreparation failed = Prepare(host, bad);
        Assert.False(failed.Succeeded);
        Assert.Contains(failed.Snapshot!.InputSlotStatuses, status => status.BlocksBuild && status.InspectionIssueCode == code);
        Assert.Contains(failed.Issues, issue => issue.Code == code && issue.OperationId == CompositionAddressSpaceIds.TpInput &&
            issue.Message.Contains(defect == "zero" ? "read as 0" : "unreadable", StringComparison.Ordinal));
        var effects = new ForbiddenEffects();
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpInput] = new byte[0x6000],
            [CompositionAddressSpaceIds.TpInput] = bad,
        };
        InputArtifactBinding[] bindings = [.. inputs.Keys.Select(space => AcceptedSessionExecutionInputs.CreateCompiledBinding(composition, space, space + ".bin"))];
        var service = new CompositionRunService(new FakeArtifactReader(inputs.ToDictionary(static pair => pair.Key + ".bin", static pair => pair.Value)),
            new FakeClock(Enumerable.Repeat(DateTimeOffset.UnixEpoch, 20)), effects, effects);
        var request = new CompositionRunRequest("tp-count", composition, bindings, composition.V2Details.OutputNamingRequirement.FileNameTemplate,
            outputNamingInspection: AcceptedOutputNamingInspection.Accept(baseline.Snapshot),
            outputNamingAdmission: OutputNamingAdmissionIdentity.Capture(baseline.Snapshot.ExactCapability, baseline.Snapshot.AuthoringRevision.Value),
            resolvedCapability: baseline.Snapshot.ExactCapability);
        foreach (bool build in new[] { false, true })
        {
            CompositionRunResult result = await service.PreviewOrBuildAsync(request, build, TestContext.Current.CancellationToken);
            Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
            Assert.Contains(result.Report.Issues, issue => issue.Code == code && issue.OperationId == CompositionAddressSpaceIds.TpInput);
            Assert.DoesNotContain(result.Report.Issues, static issue => issue.Code == "AB_TP_TOPOLOGY_MISMATCH");
            Assert.Empty(result.OutputBytes.ToArray());
            Assert.Empty(result.Report.Mutations);
            Assert.Null(result.CommittedOutputId);
        }
    }

    /// <summary>Other input classes keep their own rules; every positive single-TP count is admitted.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PositiveStandardTpAndIgnoredTailDoNotAcquireAbPairRules(byte count)
    {
        byte[] tp = Tp(count);
        Array.Resize(ref tp, tp.Length + 0x1000);
        Tp().AsSpan(0x1000, 0x1000).CopyTo(tp.AsSpan(0x40000));
        CompiledAuthoringSessionPreparation prepared = Prepare(BootstrapTestHost.Services, tp);
        Assert.True(prepared.Succeeded, string.Join(',', prepared.Issues.Select(static issue => issue.Code)));
        CompiledInputArtifactInspectionResult dp = CompiledInputArtifactInspectionService.Inspect(
            prepared.Snapshot!.ExactCapability!.CompiledComposition, CompositionAddressSpaceIds.DpInput, new byte[0x6000]);
        Assert.False(dp.BlocksBuild);
        Assert.DoesNotContain(prepared.Issues, static issue => issue.Code == "AB_TP_TOPOLOGY_MISMATCH");
    }

    private static CompiledAuthoringSessionPreparation Prepare(CompositionHostServices host, byte[] tp)
    {
        return host.StandardMergeAuthoring.PrepareSession(new AuthoringSessionState(ExperienceIds.StandardMerge), "NT51929",
            [new(CompositionAddressSpaceIds.DpInput, "dp.bin", new byte[0x6000]), new(CompositionAddressSpaceIds.TpInput, "tp.bin", tp)]);
    }

    private static byte[] Tp(byte count = 2)
    {
        byte[] bytes = new byte[0x40000];
        bytes[0x1000] = 0x81;
        bytes[0x1001] = 0x7E;
        bytes[0x1017] = count;
        "\0NVT"u8.CopyTo(bytes.AsSpan(0x1FFC));
        return bytes;
    }

    private sealed class ForbiddenEffects : ICompositionOutputWriter, IExternalProcessor
    {
        public ValueTask<CompositionOutputCommitReceipt> CommitAsync(string fileName, ReadOnlyMemory<byte> outputBytes, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid TP must not publish output.");
        }
        public ValueTask<ExternalProcessorResult> TransformAsync(ExternalProcessorRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid TP must not invoke a processor.");
        }
    }
}
