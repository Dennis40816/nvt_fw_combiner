using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies real-package Setup admission and root-custody boundaries.</summary>
public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    /// <summary>A real schema-1.2 ZIP is the independent source of every promoted version byte.</summary>
    [Fact]
    public async Task MaterializerPromotesRealPackageAndMatchesEverySourceArchiveMember()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission expectedAdmission = Admission(candidate);
        var repository = new FileSystemManagedVersionRepository();
        ManagedPackageVerificationResult verified = await repository.VerifyPackageAsync(
            candidate.Identity.SourceRoot,
            candidate.Package,
            TestContext.Current.CancellationToken);
        Assert.True(verified.IsVerified, verified.Issue.ToString());
        Assert.True(verified.HasSupportedManagedLauncher);
        string ordinaryRoot = Path.Combine(temporary.Path, "ordinary-managed");
        _ = Directory.CreateDirectory(ordinaryRoot);
        ManagedVersionInstallResult ordinary = await repository.InstallAsync(
            ordinaryRoot,
            candidate.Identity.SourceRoot,
            candidate.Package,
            TestContext.Current.CancellationToken);
        Assert.True(ordinary.IsSuccess, ordinary.Issue.ToString());
        Assert.Equal(expectedAdmission, ordinary.Admission);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(repository);
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(expectedAdmission),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(expectedAdmission, installation.Admission);
        string installedVersion = Path.Combine(root, "versions", candidate.Package.Version.ToString());
        string packagePath = Path.Combine(
            candidate.Identity.SourceRoot,
            candidate.Package.PackagePath.Value.Replace('/', Path.DirectorySeparatorChar));
        Dictionary<string, (long Length, string Sha256)> expectedMembers =
            await ReadPackageMembersAsync(packagePath, candidate.Package.Version.ToString());
        Dictionary<string, (long Length, string Sha256)> actualMembers =
            await ReadInstalledMembersAsync(installedVersion);
        Dictionary<string, (long Length, string Sha256)> ordinaryMembers =
            await ReadInstalledMembersAsync(Path.Combine(
                ordinaryRoot,
                FileSystemManagedVersionRepository.VersionsDirectoryName,
                candidate.Package.Version.ToString()));
        Assert.True(actualMembers.Remove(FileSystemManagedVersionRepository.AdmissionFileName));
        Assert.True(ordinaryMembers.Remove(FileSystemManagedVersionRepository.AdmissionFileName));
        Assert.Equal(
            expectedMembers.Keys.Order(StringComparer.Ordinal),
            actualMembers.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedMembers.Keys.Order(StringComparer.Ordinal),
            ordinaryMembers.Keys.Order(StringComparer.Ordinal));
        foreach ((string path, (long length, string sha256)) in expectedMembers)
        {
            Assert.Equal((length, sha256), actualMembers[path]);
            Assert.Equal((length, sha256), ordinaryMembers[path]);
        }
        Assert.Equal(
            [
                FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
                FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName,
                FileSystemManagedFirstInstallationRootMaterializer.SeedFileName,
                FileSystemManagedVersionRepository.VersionsDirectoryName,
            ],
            Directory.EnumerateFileSystemEntries(root)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal));
        string marker = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        Assert.True(File.Exists(marker));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.CompleteAsync(TestContext.Current.CancellationToken));
        installation.Dispose();
        Assert.False(File.Exists(marker));
    }

    /// <summary>Setup reserves only its three files, two directories, and exact payload bytes.</summary>
    [Fact]
    public async Task SetupTreeLimitsAdmitExactOverheadAndRejectEveryOneOverDimension()
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "setup-root");
        _ = Directory.CreateDirectory(root);
        byte[] seed = new byte[37];
        await File.WriteAllBytesAsync(
            Path.Combine(root, FileSystemManagedFirstInstallationRootMaterializer.SeedFileName),
            seed,
            TestContext.Current.CancellationToken);
        ManagedDistributionPayloadIdentity payload = PayloadIdentity();

        WindowsStableTreeLimits limits =
            FileSystemManagedFirstInstallationRootMaterializer.CreateSetupTreeLimits(root, payload);
        long expectedBytes = checked(
            FileSystemManagedVersionRepository.MaximumInstalledBytes +
            payload.LauncherSize +
            payload.Bootstrap.Length +
            seed.LongLength);
        Assert.Equal(FileSystemManagedVersionRepository.MaximumInstalledFiles + 3, limits.MaximumFiles);
        Assert.Equal(
            FileSystemManagedVersionRepository.MaximumInstalledDirectories + 2,
            limits.MaximumDirectories);
        Assert.Equal(expectedBytes, limits.MaximumBytes);
        Assert.True(new WindowsStableTreeReservation(
            limits.MaximumFiles,
            limits.MaximumDirectories,
            limits.MaximumBytes,
            limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(
            limits.MaximumFiles + 1,
            limits.MaximumDirectories,
            limits.MaximumBytes,
            limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(
            limits.MaximumFiles,
            limits.MaximumDirectories + 1,
            limits.MaximumBytes,
            limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(
            limits.MaximumFiles,
            limits.MaximumDirectories,
            limits.MaximumBytes + 1,
            limits).IsWithinLimits);
    }

    /// <summary>The exact renamed root stays non-replaceable until closed custody owns the same identity.</summary>
    [Fact]
    public async Task WholeRootPromotionTransfersCustodyWithoutReplacementWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        bool replacementBlocked = false;
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            afterRootPromotion: promotedRoot =>
            {
                try
                {
                    Directory.Move(promotedRoot, promotedRoot + ".replacement");
                }
                catch (IOException)
                {
                    replacementBlocked = true;
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
        Assert.True(replacementBlocked);
        using IManagedPromotedFirstInstallation installation = result.Installation!;
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.RecordBootstrapLaunchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ManagedFirstInstallationTransactionIssue.None,
            await installation.CompleteAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Cancellation during post-promotion capture retains a staging-phase recovery marker.</summary>
    [Fact]
    public async Task PostPromotionCaptureCancellationRetainsStagingPhaseRecoveryEvidence()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        using var cancellation = new CancellationTokenSource();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission),
            afterRootPromotion: _ => cancellation.Cancel());
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            cancellation.Token);

        string markerPath = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        JsonObject marker = JsonNode.Parse(await File.ReadAllTextAsync(
            markerPath,
            TestContext.Current.CancellationToken))!.AsObject();
        Assert.Equal(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, result.Issue);
        Assert.Null(result.Installation);
        Assert.True(Directory.Exists(root));
        Assert.Equal("staging", marker["phase"]!.GetValue<string>());
    }
}
