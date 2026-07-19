using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    /// <summary>Preview publishes only its applicable phases in exact lifecycle order.</summary>
    [Fact]
    public async Task PreviewPublishesApplicableLifecyclePhases()
    {
        CompositionRunService service = CreateService(out _);
        var progress = new CompositionRunProgressFeed();

        Assert.False(progress.IsAttached);

        CompositionRunResult result = await service.PreviewAsync(
            CreateRequest(),
            progress,
            TestContext.Current.CancellationToken);
        List<CompositionRunProgressSnapshot> snapshots = await ReadProgressAsync(progress);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.True(progress.IsAttached);
        AssertLinearProgress(
            snapshots,
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.ExecutingComposition,
                CompositionRunPhase.ValidatingOutput,
                CompositionRunPhase.PreparingReport,
            ]);
        Assert.All(snapshots, static snapshot => Assert.Null(snapshot.CommittedOutputId));
    }

    /// <summary>Automatic Build reports commit only after validation and before report projection.</summary>
    [Fact]
    public async Task AutomaticBuildPublishesCommitAfterValidation()
    {
        CompositionRunService service = CreateService(out FakeOutputWriter writer);
        var progress = new CompositionRunProgressFeed();

        CompositionRunResult result = await service.PreviewOrBuildAsync(
            CreateRequest(),
            build: true,
            progress,
            TestContext.Current.CancellationToken);
        List<CompositionRunProgressSnapshot> snapshots = await ReadProgressAsync(progress);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.True(writer.WasCalled);
        AssertLinearProgress(
            snapshots,
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.ExecutingComposition,
                CompositionRunPhase.ValidatingOutput,
                CompositionRunPhase.CommittingOutput,
                CompositionRunPhase.PreparingReport,
            ]);
        Assert.Equal("committed:synthetic-standard-merge.bin", snapshots[^1].CommittedOutputId);
        Assert.All(snapshots.Take(snapshots.Count - 1), static snapshot => Assert.Null(snapshot.CommittedOutputId));
    }

    /// <summary>Repeated processor operations produce one bounded processor phase transition.</summary>
    [Fact]
    public async Task MultipleExternalOperationsPublishOneProcessorPhase()
    {
        var processor = new FakeExternalProcessor(request =>
            ExternalProcessorResult.Success(request.InputBytes, []));
        var service = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            null,
            processor);
        var progress = new CompositionRunProgressFeed();

        CompositionRunResult result = await service.PreviewAsync(
            CreateMultipleExternalProcessorRequest(),
            progress,
            TestContext.Current.CancellationToken);
        List<CompositionRunProgressSnapshot> snapshots = await ReadProgressAsync(progress);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(2, processor.CallCount);
        AssertLinearProgress(
            snapshots,
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.ExecutingComposition,
                CompositionRunPhase.RunningExternalProcessor,
                CompositionRunPhase.ValidatingOutput,
                CompositionRunPhase.PreparingReport,
            ]);
    }

    /// <summary>Input failure skips phases that never execute and does not mark them completed.</summary>
    [Fact]
    public async Task InputFailureReportsOnlyEnteredPhases()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp]));
        var progress = new CompositionRunProgressFeed();

        CompositionRunResult result = await service.PreviewAsync(
            CreateRequest(),
            progress,
            TestContext.Current.CancellationToken);
        List<CompositionRunProgressSnapshot> snapshots = await ReadProgressAsync(progress);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Equal(
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.PreparingReport,
            ],
            snapshots.Select(static snapshot => snapshot.CurrentPhase));
        CompositionRunProgressSnapshot report = snapshots[^1];
        Assert.Equal(
            [CompositionRunPhase.Preparing, CompositionRunPhase.ReadingInputs],
            report.CompletedPhases);
        Assert.DoesNotContain(CompositionRunPhase.ExecutingComposition, report.CompletedPhases);
        Assert.Equal(5, report.CurrentStep);
        Assert.Equal(5, report.StepCount);
        Assert.Null(report.CommittedOutputId);
    }

    /// <summary>Cancellation retains the last entered phase and never fabricates validation or report work.</summary>
    [Fact]
    public async Task CancellationStopsProgressAtInputReading()
    {
        var service = new CompositionRunService(
            new CancellingArtifactReader(),
            new FakeClock([FirstTimestamp]));
        var progress = new CompositionRunProgressFeed();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.PreviewAsync(CreateRequest(), progress, cancellationSource.Token));
        List<CompositionRunProgressSnapshot> snapshots = await ReadProgressAsync(progress);

        Assert.Equal(
            [CompositionRunPhase.Preparing, CompositionRunPhase.ReadingInputs],
            snapshots.Select(static snapshot => snapshot.CurrentPhase));
        Assert.DoesNotContain(
            snapshots,
            static snapshot => snapshot.CurrentPhase is CompositionRunPhase.ValidatingOutput or
                CompositionRunPhase.PreparingReport);
    }

    /// <summary>Rejected final validation never enters or completes the Build commit phase.</summary>
    [Fact]
    public async Task FinalValidationFailureLeavesCommitIncomplete()
    {
        var processor = new FakeExternalProcessor(request =>
            ExternalProcessorResult.Success(request.InputBytes, []));
        var writer = new FakeOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            writer,
            processor);
        var progress = new CompositionRunProgressFeed();

        CompositionRunResult result = await service.PreviewOrBuildAsync(
            CreateFirmwareConfigBackupValidationRequest(),
            build: true,
            progress,
            TestContext.Current.CancellationToken);
        List<CompositionRunProgressSnapshot> snapshots = await ReadProgressAsync(progress);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.False(writer.WasCalled);
        Assert.Equal(
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.ExecutingComposition,
                CompositionRunPhase.RunningExternalProcessor,
                CompositionRunPhase.ValidatingOutput,
                CompositionRunPhase.PreparingReport,
            ],
            snapshots.Select(static snapshot => snapshot.CurrentPhase));
        CompositionRunProgressSnapshot report = snapshots[^1];
        Assert.Contains(CompositionRunPhase.CommittingOutput, report.ApplicablePhases);
        Assert.DoesNotContain(CompositionRunPhase.CommittingOutput, report.CompletedPhases);
        Assert.Equal(7, report.CurrentStep);
        Assert.Equal(7, report.StepCount);
    }

    /// <summary>Commit adapter failure retains commit as the last truthful phase and emits no report phase.</summary>
    [Fact]
    public async Task CommitFailureStopsBeforePreparingReport()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["dp-artifact"] = [1, 2, 3, 4],
                ["tp-artifact"] = [9, 8, 7, 6],
            }),
            new FakeClock([FirstTimestamp]),
            new ThrowingOutputWriter());
        var progress = new CompositionRunProgressFeed();

        _ = await Assert.ThrowsAsync<IOException>(async () =>
            await service.PreviewOrBuildAsync(
                CreateRequest(),
                build: true,
                progress,
                TestContext.Current.CancellationToken));
        List<CompositionRunProgressSnapshot> snapshots = await ReadProgressAsync(progress);

        Assert.Equal(CompositionRunPhase.CommittingOutput, snapshots[^1].CurrentPhase);
        Assert.DoesNotContain(
            snapshots,
            static snapshot => snapshot.CurrentPhase == CompositionRunPhase.PreparingReport);
    }

    private static CompositionRunRequest CreateMultipleExternalProcessorRequest()
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable)],
            [
                CreateExternalOperation("processor-a", 10, OverlapPolicy.Reject),
                CreateExternalOperation("processor-b", 20, OverlapPolicy.ReplaceExisting),
            ]);
        return new CompositionRunRequest(
            "run-progress-multiple-processors",
            CreateCompiledComposition(
                plan,
                new LegacyCompiledCompositionIdentity(
                    "progress-multiple-processors",
                    "1.0.0",
                    "NT-SYNTHETIC",
                    "progress",
                    "standard-merge",
                    CompositionKind.Merge),
                "progress.bin"),
            [],
            "progress.bin");
    }

    private static CompositionOperation CreateExternalOperation(
        string operationId,
        int sequence,
        OverlapPolicy overlapPolicy)
    {
        return CompositionOperation.RunExternalProcessor(
            operationId,
            sequence,
            "output-image",
            new ByteRange(0, 4),
            new ExternalProcessorInvocation(
                "processor-v1",
                "tool-v1",
                [new ByteRange(0, 4)],
                [new ByteRange(0, 4)]),
            overlapPolicy,
            "run synthetic processor for progress");
    }

    private static void AssertLinearProgress(
        List<CompositionRunProgressSnapshot> snapshots,
        IReadOnlyList<CompositionRunPhase> expectedPhases)
    {
        Assert.Equal(expectedPhases, snapshots.Select(static snapshot => snapshot.CurrentPhase));
        Assert.All(snapshots, snapshot => Assert.Equal(expectedPhases, snapshot.ApplicablePhases));
        for (int index = 0; index < snapshots.Count; index++)
        {
            Assert.Equal(expectedPhases.Take(index), snapshots[index].CompletedPhases);
            Assert.Equal(index + 1, snapshots[index].CurrentStep);
            Assert.Equal(expectedPhases.Count, snapshots[index].StepCount);
        }
    }

    private static async Task<List<CompositionRunProgressSnapshot>> ReadProgressAsync(
        CompositionRunProgressFeed progress)
    {
        var snapshots = new List<CompositionRunProgressSnapshot>();
        await foreach (CompositionRunProgressSnapshot snapshot in progress.ReadAllAsync())
        {
            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    private sealed class CancellingArtifactReader : IArtifactReader
    {
        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
            string artifactId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Expected cancellation before artifact read.");
        }
    }

    private sealed class ThrowingOutputWriter : ICompositionOutputWriter
    {
        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            throw new IOException("Synthetic commit failure.");
        }
    }
}
