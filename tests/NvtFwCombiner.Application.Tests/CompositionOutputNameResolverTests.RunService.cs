using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionOutputNameResolverTests
{
    /// <summary>The run service commits the name derived from the same accepted input bytes it executes.</summary>
    [Fact]
    public async Task CurrentInspectionCommitsCanonicalAutomaticName()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        var accepted = new AcceptedOutputNamingInspection(
            composition.CompilationFingerprint,
            fixture.Plan,
            fixture.Snapshot);
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["input-artifact"] = fixture.Bytes,
            }),
            new FakeClock([RunTime, RunTime.AddSeconds(1)]),
            writer);
        var request = new CompositionRunRequest(
            "normal-output-current",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            outputNamingInspection: accepted);

        CompositionRunResult result = await service.PreviewOrBuildAsync(
            request,
            build: true,
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.True(writer.WasCalled);
        Assert.Equal(
            "NT51929_FlashCode_D8205T8004_20260728.bin",
            writer.FileName);
        Assert.Equal(writer.FileName, result.Report.Output.FileName);
        Assert.NotNull(result.Report.OutputNaming);
    }

    /// <summary>A stale inspection blocks execution and output publication instead of naming different bytes.</summary>
    [Fact]
    public async Task StaleInspectionCannotExecuteOrCommit()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        var accepted = new AcceptedOutputNamingInspection(
            composition.CompilationFingerprint,
            fixture.Plan,
            fixture.Snapshot);
        byte[] changedBytes = [.. fixture.Bytes];
        changedBytes[0] ^= 0x01;
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["input-artifact"] = changedBytes,
            }),
            new FakeClock([RunTime, RunTime.AddSeconds(1)]),
            writer);
        var request = new CompositionRunRequest(
            "normal-output-stale",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            outputNamingInspection: accepted);

        CompositionRunResult result = await service.PreviewOrBuildAsync(
            request,
            build: true,
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.False(writer.WasCalled);
        Assert.Null(result.Report.OutputNaming);
        CompositionIssue issue = Assert.Single(
            result.Report.Issues,
            static candidate => candidate.Code == "output-naming.inspection-stale");
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        Assert.All(
            result.Report.Operations,
            static operation => Assert.Equal(OperationRunStatus.Skipped, operation.Status));
    }

    /// <summary>A naming inspection from a different compiled capability cannot enter a run request.</summary>
    [Fact]
    public void DifferentCapabilityFingerprintIsRejectedAtRequestBoundary()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);

        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "normal-output-wrong-capability",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            outputNamingInspection: fixture.AcceptedInspection));
    }

    /// <summary>A compiled normal renderer cannot execute without the accepted inspection boundary.</summary>
    [Fact]
    public void NormalRendererRequiresAcceptedInspection()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);

        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "normal-output-missing-inspection",
            composition,
            [CreateInputBinding()],
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template));
    }
}
