using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies installed launcher release identity and content admission.</summary>
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
        public string LauncherSha256 { get; }
        public ManagedVersionAdmission Admission { get; }

        public static async Task<LauncherFixture> CreateAsync(int protocolVersion = 1)
        {
            string root = Path.Combine(Path.GetTempPath(), $"nfc-launcher-repository-{Guid.NewGuid():N}");
            string managedRoot = Path.Combine(root, "managed");
            string versionRoot = Path.Combine(managedRoot, "versions", "1.0.0");
            _ = Directory.CreateDirectory(versionRoot);
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
            var admission = new ManagedVersionAdmission(
                ManagedAppVersion.Parse("1.0.0"),
                "catalog-admission-v1",
                Hash(manifest));
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
}
