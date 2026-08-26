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
        public ManagedVersionAdmission Admission { get; }

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
}
