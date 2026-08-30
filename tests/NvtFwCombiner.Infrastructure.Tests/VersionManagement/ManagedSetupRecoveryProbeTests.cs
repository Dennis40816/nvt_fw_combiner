using System.Diagnostics;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Locks strict bounded marker decoding and stable read-only observation.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed partial class ManagedSetupRecoveryProbeTests
{
    private const uint JobObjectAssignProcess = 0x0001;
    private const uint JobObjectQuery = 0x0004;

    /// <summary>Lifetime diagnosis never creates a missing lease file for any managed role.</summary>
    [Theory]
    [InlineData(ManagedProcessLifetimeKind.Bootstrap)]
    [InlineData(ManagedProcessLifetimeKind.Application)]
    [InlineData(ManagedProcessLifetimeKind.Launcher)]
    public async Task LifetimeProbeReportsExitedWithoutCreatingAuthority(
        ManagedProcessLifetimeKind kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var temporary = new TemporaryDirectory();
        string stateDirectory = Path.Combine(temporary.Path, "state");
        _ = Directory.CreateDirectory(stateDirectory);
        string statePath = Path.Combine(stateDirectory, "version-manager-state.json");
        string leasePath = statePath + LifetimeSuffix(kind);

        ManagedProcessLifetimeStatus status = await new FileSystemManagedProcessLifetimeProbe()
            .ObserveAsync(statePath, kind, TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessLifetimeStatus.Exited, status);
        Assert.False(File.Exists(leasePath));
    }

    /// <summary>Missing ancestry is incomplete health and is never created by observation.</summary>
    [Theory]
    [InlineData(ManagedProcessLifetimeKind.Bootstrap)]
    [InlineData(ManagedProcessLifetimeKind.Application)]
    [InlineData(ManagedProcessLifetimeKind.Launcher)]
    public async Task LifetimeProbeDoesNotCreateMissingAncestry(
        ManagedProcessLifetimeKind kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var temporary = new TemporaryDirectory();
        string missingParent = Path.Combine(temporary.Path, "missing", "state");
        string statePath = Path.Combine(missingParent, "version-manager-state.json");

        ManagedProcessLifetimeStatus status = await new FileSystemManagedProcessLifetimeProbe()
            .ObserveAsync(
                statePath,
                kind,
                TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessLifetimeStatus.Unavailable, status);
        Assert.False(Directory.Exists(missingParent));
    }

    /// <summary>Application and Launcher jobs remain Active even without a lease file.</summary>
    [Theory]
    [InlineData(ManagedProcessLifetimeKind.Application)]
    [InlineData(ManagedProcessLifetimeKind.Launcher)]
    public async Task LifetimeProbeObservesJobOnlyActiveAuthority(
        ManagedProcessLifetimeKind kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var temporary = new TemporaryDirectory();
        string statePath = Path.Combine(temporary.Path, "state", "version-manager-state.json");
        string leasePath = statePath + LifetimeSuffix(kind);
        using Process process = Assert.IsType<Process>(Process.Start(new ProcessStartInfo(
            "cmd.exe",
            "/d /c ping 127.0.0.1 -n 30 >nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        }));
        using ManagedProcessLifetimeLease? lease = ManagedProcessLifetimeLease.TryAcquire(
            statePath,
            kind);
        Assert.NotNull(lease);
        nint rawJob = OpenJobObjectForTest(
            JobObjectAssignProcess | JobObjectQuery,
            inheritHandle: false,
            lease.JobName);
        Assert.NotEqual(nint.Zero, rawJob);
        using var job = new SafeFileHandle(rawJob, ownsHandle: true);
        Assert.True(AssignProcessToJobObjectForTest(job.DangerousGetHandle(), process.Handle));
        lease.Dispose();
        File.Delete(leasePath);

        ManagedProcessLifetimeStatus status = await new FileSystemManagedProcessLifetimeProbe()
            .ObserveAsync(statePath, kind, TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessLifetimeStatus.Active, status);
        process.Refresh();
        Assert.False(process.HasExited);
        job.Dispose();
        Assert.True(process.WaitForExit(5_000));
    }

    /// <summary>An exclusive existing lifetime lease remains Active and becomes Exited after release.</summary>
    [Theory]
    [InlineData(ManagedProcessLifetimeKind.Bootstrap)]
    [InlineData(ManagedProcessLifetimeKind.Application)]
    [InlineData(ManagedProcessLifetimeKind.Launcher)]
    public async Task LifetimeProbeObservesActiveAndReleasedAuthority(
        ManagedProcessLifetimeKind kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var temporary = new TemporaryDirectory();
        string statePath = Path.Combine(temporary.Path, "state", "version-manager-state.json");
        ManagedProcessLifetimeLease? lease = ManagedProcessLifetimeLease.TryAcquire(statePath, kind);
        Assert.NotNull(lease);
        var probe = new FileSystemManagedProcessLifetimeProbe();

        ManagedProcessLifetimeStatus active = await probe.ObserveAsync(
            statePath,
            kind,
            TestContext.Current.CancellationToken);
        lease.Dispose();
        ManagedProcessLifetimeStatus exited = await probe.ObserveAsync(
            statePath,
            kind,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessLifetimeStatus.Active, active);
        Assert.Equal(ManagedProcessLifetimeStatus.Exited, exited);
    }

    /// <summary>Cancellation prevents lifetime path observation.</summary>
    [Fact]
    public async Task LifetimeProbeHonorsCancellationBeforeObservation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var probe = new FileSystemManagedProcessLifetimeProbe();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await probe.ObserveAsync(
                @"C:\state\version-manager-state.json",
                ManagedProcessLifetimeKind.Bootstrap,
                cancellation.Token));
    }

    /// <summary>The canonical writer and reader share one strict codec.</summary>
    [Fact]
    public async Task CodecRoundTripsCanonicalMarkerAndRejectsMalformedOrOversizedBytes()
    {
        ManagedSetupTransactionDocument expected = CreateMarker(@"C:\managed\NVT FW Combiner");
        byte[] bytes = ManagedSetupTransactionCodec.Serialize(expected);

        await using var stream = new MemoryStream(bytes, writable: false);
        ManagedSetupTransactionDocument? actual = await ManagedSetupTransactionCodec.ReadAsync(
            stream,
            TestContext.Current.CancellationToken);

        Assert.NotNull(actual);
        Assert.True(ManagedSetupTransactionDocument.Equivalent(expected, actual));
        Assert.Null(ManagedSetupTransactionCodec.Parse(Encoding.UTF8.GetBytes("{}")));
        Assert.Null(ManagedSetupTransactionCodec.Parse(
            Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace(
                "\"phase\": \"staging\"",
                "\"phase\": \"staging\", \"unknown\": true",
                StringComparison.Ordinal))));
        Assert.Null(ManagedSetupTransactionCodec.Parse(
            Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace(
                "\"phase\": \"staging\"",
                "\"phase\": \"unknown\"",
                StringComparison.Ordinal))));
        Assert.Null(ManagedSetupTransactionCodec.Parse(bytes.AsMemory(0, bytes.Length - 1)));
        Assert.Null(ManagedSetupTransactionCodec.Parse(
            new byte[ManagedSetupTransactionCodec.MaximumDocumentBytes + 1]));
    }

    /// <summary>Canonical marker bytes authored before codec extraction remain compatible.</summary>
    [Fact]
    public void CodecReadsIndependentlyAuthoredPreExtractionMarkerBytes()
    {
        string root = Path.GetFullPath(@"C:\managed\NVT FW Combiner");
        string state = Path.GetFullPath(@"C:\state\version-manager-state.json");
        byte[] bytes = CanonicalPreExtractionMarkerBytes(root, state);

        ManagedSetupTransactionDocument? actual = ManagedSetupTransactionCodec.Parse(bytes);

        Assert.NotNull(actual);
        Assert.True(ManagedSetupTransactionDocument.Equivalent(CreateMarker(root, state), actual));
    }

    /// <summary>An absent exact marker returns Absent without creating any path.</summary>
    [Fact]
    public async Task ProbeReportsAbsentWithoutFilesystemMutation()
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        SortedDictionary<string, string> before = CaptureTree(temporary.Path);

        ManagedSetupRecoveryFact fact = await new FileSystemManagedSetupRecoveryProbe().ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.Absent, fact.Kind);
        Assert.Equal(before, CaptureTree(temporary.Path));
    }

    /// <summary>Missing parent ancestry is unavailable, not a proven marker absence.</summary>
    [Fact]
    public async Task ProbeDoesNotTreatMissingParentAsAbsent()
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "missing-parent", "managed");
        string state = Path.Combine(temporary.Path, "state.json");

        ManagedSetupRecoveryFact fact = await new FileSystemManagedSetupRecoveryProbe().ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.Unavailable, fact.Kind);
        Assert.Null(fact.Transaction);
    }

    /// <summary>A strict marker binds root, state, phase, and the exact owned-path set.</summary>
    [Fact]
    public async Task ProbeReturnsExactTypedTransactionOnlyForEveryBoundIdentity()
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        ManagedSetupTransactionDocument marker = CreateMarker(root, state);
        string path = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        await File.WriteAllBytesAsync(
            path,
            ManagedSetupTransactionCodec.Serialize(marker),
            TestContext.Current.CancellationToken);
        SortedDictionary<string, string> before = CaptureTree(temporary.Path);

        ManagedSetupRecoveryFact fact = await new FileSystemManagedSetupRecoveryProbe().ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.Exact, fact.Kind);
        ManagedSetupRecoveryTransaction transaction = Assert.IsType<ManagedSetupRecoveryTransaction>(
            fact.Transaction);
        Assert.Equal(marker.TransactionId, transaction.TransactionId);
        Assert.Equal(ManagedSetupRecoveryPhase.Staging, transaction.Phase);
        Assert.Equal(marker.OwnedPaths, transaction.OwnedPaths);
        Assert.Equal(marker.Candidate.EntryIdentity, transaction.Candidate.EntryIdentity);
        Assert.Equal(marker.ManagedRootIdentity, transaction.ManagedRootIdentity);
        Assert.Equal(marker.StatePathIdentity, transaction.StatePathIdentity);
        Assert.Equal(
            marker.DistributionLauncherExecutable.Size,
            transaction.Payload.LauncherSize);
        Assert.Equal(
            marker.DistributionLauncherExecutable.Sha256,
            transaction.Payload.LauncherSha256);
        Assert.Equal(marker.PayloadAdmission.DescriptorSize, transaction.Payload.DescriptorSize);
        Assert.Equal(marker.PayloadAdmission.DescriptorSha256, transaction.Payload.DescriptorSha256);
        Assert.Equal(
            marker.PayloadAdmission.BootstrapInstalledFileName,
            transaction.Payload.BootstrapFileName);
        Assert.Equal(marker.PayloadAdmission.BootstrapSize, transaction.Payload.BootstrapSize);
        Assert.Equal(marker.PayloadAdmission.BootstrapSha256, transaction.Payload.BootstrapSha256);
        Assert.Equal(marker.Candidate.RegistryRevision, transaction.Candidate.RegistryRevision);
        Assert.Equal(marker.Candidate.RegistryDigest, transaction.Candidate.RegistryDigest);
        Assert.Equal(marker.Candidate.CatalogSchemaVersion, transaction.Candidate.CatalogSchemaVersion);
        Assert.Equal(marker.Candidate.CatalogLatestVersion, transaction.Candidate.CatalogLatestVersion);
        Assert.Equal(marker.Candidate.CatalogDigest, transaction.Candidate.CatalogDigest);
        Assert.Equal(marker.Candidate.CatalogPath, transaction.Candidate.CatalogPath);
        Assert.Equal(marker.Candidate.RegistryId, transaction.Candidate.RegistryId);
        Assert.Equal(marker.Candidate.SourceRoot, transaction.Candidate.SourceRoot);
        Assert.Equal(marker.Candidate.SourceStatus, transaction.Candidate.SourceStatus);
        Assert.Equal(marker.Candidate.Version, transaction.Candidate.Version);
        Assert.Equal(marker.Candidate.PackagePath, transaction.Candidate.PackagePath);
        Assert.Equal(marker.Candidate.PackageSize, transaction.Candidate.PackageSize);
        Assert.Equal(marker.Candidate.PackageSha256, transaction.Candidate.PackageSha256);
        Assert.Equal(
            marker.Candidate.ReleaseManifestSha256,
            transaction.Candidate.ReleaseManifestSha256);
        Assert.Equal(before, CaptureTree(temporary.Path));
    }

    /// <summary>Every and only schema-declared phase projects to the typed phase.</summary>
    [Theory]
    [InlineData("staging", ManagedSetupRecoveryPhase.Staging)]
    [InlineData("root-promoted", ManagedSetupRecoveryPhase.RootPromoted)]
    [InlineData("bootstrap-launch-recorded", ManagedSetupRecoveryPhase.BootstrapLaunchRecorded)]
    public async Task ProbeProjectsEveryAcceptedPhase(
        string markerPhase,
        ManagedSetupRecoveryPhase expected)
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        ManagedSetupTransactionDocument marker = CreateMarker(root, state) with { Phase = markerPhase };
        await File.WriteAllBytesAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root),
            ManagedSetupTransactionCodec.Serialize(marker),
            TestContext.Current.CancellationToken);

        ManagedSetupRecoveryFact fact = await new FileSystemManagedSetupRecoveryProbe().ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.Exact, fact.Kind);
        Assert.Equal(expected, Assert.IsType<ManagedSetupRecoveryTransaction>(fact.Transaction).Phase);
    }

    /// <summary>The materializer's three durable marker phases remain readable by recovery.</summary>
    [Fact]
    public async Task RecoveryReadsEveryMaterializerProducedMarkerPhase()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        byte[] stagingBytes = await CaptureMaterializerStagingMarkerAsync();
        byte[] promotedBytes = await CaptureMaterializerPromotedMarkerAsync(
            recordBootstrapLaunch: false);
        byte[] launchRecordedBytes = await CaptureMaterializerPromotedMarkerAsync(
            recordBootstrapLaunch: true);

        Assert.Equal(
            ManagedSetupTransactionCodec.StagingPhase,
            Assert.IsType<ManagedSetupTransactionDocument>(
                ManagedSetupTransactionCodec.Parse(stagingBytes)).Phase);
        Assert.Equal(
            ManagedSetupTransactionCodec.RootPromotedPhase,
            Assert.IsType<ManagedSetupTransactionDocument>(
                ManagedSetupTransactionCodec.Parse(promotedBytes)).Phase);
        Assert.Equal(
            ManagedSetupTransactionCodec.BootstrapLaunchRecordedPhase,
            Assert.IsType<ManagedSetupTransactionDocument>(
                ManagedSetupTransactionCodec.Parse(launchRecordedBytes)).Phase);
    }

    /// <summary>A valid foreign marker is never admitted as the caller's transaction.</summary>
    [Fact]
    public async Task ProbeReportsIdentityMismatchForForeignStateOrOwnedPaths()
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        ManagedSetupTransactionDocument marker = CreateMarker(root, state) with
        {
            OwnedPaths = ["managed", "foreign.json"],
        };
        await File.WriteAllBytesAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root),
            ManagedSetupTransactionCodec.Serialize(marker),
            TestContext.Current.CancellationToken);

        ManagedSetupRecoveryFact fact = await new FileSystemManagedSetupRecoveryProbe().ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.IdentityMismatch, fact.Kind);
        Assert.Null(fact.Transaction);
    }

    /// <summary>A schema-valid marker for another root or state is foreign.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProbeRejectsRootOrStateIdentityMismatch(bool mismatchRoot)
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        ManagedSetupTransactionDocument marker = CreateMarker(root, state) with
        {
            ManagedRootIdentity = mismatchRoot
                ? Path.Combine(temporary.Path, "foreign-root")
                : root,
            StatePathIdentity = mismatchRoot
                ? state
                : Path.Combine(temporary.Path, "foreign-state.json"),
        };
        await File.WriteAllBytesAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root),
            ManagedSetupTransactionCodec.Serialize(marker),
            TestContext.Current.CancellationToken);

        ManagedSetupRecoveryFact fact = await new FileSystemManagedSetupRecoveryProbe().ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.IdentityMismatch, fact.Kind);
        Assert.Null(fact.Transaction);
    }

    /// <summary>Present invalid or oversized bytes remain Malformed rather than Absent.</summary>
    [Fact]
    public async Task ProbeReportsMalformedForInvalidOrOversizedMarkerBytes()
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        var probe = new FileSystemManagedSetupRecoveryProbe();
        await File.WriteAllTextAsync(marker, "{}", TestContext.Current.CancellationToken);
        SortedDictionary<string, string> invalidBefore = CaptureTree(temporary.Path);

        ManagedSetupRecoveryFact invalid = await probe.ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.Malformed, invalid.Kind);
        Assert.Equal(invalidBefore, CaptureTree(temporary.Path));
        await File.WriteAllBytesAsync(
            marker,
            new byte[ManagedSetupTransactionCodec.MaximumDocumentBytes + 1],
            TestContext.Current.CancellationToken);
        SortedDictionary<string, string> oversizedBefore = CaptureTree(temporary.Path);
        ManagedSetupRecoveryFact oversized = await probe.ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);
        Assert.Equal(ManagedSetupRecoveryFactKind.Malformed, oversized.Kind);
        Assert.Equal(oversizedBefore, CaptureTree(temporary.Path));
    }

    /// <summary>Reparse markers are rejected without following their target.</summary>
    [Fact]
    public async Task ProbeRejectsReparseMarkerWithoutReadingTarget()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        string target = Path.Combine(temporary.Path, "target.json");
        await File.WriteAllBytesAsync(
            target,
            ManagedSetupTransactionCodec.Serialize(CreateMarker(root, state)),
            TestContext.Current.CancellationToken);
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        _ = File.CreateSymbolicLink(marker, target);

        ManagedSetupRecoveryFact fact = await new FileSystemManagedSetupRecoveryProbe().ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.IdentityMismatch, fact.Kind);
    }

    /// <summary>Custody failures remain typed and cancellation is not swallowed.</summary>
    [Theory]
    [InlineData((int)WindowsStableCustodyIssue.AccessDenied, ManagedSetupRecoveryFactKind.AccessDenied)]
    [InlineData((int)WindowsStableCustodyIssue.Changed, ManagedSetupRecoveryFactKind.Changed)]
    [InlineData((int)WindowsStableCustodyIssue.Contended, ManagedSetupRecoveryFactKind.Unavailable)]
    [InlineData((int)WindowsStableCustodyIssue.Unavailable, ManagedSetupRecoveryFactKind.Unavailable)]
    public async Task ProbeMapsStableCustodyFailures(
        int issueValue,
        ManagedSetupRecoveryFactKind expected)
    {
        WindowsStableCustodyIssue issue = (WindowsStableCustodyIssue)issueValue;
        var probe = new FileSystemManagedSetupRecoveryProbe(
            (_, _) => WindowsStableCustodyResult.Failure(issue),
            _ => true);

        ManagedSetupRecoveryFact fact = await probe.ObserveAsync(
            @"C:\managed\root",
            @"C:\state\state.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, fact.Kind);
    }

    /// <summary>A failed held-identity revalidation is Changed, never Exact.</summary>
    [Fact]
    public async Task ProbeReportsChangedWhenPostReadCustodyRevalidationFails()
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        await File.WriteAllBytesAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root),
            ManagedSetupTransactionCodec.Serialize(CreateMarker(root, state)),
            TestContext.Current.CancellationToken);
        var probe = new FileSystemManagedSetupRecoveryProbe(
            static (path, cancellationToken) => WindowsStablePathCustody.TryAcquireFile(
                path,
                cancellationToken: cancellationToken),
            _ => false);

        ManagedSetupRecoveryFact fact = await probe.ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.Changed, fact.Kind);
        Assert.Null(fact.Transaction);
    }

    /// <summary>Held marker custody blocks same-name mutation, rename, and deletion.</summary>
    [Fact]
    public async Task ProbeCustodyBlocksSameNameWriteRenameAndDelete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        string markerPath = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        await File.WriteAllBytesAsync(
            markerPath,
            ManagedSetupTransactionCodec.Serialize(CreateMarker(root, state)),
            TestContext.Current.CancellationToken);
        SortedDictionary<string, string> before = CaptureTree(temporary.Path);
        var blocked = new List<string>();
        var probe = new FileSystemManagedSetupRecoveryProbe(
            static (path, cancellationToken) => WindowsStablePathCustody.TryAcquireFile(
                path,
                cancellationToken: cancellationToken),
            _ =>
            {
                RecordBlocked("write", () => File.WriteAllText(markerPath, "replacement"));
                RecordBlocked("rename", () => File.Move(markerPath, markerPath + ".moved"));
                RecordBlocked("delete", () => File.Delete(markerPath));
                return true;
            });

        ManagedSetupRecoveryFact fact = await probe.ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryFactKind.Exact, fact.Kind);
        Assert.Equal(["write", "rename", "delete"], blocked);
        Assert.Equal(before, CaptureTree(temporary.Path));
        return;

        void RecordBlocked(string operation, Action attack)
        {
            try
            {
                attack();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                blocked.Add(operation);
            }
        }
    }

    /// <summary>A held-file removal/replacement race is blocked or observed as Changed.</summary>
    [Fact]
    public async Task ProbeNeverAdmitsHeldFileRemovalReplacementRaceAsExactReplacement()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var temporary = new TemporaryDirectory();
        string root = Path.Combine(temporary.Path, "managed");
        string state = Path.Combine(temporary.Path, "state.json");
        string markerPath = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        string displacedPath = markerPath + ".displaced";
        await File.WriteAllBytesAsync(
            markerPath,
            ManagedSetupTransactionCodec.Serialize(CreateMarker(root, state)),
            TestContext.Current.CancellationToken);
        SortedDictionary<string, string> before = CaptureTree(temporary.Path);
        bool replacementAttempted = false;
        bool replacementCommitted = false;
        var probe = new FileSystemManagedSetupRecoveryProbe(
            (path, cancellationToken) => WindowsStablePathCustody.TryAcquireFile(
                path,
                stage =>
                {
                    if (stage != WindowsStableCustodyStage.AfterTreeCaptured)
                    {
                        return;
                    }
                    replacementAttempted = true;
                    try
                    {
                        File.Move(markerPath, displacedPath);
                        File.WriteAllText(markerPath, "replacement");
                        replacementCommitted = true;
                    }
                    catch (Exception exception) when (exception is
                        IOException or UnauthorizedAccessException)
                    {
                    }
                },
                cancellationToken),
            static custody => custody.RevalidateClosedTree());

        ManagedSetupRecoveryFact fact = await probe.ObserveAsync(
            root,
            state,
            TestContext.Current.CancellationToken);

        Assert.True(replacementAttempted);
        Assert.True(
            !replacementCommitted || fact.Kind == ManagedSetupRecoveryFactKind.Changed,
            $"A committed replacement returned {fact.Kind}.");
        if (File.Exists(displacedPath))
        {
            File.Delete(markerPath);
            File.Move(displacedPath, markerPath);
        }
        Assert.Equal(before, CaptureTree(temporary.Path));
    }

    /// <summary>Cancellation stops before any filesystem observation.</summary>
    [Fact]
    public async Task ProbeHonorsCancellationBeforeObservation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var probe = new FileSystemManagedSetupRecoveryProbe();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await probe.ObserveAsync(@"C:\managed\root", @"C:\state\state.json", cancellation.Token));
    }
}
