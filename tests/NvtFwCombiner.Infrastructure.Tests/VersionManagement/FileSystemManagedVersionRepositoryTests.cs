using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Tests closed-package staging, inventory, and destructive guards.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed partial class FileSystemManagedVersionRepositoryTests
{
    private static readonly string[] WorkerProtocols = ["1.0"];
    private static readonly JsonSerializerOptions CatalogJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A valid package promotes atomically and later tamper is reported as damaged.</summary>
    [Fact]
    public async Task InstallPromotesClosedPayloadAndInventoryDetectsTamper()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6");
        var repository = new FileSystemManagedVersionRepository();

        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        Assert.True(installed.IsSuccess, installed.Issue.ToString());
        ManagedVersionAdmission admission = Assert.IsType<ManagedVersionAdmission>(installed.Admission);
        ManagedVersionInventory healthy = RequireInventory(await repository.InventoryAsync(
            managedRoot,
            [admission],
            admission.Version,
            admission.Version,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));
        await File.AppendAllTextAsync(
            Path.Combine(managedRoot, "versions", "0.10.6", "README.txt"),
            "tampered",
            TestContext.Current.CancellationToken);
        ManagedVersionInventory damaged = RequireInventory(await repository.InventoryAsync(
            managedRoot,
            [admission],
            admission.Version,
            admission.Version,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));

        Assert.False(installed.WasAlreadyInstalled);
        Assert.Equal(1, healthy.HealthyCount);
        Assert.Equal(1, damaged.DamagedCount);
        Assert.Equal(ManagedVersionDamageReason.ContentMismatch, Assert.Single(damaged.Versions).DamageReason);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, ".staging")) &&
                     Directory.EnumerateFileSystemEntries(Path.Combine(managedRoot, ".staging")).Any());
    }

    /// <summary>The exact verified application cannot be swapped before Process.Start while its lease is held.</summary>
    [Fact]
    public async Task AcquiredApplicationLeaseDeniesExecutableSwapUntilReleased()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6", useReadyProbe: true);
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.True(installed.IsSuccess, installed.Issue.ToString());
        ManagedExecutableLaunchLeaseResult acquired = await repository.AcquireApplicationLaunchLeaseAsync(
            managedRoot,
            installed.Admission!,
            TestContext.Current.CancellationToken);
        Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
        string executable = Path.Combine(managedRoot, "versions", "0.10.6", "NvtFwCombiner.exe");
        string displaced = executable + ".displaced";
        _ = Assert.Throws<IOException>(() => File.Move(executable, displaced));

        acquired.Lease!.Dispose();
        File.Move(executable, displaced);
        File.Move(displaced, executable);
        Assert.True(File.Exists(executable));
    }

    /// <summary>A healthy self-admission alone is observed evidence, not committed authority.</summary>
    [Fact]
    public async Task HealthySelfAdmissionWithoutCommittedStateIsOnlyObservedUnadmittedFact()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6");
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        ManagedVersionInventory observed = RequireInventory(await repository.InventoryAsync(
            managedRoot,
            admissions: [],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));

        InstalledVersionSnapshot row = Assert.Single(observed.Versions);
        Assert.True(installed.IsSuccess);
        Assert.Equal(ManagedVersionAdmissionState.Unadmitted, row.AdmissionState);
        Assert.Equal(installed.Admission, row.ObservedAdmission);
        Assert.Equal(ManagedVersionIntegrity.Healthy, row.Integrity);
    }

    /// <summary>Package verification is content-bound and survives moving the complete source folder.</summary>
    [Fact]
    public async Task PackageVerificationSurvivesUpdateSourceFolderRelocation()
    {
        using var workspace = TempWorkspace.Create();
        string originalSource = workspace.PathFor("source-original");
        string relocatedSource = workspace.PathFor("source-relocated");
        UpdateCatalogVersionSnapshot package = CreatePackage(originalSource, "0.10.6");
        Directory.Move(originalSource, relocatedSource);

        ManagedPackageVerificationResult verified = await new FileSystemManagedVersionRepository()
            .VerifyPackageAsync(
                relocatedSource,
                package,
                TestContext.Current.CancellationToken);

        Assert.True(verified.IsVerified, verified.Issue.ToString());
        Assert.Equal(package.Identity, verified.Candidate!.AdmissionIdentity);
        Assert.DoesNotContain(originalSource, verified.Candidate.AdmissionIdentity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(relocatedSource, verified.Candidate.AdmissionIdentity, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Archive traversal fails without a partial installed directory.</summary>
    [Fact]
    public async Task ZipTraversalFailsAndCleansStaging()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6", "../escape.txt");

        ManagedVersionInstallResult result = await new FileSystemManagedVersionRepository().InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.UnsafeArchive, result.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        Assert.False(File.Exists(workspace.PathFor("escape.txt")));
    }

    /// <summary>A Windows device-name archive path is rejected during verification, before it can appear Verified.</summary>
    [Fact]
    public async Task WindowsDeviceNameArchivePathNeverVerifies()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string packageRoot = "NvtFwCombiner-v0.10.6-win-x64";
        UpdateCatalogVersionSnapshot package = CreatePackage(
            sourceRoot,
            "0.10.6",
            $"{packageRoot}/CON/payload.txt");

        ManagedPackageVerificationResult result = await new FileSystemManagedVersionRepository().VerifyPackageAsync(
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsVerified);
        Assert.Equal(ManagedVersionInstallIssue.UnsafeArchive, result.Issue);
    }

    /// <summary>Active deletion is blocked while an exact non-active admitted child may be removed.</summary>
    [Fact]
    public async Task DeleteProtectsActiveAndRemovesOnlyExactAdmittedChild()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult firstInstall = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.5"),
            TestContext.Current.CancellationToken);
        Assert.True(firstInstall.IsSuccess, firstInstall.Issue.ToString());
        ManagedVersionAdmission first = Assert.IsType<ManagedVersionAdmission>(firstInstall.Admission);
        ManagedVersionInstallResult secondInstall = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);
        Assert.True(secondInstall.IsSuccess, secondInstall.Issue.ToString());
        ManagedVersionAdmission second = Assert.IsType<ManagedVersionAdmission>(secondInstall.Admission);

        ManagedVersionDeleteIssue blocked = await repository.DeleteAsync(
            managedRoot,
            second,
            second.Version,
            TestContext.Current.CancellationToken);
        ManagedVersionDeleteIssue deleted = await repository.DeleteAsync(
            managedRoot,
            first,
            second.Version,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionDeleteIssue.ActiveVersion, blocked);
        Assert.Equal(ManagedVersionDeleteIssue.None, deleted);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.5")));
        Assert.True(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
    }

    /// <summary>An oversized installed manifest is rejected before allocating its untrusted length.</summary>
    [Fact]
    public async Task InventoryRejectsOversizedInstalledManifest()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);
        ManagedVersionAdmission admission = Assert.IsType<ManagedVersionAdmission>(installed.Admission);
        await File.WriteAllBytesAsync(
            Path.Combine(managedRoot, "versions", "0.10.6", "RELEASE-MANIFEST.json"),
            new byte[FileSystemManagedVersionRepository.MaximumManifestBytes + 1],
            TestContext.Current.CancellationToken);

        ManagedVersionInventory inventory = RequireInventory(await repository.InventoryAsync(
            managedRoot,
            [admission],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));

        InstalledVersionSnapshot damaged = Assert.Single(inventory.Versions);
        Assert.Equal(ManagedVersionIntegrity.Damaged, damaged.Integrity);
        Assert.Equal(ManagedVersionDamageReason.ManifestMismatch, damaged.DamageReason);
    }

    /// <summary>A malformed admission never escapes as an exception or permits a destructive delete.</summary>
    [Fact]
    public async Task MalformedAdmissionBlocksDeleteWithoutRemovingDirectory()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);
        ManagedVersionAdmission admission = Assert.IsType<ManagedVersionAdmission>(installed.Admission);
        string versionRoot = Path.Combine(managedRoot, "versions", "0.10.6");
        await File.WriteAllTextAsync(
            Path.Combine(versionRoot, FileSystemManagedVersionRepository.AdmissionFileName),
            "{broken-json",
            TestContext.Current.CancellationToken);

        ManagedVersionDeleteIssue issue = await repository.DeleteAsync(
            managedRoot,
            admission,
            activeVersion: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionDeleteIssue.UnsafeTarget, issue);
        Assert.True(Directory.Exists(versionRoot));
    }

    /// <summary>A local-folder lab proves install, relocation, damage rollback, offline switch, and deletion together.</summary>
    [Fact]
    public async Task LocalFolderUpgradeLabSurvivesRelocationAndOfflineSwitching()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("version-update-source-lab");
        string relocatedSource = workspace.PathFor("version-update-source-relocated");
        string managedRoot = CreateManagedRoot(workspace, "managed-root");
        string statePath = workspace.PathFor("version-manager.v1.json");
        string? configuredLab = Environment.GetEnvironmentVariable("NVT_VERSION_UPDATE_SOURCE_LAB");
        UpdateCatalogVersionSnapshot v105;
        UpdateCatalogVersionSnapshot v106;
        if (string.IsNullOrWhiteSpace(configuredLab))
        {
            v105 = CreatePackage(sourceRoot, "0.10.5", useReadyProbe: true);
            v106 = CreatePackage(sourceRoot, "0.10.6", useReadyProbe: true);
            await WriteCatalogAsync(sourceRoot, [v105, v106]);
        }
        else
        {
            CopyDirectory(Path.GetFullPath(configuredLab), sourceRoot);
            UpdateCatalogLoadResult loaded = await new FileSystemUpdateCatalogSource().LoadAsync(
                sourceRoot,
                TestContext.Current.CancellationToken);
            Assert.True(loaded.IsSuccess, loaded.Issue.ToString());
            v105 = Assert.Single(loaded.Snapshot!.Versions, version => version.Version == ManagedAppVersion.Parse("0.10.5"));
            v106 = Assert.Single(loaded.Snapshot.Versions, version => version.Version == ManagedAppVersion.Parse("0.10.6"));
        }

        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult initialInstall = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            v105,
            TestContext.Current.CancellationToken);
        ManagedVersionAdmission seedAdmission = Assert.IsType<ManagedVersionAdmission>(initialInstall.Admission);
        VersionManagerState seedState = VersionManagerState.Create(
            updateSource: null,
            activeVersion: v105.Version,
            lastKnownGoodVersion: v105.Version,
            admissions: [seedAdmission],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false);
        string seedPath = Path.Combine(managedRoot, "version-manager.seed.v1.json");
        await new JsonVersionManagerStateStore(seedPath, allowUnboundSeedTemplate: true).SaveAsync(
            seedState,
            TestContext.Current.CancellationToken);
        ManagedVersionSeedOutcome seedOutcome = await new ManagedVersionSeedBootstrapper(
                managedRoot,
                new JsonVersionManagerStateStore(statePath),
                new JsonVersionManagerStateStore(seedPath),
                repository)
            .EnsureInitializedAsync(
                LauncherBootstrapCoordinator.StartupWriterLeaseTimeout,
                TestContext.Current.CancellationToken);
        Assert.Equal(ManagedVersionSeedOutcome.Seeded, seedOutcome);
        ManagedLauncherResult seeded = await RunLauncherAsync();
        Assert.Equal(ManagedLauncherOutcome.Ready, seeded.Outcome);
        Assert.Equal(v105.Version, seeded.RunningVersion);

        using (VersionManagementExperience experience = CreateExperience("0.10.5"))
        {
            _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
            _ = await experience.CommitUpdateSourceAsync(sourceRoot, TestContext.Current.CancellationToken);
            Assert.True((await experience.InstallAsync(v106.Version, TestContext.Current.CancellationToken)).Install.IsSuccess);
            _ = await experience.PrepareActivationAsync(v106.Version, TestContext.Current.CancellationToken);
        }
        await File.AppendAllTextAsync(
            Path.Combine(managedRoot, "versions", "0.10.6", "README.txt"),
            "tampered",
            TestContext.Current.CancellationToken);
        ManagedLauncherResult rolledBack = await RunLauncherAsync();
        Assert.Equal(ManagedLauncherOutcome.RolledBack, rolledBack.Outcome);
        Assert.Equal(v105.Version, rolledBack.RunningVersion);
        Assert.Equal(v106.Version, rolledBack.FailedVersion);

        using (VersionManagementExperience experience = CreateExperience("0.10.5"))
        {
            VersionManagementSnapshot damaged = await experience.InitializeAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(ManagedVersionIntegrity.Damaged, damaged.Inventory.Find(v106.Version)!.Integrity);
            VersionDeleteOperationResult deleted = await experience.DeleteAsync(
                v106.Version,
                rollbackLossConfirmed: false,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionDeleteOperationIssue.None, deleted.OperationIssue);
            Assert.Equal(ManagedVersionDeleteIssue.None, deleted.RepositoryIssue);
        }

        Directory.Move(sourceRoot, relocatedSource);
        using (VersionManagementExperience experience = CreateExperience("0.10.5"))
        {
            _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
            VersionManagementSnapshot offline = await experience.CheckAsync(
                isAutomatic: false,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionSourceStatus.Offline, offline.SourceStatus);
            VersionManagementSnapshot relocated = await experience.CommitUpdateSourceAsync(
                relocatedSource,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionSourceStatus.Connected, relocated.SourceStatus);
            Assert.True((await experience.InstallAsync(v106.Version, TestContext.Current.CancellationToken)).Install.IsSuccess);
            _ = await experience.PrepareActivationAsync(v106.Version, TestContext.Current.CancellationToken);
        }
        ManagedLauncherResult upgraded = await RunLauncherAsync();
        Assert.Equal(ManagedLauncherOutcome.Ready, upgraded.Outcome);
        Assert.Equal(v106.Version, upgraded.RunningVersion);

        Directory.Move(relocatedSource, workspace.PathFor("version-update-source-offline"));
        using (VersionManagementExperience experience = CreateExperience("0.10.6"))
        {
            _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
            VersionManagementSnapshot offline = await experience.CheckAsync(
                isAutomatic: false,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionSourceStatus.Offline, offline.SourceStatus);
            _ = await experience.PrepareActivationAsync(v105.Version, TestContext.Current.CancellationToken);
        }
        ManagedLauncherResult switchedOffline = await RunLauncherAsync();
        Assert.Equal(ManagedLauncherOutcome.Ready, switchedOffline.Outcome);
        Assert.Equal(v105.Version, switchedOffline.RunningVersion);

        using (VersionManagementExperience experience = CreateExperience("0.10.5"))
        {
            _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
            VersionDeleteOperationResult deleted = await experience.DeleteAsync(
                v106.Version,
                rollbackLossConfirmed: false,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionDeleteOperationIssue.None, deleted.OperationIssue);
            Assert.Equal(ManagedVersionDeleteIssue.None, deleted.RepositoryIssue);
            Assert.Null(deleted.Snapshot.Inventory.Find(v106.Version));
        }

        VersionManagementExperience CreateExperience(string runningVersion)
        {
            return new(
                ManagedAppVersion.Parse(runningVersion),
                managedRoot,
                new JsonVersionManagerStateStore(statePath),
                new FileSystemUpdateCatalogSource(),
                new FileSystemManagedVersionRepository(),
                new JsonLauncherMutationFence(statePath));
        }

        async ValueTask<ManagedLauncherResult> RunLauncherAsync()
        {
            var coordinator = new ManagedActivationCoordinator(
                managedRoot,
                new JsonVersionManagerStateStore(statePath),
                new FileSystemManagedVersionRepository(),
                new AnonymousPipeManagedApplicationProcess(statePath),
                TimeSpan.FromSeconds(5));
            ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);
            return result;
        }
    }

    private static UpdateCatalogVersionSnapshot CreatePackage(string sourceRoot,
        string version,
        string? maliciousEntry = null,
        bool useReadyProbe = false,
        bool nullManifestCollections = false,
        Action<JsonObject>? mutateManifest = null,
        Action<ZipArchive, string>? mutateArchive = null,
        string? omittedPayload = null,
        Action<string>? mutatePackage = null,
        bool includeProductionContractPayload = true,
        bool omitChecksumDocument = false,
        Func<byte[], byte[]>? mutateChecksumDocument = null,
        bool includeManagedLauncher = false,
        int additionalPayloadFiles = 0,
        byte[]? managedLauncherBytes = null)
    {
        string packages = Path.Combine(sourceRoot, "packages");
        _ = Directory.CreateDirectory(packages);
        string packagePath = Path.Combine(packages, $"NvtFwCombiner-v{version}-win-x64.zip");
        string packageRoot = $"NvtFwCombiner-v{version}-win-x64";
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            ["NvtFwCombiner.exe"] = [0x4d, 0x5a, 0x01],
            ["external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe"] = [0x4d, 0x5a, 0x02],
            ["THIRD-PARTY-NOTICES.txt"] = Encoding.UTF8.GetBytes("notices"),
            ["LICENSE.txt"] = Encoding.UTF8.GetBytes("license"),
            ["README.txt"] = Encoding.UTF8.GetBytes("readme"),
        };
        if (includeProductionContractPayload)
        {
            files["docs/contracts/canonical-capability-policy-v1.json"] = Encoding.UTF8.GetBytes("{}");
            files["profiles/built-in/package-trust-index.json"] = Encoding.UTF8.GetBytes("{}");
        }
        if (includeManagedLauncher)
        {
            files[ManagedLauncherIdentity.ExecutablePath] = managedLauncherBytes ?? [0x4d, 0x5a, 0x03];
        }
        for (int index = 0; index < additionalPayloadFiles; index++)
        {
            files[$"reference/entry-{index:D4}.txt"] = [0x00];
        }
        if (useReadyProbe)
        {
            string probeRoot = Path.Combine(AppContext.BaseDirectory, "ready-probe");
            foreach (string source in Directory.EnumerateFiles(probeRoot))
            {
                string fileName = Path.GetFileName(source);
                string targetName = string.Equals(
                    fileName,
                    "NvtFwCombiner.ReadyProbe.exe",
                    StringComparison.OrdinalIgnoreCase)
                        ? "NvtFwCombiner.exe"
                        : fileName;
                files[targetName] = File.ReadAllBytes(source);
            }
        }
        byte[] manifest = CreateReleaseManifest(
            version,
            files,
            nullManifestCollections,
            mutateManifest,
            includeManagedLauncher);
        byte[] checksums = CreateChecksumDocument(files, manifest);
        if (mutateChecksumDocument is not null)
        {
            checksums = mutateChecksumDocument(checksums);
        }
        using (FileStream output = File.Create(packagePath))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
        {
            WriteEntry(archive, $"{packageRoot}/RELEASE-MANIFEST.json", manifest);
            if (!omitChecksumDocument)
            {
                WriteEntry(archive, $"{packageRoot}/SHA256SUMS.txt", checksums);
            }
            foreach ((string path, byte[] bytes) in files)
            {
                if (!string.Equals(path, omittedPayload, StringComparison.Ordinal))
                {
                    WriteEntry(archive, $"{packageRoot}/{path}", bytes);
                }
            }
            if (maliciousEntry is not null)
            {
                WriteEntry(archive, maliciousEntry, [1]);
            }
            mutateArchive?.Invoke(archive, packageRoot);
        }

        mutatePackage?.Invoke(packagePath);
        byte[] packageBytes = File.ReadAllBytes(packagePath);
        string relativePackage = $"packages/NvtFwCombiner-v{version}-win-x64.zip";
        var document = new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [new(
                version,
                "2026-08-21T00:00:00Z",
                relativePackage,
                packageBytes.Length,
                Convert.ToHexStringLower(SHA256.HashData(packageBytes)),
                Convert.ToHexStringLower(SHA256.HashData(manifest)),
                $"Release {version}")]);
        UpdateCatalogVersionSnapshot snapshot = Assert.Single(UpdateCatalogValidator.Validate(document).Snapshot!.Versions);
        return snapshot;
    }

    private static string CreateManagedRoot(TempWorkspace workspace, string relativePath)
    {
        string root = workspace.PathFor(relativePath);
        _ = Directory.CreateDirectory(root);
        return root;
    }

    private static async Task WriteCatalogAsync(
        string sourceRoot,
        IReadOnlyList<UpdateCatalogVersionSnapshot> versions)
    {
        var document = new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [.. versions.Select(version => new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogVersionDocument(
                version.Version.ToString(),
                version.PublishedAt.UtcDateTime.ToString(
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    System.Globalization.CultureInfo.InvariantCulture),
                version.PackagePath.Value,
                version.PackageSize,
                version.PackageSha256,
                version.ReleaseManifestSha256,
                version.ReleaseNotes))]);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            document,
            CatalogJsonOptions);
        await File.WriteAllBytesAsync(
            Path.Combine(sourceRoot, FileSystemUpdateCatalogSource.CatalogFileName),
            bytes,
            TestContext.Current.CancellationToken);
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        _ = Directory.CreateDirectory(destinationRoot);
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            _ = Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }
        foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, source));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
    }

    private static byte[] CreateReleaseManifest(
        string version,
        Dictionary<string, byte[]> files,
        bool nullCollections = false,
        Action<JsonObject>? mutateManifest = null,
        bool includeManagedLauncher = false)
    {
        object[] entries = [.. files.Select(pair => new
        {
            path = pair.Key,
            size = pair.Value.Length,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(pair.Value)),
            role = pair.Key switch
            {
                "NvtFwCombiner.exe" => "application",
                "THIRD-PARTY-NOTICES.txt" => "notices",
                "LICENSE.txt" => "license",
                "README.txt" => "readme",
                ManagedLauncherIdentity.ExecutablePath => "launcher",
                "docs/contracts/canonical-capability-policy-v1.json" => "capabilityPolicy",
                _ when pair.Key.StartsWith("profiles/built-in/", StringComparison.Ordinal) => "builtInProfile",
                _ when pair.Key.StartsWith("external-tools/", StringComparison.Ordinal) => "externalTool",
                _ when !pair.Key.Contains('/') => "application",
                _ => "reference",
            },
        })];
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = includeManagedLauncher ? "1.2" : "1.1",
            product = "NVT FW Combiner",
            version,
            sourceCommit = new string('a', 40),
            sourceTag = $"v{version}",
            runtimeIdentifier = "win-x64",
            licenseSpdx = "MIT",
            workerProtocolVersions = nullCollections ? null : WorkerProtocols,
            approvedProcessorIds = Array.Empty<string>(),
            processorBundleSha256 = new string('b', 64),
            embeddedProfileCatalogSha256 = new string('c', 64),
            embeddedSchemaBundleSha256 = new string('d', 64),
            files = nullCollections ? null : entries,
            sbomAsset = $"NvtFwCombiner-v{version}-win-x64.spdx.json",
            provenanceAsset = $"NvtFwCombiner-v{version}-win-x64.provenance.json",
        });
        JsonObject document = JsonNode.Parse(bytes)!.AsObject();
        if (includeManagedLauncher)
        {
            byte[] launcher = files[ManagedLauncherIdentity.ExecutablePath];
            document["versionManagementProtocolVersion"] =
                ManagedLauncherIdentity.SupportedProtocolVersion;
            document["launcher"] = JsonSerializer.SerializeToNode(new
            {
                launcherVersion = version,
                protocolVersion = ManagedLauncherIdentity.SupportedProtocolVersion,
                executableRelativePath = ManagedLauncherIdentity.ExecutablePath,
                size = launcher.LongLength,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(launcher)),
            });
        }
        mutateManifest?.Invoke(document);
        return JsonSerializer.SerializeToUtf8Bytes(document);
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] bytes)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        using Stream output = entry.Open();
        output.Write(bytes);
    }

    private static byte[] CreateChecksumDocument(
        IReadOnlyDictionary<string, byte[]> files,
        byte[] manifest)
    {
        IEnumerable<(string Path, string Hash)> entries = files
            .Select(pair => (
                Path: pair.Key,
                Hash: Convert.ToHexStringLower(SHA256.HashData(pair.Value))))
            .Append((
                Path: "RELEASE-MANIFEST.json",
                Hash: Convert.ToHexStringLower(SHA256.HashData(manifest))))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal);
        return Encoding.UTF8.GetBytes(string.Join(
            "\n",
            entries.Select(entry => $"{entry.Hash}  {entry.Path}")) + "\n");
    }
}
