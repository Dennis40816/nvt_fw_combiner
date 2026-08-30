using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies bounded root observation and one atomic fresh-root transaction.</summary>
public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    private const string HashA =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    /// <summary>Destination admission proves write access without leaving a probe artifact.</summary>
    [Fact]
    public async Task DestinationAdmissionLeavesNoProbeResidue()
    {
        await using TemporaryRoot temporary = new();
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(Admission(candidate)));

        ManagedFirstInstallationMaterializationIssue issue = await materializer
            .AdmitDestinationAsync(
                Path.Combine(temporary.Path, "NvtFwCombiner"),
                TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.None, issue);
        Assert.Empty(Directory.EnumerateFiles(temporary.Path, ".nfc-setup-probe-*.tmp"));
    }

    /// <summary>Admission rejects a destination whose exact parent does not exist.</summary>
    [Fact]
    public async Task DestinationAdmissionRequiresExactExistingParent()
    {
        await using TemporaryRoot temporary = new();
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(Admission(candidate)));

        ManagedFirstInstallationMaterializationIssue issue = await materializer
            .AdmitDestinationAsync(
                Path.Combine(temporary.Path, "missing", "NvtFwCombiner"),
                TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.InvalidDestination, issue);
        Assert.False(Directory.Exists(Path.Combine(temporary.Path, "missing")));
    }

    /// <summary>The final handle-bound admission rejects raced root or Setup residue before source access.</summary>
    [Theory]
    [InlineData("root")]
    [InlineData("marker")]
    [InlineData("staging")]
    public async Task FinalDestinationAdmissionRejectsRacedResidueBeforeSourceAccess(string residue)
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var repository = new MaterializingRepository(admission);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            repository,
            stagingCustodyAcquired: null,
            destinationCustodyAcquired: (managedRoot, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.Equal(root, managedRoot);
                switch (residue)
                {
                    case "root":
                        _ = Directory.CreateDirectory(root);
                        break;
                    case "marker":
                        File.WriteAllText(
                            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root),
                            "foreign");
                        break;
                    case "staging":
                        _ = Directory.CreateDirectory(
                            FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root));
                        break;
                    default:
                        throw new InvalidOperationException("Undefined test residue.");
                }
                return ValueTask.CompletedTask;
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Equal(0, payload.CopyCount);
        Assert.Equal(0, repository.InstallCount);
    }

    /// <summary>The admitted exact parent cannot be renamed before the final handle-bound proof.</summary>
    [Fact]
    public async Task DestinationCustodyBlocksExactParentSwapBeforeSourceAccess()
    {
        await using TemporaryRoot temporary = new();
        string parent = Path.Combine(temporary.Path, "install-parent");
        _ = Directory.CreateDirectory(parent);
        string root = Path.Combine(parent, "NvtFwCombiner");
        string movedParent = Path.Combine(temporary.Path, "moved-parent");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        bool swapBlocked = false;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            stagingCustodyAcquired: null,
            destinationCustodyAcquired: (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    Directory.Move(parent, movedParent);
                }
                catch (IOException)
                {
                    swapBlocked = true;
                }
                return ValueTask.CompletedTask;
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.True(swapBlocked);
        Assert.True(result.IsSuccess, result.Issue.ToString());
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.True(Directory.Exists(root));
        Assert.False(Directory.Exists(movedParent));
    }

    /// <summary>Cancellation after native marker creation releases the transferred file handle.</summary>
    [Fact]
    public async Task MarkerWriteCancellationReleasesNativeHandle()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        using var cancellation = new CancellationTokenSource();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            stagingCustodyAcquired: null,
            destinationCustodyAcquired: (_, _) =>
            {
                cancellation.Cancel();
                return ValueTask.CompletedTask;
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await materializer.MaterializeAsync(
                root,
                Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
                payload,
                candidate,
                ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
                cancellation.Token));

        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        string displaced = marker + ".displaced";
        File.Move(marker, displaced);
        Assert.True(File.Exists(displaced));
    }

    /// <summary>Known marker and staging collisions remain typed recovery evidence.</summary>
    [Fact]
    public async Task HandleBoundCreateCollisionsReturnRecoveryRequired()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        Assert.Equal(
            ManagedFirstInstallationMaterializationIssue.None,
            WindowsManagedSetupPathCustody.TryAcquire(root, out WindowsManagedSetupPathCustody? custody));
        using (custody)
        {
            Assert.Equal(
                ManagedFirstInstallationMaterializationIssue.None,
                custody!.AdmitFreshDestination());
            string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
            File.WriteAllText(marker, "foreign");
            ManagedFirstInstallationMaterializationIssue markerIssue = custody.CreateMarker(
                Path.GetFileName(marker),
                out Microsoft.Win32.SafeHandles.SafeFileHandle markerHandle);
            markerHandle.Dispose();
            File.Delete(marker);
            string staging = FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root);
            _ = Directory.CreateDirectory(staging);
            ManagedFirstInstallationMaterializationIssue stagingIssue = custody.CreateStaging(
                staging,
                Path.Combine(staging, Guid.NewGuid().ToString("N")));

            Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, markerIssue);
            Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, stagingIssue);
        }
    }

    /// <summary>Reserved-name sharing and delete-pending races remain recovery evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservedMarkerHandleRaceReturnsRecoveryRequired(bool deletePending)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        Assert.Equal(
            ManagedFirstInstallationMaterializationIssue.None,
            WindowsManagedSetupPathCustody.TryAcquire(root, out WindowsManagedSetupPathCustody? custody));
        using (custody)
        {
            string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
            using Microsoft.Win32.SafeHandles.SafeFileHandle held = File.OpenHandle(
                marker,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                deletePending ? FileShare.ReadWrite | FileShare.Delete : FileShare.None,
                deletePending ? FileOptions.DeleteOnClose : FileOptions.None);

            Assert.Equal(
                ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
                custody!.AdmitFreshDestination());
        }
    }

    /// <summary>Wrong reserved-entry types are residue, not promotion or destination failures.</summary>
    [Fact]
    public async Task ReservedEntryTypeRaceReturnsRecoveryRequired()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        Assert.Equal(
            ManagedFirstInstallationMaterializationIssue.None,
            WindowsManagedSetupPathCustody.TryAcquire(root, out WindowsManagedSetupPathCustody? custody));
        using (custody)
        {
            string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
            _ = Directory.CreateDirectory(marker);
            ManagedFirstInstallationMaterializationIssue markerIssue = custody!.CreateMarker(
                Path.GetFileName(marker),
                out Microsoft.Win32.SafeHandles.SafeFileHandle markerHandle);
            markerHandle.Dispose();
            Directory.Delete(marker);
            string staging = FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root);
            File.WriteAllText(staging, "foreign");
            ManagedFirstInstallationMaterializationIssue stagingIssue = custody.CreateStaging(
                staging,
                Path.Combine(staging, Guid.NewGuid().ToString("N")));

            Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, markerIssue);
            Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, stagingIssue);
        }
    }

    /// <summary>FILE_RENAME_INFORMATION uses the documented ABI on both supported pointer sizes.</summary>
    [Theory]
    [InlineData(4, 12)]
    [InlineData(8, 20)]
    public void RenameBufferUsesDocumentedFileNameOffset(int pointerSize, int expectedOffset)
    {
        Assert.Equal(expectedOffset, WindowsManagedSetupPathCustody.GetRenameFileNameOffset(pointerSize));
    }

    /// <summary>The probe distinguishes absent, present, and exact Setup residue without recursion.</summary>
    [Fact]
    public async Task RootProbeClassifiesAbsentPresentAndExactResidue()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        await File.WriteAllTextAsync(
            Path.Combine(
                temporary.Path,
                FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName),
            "distribution-media",
            TestContext.Current.CancellationToken);
        var probe = new FileSystemManagedInstallationRootProbe();

        ManagedInstallationRootObservation absent = await probe.ObserveAsync(
            root,
            TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(root);
        ManagedInstallationRootObservation present = await probe.ObserveAsync(
            root,
            TestContext.Current.CancellationToken);
        Directory.Delete(root);
        await File.WriteAllTextAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root),
            "residue",
            TestContext.Current.CancellationToken);
        ManagedInstallationRootObservation residue = await probe.ObserveAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedInstallationRootStatus.Absent, absent.Status);
        Assert.Equal(ManagedInstallationRootStatus.Present, present.Status);
        Assert.Equal(ManagedInstallationRootStatus.Residue, residue.Status);
    }

    /// <summary>Either deterministic Setup residue path blocks fresh Setup without inspecting children.</summary>
    [Fact]
    public async Task RootProbeTreatsStagingContainerAndFileDestinationAsNonAbsent()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        var probe = new FileSystemManagedInstallationRootProbe();
        _ = Directory.CreateDirectory(
            FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root));

        ManagedInstallationRootObservation stagingResidue = await probe.ObserveAsync(
            root,
            TestContext.Current.CancellationToken);
        Directory.Delete(FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root));
        await File.WriteAllTextAsync(root, "foreign", TestContext.Current.CancellationToken);
        ManagedInstallationRootObservation fileDestination = await probe.ObserveAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedInstallationRootStatus.Residue, stagingResidue.Status);
        Assert.Equal(ManagedInstallationRootStatus.InvalidDestination, fileDestination.Status);
    }

    /// <summary>A blocked read-only filesystem observation cannot extend the caller's hard deadline.</summary>
    [Fact]
    public async Task RootProbeCallerCanAbandonBlockedReadOnlyObservation()
    {
        using var release = new ManualResetEventSlim(initialState: false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new FileSystemManagedInstallationRootProbe(_ =>
        {
            entered.SetResult();
            release.Wait();
            return new(ManagedInstallationRootStatus.Present);
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        try
        {
            Task observation = probe.ObserveAsync(
                Path.Combine(Path.GetTempPath(), "blocked-probe"),
                deadline.Token).AsTask();
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await observation);
        }
        finally
        {
            release.Set();
        }
    }

    /// <summary>The embedded source measures, recaptures, and copies only exact admitted bytes.</summary>
    [Fact]
    public async Task EmbeddedPayloadSourceInspectsAndCapturesExactResources()
    {
        await using TemporaryRoot temporary = new();
        string probe = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        string launcher = Path.Combine(temporary.Path, "distribution.exe");
        File.Copy(probe, launcher);
        byte[] bootstrap = await File.ReadAllBytesAsync(
            probe,
            TestContext.Current.CancellationToken);
        byte[] descriptor = PayloadDescriptor(bootstrap);
        var source = new EmbeddedManagedDistributionPayloadSource(
            launcher,
            descriptor,
            bootstrap);

        ManagedDistributionPayloadInspectionResult inspected = await source.InspectAsync(
            TestContext.Current.CancellationToken);
        ManagedDistributionPayloadCaptureResult captured = await source.CaptureExactAsync(
            Assert.IsType<ManagedDistributionPayloadIdentity>(inspected.Identity),
            TestContext.Current.CancellationToken);
        using IManagedDistributionPayloadCapture capture = Assert.IsType<IManagedDistributionPayloadCapture>(
            captured.Capture,
            exactMatch: false);
        IManagedDistributionPayloadContent content = Assert.IsType<IManagedDistributionPayloadContent>(
            capture,
            exactMatch: false);
        string launcherCopy = Path.Combine(temporary.Path, "launcher-copy.exe");
        string bootstrapCopy = Path.Combine(temporary.Path, "bootstrap-copy.exe");
        await content.CopyDistributionLauncherAsync(
            launcherCopy,
            TestContext.Current.CancellationToken);
        await content.CopyBootstrapAsync(
            bootstrapCopy,
            TestContext.Current.CancellationToken);

        Assert.True(inspected.IsSuccess);
        Assert.True(captured.IsSuccess);
        Assert.Equal(
            Hash(await File.ReadAllBytesAsync(launcher, TestContext.Current.CancellationToken)),
            Hash(await File.ReadAllBytesAsync(launcherCopy, TestContext.Current.CancellationToken)));
        Assert.Equal(
            Hash(bootstrap),
            Hash(await File.ReadAllBytesAsync(bootstrapCopy, TestContext.Current.CancellationToken)));
    }

    /// <summary>The healthy entry projects Bootstrap identity without touching either payload binary.</summary>
    [Fact]
    public async Task BootstrapIdentityProjectionUsesDescriptorOnly()
    {
        byte[] declaredBootstrap = "descriptor-only-bootstrap"u8.ToArray();
        var descriptor = new TrackingResource(PayloadDescriptor(declaredBootstrap));
        var bootstrap = new TrackingResource(declaredBootstrap);
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            descriptor.Open,
            bootstrap.Open);

        ManagedDistributionPayloadEntryAdmissionResult projected = await source.AdmitEntryAsync(
            TestContext.Current.CancellationToken);

        Assert.True(projected.IsSuccess);
        ManagedImmutableBootstrapIdentity exact = Assert.IsType<ManagedImmutableBootstrapIdentity>(
            projected.Bootstrap);
        Assert.Equal("NvtFwCombiner.Bootstrap.exe", exact.FileName);
        Assert.Equal(declaredBootstrap.LongLength, exact.Length);
        Assert.Equal(Hash(declaredBootstrap), exact.Sha256);
        Assert.Equal(1, descriptor.OpenCount);
        Assert.Equal(1, bootstrap.OpenCount);
        Assert.Equal(1, bootstrap.LengthReadCount);
        Assert.Equal(0, bootstrap.BytesRead);
    }

    /// <summary>Launcher mutation after review is a typed source change, never a mixed capture.</summary>
    [Fact]
    public async Task EmbeddedPayloadSourceRejectsLauncherChangedAfterInspection()
    {
        await using TemporaryRoot temporary = new();
        string probe = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        string launcher = Path.Combine(temporary.Path, "distribution.exe");
        File.Copy(probe, launcher);
        byte[] bootstrap = await File.ReadAllBytesAsync(
            probe,
            TestContext.Current.CancellationToken);
        var source = new EmbeddedManagedDistributionPayloadSource(
            launcher,
            PayloadDescriptor(bootstrap),
            bootstrap);
        ManagedDistributionPayloadInspectionResult inspected = await source.InspectAsync(
            TestContext.Current.CancellationToken);
        await using (var changed = new FileStream(launcher, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            changed.WriteByte(0xA5);
            await changed.FlushAsync(TestContext.Current.CancellationToken);
        }

        ManagedDistributionPayloadCaptureResult captured = await source.CaptureExactAsync(
            Assert.IsType<ManagedDistributionPayloadIdentity>(inspected.Identity),
            TestContext.Current.CancellationToken);

        Assert.True(inspected.IsSuccess);
        Assert.Null(captured.Capture);
        Assert.Equal(ManagedDistributionPayloadIssue.Changed, captured.Issue);
    }

    /// <summary>One closed staging root is promoted and its marker survives until explicit completion.</summary>
    [Fact]
    public async Task MaterializerPromotesClosedRootAndMarkerLifecycle()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var repository = new MaterializingRepository(admission);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(repository);
        using var payload = new TestPayloadCapture(PayloadIdentity());
        VersionManagerState seed = ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission);

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            seed,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(1, repository.InstallCount);
        Assert.Equal("distribution-launcher", await File.ReadAllTextAsync(
            Path.Combine(root, FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName),
            TestContext.Current.CancellationToken));
        Assert.Equal("immutable-bootstrap", await File.ReadAllTextAsync(
            Path.Combine(root, FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName),
            TestContext.Current.CancellationToken));
        Assert.True(File.Exists(Path.Combine(root, FileSystemManagedFirstInstallationRootMaterializer.SeedFileName)));
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        Assert.True(File.Exists(marker));

        ManagedFirstInstallationTransactionIssue recorded = await installation
            .RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ManagedFirstInstallationTransactionIssue.None, recorded);

        ManagedFirstInstallationTransactionIssue completed = await installation
            .CompleteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ManagedFirstInstallationTransactionIssue.None, completed);
        installation.Dispose();
        Assert.False(File.Exists(marker));
    }

    /// <summary>Repository staging cleanup deletes only the child bound beneath the held staging root.</summary>
    [Fact]
    public async Task RepositoryStagingSwapIsBlockedBeforeHeldChildDeletion()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var repository = new MaterializingRepository(admission, createRepositoryStaging: true);
        bool swapBlocked = false;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            repository,
            stagingCustodyAcquired: null,
            beforeRepositoryStagingDelete: staging =>
            {
                try
                {
                    Directory.Move(staging, staging + ".displaced");
                }
                catch (IOException)
                {
                    swapBlocked = true;
                }
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.True(swapBlocked);
        result.Installation!.Dispose();
    }

    /// <summary>An existing destination is never adopted, merged, or overwritten.</summary>
    [Fact]
    public async Task MaterializerNeverOverwritesExistingRoot()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        _ = Directory.CreateDirectory(root);
        string sentinel = Path.Combine(root, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "owner-data", TestContext.Current.CancellationToken);
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var repository = new MaterializingRepository(admission);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(repository);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Installation);
        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Equal(0, repository.InstallCount);
        Assert.Equal("owner-data", await File.ReadAllTextAsync(
            sentinel,
            TestContext.Current.CancellationToken));
    }

    /// <summary>Stable staging custody prevents a pre-copy directory swap from escaping the root.</summary>
    [Fact]
    public async Task StagingSwapAttemptBeforeFirstCopyCannotWriteOutsideRoot()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        string outside = Path.Combine(temporary.Path, "outside");
        _ = Directory.CreateDirectory(outside);
        string sentinel = Path.Combine(outside, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "outside-owner", TestContext.Current.CancellationToken);
        bool swapBlocked = false;
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            (stagingRoot, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    Directory.Delete(stagingRoot);
                    _ = Directory.CreateSymbolicLink(stagingRoot, outside);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    swapBlocked = true;
                }
                return ValueTask.CompletedTask;
            });
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.True(swapBlocked);
        Assert.Equal("outside-owner", await File.ReadAllTextAsync(
            sentinel,
            TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(
            outside,
            FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName)));
    }

}
