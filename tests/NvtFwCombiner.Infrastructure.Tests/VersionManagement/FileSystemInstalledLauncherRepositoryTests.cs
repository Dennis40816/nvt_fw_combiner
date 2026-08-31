using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies installed launcher release identity and content admission.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class FileSystemInstalledLauncherRepositoryTests
{
    /// <summary>Exact release-coupled content is projected into an owner-bound identity.</summary>
    [Fact]
    public async Task ExactReleaseCoupledLauncherReturnsBoundOwnerIdentity()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        var repository = new FileSystemInstalledLauncherRepository();

        InstalledLauncherResult result = await repository.VerifyAsync(
            fixture.ManagedRoot,
            fixture.Admission,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsVerified);
        Assert.Equal(fixture.Admission.Version, result.Identity!.OwnerAppVersion);
        Assert.Equal(fixture.Admission.AdmissionIdentity, result.Identity.OwnerAdmissionIdentity);
        Assert.Equal(fixture.Admission.ReleaseManifestSha256, result.Identity.OwnerReleaseManifestSha256);
        Assert.Equal(ManagedAppVersion.Parse("1.0.0"), result.Identity.LauncherVersion);
        Assert.Equal(fixture.LauncherSha256, result.Identity.Sha256);
    }

    /// <summary>Changed executable bytes cannot retain their admitted identity.</summary>
    [Fact]
    public async Task LauncherBytesChangedReturnsTamperedWithoutIdentity()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        await File.AppendAllTextAsync(
            fixture.LauncherPath,
            "changed",
            TestContext.Current.CancellationToken);

        InstalledLauncherResult result = await new FileSystemInstalledLauncherRepository().VerifyAsync(
            fixture.ManagedRoot,
            fixture.Admission,
            TestContext.Current.CancellationToken);

        Assert.Null(result.Identity);
        Assert.Equal(InstalledLauncherIssue.Tampered, result.Issue);
    }

    /// <summary>A same-length Launcher substitution cannot cross the fast admission boundary.</summary>
    [Fact]
    public async Task SameLengthLauncherBytesChangedReturnsTamperedBeforeLeaseAdmission()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        byte[] changed = await File.ReadAllBytesAsync(
            fixture.LauncherPath,
            TestContext.Current.CancellationToken);
        changed[^1] ^= 0xff;
        await File.WriteAllBytesAsync(
            fixture.LauncherPath,
            changed,
            TestContext.Current.CancellationToken);

        InstalledLauncherLaunchResult acquired = await new FileSystemInstalledLauncherRepository()
            .AcquireLaunchLeaseAsync(
                fixture.ManagedRoot,
                fixture.Admission,
                TestContext.Current.CancellationToken);

        Assert.Null(acquired.Identity);
        Assert.Null(acquired.Lease);
        Assert.Equal(InstalledLauncherIssue.Tampered, acquired.Issue);
    }

    /// <summary>Non-Launcher bytes are deferred to full activation while exact Launcher custody remains valid.</summary>
    [Theory]
    [InlineData("NvtFwCombiner.Core.dll")]
    [InlineData("NvtFwCombiner.runtimeconfig.json")]
    [InlineData("external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe")]
    public async Task DeclaredNonLauncherMemberChangedIsRejectedByApplicationActivationAfterLeaseAdmission(
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        string changedPath = fixture.VersionPath(relativePath);
        byte[] changed = await File.ReadAllBytesAsync(
            changedPath,
            TestContext.Current.CancellationToken);
        changed[^1] ^= 0xff;
        await File.WriteAllBytesAsync(
            changedPath,
            changed,
            TestContext.Current.CancellationToken);
        InstalledLauncherLaunchResult acquired = await new FileSystemInstalledLauncherRepository()
            .AcquireLaunchLeaseAsync(
                fixture.ManagedRoot,
                fixture.Admission,
                TestContext.Current.CancellationToken);
        Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
        acquired.Lease!.Dispose();

        ManagedExecutableLaunchLeaseResult applicationLease =
            await new FileSystemManagedVersionRepository().AcquireApplicationLaunchLeaseAsync(
                fixture.ManagedRoot,
                fixture.Admission,
                TestContext.Current.CancellationToken);

        Assert.Null(applicationLease.Lease);
        Assert.Equal(ManagedExecutableLaunchIssue.Tampered, applicationLease.Issue);
    }

    /// <summary>Pre-existing missing or foreign topology cannot reach the version Launcher.</summary>
    [Theory]
    [InlineData("missing-file")]
    [InlineData("unexpected-file")]
    [InlineData("unexpected-directory")]
    public async Task PreExistingTopologyMismatchFailsBeforeLeaseAdmission(string mutation)
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        switch (mutation)
        {
            case "missing-file":
                File.Delete(fixture.LibraryPath);
                break;
            case "unexpected-file":
                await File.WriteAllTextAsync(
                    fixture.VersionPath("unexpected.dll"),
                    "foreign",
                    TestContext.Current.CancellationToken);
                break;
            case "unexpected-directory":
                _ = Directory.CreateDirectory(fixture.VersionPath("unexpected"));
                break;
            default:
                throw new InvalidOperationException("The test mutation is undefined.");
        }

        InstalledLauncherLaunchResult acquired = await new FileSystemInstalledLauncherRepository()
            .AcquireLaunchLeaseAsync(
                fixture.ManagedRoot,
                fixture.Admission,
                TestContext.Current.CancellationToken);

        Assert.Null(acquired.Identity);
        Assert.Null(acquired.Lease);
        Assert.Equal(InstalledLauncherIssue.Tampered, acquired.Issue);
    }

    /// <summary>The exact verified launcher cannot be swapped before Process.Start while its lease is held.</summary>
    [Fact]
    public async Task AcquiredLauncherLeaseDeniesExecutableSwapUntilReleased()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        InstalledLauncherLaunchResult acquired = await new FileSystemInstalledLauncherRepository()
            .AcquireLaunchLeaseAsync(
                fixture.ManagedRoot,
                fixture.Admission,
                TestContext.Current.CancellationToken);

        Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
        string displaced = fixture.LauncherPath + ".displaced";
        _ = Assert.Throws<IOException>(() => File.Move(fixture.LauncherPath, displaced));

        acquired.Lease!.Dispose();
        File.Move(fixture.LauncherPath, displaced);
        File.Move(displaced, fixture.LauncherPath);
        Assert.True(File.Exists(fixture.LauncherPath));
    }

    /// <summary>The composite lease protects every admitted member and its version root.</summary>
    [Fact]
    public async Task AcquiredLauncherLeaseDeniesManifestLibraryAndRootMutationUntilReleased()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        InstalledLauncherLaunchResult acquired = await new FileSystemInstalledLauncherRepository()
            .AcquireLaunchLeaseAsync(
                fixture.ManagedRoot,
                fixture.Admission,
                TestContext.Current.CancellationToken);

        Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
        _ = Assert.Throws<IOException>(() => File.AppendAllText(fixture.ManifestPath, "changed"));
        _ = Assert.Throws<IOException>(() => File.AppendAllText(fixture.LibraryPath, "changed"));
        _ = Assert.Throws<IOException>(() => Directory.Move(
            fixture.VersionRoot,
            fixture.VersionRoot + ".displaced"));

        acquired.Lease!.Dispose();
        File.AppendAllText(fixture.LibraryPath, "released");
    }

    /// <summary>A namespace addition after manifest proof invalidates the composite lease.</summary>
    [Fact]
    public async Task AddedChildAfterManifestProofFailsClosedAndReleasesCustody()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        string unexpected = Path.Combine(fixture.VersionRoot, "unexpected.dll");
        var repository = new FileSystemInstalledLauncherRepository(
            ManagedPathSafety.ReadBoundedFileAsync,
            custodyHook: null,
            beforeLeaseCreation: () => File.WriteAllText(unexpected, "foreign"));

        InstalledLauncherLaunchResult acquired = await repository.AcquireLaunchLeaseAsync(
            fixture.ManagedRoot,
            fixture.Admission,
            TestContext.Current.CancellationToken);

        Assert.Null(acquired.Lease);
        Assert.Equal(InstalledLauncherIssue.UnsafePath, acquired.Issue);
        File.Delete(unexpected);
    }

    /// <summary>A real repository lease rejects a child added at the final Process.Start gate.</summary>
    [Fact]
    public async Task RepositoryLeaseRejectsLateChildBeforeLauncherProcessStart()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync(useRunnableLauncher: true);
        InstalledLauncherLaunchResult acquired = await new FileSystemInstalledLauncherRepository()
            .AcquireLaunchLeaseAsync(
                fixture.ManagedRoot,
                fixture.Admission,
                TestContext.Current.CancellationToken);
        Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
        string unexpected = fixture.VersionPath("late-child.dll");
        using BootstrapAdmissionSignal admission = BootstrapAdmissionSignal.Capture();
        var process = new AnonymousPipeManagedLauncherProcess(
            ManagedProcessTermination.Instance,
            admission,
            beforeStartValidation: () => File.WriteAllText(unexpected, "foreign"));

        LauncherProcessStartResult started = await process.StartUntilReadyAsync(
            fixture.ManagedRoot,
            Path.Combine(fixture.Root, "state", "version-manager.v1.json"),
            acquired.Identity!,
            acquired.Lease!,
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(LauncherProcessStartOutcome.StartFailed, started.Outcome);
        Assert.True(File.Exists(unexpected));
        acquired.Lease!.Dispose();
        File.Delete(unexpected);
    }

    /// <summary>Path admission and the stable leaf open cannot be separated by an ancestor substitution.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AcquiredLauncherLeaseClosesAncestorAdmissionRace(int ancestorLevels)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        string ancestor = fixture.LauncherPath;
        for (int level = 0; level < ancestorLevels; level++)
        {
            ancestor = Path.GetDirectoryName(ancestor)!;
        }
        string displaced = ancestor + ".displaced";
        string executableRelativePath = Path.GetRelativePath(ancestor, fixture.LauncherPath);
        bool substitutionBlocked = false;
        bool substitutionPerformed = false;
        ManagedExecutableLaunchLeaseResult acquired = await StableManagedExecutableLaunchLease
            .TryAcquireAsync(
                fixture.LauncherPath,
                new FileInfo(fixture.LauncherPath).Length,
                fixture.LauncherSha256,
                () =>
                {
                    try
                    {
                        Directory.Move(ancestor, displaced);
                        string replacement = Path.Combine(ancestor, executableRelativePath);
                        _ = Directory.CreateDirectory(Path.GetDirectoryName(replacement)!);
                        File.Copy(Path.Combine(displaced, executableRelativePath), replacement);
                        substitutionPerformed = true;
                    }
                    catch (IOException)
                    {
                        substitutionBlocked = true;
                    }
                },
                TestContext.Current.CancellationToken);

        acquired.Lease?.Dispose();
        if (Directory.Exists(displaced))
        {
            Directory.Delete(ancestor, recursive: true);
            Directory.Move(displaced, ancestor);
        }

        if (substitutionPerformed)
        {
            Assert.False(acquired.IsAcquired);
            Assert.Equal(ManagedExecutableLaunchIssue.UnsafePath, acquired.Issue);
        }
        else
        {
            Assert.True(substitutionBlocked);
            Assert.True(acquired.IsAcquired);
        }
    }

    /// <summary>Healthy launch does not pre-read and hash the executable before stable custody hashes it.</summary>
    [Fact]
    public async Task AcquireLaunchLeaseDoesNotPreReadExecutableBytes()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        int executableReads = 0;
        var repository = new FileSystemInstalledLauncherRepository(ReadBoundedAsync);

        InstalledLauncherLaunchResult acquired = await repository.AcquireLaunchLeaseAsync(
            fixture.ManagedRoot,
            fixture.Admission,
            TestContext.Current.CancellationToken);

        Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
        Assert.Equal(0, executableReads);
        acquired.Lease!.Dispose();

        async ValueTask<byte[]?> ReadBoundedAsync(
            string path,
            int maximumBytes,
            CancellationToken cancellationToken)
        {
            if (string.Equals(path, fixture.LauncherPath, StringComparison.OrdinalIgnoreCase))
            {
                executableReads++;
            }
            FileInfo file = new(path);
            return !file.Exists || file.Length > maximumBytes
                ? null
                : await File.ReadAllBytesAsync(path, cancellationToken);
        }
    }

    /// <summary>The owner admission must pin the exact release manifest.</summary>
    [Fact]
    public async Task AdmissionPinsAnotherManifestReturnsInvalidManifest()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync();
        ManagedVersionAdmission wrong = fixture.Admission with
        {
            ReleaseManifestSha256 = new string('f', 64),
        };

        InstalledLauncherResult result = await new FileSystemInstalledLauncherRepository().VerifyAsync(
            fixture.ManagedRoot,
            wrong,
            TestContext.Current.CancellationToken);

        Assert.Null(result.Identity);
        Assert.Equal(InstalledLauncherIssue.InvalidManifest, result.Issue);
    }

    /// <summary>An unsupported launcher protocol fails at the strict manifest boundary.</summary>
    [Fact]
    public async Task UnsupportedProtocolReturnsInvalidManifest()
    {
        await using LauncherFixture fixture = await LauncherFixture.CreateAsync(protocolVersion: 2);

        InstalledLauncherResult result = await new FileSystemInstalledLauncherRepository().VerifyAsync(
            fixture.ManagedRoot,
            fixture.Admission,
            TestContext.Current.CancellationToken);

        Assert.Null(result.Identity);
        Assert.Equal(InstalledLauncherIssue.InvalidManifest, result.Issue);
    }

    private sealed class LauncherFixture : IAsyncDisposable
    {
        private static readonly string[] WorkerProtocols = ["1.0"];
        private LauncherFixture(
            string root,
            string managedRoot,
            string launcherPath,
            string launcherSha256,
            ManagedVersionAdmission admission)
        {
            Root = root;
            ManagedRoot = managedRoot;
            LauncherPath = launcherPath;
            LauncherSha256 = launcherSha256;
            Admission = admission;
        }

        public string Root { get; }
        public string ManagedRoot { get; }
        public string LauncherPath { get; }
        public string VersionRoot => Path.GetDirectoryName(Path.GetDirectoryName(LauncherPath)!)!;
        public string ManifestPath => Path.Combine(VersionRoot, "RELEASE-MANIFEST.json");
        public string LibraryPath => Path.Combine(VersionRoot, "NvtFwCombiner.Core.dll");
        public string LauncherSha256 { get; }
        public ManagedVersionAdmission Admission { get; }

        public string VersionPath(string relativePath)
        {
            return Path.Combine(
                VersionRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static async Task<LauncherFixture> CreateAsync(
            int protocolVersion = 1,
            bool useRunnableLauncher = false)
        {
            string root = Path.Combine(Path.GetTempPath(), $"nfc-launcher-repository-{Guid.NewGuid():N}");
            string managedRoot = Path.Combine(root, "managed");
            string versionRoot = Path.Combine(managedRoot, "versions", "1.0.0");
            _ = Directory.CreateDirectory(versionRoot);
            byte[] launcherBytes = useRunnableLauncher
                ? await File.ReadAllBytesAsync(
                    Environment.GetEnvironmentVariable("COMSPEC") ??
                        Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                    TestContext.Current.CancellationToken)
                : PortableExecutable(0x02);
            Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
            {
                ["NvtFwCombiner.exe"] = [0x4d, 0x5a, 0x01],
                ["NvtFwCombiner.Core.dll"] = [0x4d, 0x5a, 0x04],
                ["NvtFwCombiner.runtimeconfig.json"] = Encoding.UTF8.GetBytes("{}"),
                [ManagedLauncherIdentity.ExecutablePath] = launcherBytes,
                ["external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe"] = [0x4d, 0x5a, 0x03],
                ["THIRD-PARTY-NOTICES.txt"] = Encoding.UTF8.GetBytes("notices"),
                ["LICENSE.txt"] = Encoding.UTF8.GetBytes("license"),
                ["README.txt"] = Encoding.UTF8.GetBytes("readme"),
                ["docs/contracts/canonical-capability-policy-v1.json"] = Encoding.UTF8.GetBytes("{}"),
                ["profiles/built-in/catalog.json"] = Encoding.UTF8.GetBytes("{}"),
            };
            foreach ((string relativePath, byte[] bytes) in files)
            {
                string path = Path.Combine(versionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            }

            string launcherSha = Hash(files[ManagedLauncherIdentity.ExecutablePath]);
            object[] entries = [.. files.Select(pair => new
            {
                path = pair.Key,
                size = pair.Value.Length,
                sha256 = Hash(pair.Value),
                role = pair.Key switch
                {
                    "NvtFwCombiner.exe" or "NvtFwCombiner.Core.dll" or
                        "NvtFwCombiner.runtimeconfig.json" => "application",
                    ManagedLauncherIdentity.ExecutablePath => "launcher",
                    "THIRD-PARTY-NOTICES.txt" => "notices",
                    "LICENSE.txt" => "license",
                    "README.txt" => "readme",
                    "docs/contracts/canonical-capability-policy-v1.json" => "capabilityPolicy",
                    _ when pair.Key.StartsWith("profiles/built-in/", StringComparison.Ordinal) => "builtInProfile",
                    _ => "externalTool",
                },
            })];
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = "1.2",
                product = "NVT FW Combiner",
                version = "1.0.0",
                sourceCommit = new string('a', 40),
                sourceTag = "v1.0.0",
                runtimeIdentifier = "win-x64",
                licenseSpdx = "MIT",
                workerProtocolVersions = WorkerProtocols,
                approvedProcessorIds = Array.Empty<string>(),
                processorBundleSha256 = new string('b', 64),
                embeddedProfileCatalogSha256 = new string('c', 64),
                embeddedSchemaBundleSha256 = new string('d', 64),
                files = entries,
                sbomAsset = "NvtFwCombiner-v1.0.0-win-x64.spdx.json",
                provenanceAsset = "NvtFwCombiner-v1.0.0-win-x64.provenance.json",
                versionManagementProtocolVersion = protocolVersion,
                launcher = new
                {
                    launcherVersion = "1.0.0",
                    protocolVersion,
                    executableRelativePath = ManagedLauncherIdentity.ExecutablePath,
                    size = files[ManagedLauncherIdentity.ExecutablePath].Length,
                    sha256 = launcherSha,
                },
            });
            await File.WriteAllBytesAsync(
                Path.Combine(versionRoot, "RELEASE-MANIFEST.json"),
                manifest,
                TestContext.Current.CancellationToken);
            IEnumerable<(string Path, string Hash)> checksumEntries = files
                .Select(pair => (Path: pair.Key, Hash: Hash(pair.Value)))
                .Append((Path: "RELEASE-MANIFEST.json", Hash: Hash(manifest)))
                .OrderBy(entry => entry.Path, StringComparer.Ordinal);
            await File.WriteAllTextAsync(
                Path.Combine(versionRoot, "SHA256SUMS.txt"),
                string.Join("\n", checksumEntries.Select(entry =>
                    $"{entry.Hash}  {entry.Path}")) + "\n",
                TestContext.Current.CancellationToken);
            var admission = new ManagedVersionAdmission(
                ManagedAppVersion.Parse("1.0.0"),
                "catalog-admission-v1",
                Hash(manifest));
            await File.WriteAllBytesAsync(
                Path.Combine(versionRoot, FileSystemManagedVersionRepository.AdmissionFileName),
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    version = admission.Version.ToString(),
                    admissionIdentity = admission.AdmissionIdentity,
                    releaseManifestSha256 = admission.ReleaseManifestSha256,
                }),
                TestContext.Current.CancellationToken);
            return new(root, managedRoot, Path.Combine(versionRoot, "launcher", "NvtFwCombiner.Launcher.exe"), launcherSha, admission);
        }

        public ValueTask DisposeAsync()
        {
            Directory.Delete(Root, recursive: true);
            return ValueTask.CompletedTask;
        }

        private static string Hash(byte[] bytes)
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
    }

    private static byte[] PortableExecutable(byte marker)
    {
        byte[] bytes = new byte[68];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        bytes[0x3c] = 64;
        bytes[64] = (byte)'P';
        bytes[65] = (byte)'E';
        bytes[2] = marker;
        return bytes;
    }
}
