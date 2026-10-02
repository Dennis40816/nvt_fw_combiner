using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// The capture boundary of accepted execution (ADR 0072, 2026-09-26 amendment; owner decision 68): a
/// run is admitted once, before destination preparation. A catalog reload before that point refuses it
/// with the typed pre-run refusal; a reload after it affects only the next run.
/// </summary>
public sealed class AcceptedExecutionAdmissionTests
{
    private const int TargetStart = 0x3E020;
    private static readonly byte[] Replacement = [0xA5, 0x5A];

    /// <summary>A catalog reload before admission refuses the run before any destination is prepared.</summary>
    [Fact]
    public async Task CatalogReloadBeforeAdmissionRefusesTheRunBeforeDestinationPreparation()
    {
        using var workspace = TempWorkspace.Create("nfc-admission-before");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var destinations = new ObservedDestinations(onPrepare: null);
        CompositionExecutionExperience execution = CreateExecution(host, destinations);
        AcceptedGeneralRun accepted = await PrepareGeneralSessionAsync(host, workspace);
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        string outputPath = workspace.PathFor("must-not-exist.bin");

        CompositionPreRunRefusalException refusal = await Assert.ThrowsAsync<CompositionPreRunRefusalException>(
            () => execution.ExecuteAsync(
                    accepted.CreateBuildRequest(outputPath),
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken)
                .AsTask());

        CompositionIssue issue = Assert.Single(refusal.Issues);
        Assert.Equal(CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale, issue.Code);
        Assert.Equal(ExperienceIds.GeneralMerge, issue.OperationId);
        Assert.Contains("catalog was reloaded", issue.Message, StringComparison.Ordinal);
        Assert.Equal(0, destinations.Calls);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>
    /// A catalog reload while the destination is prepared does not reach the already admitted run, which
    /// commits its output; the next run of the same accepted session is refused before its destination.
    /// </summary>
    [Fact]
    public async Task CatalogReloadDuringDestinationPreparationAffectsOnlyTheNextRun()
    {
        using var workspace = TempWorkspace.Create("nfc-admission-during");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var destinations = new ObservedDestinations(
            onPrepare: () => Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded));
        CompositionExecutionExperience execution = CreateExecution(host, destinations);
        AcceptedGeneralRun accepted = await PrepareGeneralSessionAsync(host, workspace);
        string outputPath = workspace.PathFor("admitted.bin");

        CompositionRunResult result = await execution.ExecuteAsync(
            accepted.CreateBuildRequest(outputPath),
            new CompositionRunProgressFeed(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal(outputPath, result.CommittedOutputId);
        byte[] committed = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(accepted.OutputLength, committed.Length);
        Assert.Equal(Replacement, committed.AsSpan(TargetStart, Replacement.Length).ToArray());
        Assert.Equal(1, destinations.Calls);

        string nextOutputPath = workspace.PathFor("next.bin");
        CompositionPreRunRefusalException refusal = await Assert.ThrowsAsync<CompositionPreRunRefusalException>(
            () => execution.ExecuteAsync(
                    accepted.CreateBuildRequest(nextOutputPath),
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken)
                .AsTask());

        Assert.Equal(CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale, Assert.Single(refusal.Issues).Code);
        Assert.Equal(1, destinations.Calls);
        Assert.False(File.Exists(nextOutputPath));
    }

    /// <summary>An invalid request stays an invariant failure, and is reported first, even when the catalog was reloaded.</summary>
    [Fact]
    public async Task InvalidRequestStaysAnInvariantFailureWhenTheCatalogIsAlsoStale()
    {
        using var workspace = TempWorkspace.Create("nfc-admission-invariant");
        CompositionHostServices host = await CreateLoadedHostAsync();
        var destinations = new ObservedDestinations(onPrepare: null);
        CompositionExecutionExperience execution = CreateExecution(host, destinations);
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace", "nt51927-3chip-self-20260705");
        Dictionary<string, string> paths = fixture.GetProperty("artifacts").EnumerateArray()
            .ToDictionary(artifact => artifact.GetProperty("slotId").GetString()!, CanonicalGoldenTestData.ArtifactPath);
        Dictionary<string, byte[]> bytes = paths.ToDictionary(static pair => pair.Key, static pair => File.ReadAllBytes(pair.Value));
        CtrlRamAuthoringSessionPreparation prepared = host.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), "NT51927", "3", paths, bytes);
        Assert.True(prepared.Succeeded, CompositionExecutionTestSupport.FormatIssues(prepared.Issues));
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        string outputPath = workspace.PathFor("must-not-exist.bin");

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => execution.ExecuteAsync(
                    new AcceptedCompositionExecutionRequest(
                        prepared.AcceptedSession!, paths, build: true, outputPath: outputPath, actionReadiness: null),
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken)
                .AsTask());

