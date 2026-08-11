using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class Nt51929CtrlRamFw200SingleEvidenceTests
{
    /// <summary>A missing current-machine dependency blocks both headless actions before byte execution.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeDependencyBlockerStopsPreviewAndBuildBeforeProcessorAsync(bool build)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-runtime-blocked");
        string outputPath = workspace.PathFor("blocked.bin");
        var processor = new PassThroughProcessor();
        var readinessProvider = new SwitchableRuntimeDependencyReadinessProvider(isReady: false);

        CapabilityActionReadinessSnapshot readiness = await CtrlRamReplaceTestSupport.GetReadinessAsync(BootstrapTestHost.Canonical,
            "NT51929",
            "single",
            CreateSlotPaths(evidence, evidence.Expected.Path),
            firmwareVersionEdit: null,
            readinessProvider,
            generation: 12,
            static generation => generation == 12,
            TestContext.Current.CancellationToken);

        CapabilityActionAvailability action = build ? readiness.Build : readiness.Preview;
        Assert.False(action.IsAvailable);
        Assert.Equal(0, processor.CallCount);
        Assert.False(File.Exists(outputPath));
        Assert.Equal([12], readinessProvider.ObservedGenerations);
        Assert.Contains(
            action.Blockers,
            blocker => StringComparer.Ordinal.Equals(
                CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked,
                blocker.Code));
    }

    /// <summary>A later refresh observes an installed dependency and allows the next Build to run.</summary>
    [Fact]
    public async Task RuntimeDependencyRefreshRecoversWithoutRestartingTheWorkflowAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-runtime-recovery");
        string outputPath = workspace.PathFor("recovered.bin");
        var processor = new PassThroughProcessor();
        var readinessProvider = new SwitchableRuntimeDependencyReadinessProvider(isReady: false);
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(evidence, evidence.Expected.Path);

        CapabilityActionReadinessSnapshot blocked = await CtrlRamReplaceTestSupport.GetReadinessAsync(BootstrapTestHost.Canonical,
            "NT51929", "single", slots, firmwareVersionEdit: null,
            readinessProvider, generation: 20, static generation => generation == 20,
            TestContext.Current.CancellationToken);

        readinessProvider.IsReady = true;
        CompositionRunResult recovered = await CtrlRamReplaceTestSupport.RunWithProcessorAsync(BootstrapTestHost.Canonical,
            "NT51929", "single", slots, true, outputPath, null, processor,
            readinessProvider, 21, static generation => generation == 21,
            TestContext.Current.CancellationToken);

        Assert.False(blocked.Build.IsAvailable);
        Assert.True(recovered.Succeeded, CompositionRunReportJson.Serialize(recovered));
        Assert.Equal(1, processor.CallCount);
        Assert.True(File.Exists(outputPath));
        Assert.Equal([20, 21], readinessProvider.ObservedGenerations);
    }

    /// <summary>A concurrent environment replacement invalidates the completed probe before execution.</summary>
    [Fact]
    public async Task SupersededRuntimeGenerationBlocksBeforeProcessorExecutionAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-runtime-stale");
        string outputPath = workspace.PathFor("stale.bin");
        var processor = new PassThroughProcessor();
        var readinessProvider = new SwitchableRuntimeDependencyReadinessProvider(isReady: true);

        CapabilityActionReadinessSnapshot readiness = await CtrlRamReplaceTestSupport.GetReadinessAsync(BootstrapTestHost.Canonical,
            "NT51929",
            "single",
            CreateSlotPaths(evidence, evidence.Expected.Path),
            firmwareVersionEdit: null,
            readinessProvider,
            generation: 30,
            static _ => false,
            TestContext.Current.CancellationToken);

        Assert.False(readiness.Build.IsAvailable);
        Assert.Equal(0, processor.CallCount);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(
            readiness.Build.Blockers,
            blocker => StringComparer.Ordinal.Equals(
                CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
                blocker.Code));
    }

    private sealed class SwitchableRuntimeDependencyReadinessProvider(
        bool isReady) : IRuntimeDependencyReadinessProvider
    {
        private readonly List<long> _observedGenerations = [];

        public bool IsReady { get; set; } = isReady;

        public IReadOnlyList<long> ObservedGenerations => _observedGenerations;

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _observedGenerations.Add(generation);
            return ValueTask.FromResult(new RuntimeDependencyReadinessSnapshot(
                request.RouteId,
                request.CapabilityFingerprint,
                request.CompilationFingerprint,
                request.ResolutionToken,
                request.AuthoringRevision,
                generation,
                DateTimeOffset.UnixEpoch,
                request.Dependencies.Select(dependency => IsReady
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
}
