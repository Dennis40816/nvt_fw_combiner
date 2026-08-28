using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    private sealed class SequencedCatalog(params CanonicalSupportMatrixQueryResult[] results) :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        private int _index;

        public CanonicalSupportMatrixQueryResult Query()
        {
            return results[Math.Min(_index, results.Length - 1)];
        }

        public void Reload(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _index = Math.Min(_index + 1, results.Length - 1);
        }
    }

    private sealed class UnusedReadinessProvider : IRuntimeDependencyReadinessProvider
    {
        internal static UnusedReadinessProvider Instance { get; } = new();

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingReloadCatalog :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        internal TaskCompletionSource ReloadEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseReload { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public CanonicalSupportMatrixQueryResult Query()
        {
            return Result(CanonicalSupportMatrixCatalogState.Current, Matrix("catalog:blocking"));
        }

        public void Reload(CancellationToken cancellationToken)
        {
            ReloadEntered.SetResult();
            ReleaseReload.Task.Wait(cancellationToken);
        }
    }

    private sealed class BlockingInspectionReader(BuiltInFirmwareInspection firmwareInspection)
    {
        private int _blockNextBatch;
        private int _batchCount;
        private IReadOnlyList<string> _lastInspectionIds = [];
        private IReadOnlyList<FirmwareInspectionSnapshotInput> _lastInputs = [];

        internal int BatchCount => Volatile.Read(ref _batchCount);

        internal IReadOnlyList<string> LastInspectionIds =>
            Volatile.Read(ref _lastInspectionIds);

        internal IReadOnlyList<FirmwareInspectionSnapshotInput> LastInputs =>
            Volatile.Read(ref _lastInputs);

        internal TaskCompletionSource InspectionEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseInspection { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal void BlockNextBatch()
        {
            Volatile.Write(ref _blockNextBatch, 1);
        }

        internal IReadOnlyList<FirmwareInspectionSnapshotResult> Read(
            string selectedIc,
            IReadOnlyList<FirmwareInspectionSnapshotInput> inputs)
        {
            _ = Interlocked.Increment(ref _batchCount);
            Volatile.Write(
                ref _lastInspectionIds,
                Array.AsReadOnly(inputs.Select(static input => input.InspectionId).ToArray()));
            Volatile.Write(
                ref _lastInputs,
                Array.AsReadOnly(inputs.ToArray()));
            if (Interlocked.Exchange(ref _blockNextBatch, 0) == 1)
            {
                InspectionEntered.SetResult();
                ReleaseInspection.Task.Wait(TestContext.Current.CancellationToken);
            }

            return BuiltInFirmwareInspection.InspectFirmwareBatch(firmwareInspection, selectedIc, inputs);
        }
    }

    private sealed class StubCatalog :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        private bool _reloaded;

        public CanonicalSupportMatrixQueryResult Query()
        {
            return _reloaded
                ? new CanonicalSupportMatrixQueryResult(
                    CanonicalSupportMatrixCatalogState.Current,
                    new CanonicalSupportMatrixSnapshot(
                        "canonical-capability-policy",
                        "1.5.0",
                        new string('a', 64),
                        new ResolutionToken("catalog:ui-test"),
                        []))
                : new CanonicalSupportMatrixQueryResult(
                    CanonicalSupportMatrixCatalogState.ColdStartBlocked,
                    matrix: null,
                    [new CapabilityCatalogIssue("catalog.invalid", "private detail", null)]);
        }

        public void Reload(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _reloaded = true;
        }
    }

    private sealed class StubRuntimeProbe : ISystemRuntimeProbe
    {
        public SystemRuntimeFacts Probe()
        {
            return new SystemRuntimeFacts(".NET test", "Windows test", "x64");
        }
    }

    private sealed class StubClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private sealed class CapturingDiagnosticsExporter : ISystemDiagnosticsExporter
    {
        internal SystemDiagnosticsBundle? Bundle { get; private set; }

        public ValueTask ExportAsync(
            SystemDiagnosticsBundle bundle,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Bundle = bundle;
            return ValueTask.CompletedTask;
        }
    }
}
