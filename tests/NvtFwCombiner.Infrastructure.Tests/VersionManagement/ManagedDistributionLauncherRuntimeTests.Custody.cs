using System.IO.Compression;
using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies post-promotion custody and recovery behavior.</summary>
public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    /// <summary>A nested junction introduced during real package extraction cannot redirect a write.</summary>
    [Fact]
    public async Task RealPackageNestedJunctionRaceFailsClosedWithoutExternalWriteOrPromotion()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        string outside = Path.Combine(temporary.Path, "outside");
        _ = Directory.CreateDirectory(outside);
        string sentinel = Path.Combine(outside, "sentinel.txt");
        await File.WriteAllTextAsync(
            sentinel,
            "outside-owner",
            TestContext.Current.CancellationToken);
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        bool junctionCreated = false;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository(),
            stagingCustodyAcquired: null,
            afterPackageDirectoryCreated: createdDirectory =>
            {
                if (!string.Equals(
                        Path.GetFileName(createdDirectory),
                        "external-tools",
                        StringComparison.Ordinal))
                {
                    return;
                }
                try
                {
                    _ = Directory.CreateSymbolicLink(
                        Path.Combine(createdDirectory, "crc-worker"),
                        outside);
                    junctionCreated = true;
                }
                catch (UnauthorizedAccessException exception)
                {
                    Assert.Skip($"Directory-link privilege is unavailable: {exception.Message}");
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

        Assert.True(junctionCreated);
        Assert.Null(result.Installation);
        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.False(Directory.Exists(root));
        Assert.Equal("outside-owner", await File.ReadAllTextAsync(
            sentinel,
            TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(outside, "0.1.0", "Nfc.CrcWorker.exe")));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Terminal verification catches a post-promotion byte change while the marker stays blocking.</summary>
    [Fact]
    public async Task MaterializerRetainsMarkerWhenPostPromotionVerificationDetectsTamper()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var repository = new MaterializingRepository(admission, (inventoryCount, observedRoot) =>
        {
            if (inventoryCount == 1)
            {
                File.WriteAllText(
                    Path.Combine(
                        observedRoot,
                        FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName),
                    "tampered");
            }
        });
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
        Assert.True(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Verified child custody blocks bytes and detects topology mutation at the marker boundary.</summary>
    [Fact]
    public async Task ClosedRootCustodyBlocksContentMutationAndRejectsLateTopologyAtMarkerProofBoundary()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        bool byteMutationBlocked = false;
        bool topologyMutationBlocked = false;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            stagingCustodyAcquired: null,
            closedRootVerified: (verifiedRoot, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.WriteAllText(
                        Path.Combine(
                            verifiedRoot,
                            FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName),
                        "changed");
                }
                catch (IOException)
                {
                    byteMutationBlocked = true;
                }
                try
                {
                    File.WriteAllText(Path.Combine(verifiedRoot, "unexpected.txt"), "foreign");
                }
                catch (IOException)
                {
                    topologyMutationBlocked = true;
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
        Assert.True(byteMutationBlocked);
        Assert.False(topologyMutationBlocked);
        Assert.True(File.Exists(Path.Combine(root, "unexpected.txt")));
        Assert.True(File.Exists(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>A child added after the final marker proof cannot advance the marker phase.</summary>
    [Fact]
    public async Task MarkerPhaseAdvanceRevalidatesAfterAdversarialProofHook()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        bool armed = false;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            stagingCustodyAcquired: null,
            afterMarkerTopologyProof: () =>
            {
                if (armed)
                {
                    File.WriteAllText(Path.Combine(root, "late-marker-child.txt"), "foreign");
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
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        armed = true;

        ManagedFirstInstallationTransactionIssue advanced = await installation
            .RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationTransactionIssue.RecoveryRequired, advanced);
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>A child added after delete proof preserves the exact marker as recovery evidence.</summary>
    [Fact]
    public async Task MarkerDeleteRevalidatesAfterAdversarialProofHook()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        bool armed = false;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            stagingCustodyAcquired: null,
            afterMarkerTopologyProof: () =>
            {
                if (armed)
                {
                    File.WriteAllText(Path.Combine(root, "late-delete-child.txt"), "foreign");
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
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken));
        armed = true;

        ManagedFirstInstallationTransactionIssue completed = await installation
            .CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationTransactionIssue.RecoveryRequired, completed);
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>An ambiguous failure after promotion is recovery, never an ordinary retryable failure.</summary>
    [Fact]
    public async Task PostPromotionContradictionReturnsRecoveryAndRetainsMarker()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            stagingCustodyAcquired: null,
            closedRootVerified: (_, _) => throw new IOException("proof changed"));
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.True(Directory.Exists(root));
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
    }

    /// <summary>Exclusive marker custody blocks replacement while the transaction advances.</summary>
    [Fact]
    public async Task PromotedTransactionBlocksMarkerReplacement()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission));
        using var payload = new TestPayloadCapture(PayloadIdentity());
        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        bool replacementBlocked = false;
        try
        {
            await File.WriteAllTextAsync(marker, "{}", TestContext.Current.CancellationToken);
        }
        catch (IOException)
        {
            replacementBlocked = true;
        }

        ManagedFirstInstallationTransactionIssue recorded = await installation
            .RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken);

        Assert.True(replacementBlocked);
        Assert.Equal(ManagedFirstInstallationTransactionIssue.None, recorded);
        Assert.True(Directory.Exists(root));
    }

    /// <summary>Promoted child custody blocks mutation through the launch-record phase.</summary>
    [Fact]
    public async Task PromotedTransactionBlocksRootFactChangesBeforeLaunchRecord()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission));
        using var payload = new TestPayloadCapture(PayloadIdentity());
        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        bool mutationBlocked = false;
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, FileSystemManagedFirstInstallationRootMaterializer.SeedFileName),
                "{}",
                TestContext.Current.CancellationToken);
        }
        catch (IOException)
        {
            mutationBlocked = true;
        }

        ManagedFirstInstallationTransactionIssue recorded = await installation
            .RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken);

        Assert.True(mutationBlocked);
        Assert.Equal(ManagedFirstInstallationTransactionIssue.None, recorded);
    }

    /// <summary>Completion blocks replacement and deletes only its exact held marker.</summary>
    [Fact]
    public async Task CompletionBlocksReplacementAndDeletesOwnedMarker()
    {
        await using TemporaryRoot temporary = new();
        (ManagedFirstInstallationMaterializationResult result, string marker) =
            await MaterializePromotedInstallationAsync(temporary.Path);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(
                TestContext.Current.CancellationToken));
        bool replacementBlocked = false;
        try
        {
            await File.WriteAllTextAsync(
                marker,
                "{}",
                TestContext.Current.CancellationToken);
        }
        catch (IOException)
        {
            replacementBlocked = true;
        }

        ManagedFirstInstallationTransactionIssue completed = await installation
            .CompleteAsync(TestContext.Current.CancellationToken);

        Assert.True(replacementBlocked);
        Assert.Equal(ManagedFirstInstallationTransactionIssue.None, completed);
        installation.Dispose();
        Assert.False(File.Exists(marker));
    }

    /// <summary>
    /// Promoted custody blocks replacement while its clone-derived executable lease starts Bootstrap.
    /// </summary>
    [Fact]
    public async Task PromotedCustodyCloneStartsWhileRootReplacementBlocked()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using TemporaryRoot temporary = new();
        string bootstrapSource = Environment.GetEnvironmentVariable("ComSpec") ??
            Path.Combine(Environment.SystemDirectory, "cmd.exe");
        ManagedDistributionPayloadIdentity payloadIdentity =
            await PayloadIdentityAsync(bootstrapSource);
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission));
        using var payload = new TestPayloadCapture(
            payloadIdentity,
            bootstrapSource: bootstrapSource);
        string statePath = Path.Combine(temporary.Path, "state", "version-manager.v1.json");
        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            Path.Combine(temporary.Path, "NvtFwCombiner"),
            statePath,
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Issue.ToString());
        IManagedPromotedFirstInstallation installation = Assert.IsType<
            IManagedPromotedFirstInstallation>(result.Installation, exactMatch: false);
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(
                TestContext.Current.CancellationToken));

        string bootstrap = Path.Combine(
            installation.ManagedRoot,
            payloadIdentity.Bootstrap.FileName);
        _ = Assert.Throws<IOException>(() => Directory.Move(
            installation.ManagedRoot,
            installation.ManagedRoot + ".replacement"));

        ManagedExecutableLaunchLeaseResult acquired = await installation
            .AcquireBootstrapLaunchLeaseAsync(
                payloadIdentity.Bootstrap,
                TestContext.Current.CancellationToken);
        Assert.True(acquired.IsAcquired);
        Assert.Equal(ManagedExecutableLaunchIssue.None, acquired.Issue);
        IManagedExecutableLaunchLease? ownedLease = acquired.Lease;
        try
        {
            ManagedExecutableLaunchLeaseResult duplicate = await installation
                .AcquireBootstrapLaunchLeaseAsync(
                    payloadIdentity.Bootstrap,
                    TestContext.Current.CancellationToken);
            Assert.False(duplicate.IsAcquired);
            Assert.Null(duplicate.Lease);
            Assert.Equal(ManagedExecutableLaunchIssue.Unavailable, duplicate.Issue);

            var handoff = new StableLauncherHandoff(installation.ManagedRoot, statePath);
            ImmutableBootstrapStartResult started = await handoff.StartAsync(
                    installation.ManagedRoot,
                    payloadIdentity.Bootstrap,
                    ownedLease!,
                    TestContext.Current.CancellationToken);
            ownedLease = null;
            using IImmutableBootstrapLaunch launch = Assert.IsType<IImmutableBootstrapLaunch>(
                started.Launch,
                exactMatch: false);
            Assert.True(started.IsStarted);
            var budget = new ImmutableBootstrapWaitBudget(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10));
            ImmutableBootstrapAdmissionResult observed = await launch.WaitForAdmissionAsync(
                budget,
                TestContext.Current.CancellationToken);
            Assert.Equal(
                ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
                observed.Outcome);

            _ = Assert.Throws<IOException>(() => Directory.Move(
                installation.ManagedRoot,
                installation.ManagedRoot + ".replacement"));
        }
        finally
        {
            ownedLease?.Dispose();
            installation.Dispose();
        }
    }

    /// <summary>A cancelled first clone duplicate is closed before control returns.</summary>
    [Fact]
    public async Task CloneCancellationClosesFirstPartialDuplicate()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "held-root");
        _ = Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, "payload.bin"),
            "verified-payload",
            TestContext.Current.CancellationToken);
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquirePromotableTree(
            root,
            cancellationToken: TestContext.Current.CancellationToken);
        using WindowsStablePathCustody original = Assert.IsType<WindowsStablePathCustody>(
            acquired.Custody,
            exactMatch: true);
        Assert.True(acquired.IsAcquired);

        using var cancellation = new CancellationTokenSource();
        _ = Assert.Throws<OperationCanceledException>(() => original.TryClone(
            _ => cancellation.Cancel(),
            cancellation.Token));

        original.Dispose();
        WindowsStableCustodyResult reacquired = WindowsStablePathCustody.TryAcquireWritableParent(
            root,
            cancellationToken: TestContext.Current.CancellationToken);
        using WindowsStablePathCustody replacement = Assert.IsType<WindowsStablePathCustody>(
            reacquired.Custody,
            exactMatch: true);
        Assert.True(reacquired.IsAcquired);
    }

    /// <summary>Exclusive transaction custody blocks a competing marker handle before completion.</summary>
    [Fact]
    public async Task CompletionBlocksCompetingMarkerOpen()
    {
        await using TemporaryRoot temporary = new();
        (ManagedFirstInstallationMaterializationResult result, string marker) =
            await MaterializePromotedInstallationAsync(temporary.Path);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(
                TestContext.Current.CancellationToken));

        bool competingBlocked = false;
        try
        {
            await using var competing = new FileStream(
                marker,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }
        catch (IOException)
        {
            competingBlocked = true;
        }
        ManagedFirstInstallationTransactionIssue completed = await installation.CompleteAsync(
            TestContext.Current.CancellationToken);

        Assert.True(competingBlocked);
        Assert.Equal(ManagedFirstInstallationTransactionIssue.None, completed);
        installation.Dispose();
        Assert.False(File.Exists(marker));
    }

    /// <summary>Promoted child custody blocks mutation through terminal marker deletion.</summary>
    [Fact]
    public async Task CompletionBlocksRootFactChangesBeforeDeletingMarker()
    {
        await using TemporaryRoot temporary = new();
        (ManagedFirstInstallationMaterializationResult result, string marker) =
            await MaterializePromotedInstallationAsync(temporary.Path);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(
                TestContext.Current.CancellationToken));
        bool mutationBlocked = false;
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(
                    installation.ManagedRoot,
                    FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName),
                "changed-after-ready",
                TestContext.Current.CancellationToken);
        }
        catch (IOException)
        {
            mutationBlocked = true;
        }

        ManagedFirstInstallationTransactionIssue completed = await installation.CompleteAsync(
            TestContext.Current.CancellationToken);

        Assert.True(mutationBlocked);
        Assert.Equal(ManagedFirstInstallationTransactionIssue.None, completed);
        installation.Dispose();
        Assert.False(File.Exists(marker));
    }

    /// <summary>Terminal root verification retains exclusive custody of the marker it deletes.</summary>
    [Fact]
    public async Task CompletionHoldsExactMarkerWhileRevalidatingRoot()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        bool replacementBlocked = false;
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var repository = new MaterializingRepository(admission, (inventoryCount, _) =>
        {
            if (inventoryCount == 3)
            {
                try
                {
                    File.WriteAllText(marker, "replacement");
                }
                catch (IOException)
                {
                    replacementBlocked = true;
                }
            }
        });
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(repository);
        using var payload = new TestPayloadCapture(PayloadIdentity());
        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(
                TestContext.Current.CancellationToken));

        ManagedFirstInstallationTransactionIssue completed = await installation.CompleteAsync(
            TestContext.Current.CancellationToken);

        Assert.True(replacementBlocked);
        Assert.Equal(ManagedFirstInstallationTransactionIssue.None, completed);
        installation.Dispose();
        Assert.False(File.Exists(marker));
    }

    /// <summary>A pre-promotion failure preserves exact staging and marker recovery evidence.</summary>
    [Fact]
    public async Task PrePromotionFailureRetainsMarkerAndOwnedStaging()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission));
        using var payload = new TestPayloadCapture(PayloadIdentity(), "changed-launcher");

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        ManagedInstallationRootObservation observed = await new FileSystemManagedInstallationRootProbe()
            .ObserveAsync(root, TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationMaterializationIssue.SourceChanged, result.Issue);
        Assert.Equal(ManagedInstallationRootStatus.Residue, observed.Status);
        Assert.True(File.Exists(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)));
        Assert.True(Directory.Exists(
            FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root)));
    }

    private static async Task<Dictionary<string, (long Length, string Sha256)>>
        ReadPackageMembersAsync(string packagePath, string version)
    {
        string prefix = $"NvtFwCombiner-v{version}-win-x64/";
        var members = new Dictionary<string, (long Length, string Sha256)>(StringComparer.Ordinal);
        await using FileStream stream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (ZipArchiveEntry entry in archive.Entries.Where(
                     entry => entry.Length > 0 && entry.FullName.StartsWith(prefix, StringComparison.Ordinal)))
        {
            string relative = entry.FullName[prefix.Length..];
            await using Stream content = entry.Open();
            byte[] digest = await SHA256.HashDataAsync(content, TestContext.Current.CancellationToken);
            members.Add(relative, (entry.Length, Convert.ToHexStringLower(digest)));
        }
        return members;
    }

    private static async Task<Dictionary<string, (long Length, string Sha256)>>
        ReadInstalledMembersAsync(string installedVersion)
    {
        var members = new Dictionary<string, (long Length, string Sha256)>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(installedVersion, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(installedVersion, file).Replace('\\', '/');
            await using FileStream content = File.OpenRead(file);
            byte[] digest = await SHA256.HashDataAsync(content, TestContext.Current.CancellationToken);
            members.Add(relative, (content.Length, Convert.ToHexStringLower(digest)));
        }
        return members;
    }

}
