using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeGoldenRegressionTests
{
    /// <summary>A missing AB processor is one typed action blocker before processor or output work.</summary>
    [Fact]
    public async Task ProcessorBackedAbMergeMissingDependencyBlocksBeforeExecutionAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-missing");
        IReadOnlyDictionary<string, string> paths = WriteGoldenInputs(
            workspace,
            ReadGoldenCase("nt51950-ab-boe-d82t80"));
        ActiveSessionSnapshot accepted = PrepareAcceptedSession(
            BootstrapTestHost.Services,
            "NT51950",
            paths,
            new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test"));
        var runtime = new TestRuntimeLeaseProvider(
            isReady: false,
            generation: 12,
            static generation => generation == 12);
        var authoring = new AbMergeAuthoringExperience(
            BootstrapTestHost.Canonical.Compiler,
            BootstrapTestHost.Canonical.Catalog,
            runtime);

        CapabilityActionReadinessSnapshot readiness = Assert.IsType<CapabilityActionReadinessSnapshot>(
            await authoring.GetActionReadinessAsync(
                accepted,
                TestContext.Current.CancellationToken));
        var processor = new CountingPassThroughProcessor();
        string outputPath = workspace.PathFor("must-not-exist.bin");
        ICompositionExecution execution = CompositionExecutionTestSupport.Create(
            BootstrapTestHost.Canonical,
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

    /// <summary>An AB readiness lease from generation N cannot execute after generation N+1 is published.</summary>
    [Fact]
    public async Task ProcessorBackedAbMergeRejectsStaleRuntimeGenerationBeforeExecutionAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-stale");
        IReadOnlyDictionary<string, string> paths = WriteGoldenInputs(
            workspace,
            ReadGoldenCase("nt51950-ab-boe-d82t80"));
        ActiveSessionSnapshot accepted = PrepareAcceptedSession(
            BootstrapTestHost.Services,
            "NT51950",
            paths,
            new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test"));
        var authoring = new AbMergeAuthoringExperience(
            BootstrapTestHost.Canonical.Compiler,
            BootstrapTestHost.Canonical.Catalog,
            new TestRuntimeLeaseProvider(
                isReady: true,
                generation: 30,
                static generation => generation == 30));
        CapabilityActionReadinessSnapshot readiness = Assert.IsType<CapabilityActionReadinessSnapshot>(
            await authoring.GetActionReadinessAsync(
                accepted,
                TestContext.Current.CancellationToken));
        var processor = new CountingPassThroughProcessor();
        string outputPath = workspace.PathFor("stale-must-not-exist.bin");
        ICompositionExecution execution = CompositionExecutionTestSupport.Create(
            BootstrapTestHost.Canonical,
            () => new CompositionExternalProcessorLease(31, processor),
            static generation => generation == 31);

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

        Assert.Contains("runtime generation", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, processor.CallCount);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>The current positive AB processor generation can execute the direct canonical case.</summary>
    [Fact]
    public async Task ProcessorBackedAbMergeCurrentRuntimeGenerationExecutesAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-runtime-current");
        IReadOnlyDictionary<string, string> paths = WriteGoldenInputs(
            workspace,
            ReadGoldenCase("nt51950-ab-boe-d82t80"));
        ActiveSessionSnapshot accepted = PrepareAcceptedSession(
            BootstrapTestHost.Services,
            "NT51950",
            paths,
            new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test"));
        CapabilityActionReadinessSnapshot readiness = Assert.IsType<CapabilityActionReadinessSnapshot>(
            await BootstrapTestHost.Services.AbMergeAuthoring.GetActionReadinessAsync(
                accepted,
                TestContext.Current.CancellationToken));

        CompositionRunResult result = await BootstrapTestHost.Services.CompositionExecution.ExecuteAsync(
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
        ActiveSessionSnapshot accepted = PrepareAcceptedSession(
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

    private static ActiveSessionSnapshot PrepareAcceptedSession(
        CompositionHostServices host,
        string icId,
        IReadOnlyDictionary<string, string> paths,
        TopologySelection? topology = null)
    {
        CompiledAuthoringSessionPreparation prepared = AbMergeTestSupport.Prepare(
            host,
            icId,
            paths,
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
