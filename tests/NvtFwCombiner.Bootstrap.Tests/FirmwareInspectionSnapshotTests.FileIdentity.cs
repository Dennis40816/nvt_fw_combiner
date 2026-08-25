using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class FirmwareInspectionSnapshotTests
{
    /// <summary>The inspection batch returns one immutable identity and never tracks later path mutation.</summary>
    [Fact]
    public async Task InspectionBatchIdentityRemainsAcceptedAfterSameLengthDiskReplacement()
    {
        using var workspace = TempWorkspace.Create("nfc-firmware-inspection-identity");
        byte[] original = new byte[0x40000];
        original[0] = 1;
        string path = workspace.Write("firmware.bin", original);
        IFirmwareInspection inspection = BootstrapTestHost.Services.FirmwareInspectionExperience;
        var progress = new RecordingAuthoringInspectionProgress();

        FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput("base", path)],
            TestContext.Current.CancellationToken,
            progress);
        Assert.True(batch.IsContentStable);
        Assert.Equal(
            [new AuthoringInspectionProgress(0, 1), new AuthoringInspectionProgress(1, 1)],
            progress.Updates);
        FileStamp stamp = Assert.IsType<FileStamp>(batch.FileStamps[path]);
        Assert.Equal(0x40000, stamp.AcceptedLength);
        Assert.Equal(stamp, batch.InspectionsById["base"].FileStamp);

        byte[] replacement = new byte[original.Length];
        replacement[^1] = 2;
        await File.WriteAllBytesAsync(
            path,
            replacement,
            TestContext.Current.CancellationToken);

        Assert.Equal(stamp, batch.FileStamps[path]);
        Assert.Equal(stamp, batch.InspectionsById["base"].FileStamp);
        Assert.NotEqual(stamp, FileStamp.FromBytes(replacement));
    }

    /// <summary>A changing source is reported distinctly and cannot publish a content stamp.</summary>
    [Fact]
    public async Task InspectionPortReportsChangeDuringTheCoherentRead()
    {
        BuiltInFirmwareInspection inspection = CreateInspection(
            new DelegatingContentInspector((_, _, _) =>
                ValueTask.FromException<SelectedFileContentInspection>(
                    new SelectedFileChangedDuringInspectionException())));

        FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput("base", "changing.bin")],
            TestContext.Current.CancellationToken);

        Assert.Null(batch.FileStamps["changing.bin"]);
        Assert.Contains("changing.bin", batch.UnstableFilePaths);
        Assert.False(batch.IsContentStable);
        Assert.Null(batch.InspectionsById["base"].FileStamp);
    }

    /// <summary>Cancellation stops the adapter read without producing an unavailable result.</summary>
    [Fact]
    public async Task InspectionPortPropagatesCooperativeCancellation()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        BuiltInFirmwareInspection inspection = CreateInspection(
            new DelegatingContentInspector(async (_, _, token) =>
            {
                _ = entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Cancellation should stop the active read.");
            }));
        using var cancellation = new CancellationTokenSource();
        ValueTask<FirmwareInspectionBatchResult> pending = inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput("base", "cancelled.bin")],
            cancellation.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(pending.AsTask);
    }

    /// <summary>Unreadable input remains distinct from a changing source and publishes no stamp.</summary>
    [Fact]
    public async Task InspectionPortReportsUnreadableSourceWithoutInventingIdentity()
    {
        BuiltInFirmwareInspection inspection = CreateInspection(
            new DelegatingContentInspector((_, _, _) =>
                ValueTask.FromException<SelectedFileContentInspection>(
                    new FileNotFoundException())));

        FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput("base", "missing.bin")],
            TestContext.Current.CancellationToken);

        Assert.Null(batch.FileStamps["missing.bin"]);
        Assert.DoesNotContain("missing.bin", batch.UnstableFilePaths);
        Assert.True(batch.IsContentStable);
        Assert.Null(batch.InspectionsById["base"].FileStamp);
    }

    /// <summary>A malformed locator remains a stable unavailable input instead of faulting the whole batch.</summary>
    [Fact]
    public async Task InspectionPortReportsMalformedPathWithoutCallingContentAdapter()
    {
        const string malformedPath = "malformed\0firmware.bin";
        int reads = 0;
        BuiltInFirmwareInspection inspection = CreateInspection(
            new DelegatingContentInspector((_, _, _) =>
            {
                reads++;
                throw new InvalidOperationException("Malformed paths must not reach the content adapter.");
            }));
        var progress = new RecordingAuthoringInspectionProgress();

        FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput("base", malformedPath)],
            TestContext.Current.CancellationToken,
            progress);

        Assert.Equal(0, reads);
        Assert.True(batch.IsContentStable);
        Assert.Null(batch.FileStamps[malformedPath]);
        Assert.Null(batch.InspectionsById["base"].FileStamp);
        Assert.Equal(
            [new AuthoringInspectionProgress(0, 1), new AuthoringInspectionProgress(1, 1)],
            progress.Updates);
    }

    /// <summary>Concurrent reads of one path retain request-scoped bytes and identities.</summary>
    [Fact]
    public async Task ConcurrentInspectionRequestsKeepContentIdentityRequestScoped()
    {
        byte[] firstBytes = [1, 2, 3, 4];
        byte[] secondBytes = [4, 3, 2, 1];
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int invocation = 0;
        BuiltInFirmwareInspection inspection = CreateInspection(
            new DelegatingContentInspector(async (path, maximumBytes, token) =>
            {
                Assert.Equal(100_000_000, maximumBytes);
                int current = Interlocked.Increment(ref invocation);
                byte[] bytes = current == 1 ? firstBytes : secondBytes;
                if (current == 1)
                {
                    _ = firstStarted.TrySetResult();
                    await releaseFirst.Task.WaitAsync(token);
                }

                return new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes),
                    Path.GetFileName(path),
                    acceptedBytes: bytes);
            }));

        ValueTask<FirmwareInspectionBatchResult> first = inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput("base", "shared.bin")],
            TestContext.Current.CancellationToken);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        FirmwareInspectionBatchResult second = await inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput("base", "shared.bin")],
            TestContext.Current.CancellationToken);
        _ = releaseFirst.TrySetResult();
        FirmwareInspectionBatchResult firstResult = await first;

        Assert.Equal(
            FileStamp.FromBytes(firstBytes),
            firstResult.FileStamps["shared.bin"]);
        Assert.Equal(
            FileStamp.FromBytes(secondBytes),
            second.FileStamps["shared.bin"]);
        Assert.NotEqual(
            firstResult.InspectionsById["base"].FileStamp,
            second.InspectionsById["base"].FileStamp);
    }

    /// <summary>Every fixed-workflow typed binding reaches the shared compiled ceiling owner.</summary>
    [Theory]
    [InlineData("standard")]
    [InlineData("ab")]
    [InlineData("dp-replace")]
    [InlineData("ctrlram-replace")]
    public async Task FixedWorkflowBindingFieldsReuseTheCompiledReadCeiling(string workflow)
    {
        IsolatedBootstrapTestHost host = CreateLoadedHost();
        ResolvedCapability capability = DpReplaceCapability(host, includeLdc: false);
        long observedMaximum = 0;
        BuiltInFirmwareInspection inspection = CreateInspection(
            host,
            new DelegatingContentInspector((_, maximumBytes, _) =>
            {
                observedMaximum = maximumBytes;
                return ValueTask.FromException<SelectedFileContentInspection>(
                    new ResourceCeilingObservedException());
            }));
        FirmwareInspectionSnapshotInput input = workflow switch
        {
            "standard" => new(
                "input",
                "input.bin",
                StandardMergeAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                ExactCapability: capability),
            "ab" => new(
                "input",
                "input.bin",
                AbMergeAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                ExactCapability: capability),
            "dp-replace" => new(
                "input",
                "input.bin",
                DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                ExactCapability: capability),
            "ctrlram-replace" => new(
                "input",
                "input.bin",
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                ExactCapability: capability),
            _ => throw new ArgumentOutOfRangeException(nameof(workflow)),
        };

        _ = await Assert.ThrowsAsync<ResourceCeilingObservedException>(() =>
            inspection.InspectFirmwareBatchAsync(
                "NT51926",
                [input],
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(0x40000, observedMaximum);
    }

    /// <summary>CtrlRAM base discovery performs one hard-bounded read before an exact route exists.</summary>
    [Fact]
    public async Task CtrlRamBaseDiscoveryUsesTheSharedHardCeilingOnce()
    {
        int reads = 0;
        long observedMaximum = 0;
        BuiltInFirmwareInspection inspection = CreateInspection(
            new DelegatingContentInspector((_, maximumBytes, _) =>
            {
                reads++;
                observedMaximum = maximumBytes;
                return ValueTask.FromException<SelectedFileContentInspection>(
                    new ResourceCeilingObservedException());
            }));

        _ = await Assert.ThrowsAsync<ResourceCeilingObservedException>(() =>
            inspection.InspectFirmwareBatchAsync(
                "NT51950",
                [new FirmwareInspectionSnapshotInput(
                    "base",
                    "base.bin",
                    CtrlRamRequest: new CtrlRamInspectionRequest(
                        IcNumberSelectionTokens.SingleChip),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(1, reads);
        Assert.Equal(
            CompiledInputArtifactInspectionService.MaximumContentReadBytes,
            observedMaximum);
    }

    /// <summary>A metadata-only TP dependency does not inherit the DP binding's narrower limit.</summary>
    [Fact]
    public async Task TpMetadataDependencyKeepsTheHardCeilingWithoutItsOwnTypedBinding()
    {
        IsolatedBootstrapTestHost host = CreateLoadedHost();
        ResolvedCapability capability = DpReplaceCapability(host, includeLdc: false);
        var maxima = new Dictionary<string, long>(StringComparer.Ordinal);
        BuiltInFirmwareInspection inspection = CreateInspection(
            host,
            new DelegatingContentInspector((path, maximumBytes, _) =>
            {
                maxima.Add(path, maximumBytes);
                byte[] bytes = [1];
                return ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes),
                    acceptedBytes: bytes));
            }));

        _ = await inspection.InspectFirmwareBatchAsync(
            "NT51928",
            [new FirmwareInspectionSnapshotInput(
                "dp",
                "dp.bin",
                TpPath: "tp-metadata.bin",
                DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                ExactCapability: capability)],
            TestContext.Current.CancellationToken);

        Assert.Equal(0x40000, maxima["dp.bin"]);
        Assert.Equal(100_000_000, maxima["tp-metadata.bin"]);
    }

    /// <summary>Multiple typed bindings on one path are read once at their minimum ceiling.</summary>
    [Fact]
    public async Task SharedPathUsesTheMostRestrictiveTypedBindingCeilingOnce()
    {
        IsolatedBootstrapTestHost host = CreateLoadedHost();
        ResolvedCapability largerCapability = DpReplaceCapability(host, includeLdc: true);
        ResolvedCapability smallerCapability = DpReplaceCapability(host, includeLdc: false);
        int reads = 0;
        long observedMaximum = 0;
        BuiltInFirmwareInspection inspection = CreateInspection(
            host,
            new DelegatingContentInspector((_, maximumBytes, _) =>
            {
                reads++;
                observedMaximum = maximumBytes;
                byte[] bytes = [1];
                return ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes),
                    acceptedBytes: bytes));
            }));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inspection.InspectFirmwareBatchAsync(
                "NT51928",
                [
                    new FirmwareInspectionSnapshotInput(
                        "larger-reference",
                        "shared.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                        ExactCapability: largerCapability),
                    new FirmwareInspectionSnapshotInput(
                        "smaller-reference",
                        "shared.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                        ExactCapability: smallerCapability),
                ],
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(1, reads);
        Assert.Equal(0x40000, observedMaximum);
    }

    /// <summary>Windows path casing aliases one physical source without losing either logical result key.</summary>
    [Fact]
    public async Task WindowsCaseAliasesReadOnceAtTheStrictestBindingCeiling()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IsolatedBootstrapTestHost host = CreateLoadedHost();
        ResolvedCapability capability = DpReplaceCapability(host, includeLdc: false);
        string mixedCasePath = Path.Combine(
            Path.GetTempPath(),
            "Nfc-Inspection-Case-Alias",
            "Shared.bin");
        string upperCasePath = mixedCasePath.ToUpperInvariant();
        Assert.NotEqual(mixedCasePath, upperCasePath, StringComparer.Ordinal);
        int reads = 0;
        long observedMaximum = 0;
        byte[] bytes = [1];
        BuiltInFirmwareInspection inspection = CreateInspection(
            host,
            new DelegatingContentInspector((_, maximumBytes, _) =>
            {
                reads++;
                observedMaximum = maximumBytes;
                return ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes),
                    acceptedBytes: bytes));
            }));

        FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync(
            "NT51928",
            [
                new FirmwareInspectionSnapshotInput(
                    "bounded",
                    mixedCasePath,
                    DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                    ExactCapability: capability),
                new FirmwareInspectionSnapshotInput("alias", upperCasePath),
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, reads);
        Assert.Equal(0x40000, observedMaximum);
        Assert.Equal(FileStamp.FromBytes(bytes), batch.FileStamps[mixedCasePath]);
        Assert.Equal(batch.FileStamps[mixedCasePath], batch.FileStamps[upperCasePath]);
    }

    /// <summary>Relative and absolute aliases share one bounded read while retaining both logical result keys.</summary>
    [Fact]
    public async Task RelativeAndAbsoluteAliasesReadOnceAtTheStrictestBindingCeiling()
    {
        IsolatedBootstrapTestHost host = CreateLoadedHost();
        ResolvedCapability capability = DpReplaceCapability(host, includeLdc: false);
        string relativePath = Path.Combine(
            "nfc-inspection-relative-alias",
            "shared.bin");
        string absolutePath = Path.GetFullPath(relativePath);
        int reads = 0;
        long observedMaximum = 0;
        byte[] bytes = [1];
        BuiltInFirmwareInspection inspection = CreateInspection(
            host,
            new DelegatingContentInspector((_, maximumBytes, _) =>
            {
                reads++;
                observedMaximum = maximumBytes;
                return ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes),
                    acceptedBytes: bytes));
            }));

        FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync(
            "NT51928",
            [
                new FirmwareInspectionSnapshotInput(
                    "bounded",
                    relativePath,
                    DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase,
                    ExactCapability: capability),
                new FirmwareInspectionSnapshotInput("alias", absolutePath),
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, reads);
        Assert.Equal(0x40000, observedMaximum);
        Assert.Equal(FileStamp.FromBytes(bytes), batch.FileStamps[relativePath]);
        Assert.Equal(batch.FileStamps[relativePath], batch.FileStamps[absolutePath]);
    }

    private static BuiltInFirmwareInspection CreateInspection(
        ISelectedFileContentInspector contentInspector)
    {
        CompositionHostServices services = BootstrapTestHost.Services;
        return new BuiltInFirmwareInspection(
            BootstrapTestHost.Canonical.Catalog,
            new FirmwareMetadataPlanAuthorityResolver(
                BootstrapTestHost.Canonical.Catalog),
            BootstrapTestHost.Canonical.Projection,
            (StandardMergeAuthoringExperience)services.StandardMergeAuthoring,
            (AbMergeAuthoringExperience)services.AbMergeAuthoring,
            (DpReplaceAuthoringExperience)services.DpReplaceAuthoring,
            (CtrlRamAuthoringExperience)services.CtrlRamAuthoring,
            contentInspector);
    }

    private static BuiltInFirmwareInspection CreateInspection(
        IsolatedBootstrapTestHost host,
        ISelectedFileContentInspector contentInspector)
    {
        return new BuiltInFirmwareInspection(
            host.Canonical.Catalog,
            new FirmwareMetadataPlanAuthorityResolver(host.Canonical.Catalog),
            host.Canonical.Projection,
            (StandardMergeAuthoringExperience)host.Services.StandardMergeAuthoring,
            (AbMergeAuthoringExperience)host.Services.AbMergeAuthoring,
            (DpReplaceAuthoringExperience)host.Services.DpReplaceAuthoring,
            (CtrlRamAuthoringExperience)host.Services.CtrlRamAuthoring,
            contentInspector);
    }

    private static IsolatedBootstrapTestHost CreateLoadedHost()
    {
        var host = new IsolatedBootstrapTestHost();
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        return host;
    }

    private static ResolvedCapability DpReplaceCapability(
        IsolatedBootstrapTestHost host,
        bool includeLdc)
    {
        string[] selected = includeLdc
            ?
            [
                CompositionAddressSpaceIds.ReferenceBase,
                CompositionAddressSpaceIds.InitialCodeReplacement,
                CompositionAddressSpaceIds.LdcReplacement,
            ]
            :
            [
                CompositionAddressSpaceIds.ReferenceBase,
                CompositionAddressSpaceIds.InitialCodeReplacement,
            ];
        CompiledAuthoringSelectionSnapshot snapshot = host.Services.DpReplaceAuthoring
            .GetAuthoringSnapshot(
                "NT51928",
                selected,
                new Dictionary<string, FileStamp>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.ReferenceBase] =
                        new FileStamp(includeLdc ? 0x80000 : 0x40000, new string('a', 64)),
                },
                new AuthoringRevision(1));
        return Assert.IsType<ResolvedCapability>(Assert.Single(snapshot.Catalog.Routes).ExactCapability);
    }

    private sealed class ResourceCeilingObservedException : Exception
    {
    }

    private sealed class DelegatingContentInspector(
        Func<string, long, CancellationToken, ValueTask<SelectedFileContentInspection>> inspect)
        : ISelectedFileContentInspector
    {
        public ValueTask<SelectedFileContentInspection> InspectAsync(
            string selectedPath,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            return inspect(selectedPath, maximumBytes, cancellationToken);
        }
    }
}