        Assert.Contains("requires its exact typed action readiness", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, destinations.Calls);
        Assert.False(File.Exists(outputPath));
    }

    private static async Task<CompositionHostServices> CreateLoadedHostAsync()
    {
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        _ = await host.ExternalEnvironmentLoader.LoadToCompletionAsync(
            progress: null,
            TestContext.Current.CancellationToken);
        return host;
    }

    private static CompositionExecutionExperience CreateExecution(
        CompositionHostServices host,
        ICompositionExecutionDestinationProvider destinations)
    {
        return new CompositionExecutionExperience(
            host.Catalog,
            destinations,
            () =>
            {
                ExternalProcessorEnvironmentLease lease = host.ExternalEnvironment.AcquireCurrent();
                return new CompositionExternalProcessorLease(lease.Generation, lease.Processor);
            },
            host.ExternalEnvironment.IsCurrent,
            new SystemClock(),
            (AbMergeAuthoringExperience)host.AbMergeAuthoring);
    }

    private static async Task<AcceptedGeneralRun> PrepareGeneralSessionAsync(
        CompositionHostServices host,
        TempWorkspace workspace)
    {
        string replacementPath = workspace.Write("replacement.bin", Replacement);
        var draft = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "admission-map",
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(replacementPath),
                new ByteRange(0, Replacement.Length),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(TargetStart, Replacement.Length),
                OverlapPolicy.Reject,
                alignment: 1,
                "Execution admission boundary fixture."),
        ]);
        const int outputLength = 0x40000;
        GeneralAuthoringSessionPreparation prepared = await host.GeneralAuthoring.PrepareMergeSessionAsync(
            new AuthoringSessionState(ExperienceIds.GeneralMerge),
            "NT51926",
            new GeneralMergeDraftState(new GeneralMergeOutputInitializer(outputLength), draft),
            TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, CompositionExecutionTestSupport.FormatIssues(prepared.Issues));
        Assert.True(prepared.Readiness!.Build.IsAvailable);
        return new AcceptedGeneralRun(
            prepared.AcceptedSession!,
            prepared.Readiness,
            new Dictionary<string, string>(StringComparer.Ordinal),
            outputLength);
    }

    private sealed record AcceptedGeneralRun(
        ActiveSessionSnapshot Session,
        CapabilityActionReadinessSnapshot Readiness,
        IReadOnlyDictionary<string, string> SlotPaths,
        int OutputLength)
    {
        internal AcceptedCompositionExecutionRequest CreateBuildRequest(string outputPath)
        {
            return new AcceptedCompositionExecutionRequest(
                Session,
                SlotPaths,
                build: true,
                outputPath: outputPath,
                actionReadiness: Readiness);
        }
    }

    /// <summary>Counts destination preparation and runs an optional change while it happens.</summary>
    private sealed class ObservedDestinations(Action? onPrepare) : ICompositionExecutionDestinationProvider
    {
        private readonly ProtectedCompositionDestinationProvider _inner = new();

        internal int Calls { get; private set; }

        public CompositionExecutionDestination Prepare(CompositionExecutionDestinationRequest request)
        {
            Calls++;
            onPrepare?.Invoke();
            return _inner.Prepare(request);
        }
    }
}
