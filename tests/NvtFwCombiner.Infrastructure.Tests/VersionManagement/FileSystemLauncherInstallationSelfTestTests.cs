using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies the non-mutating managed installation health query.</summary>
public sealed class FileSystemLauncherInstallationSelfTestTests
{
    /// <summary>The query reuses exact admissions and leaves every durable byte untouched.</summary>
    [Fact]
    public async Task HealthyInstallationReturnsExactAdmissionWithoutMutation()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        IReadOnlyDictionary<string, byte[]> before = await fixture.ReadAllFilesAsync();

        LauncherInstallationSelfTestResult result = await new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            fixture.StatePath).QueryAsync(TestContext.Current.CancellationToken);
        IReadOnlyDictionary<string, byte[]> after = await fixture.ReadAllFilesAsync();

        Assert.True(result.IsHealthy);
        Assert.Equal(LauncherInstallationSelfTestIssue.None, result.Issue);
        Assert.Equal(fixture.BootstrapPath, result.Bootstrap!.Path);
        Assert.Equal(fixture.BootstrapSha256, result.Bootstrap.Sha256);
        Assert.Equal(fixture.Admission, result.ActiveLauncher!.OwnerAdmission);
        Assert.Equal(fixture.LauncherSha256, result.ActiveLauncher.Sha256);
        AssertFileSnapshotsEqual(before, after);
    }

    /// <summary>A pending launcher transaction is reported without a partial successful identity.</summary>
    [Fact]
    public async Task PendingLauncherReturnsTypedFailureWithoutMutation()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync(pendingLauncher: true);
        IReadOnlyDictionary<string, byte[]> before = await fixture.ReadAllFilesAsync();

        LauncherInstallationSelfTestResult result = await new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            fixture.StatePath).QueryAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsHealthy);
        Assert.Equal(LauncherInstallationSelfTestIssue.LauncherActivationPending, result.Issue);
        Assert.Null(result.Bootstrap);
        Assert.Null(result.ActiveLauncher);
        AssertFileSnapshotsEqual(before, await fixture.ReadAllFilesAsync());
    }

    /// <summary>A state generation changed during hashing never produces a mixed healthy result.</summary>
    [Fact]
    public async Task ConcurrentStateChangeReturnsNoPartialSuccess()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        var appStore = new SequenceAppStateStore(
            fixture.CreateAppState(),
            fixture.CreateAppState(retentionReviewDue: true));
        var launcherStore = new SequenceLauncherStateStore(
            fixture.CreateLauncherState(),
            fixture.CreateLauncherState());
        var selfTest = new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            appStore,
            launcherStore,
            new FixedLauncherRepository(fixture.ActiveLauncher));

        LauncherInstallationSelfTestResult result = await selfTest.QueryAsync(
            TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.StateChanged);
        Assert.Equal(2, appStore.LoadCount);
        Assert.Equal(2, launcherStore.LoadCount);
    }

    /// <summary>A launcher-journal generation changed during hashing also fails the complete snapshot.</summary>
    [Fact]
    public async Task ConcurrentLauncherStateChangeReturnsNoPartialSuccess()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        LauncherBootstrapState initial = fixture.CreateLauncherState();
        LauncherBootstrapState changed = LauncherBootstrapState.Create(
            fixture.ManagedRoot,
            fixture.ActiveLauncher,
            fixture.ActiveLauncher,
            pending: null,
            failed: fixture.ActiveLauncher);
        var selfTest = new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            new SequenceAppStateStore(fixture.CreateAppState(), fixture.CreateAppState()),
            new SequenceLauncherStateStore(initial, changed),
            new FixedLauncherRepository(fixture.ActiveLauncher));

        LauncherInstallationSelfTestResult result = await selfTest.QueryAsync(
            TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.StateChanged);
    }

    /// <summary>A terminal authority read failure is a changed snapshot, not a stale healthy result.</summary>
    [Fact]
    public async Task TerminalAppStateReadFailureReturnsStateChanged()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        var selfTest = new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            new SequenceAppStateStore(fixture.CreateAppState(), null),
            new SequenceLauncherStateStore(fixture.CreateLauncherState(), fixture.CreateLauncherState()),
            new FixedLauncherRepository(fixture.ActiveLauncher));

        LauncherInstallationSelfTestResult result = await selfTest.QueryAsync(
            TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.StateChanged);
    }

    /// <summary>A terminal launcher-authority read failure also rejects the complete observation.</summary>
    [Fact]
    public async Task TerminalLauncherStateReadFailureReturnsStateChanged()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        var selfTest = new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            new SequenceAppStateStore(fixture.CreateAppState(), fixture.CreateAppState()),
            new SequenceLauncherStateStore(fixture.CreateLauncherState(), null),
            new FixedLauncherRepository(fixture.ActiveLauncher));

        LauncherInstallationSelfTestResult result = await selfTest.QueryAsync(
            TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.StateChanged);
    }

    /// <summary>A missing application authority returns one typed result and no observations.</summary>
    [Fact]
    public async Task MissingAppStateReturnsNoPartialSuccess()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        File.Delete(fixture.StatePath);

        LauncherInstallationSelfTestResult result = await new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            fixture.StatePath).QueryAsync(TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.AppStateMissing);
    }

    /// <summary>An invalid launcher authority returns one typed result and no observations.</summary>
    [Fact]
    public async Task InvalidLauncherStateReturnsNoPartialSuccess()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        await File.WriteAllTextAsync(
            JsonLauncherBootstrapStateStore.DerivePath(fixture.StatePath),
            "{}",
            TestContext.Current.CancellationToken);

        LauncherInstallationSelfTestResult result = await new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            fixture.StatePath).QueryAsync(TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.LauncherStateInvalid);
    }

    /// <summary>A pending application mutation is never presented as installation health.</summary>
    [Fact]
    public async Task PendingAppMutationReturnsNoPartialSuccess()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        await new JsonVersionManagerStateStore(fixture.StatePath).SaveAsync(
            fixture.CreateAppState(
                pendingMutation: new PendingManagedVersionMutation(
                    ManagedVersionMutationKind.Delete,
                    fixture.Admission)),
            TestContext.Current.CancellationToken);

        LauncherInstallationSelfTestResult result = await new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            fixture.StatePath).QueryAsync(TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.AppTransactionPending);
    }

    /// <summary>A launcher bound to another exact admission fails before payload observation is published.</summary>
    [Fact]
    public async Task MismatchedLauncherAdmissionReturnsNoPartialSuccess()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        ManagedLauncherIdentity mismatch = ManagedLauncherIdentity.Create(
            fixture.Admission.Version,
            "different-admission",
            fixture.Admission.ReleaseManifestSha256,
            ManagedAppVersion.Parse("1.0.0"),
            1,
            ManagedLauncherIdentity.ExecutablePath,
            3,
            fixture.LauncherSha256);
        Assert.True((await new JsonLauncherBootstrapStateStore(fixture.StatePath).TrySaveAsync(
            LauncherBootstrapState.Create(fixture.ManagedRoot, mismatch, mismatch, pending: null, failed: null),
            TestContext.Current.CancellationToken)).IsSuccess);

        LauncherInstallationSelfTestResult result = await new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            fixture.StatePath).QueryAsync(TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.ActiveLauncherMismatch);
    }

    /// <summary>Launcher bytes changed after admission return the existing typed tamper result.</summary>
    [Fact]
    public async Task TamperedLauncherReturnsNoPartialSuccess()
    {
        await using SelfTestFixture fixture = await SelfTestFixture.CreateAsync();
        await File.AppendAllTextAsync(
            fixture.LauncherPath,
            "tampered",
            TestContext.Current.CancellationToken);

        LauncherInstallationSelfTestResult result = await new FileSystemLauncherInstallationSelfTest(
            fixture.ManagedRoot,
            fixture.StatePath).QueryAsync(TestContext.Current.CancellationToken);

        AssertFailure(result, LauncherInstallationSelfTestIssue.ActiveLauncherTampered);
    }

    private static void AssertFailure(
        LauncherInstallationSelfTestResult result,
        LauncherInstallationSelfTestIssue expected)
    {
        Assert.False(result.IsHealthy);
        Assert.Equal(expected, result.Issue);
        Assert.Null(result.Bootstrap);
        Assert.Null(result.ActiveLauncher);
    }

    private static void AssertFileSnapshotsEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys, actual.Keys);
        foreach ((string path, byte[] bytes) in expected)
        {
            Assert.Equal(bytes, actual[path]);
        }
    }

    private sealed class SelfTestFixture : IAsyncDisposable
    {
        private static readonly string[] WorkerProtocols = ["1.0"];

        private SelfTestFixture(
            string root,
            string managedRoot,
            string statePath,
            string bootstrapPath,
            string bootstrapSha256,
            string launcherSha256,
            ManagedVersionAdmission admission)
        {
            Root = root;
            ManagedRoot = managedRoot;
            StatePath = statePath;
            BootstrapPath = bootstrapPath;
            BootstrapSha256 = bootstrapSha256;
            LauncherSha256 = launcherSha256;
            Admission = admission;
        }

        public string Root { get; }
        public string ManagedRoot { get; }
        public string StatePath { get; }
        public string BootstrapPath { get; }
        public string BootstrapSha256 { get; }
        public string LauncherSha256 { get; }
        public string LauncherPath => Path.Combine(
            ManagedRoot,
            "versions",
            Admission.Version.ToString(),
            ManagedLauncherIdentity.ExecutablePath.Replace('/', Path.DirectorySeparatorChar));
        public ManagedVersionAdmission Admission { get; }
        public ManagedLauncherIdentity ActiveLauncher => ManagedLauncherIdentity.Create(
            Admission.Version,
            Admission.AdmissionIdentity,
            Admission.ReleaseManifestSha256,
            ManagedAppVersion.Parse("1.0.0"),
            1,
            ManagedLauncherIdentity.ExecutablePath,
            3,
            LauncherSha256);

        public VersionManagerState CreateAppState(
            bool retentionReviewDue = false,
            PendingManagedVersionMutation? pendingMutation = null)
        {
            return VersionManagerState.Create(
                updateSource: null,
                activeVersion: Admission.Version,
                lastKnownGoodVersion: Admission.Version,
                admissions: [Admission],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: retentionReviewDue,
                pendingMutation: pendingMutation,
                managedRootIdentity: ManagedRoot);
        }

        public LauncherBootstrapState CreateLauncherState()
        {
            return LauncherBootstrapState.Create(
                ManagedRoot,
                ActiveLauncher,
                ActiveLauncher,
                pending: null,
                failed: null);
        }

        public static async Task<SelfTestFixture> CreateAsync(bool pendingLauncher = false)
        {
            string root = Path.Combine(Path.GetTempPath(), $"nfc-launcher-self-test-{Guid.NewGuid():N}");
            string managedRoot = Path.Combine(root, "managed");
            string statePath = Path.Combine(root, "state", "version-manager.v1.json");
            string versionRoot = Path.Combine(managedRoot, "versions", "1.0.0");
            _ = Directory.CreateDirectory(versionRoot);
            string bootstrapPath = Path.Combine(managedRoot, "NvtFwCombiner.Bootstrap.exe");
            byte[] bootstrap = [0x4d, 0x5a, 0x42];
            await File.WriteAllBytesAsync(bootstrapPath, bootstrap, TestContext.Current.CancellationToken);
            Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
            {
                ["NvtFwCombiner.exe"] = [0x4d, 0x5a, 0x01],
                [ManagedLauncherIdentity.ExecutablePath] = [0x4d, 0x5a, 0x02],
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
                    "NvtFwCombiner.exe" => "application",
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
                versionManagementProtocolVersion = 1,
                launcher = new
                {
                    launcherVersion = "1.0.0",
                    protocolVersion = 1,
                    executableRelativePath = ManagedLauncherIdentity.ExecutablePath,
                    size = files[ManagedLauncherIdentity.ExecutablePath].Length,
                    sha256 = launcherSha,
                },
            });
            await File.WriteAllBytesAsync(
                Path.Combine(versionRoot, "RELEASE-MANIFEST.json"),
                manifest,
                TestContext.Current.CancellationToken);
            var admission = new ManagedVersionAdmission(
                ManagedAppVersion.Parse("1.0.0"),
                "catalog-admission-v1",
                Hash(manifest));
            VersionManagerState appState = VersionManagerState.Create(
                updateSource: null,
                activeVersion: admission.Version,
                lastKnownGoodVersion: admission.Version,
                admissions: [admission],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: managedRoot);
            await new JsonVersionManagerStateStore(statePath).SaveAsync(
                appState,
                TestContext.Current.CancellationToken);
            ManagedLauncherIdentity activeLauncher = ManagedLauncherIdentity.Create(
                admission.Version,
                admission.AdmissionIdentity,
                admission.ReleaseManifestSha256,
                ManagedAppVersion.Parse("1.0.0"),
                1,
                ManagedLauncherIdentity.ExecutablePath,
                files[ManagedLauncherIdentity.ExecutablePath].Length,
                launcherSha);
            PendingLauncherActivation? pending = pendingLauncher
                ? PendingLauncherActivation.Create(
                    activeLauncher,
                    previousActive: null,
                    previousLastKnownGood: null,
                    LauncherActivationPhase.Requested)
                : null;
            LauncherBootstrapState launcherState = pendingLauncher
                ? LauncherBootstrapState.Create(managedRoot, null, null, pending, failed: null)
                : LauncherBootstrapState.Create(
                    managedRoot,
                    activeLauncher,
                    activeLauncher,
                    pending: null,
                    failed: null);
            Assert.True((await new JsonLauncherBootstrapStateStore(statePath).TrySaveAsync(
                launcherState,
                TestContext.Current.CancellationToken)).IsSuccess);
            return new(
                root,
                managedRoot,
                statePath,
                bootstrapPath,
                Hash(bootstrap),
                launcherSha,
                admission);
        }

        public async Task<IReadOnlyDictionary<string, byte[]>> ReadAllFilesAsync()
        {
            var result = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                result[Path.GetRelativePath(Root, path)] = await File.ReadAllBytesAsync(
                    path,
                    TestContext.Current.CancellationToken);
            }
            return result;
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

    private sealed class FixedLauncherRepository(ManagedLauncherIdentity identity) : IInstalledLauncherRepository
    {
        public ValueTask<InstalledLauncherResult> VerifyAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new InstalledLauncherResult(identity, InstalledLauncherIssue.None));
        }
    }

    private sealed class SequenceAppStateStore(params VersionManagerState?[] states) : IVersionManagerStateStore
    {
        private readonly Queue<VersionManagerState?> _states = new(states);

        public int LoadCount { get; private set; }

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Read-only self-test must not acquire a writer lease.");
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            VersionManagerState? state = _states.Dequeue();
            return ValueTask.FromResult(state is null
                ? new VersionManagerStateLoadResult(null, VersionManagerStateLoadIssue.Unavailable)
                : new VersionManagerStateLoadResult(state, VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(VersionManagerState state, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Read-only self-test must not save app state.");
        }
    }

    private sealed class SequenceLauncherStateStore(params LauncherBootstrapState?[] states)
        : ILauncherBootstrapStateStore
    {
        private readonly Queue<LauncherBootstrapState?> _states = new(states);

        public int LoadCount { get; private set; }

        public ValueTask<LauncherBootstrapStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            LauncherBootstrapState? state = _states.Dequeue();
            return ValueTask.FromResult(state is null
                ? new LauncherBootstrapStateLoadResult(null, LauncherBootstrapStateLoadIssue.Unavailable)
                : new LauncherBootstrapStateLoadResult(state, LauncherBootstrapStateLoadIssue.None));
        }

        public ValueTask<LauncherBootstrapStateSaveResult> TrySaveAsync(
            LauncherBootstrapState state,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Read-only self-test must not save launcher state.");
        }
    }
}
