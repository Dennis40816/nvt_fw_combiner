using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class FirmwareInspectionSnapshotTests
{
    /// <summary>The Infrastructure inspection port owns stable file identity and later lease checks.</summary>
    [Fact]
    public async Task InspectionPortReturnsContentIdentityAndDetectsSameLengthReplacement()
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
        FirmwareConfigMetadataReadResult metadata =
            await inspection.ReadFirmwareConfigMetadataAsync(
                "NT51926",
                path,
                TestContext.Current.CancellationToken);

        Assert.True(batch.IsContentStable);
        Assert.Equal(
            [new AuthoringInspectionProgress(0, 1), new AuthoringInspectionProgress(1, 1)],
            progress.Updates);
        Assert.True(metadata.IsContentStable);
        FileStamp stamp = Assert.IsType<FileStamp>(metadata.FileStamp);
        Assert.Equal(stamp, batch.FileStamps[path]);
        Assert.Equal(0x40000, stamp.AcceptedLength);
        Assert.True(await inspection.IsFirmwareContentCurrentAsync(
            path,
            stamp,
            TestContext.Current.CancellationToken));

        byte[] replacement = new byte[original.Length];
        replacement[^1] = 2;
        await File.WriteAllBytesAsync(
            path,
            replacement,
            TestContext.Current.CancellationToken);

        Assert.False(await inspection.IsFirmwareContentCurrentAsync(
            path,
            stamp,
            TestContext.Current.CancellationToken));
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
                Assert.Equal(int.MaxValue, maximumBytes);
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

    private static BuiltInFirmwareInspection CreateInspection(
        ISelectedFileContentInspector contentInspector)
    {
        CompositionHostServices services = BootstrapTestHost.Services;
        return new BuiltInFirmwareInspection(
            BootstrapTestHost.Canonical.Catalog,
            BootstrapTestHost.Canonical.Projection,
            (StandardMergeAuthoringExperience)services.StandardMergeAuthoring,
            (AbMergeAuthoringExperience)services.AbMergeAuthoring,
            (DpReplaceAuthoringExperience)services.DpReplaceAuthoring,
            (CtrlRamAuthoringExperience)services.CtrlRamAuthoring,
            contentInspector);
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
