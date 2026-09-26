using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeGoldenRegressionTests
{
    /// <summary>A missing AB processor is one typed action blocker before processor or output work.</summary>
    [Fact]
    public async Task ProcessorBackedAbMergeMissingDependencyBlocksBeforeExecutionAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-missing");
        CompositionHostServices host = await CreateFormatGoldenHostAsync(workspace);
        var canonical = new CanonicalTestContext(host);
        IReadOnlyDictionary<string, string> paths = WriteGoldenInputs(
            workspace,
            ReadGoldenCase("nt51950-ab-boe-d82t80"));
        ActiveSessionSnapshot accepted = await PrepareAcceptedSessionAsync(
            host,
            "NT51950",
            paths,
            new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test"));
        var runtime = new TestRuntimeLeaseProvider(
            isReady: false,
            generation: 12,
            static generation => generation == 12);
        var authoring = new AbMergeAuthoringExperience(
            canonical.Compiler,
            canonical.Catalog,
            runtime,
            host.GetEventBufferFormatConfigurationAsync);

        CapabilityActionReadinessSnapshot readiness = Assert.IsType<CapabilityActionReadinessSnapshot>(
            await authoring.GetActionReadinessAsync(
                accepted,
                TestContext.Current.CancellationToken));
        var processor = new CountingPassThroughProcessor();
        string outputPath = workspace.PathFor("must-not-exist.bin");
        ICompositionExecution execution = CompositionExecutionTestSupport.Create(
            canonical,
            () => new CompositionExternalProcessorLease(12, processor),
            static generation => generation == 12);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => execution.ExecuteAsync(
                    new AcceptedCompositionExecutionRequest(
                        accepted,
                        paths,
                        build: true,
                        outputPath: outputPath,
                        actionReadiness: readiness),
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken)
                .AsTask());

        Assert.Contains("not installed", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            readiness.Build.Blockers,
            static blocker => blocker.Code ==
                CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked);
        Assert.Equal(0, processor.CallCount);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>
    /// An AB readiness checked at runtime generation 30 is refused with the typed pre-run refusal, and a
    /// reason that states only the known fact, when the lease acquired at admission is generation 31,
    /// has no runtime (generation 0 after a tool-configuration change), or is generation 30 but no
    /// longer current.
    /// </summary>
    [Theory]
    [InlineData(31L, true, true, "changed from 30 to 31")]
    [InlineData(0L, false, false, "generation 30 that the action readiness checked is no longer available")]
    [InlineData(30L, true, false, "generation 30 acquired for this run is no longer valid")]
    public async Task ProcessorBackedAbMergeRejectsStaleRuntimeGenerationBeforeExecutionAsync(
        long runGeneration,
        bool runtimeAvailable,
        bool generationCurrent,
        string expectedReason)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-stale");
        AcceptedAbRun run = await PrepareProcessorBackedAbRunAsync(workspace);
        var processor = new CountingPassThroughProcessor();
        var destinations = new CountingDestinations(onPrepare: null);
        string outputPath = workspace.PathFor("stale-must-not-exist.bin");
        CompositionExecutionExperience execution = CreateAbExecution(
            run,
            destinations,
            () => new CompositionExternalProcessorLease(runGeneration, runtimeAvailable ? processor : null),
            generation => generationCurrent && generation == runGeneration);

        CompositionPreRunRefusalException exception = await Assert.ThrowsAsync<CompositionPreRunRefusalException>(
            () => execution.ExecuteAsync(
                    run.CreateBuildRequest(outputPath),
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken)
                .AsTask());

        CompositionIssue issue = Assert.Single(exception.Issues);
        Assert.Equal(CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale, issue.Code);
        Assert.Contains(expectedReason, issue.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("changed from 30 to 30", issue.Message, StringComparison.Ordinal);
        Assert.Equal(ExperienceIds.AbMerge, issue.OperationId);
        Assert.Equal(0, processor.CallCount);
        Assert.Equal(0, destinations.Calls);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>
    /// A tool reload after the processor readiness was validated, while the run is being admitted,
    /// refuses the run at its admission point: no destination is prepared and no processor runs.
    /// </summary>
    [Fact]
    public async Task ProcessorBackedAbMergeRefusesAToolReloadBeforeItsAdmissionCompletesAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-admission");
        AcceptedAbRun run = await PrepareProcessorBackedAbRunAsync(workspace);
        var runtime = new SwitchableRuntime(30);
        var destinations = new CountingDestinations(onPrepare: null);
        string outputPath = workspace.PathFor("refused.bin");
        CompositionExecutionExperience execution = CreateAbExecution(
            run,
            destinations,
            runtime.Acquire,
            runtime.IsCurrent,
            new ReloadOnFirstCurrencyCheck(run.Catalog, runtime.Reload));

        CompositionPreRunRefusalException exception = await Assert.ThrowsAsync<CompositionPreRunRefusalException>(
            () => execution.ExecuteAsync(
                    run.CreateBuildRequest(outputPath),
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken)
                .AsTask());

        Assert.Equal(CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale, Assert.Single(exception.Issues).Code);
        Assert.Equal(31, runtime.Generation);
        Assert.Equal(0, destinations.Calls);
        Assert.Equal(0, runtime.ProcessorFor(30).CallCount);
        Assert.Equal(0, runtime.ProcessorFor(31).CallCount);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>
    /// A tool reload during destination preparation, after admission, does not reach the admitted run,
    /// which runs the processor of its admitted lease; the next run of the same readiness is refused.
    /// </summary>
    [Fact]
    public async Task ProcessorBackedAbMergeKeepsItsAdmittedLeaseWhenTheToolReloadsDuringDestinationPreparationAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-destination");
        AcceptedAbRun run = await PrepareProcessorBackedAbRunAsync(workspace);
        var runtime = new SwitchableRuntime(30);
        var destinations = new CountingDestinations(onPrepare: runtime.Reload);
        CompositionExecutionExperience execution = CreateAbExecution(run, destinations, runtime.Acquire, runtime.IsCurrent);

        _ = await execution.ExecuteAsync(
            run.CreateBuildRequest(workspace.PathFor("admitted.bin")),
            new CompositionRunProgressFeed(),
            TestContext.Current.CancellationToken);

        Assert.Equal(31, runtime.Generation);
        Assert.Equal(1, destinations.Calls);
        Assert.Equal(1, runtime.ProcessorFor(30).CallCount);
        Assert.Equal(0, runtime.ProcessorFor(31).CallCount);

        string nextOutputPath = workspace.PathFor("next.bin");
        CompositionPreRunRefusalException refusal = await Assert.ThrowsAsync<CompositionPreRunRefusalException>(
            () => execution.ExecuteAsync(
                    run.CreateBuildRequest(nextOutputPath),
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken)
                .AsTask());

        Assert.Contains("changed from 30 to 31", Assert.Single(refusal.Issues).Message, StringComparison.Ordinal);
        Assert.Equal(1, destinations.Calls);
        Assert.Equal(1, runtime.ProcessorFor(30).CallCount);
        Assert.Equal(0, runtime.ProcessorFor(31).CallCount);
        Assert.False(File.Exists(nextOutputPath));
    }

    private static async Task<AcceptedAbRun> PrepareProcessorBackedAbRunAsync(TempWorkspace workspace)
    {
        CompositionHostServices host = await CreateFormatGoldenHostAsync(workspace);
        var canonical = new CanonicalTestContext(host);
        IReadOnlyDictionary<string, string> paths = WriteGoldenInputs(
            workspace,
            ReadGoldenCase("nt51950-ab-boe-d82t80"));
        ActiveSessionSnapshot accepted = await PrepareAcceptedSessionAsync(
            host,
            "NT51950",
            paths,
            new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test"));
        var authoring = new AbMergeAuthoringExperience(
            canonical.Compiler,
            canonical.Catalog,
            new TestRuntimeLeaseProvider(
                isReady: true,
                generation: 30,
                static generation => generation == 30),
            host.GetEventBufferFormatConfigurationAsync);
        CapabilityActionReadinessSnapshot readiness = Assert.IsType<CapabilityActionReadinessSnapshot>(
            await authoring.GetActionReadinessAsync(
                accepted,
                TestContext.Current.CancellationToken));
        Assert.Equal(30, readiness.RuntimeDependencyGeneration);
        return new AcceptedAbRun(canonical.Catalog, canonical.AbMergeAuthoring, accepted, readiness, paths);
    }

    private static CompositionExecutionExperience CreateAbExecution(
        AcceptedAbRun run,
        ICompositionExecutionDestinationProvider destinations,
        Func<CompositionExternalProcessorLease> acquire,
        Func<long, bool> generationIsCurrent,
        ICanonicalCapabilityQuery? capabilities = null)
    {
        return new CompositionExecutionExperience(
            capabilities ?? run.Catalog,
            destinations,
            acquire,
            generationIsCurrent,
            new SystemClock(),
            run.Authoring);
    }

    private sealed record AcceptedAbRun(
        ICanonicalCapabilityQuery Catalog,
        AbMergeAuthoringExperience Authoring,
        ActiveSessionSnapshot Session,
        CapabilityActionReadinessSnapshot Readiness,
        IReadOnlyDictionary<string, string> Paths)
    {
        internal AcceptedCompositionExecutionRequest CreateBuildRequest(string outputPath)
        {
            return new AcceptedCompositionExecutionRequest(
                Session,
                Paths,
                build: true,
                outputPath: outputPath,
                actionReadiness: Readiness);
        }
    }

    /// <summary>A tool runtime whose published generation the test reloads; each generation has its own counting processor.</summary>
    private sealed class SwitchableRuntime(long generation)
    {
        private readonly Dictionary<long, CountingPassThroughProcessor> _processors = [];

        internal long Generation { get; private set; } = generation;

        internal CountingPassThroughProcessor ProcessorFor(long leaseGeneration)
        {
            if (!_processors.TryGetValue(leaseGeneration, out CountingPassThroughProcessor? processor))
            {
                processor = new CountingPassThroughProcessor();
                _processors.Add(leaseGeneration, processor);
            }

            return processor;
        }

        internal CompositionExternalProcessorLease Acquire()
        {
            return new CompositionExternalProcessorLease(Generation, ProcessorFor(Generation));
        }

        internal bool IsCurrent(long leaseGeneration)
        {
            return leaseGeneration == Generation;
        }

        internal void Reload()
        {
            Generation++;
        }
    }

    /// <summary>Counts destination preparation and runs an optional change while it happens.</summary>
    private sealed class CountingDestinations(Action? onPrepare) : ICompositionExecutionDestinationProvider
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

    /// <summary>
    /// The execution's catalog view; the first currency check made while a run is admitted first
    /// reloads the tool runtime, which places the reload after the readiness validation and before the
    /// admission completes.
    /// </summary>
    private sealed class ReloadOnFirstCurrencyCheck(ICanonicalCapabilityQuery inner, Action reload)
        : ICanonicalCapabilityQuery
    {
        private bool _reloaded;

        public CanonicalCapabilityCatalogSnapshot GetCurrentSnapshot()
        {
            return inner.GetCurrentSnapshot();
        }

        public CanonicalCapabilityCatalogSnapshot? TryGetCurrentSnapshot()
        {
            return inner.TryGetCurrentSnapshot();
        }

        public CapabilityResolutionResult Resolve(string routeId)
        {
            return inner.Resolve(routeId);
        }

        public CapabilityRouteResolutionResult ResolveDynamicRoute(string routeId)
        {
            return inner.ResolveDynamicRoute(routeId);
        }

        public CapabilityResolutionResult ResolveUniqueRoute(
            string icId,
            string workflowId,
            string icCountVariant,
            long? outputCapacity = null)
        {
            return inner.ResolveUniqueRoute(icId, workflowId, icCountVariant, outputCapacity);
        }

        public MetadataPlanResolutionResult ResolveUniqueMetadataPlan(
            string icId,
            string workflowId,
            string icCountVariant,
            long? outputCapacity = null)
        {
            return inner.ResolveUniqueMetadataPlan(icId, workflowId, icCountVariant, outputCapacity);
        }

        public MetadataPlanResolutionResult ResolveFullImageMetadataPlan(string icId, long inputLength)
        {
            return inner.ResolveFullImageMetadataPlan(icId, inputLength);
        }

        public CapabilityResolutionResult ResolveUniqueTopologyRoute(
            string icId,
            string workflowId,
            TopologySelection? topology)
        {
            return inner.ResolveUniqueTopologyRoute(icId, workflowId, topology);
        }

        public bool HasAuthorableCapability(string icId, string workflowId)
        {
            return inner.HasAuthorableCapability(icId, workflowId);
        }

        public ResolvedCapability? ResolveCurrentCompilation(
            CompiledComposition composition,
            ResolvedCapability? acceptedCapability = null)
        {
            if (!_reloaded)
            {
                _reloaded = true;
                reload();
            }

            return inner.ResolveCurrentCompilation(composition, acceptedCapability);
        }
    }

    /// <summary>The current positive AB processor generation can execute the direct canonical case.</summary>
    [Fact]
    public async Task ProcessorBackedAbMergeCurrentRuntimeGenerationExecutesAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-current");
        CompositionHostServices host = await CreateFormatGoldenHostAsync(workspace);
        IReadOnlyDictionary<string, string> paths = WriteGoldenInputs(
            workspace,
            ReadGoldenCase("nt51950-ab-boe-d82t80"));
        ActiveSessionSnapshot accepted = await PrepareAcceptedSessionAsync(
            host,
            "NT51950",
            paths,
            new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test"));
        CapabilityActionReadinessSnapshot readiness = Assert.IsType<CapabilityActionReadinessSnapshot>(
            await host.AbMergeAuthoring.GetActionReadinessAsync(
                accepted,
                TestContext.Current.CancellationToken));

        CompositionRunResult result = await host.CompositionExecution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(
                accepted,
                paths,
                build: false,
                actionReadiness: readiness),
            new CompositionRunProgressFeed(),
            TestContext.Current.CancellationToken);

        Assert.True(readiness.RuntimeDependencyGeneration > 0);
        Assert.True(readiness.Preview.IsAvailable);
        Assert.True(readiness.Build.IsAvailable);
        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
    }

    /// <summary>Processor-free AB Merge neither acquires nor requires a runtime generation.</summary>
    [Fact]
    public async Task ProcessorFreeAbMergeBypassesRuntimeReadinessAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-bypass");
        IReadOnlyDictionary<string, string> paths = WriteGoldenInputs(
            workspace,
            ReadGoldenCase("nt51929-ab-t05-d06"));
        ActiveSessionSnapshot accepted = await PrepareAcceptedSessionAsync(
            BootstrapTestHost.Services,
            "NT51929",
            paths);
        var authoring = new AbMergeAuthoringExperience(
            BootstrapTestHost.Canonical.Compiler,
            BootstrapTestHost.Canonical.Catalog,
            ThrowingRuntimeLeaseProvider.Instance);
        CapabilityActionReadinessSnapshot readiness =
            Assert.IsType<CapabilityActionReadinessSnapshot>(
                await authoring.GetActionReadinessAsync(
                accepted,
                TestContext.Current.CancellationToken));
        int leaseAcquisitions = 0;
        ICompositionExecution execution = CompositionExecutionTestSupport.Create(
            BootstrapTestHost.Canonical,
            () =>
            {
                leaseAcquisitions++;
                return new CompositionExternalProcessorLease(0, null);
            },
            static _ => false);

        CompositionRunResult result = await execution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(
                accepted,
                paths,
                build: false),
            new CompositionRunProgressFeed(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal(0, readiness.RuntimeDependencyGeneration);
        Assert.True(readiness.Preview.IsAvailable);
        Assert.True(readiness.Build.IsAvailable);
        Assert.Equal(0, leaseAcquisitions);
    }

    private static Dictionary<string, string> WriteGoldenInputs(
        TempWorkspace workspace,
        JsonElement goldenCase)
    {
        return ReadInputs(goldenCase).ToDictionary(
            static pair => pair.Key,
            pair => workspace.Write($"{pair.Key}.bin", pair.Value),
            StringComparer.Ordinal);
    }

    private static async Task<CompositionHostServices> CreateFormatGoldenHostAsync(TempWorkspace workspace)
    {
        CompositionHostServices host = CompositionHostServices.Create(
            new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools")),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        Assert.True((await host.ExternalEnvironmentLoader.LoadToCompletionAsync(
            null, TestContext.Current.CancellationToken)).Succeeded);
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(
            TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(),
            TestContext.Current.CancellationToken)).Succeeded);
        return host;
    }

    private static async Task<ActiveSessionSnapshot> PrepareAcceptedSessionAsync(
        CompositionHostServices host,
        string icId,
        IReadOnlyDictionary<string, string> paths,
        TopologySelection? topology = null)
    {
        CompiledAuthoringSessionPreparation prepared = await AbMergeTestSupport.PrepareAsync(
            host,
            icId,
            paths,
            TestContext.Current.CancellationToken,
            topology);
        Assert.True(
            prepared.Succeeded,
            CompositionExecutionTestSupport.FormatIssues(prepared.Issues));
        return Assert.IsType<ActiveSessionSnapshot>(prepared.Snapshot);
    }

    private sealed class TestRuntimeLeaseProvider(
        bool isReady,
        long generation,
        Func<long, bool> generationIsCurrent) :
        IRuntimeDependencyReadinessLeaseProvider,
        IRuntimeDependencyReadinessProvider
    {
        public RuntimeDependencyReadinessLease AcquireCurrent()
        {
            return new(this, generation, generationIsCurrent);
        }

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long requestedGeneration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new RuntimeDependencyReadinessSnapshot(
                request.RouteId,
                request.CapabilityFingerprint,
                request.CompilationFingerprint,
                request.ResolutionToken,
                request.AuthoringRevision,
                requestedGeneration,
                DateTimeOffset.UnixEpoch,
                request.Dependencies.Select(dependency => isReady
                    ? RuntimeDependencyEntry.Ready(
                        dependency.ProcessorId,
                        dependency.ToolBindingId)
                    : RuntimeDependencyEntry.Blocked(
                        dependency.ProcessorId,
                        dependency.ToolBindingId,
                        "external-tool.executable.missing",
                        "The required external processor is not installed."))));
        }
    }

    private sealed class CountingPassThroughProcessor : IExternalProcessor
    {
        internal int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(
                ExternalProcessorResult.Success(request.InputBytes, [], []));
        }
    }

    private sealed class ThrowingRuntimeLeaseProvider :
        IRuntimeDependencyReadinessLeaseProvider
    {
        internal static ThrowingRuntimeLeaseProvider Instance { get; } = new();

        public RuntimeDependencyReadinessLease AcquireCurrent()
        {
            throw new InvalidOperationException(
                "Processor-free AB Merge must not acquire a runtime lease.");
        }
    }
}
