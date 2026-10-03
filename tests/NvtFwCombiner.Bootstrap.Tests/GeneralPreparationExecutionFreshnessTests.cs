using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Actual General Build bytes follow the newest preparation through the shared host.</summary>
public sealed class GeneralPreparationExecutionFreshnessTests
{
    private const string MappingId = "freshness-map";
    private static readonly byte[] SourceBytes = [0x11, 0x12, 0x13];

    /// <summary>Every admitted General member keeps its complete small mapping output and write audit.</summary>
    [Theory]
    [InlineData("NT51917")]
    [InlineData("NT51927")]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    [InlineData("NT51923")]
    [InlineData("NT51926")]
    [InlineData("NT51928")]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public async Task UnchangedNewestMappingBuildMatchesCompleteIndependentOracle(string icId)
    {
        using var workspace = TempWorkspace.Create("general-freshness-baseline");
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        string source = workspace.Write("source.bin", SourceBytes);
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringSessionPreparation prepared = await host.GeneralAuthoring.PrepareMergeSessionAsync(
            session, icId, Draft(source, 4), TestContext.Current.CancellationToken);
        AssertPrepared(prepared);
        await AssertPreviewAndBuildAsync(host, prepared, workspace);
    }

    /// <summary>Completion ordering and late cancellation cannot make Build write the superseded target.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NewestMappingBuildSurvivesLateCompletionOrCancellation(bool newestFinishesFirst, bool cancelOld)
    {
        using var workspace = TempWorkspace.Create("general-freshness-build");
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        string source = workspace.Write("source.bin", SourceBytes);
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        using var cancellation = new CancellationTokenSource();
        var oldGate = new ProgressGate();
        var newGate = new ProgressGate();
        Task<GeneralAuthoringSessionPreparation> old = Task.Run(async () =>
            await host.GeneralAuthoring.PrepareMergeSessionAsync(session, "NT51926", Draft(source, 5), cancellation.Token, oldGate));
        try
        {
            await oldGate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Task<GeneralAuthoringSessionPreparation> newest = Task.Run(async () =>
                await host.GeneralAuthoring.PrepareMergeSessionAsync(session, "NT51926", Draft(source, 4),
                    TestContext.Current.CancellationToken, newGate));
            await newGate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            if (cancelOld)
            {
                cancellation.Cancel();
            }
            GeneralAuthoringSessionPreparation? late = null;
            Exception? oldFailure = null;
            if (newestFinishesFirst)
            {
                newGate.Release.SetResult();
                _ = await newest;
            }
            oldGate.Release.SetResult();
            oldFailure = await Record.ExceptionAsync(async () => late = await old);
            if (!newestFinishesFirst)
            {
                newGate.Release.SetResult();
            }
            GeneralAuthoringSessionPreparation accepted = await newest;
            AssertPrepared(accepted);

            // Execute the actual current session even on the unfixed implementation:
            // this catches stale bytes, rather than stopping at a state-only assertion.
            GeneralAuthoringSessionPreparation current = late?.Succeeded == true && newestFinishesFirst ? late : accepted;
            await AssertPreviewAndBuildAsync(host, current, workspace);
            Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
            if (cancelOld)
            {
                _ = Assert.IsType<OperationCanceledException>(oldFailure, exactMatch: false);
            }
            else
            {
                Assert.Null(oldFailure);
                Assert.False(late!.Succeeded);
                Assert.Null(late.AcceptedSession);
                Assert.Null(late.Readiness);
                Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, Assert.Single(late.Issues).Code);
            }
        }
        finally
        {
            _ = oldGate.Release.TrySetResult();
            _ = newGate.Release.TrySetResult();
        }
    }

    private static GeneralMergeDraftState Draft(string source, long target)
    {
        return new(new GeneralMergeOutputInitializer(16, 0xA5), new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(MappingId, ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(source), new ByteRange(0, SourceBytes.Length),
                CompositionAddressSpaceIds.OutputImage, new ByteRange(target, SourceBytes.Length),
                OverlapPolicy.Reject, 1, "Newest mapping execution evidence."),
        ]));
    }

    private static void AssertPrepared(GeneralAuthoringSessionPreparation preparation)
    {
        Assert.True(preparation.Succeeded, CompositionExecutionTestSupport.FormatIssues(preparation.Issues));
        Assert.True(preparation.Readiness!.Build.IsAvailable);
        Assert.Equal(preparation.AcceptedSession!.AuthoringRevision, preparation.Readiness.AuthoringRevision);
    }

    private static async Task AssertPreviewAndBuildAsync(CompositionHostServices host,
        GeneralAuthoringSessionPreparation preparation, TempWorkspace workspace)
    {
        byte[] expected = [0xA5, 0xA5, 0xA5, 0xA5, 0x11, 0x12, 0x13, 0xA5,
            0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5];
        string output = workspace.PathFor("newest.bin");
        List<CompositionRunResult> results = [];
        foreach (bool build in new[] { false, true })
        {
            CompositionRunResult result = await host.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(preparation.AcceptedSession!, new Dictionary<string, string>(),
                    build, outputPath: build ? output : null, actionReadiness: preparation.Readiness),
                new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
            results.Add(result);
            if (!build)
            {
                Assert.Null(result.CommittedOutputId);
                Assert.False(File.Exists(output));
            }
        }
        // Build has committed before comparing bytes, so red evidence includes
        // the actual writer as well as Preview, not just a stale state or preview.
        Assert.Equal(output, results[1].CommittedOutputId);
        Assert.Equal(expected, await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
        foreach (CompositionRunResult result in results)
        {
            Assert.Equal(expected, result.OutputBytes.ToArray());
            Assert.Equal(FileStamp.FromBytes(expected).Sha256, result.OutputSha256);
            Assert.Equal(FileStamp.FromBytes(SourceBytes).Sha256, Assert.Single(result.Report.Inputs).Sha256);
            OperationRunSummary operation = Assert.Single(result.Report.Operations);
            Assert.Equal(CompositionAddressSpaceIds.OutputImage, operation.TargetSpaceId);
            Assert.Equal(new ByteRange(0, 3), operation.SourceRange);
            Assert.Equal(new ByteRange(4, 3), operation.TargetRange);
            Assert.Equal(CompositionOperationKind.CopyRange, operation.Kind);
            Assert.Null(operation.ProcessorId);
            MutationRunSummary mutation = Assert.Single(result.Report.Mutations);
            Assert.Equal(operation.TargetRange, mutation.TargetRange);
            Assert.Equal(operation.TargetSpaceId, mutation.TargetSpaceId);
            Assert.Equal(3, mutation.ChangedByteCount);
        }
    }

    private sealed class ProgressGate : IProgress<AuthoringInspectionProgress>
    {
        internal TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Report(AuthoringInspectionProgress value)
        {
            if (value.CompletedWork == 1)
            {
                Reached.SetResult();
                Release.Task.GetAwaiter().GetResult();
            }
        }
    }
}
