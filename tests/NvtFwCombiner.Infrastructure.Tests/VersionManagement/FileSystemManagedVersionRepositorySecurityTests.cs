using System.IO.Compression;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class FileSystemManagedVersionRepositoryTests
{
    /// <summary>Every escaping or non-portable ZIP path fails before a package can appear Verified.</summary>
    [Theory]
    [InlineData("/absolute.txt")]
    [InlineData("wrong-root/payload.txt")]
    [InlineData("{root}\\backslash.txt")]
    [InlineData("{root}/payload:stream")]
    [InlineData("{root}/folder//payload.txt")]
    [InlineData("{root}/folder/../payload.txt")]
    [InlineData("{root}/folder./payload.txt")]
    public async Task UnsafeArchivePathShapesNeverVerify(string entryPattern)
    {
        ArgumentNullException.ThrowIfNull(entryPattern);
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        const string root = "NvtFwCombiner-v0.10.6-win-x64";
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            entryPattern.Replace("{root}", root, StringComparison.Ordinal));

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.UnsafeArchive, result.Issue);
    }

    /// <summary>Duplicate and link-shaped archive members fail before extraction.</summary>
    [Theory]
    [InlineData("duplicate")]
    [InlineData("link")]
    public async Task DuplicateAndLinkArchiveMembersNeverVerify(string shape)
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            mutateArchive: (archive, root) =>
            {
                ZipArchiveEntry entry = archive.CreateEntry(
                    shape == "duplicate" ? $"{root}/readme.TXT" : $"{root}/link.txt");
                if (shape == "link")
                {
                    entry.ExternalAttributes = 0xA000 << 16;
                }
                using Stream output = entry.Open();
                output.WriteByte(1);
            });

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.UnsafeArchive, result.Issue);
    }

    /// <summary>The archive-entry ceiling is enforced before any undeclared member can be materialized.</summary>
    [Fact]
    public async Task ArchiveEntryCountCeilingFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            mutateArchive: (archive, root) =>
            {
                const int fixedEntries = 6;
                for (int index = fixedEntries; index <= FileSystemManagedVersionRepository.MaximumArchiveEntries; index++)
                {
                    _ = archive.CreateEntry($"{root}/extra-{index:D4}.txt");
                }
            });

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.UnsafeArchive, result.Issue);
    }

    /// <summary>The declared expanded-byte ceiling is enforced from ZIP metadata before extraction.</summary>
    [Fact]
    public async Task ExpandedSizeCeilingFailsBeforeExtraction()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            mutateArchive: static (archive, root) =>
            {
                ZipArchiveEntry entry = archive.CreateEntry($"{root}/oversized.txt");
                using Stream output = entry.Open();
                output.WriteByte(1);
            },
            mutatePackage: static packagePath =>
            {
                byte[] bytes = File.ReadAllBytes(packagePath);
                ReadOnlySpan<byte> centralSignature = [0x50, 0x4b, 0x01, 0x02];
                int central = bytes.AsSpan().LastIndexOf(centralSignature);
                Assert.True(central >= 0);
                uint oversized = checked((uint)(FileSystemManagedVersionRepository.MaximumExpandedBytes + 1));
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(central + 24, sizeof(uint)),
                    oversized);
                File.WriteAllBytes(packagePath, bytes);
            });

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.UnsafeArchive, result.Issue);
    }

    /// <summary>Actual decompressed bytes, not a declared length hint, stop an entry immediately.</summary>
    [Fact]
    public async Task ActualEntryBytesCannotExceedDeclaredLength()
    {
        await using var source = new MemoryStream(new byte[1024]);
        var budget = new ExpandedByteBudget(maximumBytes: 2048);

        BoundedArchiveReadResult result = await BoundedArchiveReader.ReadAndHashAsync(
            source,
            declaredLength: 8,
            budget,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(BoundedArchiveReadIssue.EntryLengthExceeded, result.Issue);
        Assert.Equal(9, budget.ConsumedBytes);
        Assert.Equal(9, source.Position);
    }

    /// <summary>The aggregate limit is enforced by the same actual-byte counter across entries.</summary>
    [Fact]
    public async Task ActualExpandedBytesShareOneAggregateBudget()
    {
        var budget = new ExpandedByteBudget(maximumBytes: 10);
        await using var first = new MemoryStream(new byte[6]);
        await using var second = new MemoryStream(new byte[1024]);

        BoundedArchiveReadResult accepted = await BoundedArchiveReader.ReadAndHashAsync(
            first,
            declaredLength: 6,
            budget,
            TestContext.Current.CancellationToken);
        BoundedArchiveReadResult rejected = await BoundedArchiveReader.ReadAndHashAsync(
            second,
            declaredLength: 1024,
            budget,
            TestContext.Current.CancellationToken);

        Assert.True(accepted.IsSuccess);
        Assert.False(rejected.IsSuccess);
        Assert.Equal(BoundedArchiveReadIssue.AggregateLengthExceeded, rejected.Issue);
        Assert.Equal(11, budget.ConsumedBytes);
        Assert.Equal(5, second.Position);
    }

    /// <summary>Inner-manifest length is bounded before JSON parsing or payload trust.</summary>
    [Fact]
    public async Task OversizedInnerManifestNeverVerifies()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            mutateManifest: manifest =>
                manifest["oversized"] = new string('x', FileSystemManagedVersionRepository.MaximumManifestBytes));

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, result.Issue);
    }

    /// <summary>The production checksum document is a mandatory closed-package member.</summary>
    [Fact]
    public async Task MissingChecksumDocumentNeverVerifies()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            omitChecksumDocument: true);

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, result.Issue);
    }

    /// <summary>Checksums must match every declared payload and the release manifest exactly.</summary>
    [Theory]
    [InlineData("changed-hash")]
    [InlineData("extra-line")]
    public async Task NonCanonicalChecksumDocumentNeverVerifies(string mutation)
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            mutateChecksumDocument: bytes => mutation == "changed-hash"
                ? [.. bytes.Select((value, index) => index == 0 ? (byte)(value == '0' ? '1' : '0') : value)]
                : [.. bytes, .. System.Text.Encoding.UTF8.GetBytes($"{new string('0', 64)}  undeclared.txt\n")]);

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, result.Issue);
    }

    /// <summary>Catalog-admitted package length and digest are rechecked through the stable file handle.</summary>
    [Theory]
    [InlineData("length", ManagedVersionInstallIssue.PackageUnavailable)]
    [InlineData("digest", ManagedVersionInstallIssue.PackageMismatch)]
    public async Task ChangedPackageNeverReachesZipAdmission(
        string mutation,
        ManagedVersionInstallIssue expected)
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6");
        string packagePath = Path.Combine(sourceRoot, package.PackagePath.Value.Replace('/', Path.DirectorySeparatorChar));
        if (mutation == "length")
        {
            await File.AppendAllTextAsync(packagePath, "x", TestContext.Current.CancellationToken);
        }
        else
        {
            byte[] bytes = await File.ReadAllBytesAsync(packagePath, TestContext.Current.CancellationToken);
            bytes[^1] ^= 0xff;
            await File.WriteAllBytesAsync(packagePath, bytes, TestContext.Current.CancellationToken);
        }

        var repository = new FileSystemManagedVersionRepository();
        ManagedPackageVerificationResult result = await repository.VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        ManagedVersionInstallResult install = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(expected, result.Issue);
        Assert.Equal(expected, install.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        string stagingRoot = Path.Combine(managedRoot, ".staging");
        Assert.False(Directory.Exists(stagingRoot) && Directory.EnumerateFileSystemEntries(stagingRoot).Any());
    }

    /// <summary>A malformed ZIP is a typed verification/install failure with no partial target.</summary>
    [Fact]
    public async Task MalformedZipFailsWithoutPartialInstallation()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            mutatePackage: static packagePath =>
            {
                byte[] bytes = File.ReadAllBytes(packagePath);
                Array.Clear(bytes);
                File.WriteAllBytes(packagePath, bytes);
            });
        var repository = new FileSystemManagedVersionRepository();

        ManagedPackageVerificationResult verified = await repository.VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(verified.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.PackageUnavailable, verified.Issue);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, installed.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
    }

    /// <summary>Every required closed-payload relationship is checked before Verified is published.</summary>
    [Theory]
    [InlineData("product")]
    [InlineData("version")]
    [InlineData("role")]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("missing-fixed")]
    [InlineData("extra-file")]
    [InlineData("unknown-field")]
    public async Task InvalidManifestOrClosedPayloadNeverVerifies(string mutation)
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        Action<ZipArchive, string>? archiveMutation = null;
        if (mutation == "extra-file")
        {
            archiveMutation = static (archive, root) =>
            {
                ZipArchiveEntry entry = archive.CreateEntry($"{root}/undeclared.txt");
                using Stream output = entry.Open();
                output.WriteByte(1);
            };
        }
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            mutateManifest: manifest => MutateManifest(manifest, mutation),
            mutateArchive: archiveMutation,
            omittedPayload: mutation == "missing-fixed" ? "README.txt" : null);

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, result.Issue);
    }

    /// <summary>Reinstall is idempotent only for the exact same admitted identity.</summary>
    [Fact]
    public async Task IdenticalInstallIsIdempotentButChangedIdentityConflicts()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        var repository = new FileSystemManagedVersionRepository();
        UpdateCatalogVersionSnapshot firstPackage = CreatePackage(sourceRoot, "0.10.6");
        ManagedVersionInstallResult first = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            firstPackage,
            TestContext.Current.CancellationToken);
        ManagedVersionInstallResult second = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            firstPackage,
            TestContext.Current.CancellationToken);
        string otherSource = workspace.PathFor("other-source");
        UpdateCatalogVersionSnapshot changedPackage = CreatePackage(
            otherSource,
            "0.10.6",
            mutateManifest: manifest => manifest["sourceCommit"] = new string('e', 40));
        ManagedVersionInstallResult conflict = await repository.InstallAsync(
            managedRoot,
            otherSource,
            changedPackage,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(second.WasAlreadyInstalled);
        Assert.Equal(ManagedVersionInstallIssue.IdentityConflict, conflict.Issue);
        Assert.Equal(first.Admission, second.Admission);
    }

    /// <summary>A cancelled admission creates neither a target nor a residual staging transaction.</summary>
    [Fact]
    public async Task CancelledInstallLeavesNoTargetOrStagingTransaction()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new FileSystemManagedVersionRepository().InstallAsync(
                managedRoot,
                sourceRoot,
                package,
                cancellation.Token));

        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        Assert.False(Directory.Exists(Path.Combine(managedRoot, ".staging")) &&
                     Directory.EnumerateFileSystemEntries(Path.Combine(managedRoot, ".staging")).Any());
    }

    /// <summary>A staging-root collision fails without changing the installed-version root.</summary>
    [Fact]
    public async Task StagingRootCollisionLeavesInstalledStateUntouched()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        _ = Directory.CreateDirectory(managedRoot);
        await File.WriteAllTextAsync(
            Path.Combine(managedRoot, ".staging"),
            "collision",
            TestContext.Current.CancellationToken);

        ManagedVersionInstallResult result = await new FileSystemManagedVersionRepository().InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.PromotionFailed, result.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        Assert.Equal("collision", await File.ReadAllTextAsync(
            Path.Combine(managedRoot, ".staging"),
            TestContext.Current.CancellationToken));
    }

    /// <summary>Installed integrity distinguishes missing, extra, manifest, and failed-activation damage.</summary>
    [Theory]
    [InlineData("missing", ManagedVersionDamageReason.MissingFile)]
    [InlineData("extra", ManagedVersionDamageReason.UnexpectedPath)]
    [InlineData("manifest", ManagedVersionDamageReason.ManifestMismatch)]
    [InlineData("failed", ManagedVersionDamageReason.FailedActivation)]
    public async Task InventoryReportsExactDamageReason(
        string mutation,
        ManagedVersionDamageReason expected)
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);
        ManagedVersionAdmission admission = Assert.IsType<ManagedVersionAdmission>(installed.Admission);
        string versionRoot = Path.Combine(managedRoot, "versions", "0.10.6");
        switch (mutation)
        {
            case "missing":
                File.Delete(Path.Combine(versionRoot, "README.txt"));
                break;
            case "extra":
                await File.WriteAllTextAsync(
                    Path.Combine(versionRoot, "unexpected.txt"),
                    "unexpected",
                    TestContext.Current.CancellationToken);
                break;
            case "manifest":
                await File.AppendAllTextAsync(
                    Path.Combine(versionRoot, "RELEASE-MANIFEST.json"),
                    " ",
                    TestContext.Current.CancellationToken);
                break;
            case "failed":
                break;
            default:
                throw new InvalidOperationException($"Unknown inventory mutation '{mutation}'.");
        }

        ManagedVersionInventory inventory = RequireInventory(await repository.InventoryAsync(
            managedRoot,
            [admission],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: mutation == "failed" ? admission.Version : null,
            TestContext.Current.CancellationToken));

        InstalledVersionSnapshot row = Assert.Single(inventory.Versions);
        Assert.Equal(ManagedVersionIntegrity.Damaged, row.Integrity);
        Assert.Equal(expected, row.DamageReason);
    }

    /// <summary>An unadmitted directory is visible as damage but cannot be deleted through a forged identity.</summary>
    [Fact]
    public async Task UnadmittedDirectoryIsDamagedAndForgedDeleteIsBlocked()
    {
        using var workspace = TempWorkspace.Create();
        string managedRoot = workspace.PathFor("managed");
        string unknownRoot = Path.Combine(managedRoot, "versions", "0.10.6");
        _ = Directory.CreateDirectory(unknownRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unknownRoot, "unknown.txt"),
            "unknown",
            TestContext.Current.CancellationToken);
        var repository = new FileSystemManagedVersionRepository();

        ManagedVersionInventory inventory = RequireInventory(await repository.InventoryAsync(
            managedRoot,
            [],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));
        ManagedVersionDeleteIssue delete = await repository.DeleteAsync(
            managedRoot,
            new(ManagedAppVersion.Parse("0.10.6"), "forged", new string('a', 64)),
            activeVersion: null,
            TestContext.Current.CancellationToken);

        InstalledVersionSnapshot row = Assert.Single(inventory.Versions);
        Assert.Equal(ManagedVersionDamageReason.UnexpectedPath, row.DamageReason);
        Assert.Equal(ManagedVersionDeleteIssue.UnsafeTarget, delete);
        Assert.True(Directory.Exists(unknownRoot));
    }

    /// <summary>Inventory and delete never follow a reparse point planted inside an admitted version.</summary>
    [Fact]
    public async Task ReparsePointInsideInstalledVersionBlocksInventoryTrustAndDelete()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        string outsideRoot = workspace.PathFor("outside");
        _ = Directory.CreateDirectory(outsideRoot);
        string outsideFile = Path.Combine(outsideRoot, "preserve.txt");
        await File.WriteAllTextAsync(outsideFile, "preserve", TestContext.Current.CancellationToken);
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionAdmission admission = Assert.IsType<ManagedVersionAdmission>((await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken)).Admission);
        string versionRoot = Path.Combine(managedRoot, "versions", "0.10.6");
        _ = Directory.CreateSymbolicLink(Path.Combine(versionRoot, "linked"), outsideRoot);

        ManagedVersionInventory inventory = RequireInventory(await repository.InventoryAsync(
            managedRoot,
            [admission],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));
        ManagedVersionDeleteIssue delete = await repository.DeleteAsync(
            managedRoot,
            admission,
            activeVersion: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionDamageReason.UnexpectedPath, Assert.Single(inventory.Versions).DamageReason);
        Assert.Equal(ManagedVersionDeleteIssue.UnsafeTarget, delete);
        Assert.Equal("preserve", await File.ReadAllTextAsync(outsideFile, TestContext.Current.CancellationToken));
        Assert.True(Directory.Exists(versionRoot));
    }

    /// <summary>Real candidate process failure rolls back once to the verified prior version.</summary>
    [Theory]
    [InlineData("timeout-candidate")]
    [InlineData("exit-candidate")]
    public async Task RealCandidateProcessFailureRollsBackToVerifiedPriorVersion(string behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        string statePath = workspace.PathFor("version-manager.v1.json");
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionAdmission v105 = Assert.IsType<ManagedVersionAdmission>((await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.5", useReadyProbe: true),
            TestContext.Current.CancellationToken)).Admission);
        ManagedVersionAdmission v106 = Assert.IsType<ManagedVersionAdmission>((await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6", useReadyProbe: true),
            TestContext.Current.CancellationToken)).Admission);
        VersionManagerState pending = VersionActivationPolicy.BeginActivation(
            VersionManagerState.Create(
                updateSource: null,
                activeVersion: v105.Version,
                lastKnownGoodVersion: v105.Version,
                admissions: [v105, v106],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: managedRoot),
            v106.Version);
        var store = new JsonVersionManagerStateStore(statePath);
        await store.SaveAsync(pending, TestContext.Current.CancellationToken);

        ManagedLauncherResult result = await RunWithProbeBehaviorAsync(
            behavior,
            new ManagedActivationCoordinator(
                managedRoot,
                store,
                repository,
                new AnonymousPipeManagedApplicationProcess(),
                TimeSpan.FromMilliseconds(300)));
        await Task.Delay(500, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult state = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.RolledBack, result.Outcome);
        Assert.Equal(v105.Version, result.RunningVersion);
        Assert.Equal(v106.Version, result.FailedVersion);
        Assert.Equal(v105.Version, state.State!.ActiveVersion);
        Assert.Equal(v106.Version, state.State.FailedActivationVersion);
        Assert.Null(state.State.PendingActivation);
    }

    /// <summary>A real process crash after ready does not undo the committed activation.</summary>
    [Fact]
    public async Task RealCrashAfterReadyDoesNotTriggerAutomaticRollback()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        string statePath = workspace.PathFor("version-manager.v1.json");
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionAdmission v105 = Assert.IsType<ManagedVersionAdmission>((await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.5", useReadyProbe: true),
            TestContext.Current.CancellationToken)).Admission);
        ManagedVersionAdmission v106 = Assert.IsType<ManagedVersionAdmission>((await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6", useReadyProbe: true),
            TestContext.Current.CancellationToken)).Admission);
        VersionManagerState pending = VersionActivationPolicy.BeginActivation(
            VersionManagerState.Create(
                updateSource: null,
                activeVersion: v105.Version,
                lastKnownGoodVersion: v105.Version,
                admissions: [v105, v106],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: managedRoot),
            v106.Version);
        var store = new JsonVersionManagerStateStore(statePath);
        await store.SaveAsync(pending, TestContext.Current.CancellationToken);

        ManagedLauncherResult result = await RunWithProbeBehaviorAsync(
            "ready-exit-candidate",
            new ManagedActivationCoordinator(
                managedRoot,
                store,
                repository,
                new AnonymousPipeManagedApplicationProcess(),
                TimeSpan.FromSeconds(5)));
        await Task.Delay(500, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult state = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.Ready, result.Outcome);
        Assert.Equal(v106.Version, result.RunningVersion);
        Assert.Equal(v106.Version, state.State!.ActiveVersion);
        Assert.Equal(v106.Version, state.State.LastKnownGoodVersion);
        Assert.Null(state.State.FailedActivationVersion);
        Assert.Null(state.State.PendingActivation);
    }

}
