using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    private static async Task<(ManagedFirstInstallationMaterializationResult Result, string Marker)>
        MaterializePromotedInstallationAsync(string parent)
    {
        string root = Path.Combine(parent, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(parent);
        ManagedVersionAdmission admission = Admission(candidate);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new MaterializingRepository(admission));
        using var payload = new TestPayloadCapture(PayloadIdentity());
        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(parent, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Issue.ToString());
        return (result, FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root));
    }

    private static ManagedDistributionPayloadIdentity PayloadIdentity(
        string bootstrapFileName = "NvtFwCombiner.Bootstrap.exe")
    {
        return new(
            ManagedAppVersion.Parse("1.0.4"),
            new string('c', 40),
            "distribution-launcher"u8.Length,
            Hash("distribution-launcher"u8),
            512,
            HashA,
            new ManagedImmutableBootstrapIdentity(
                bootstrapFileName,
                "immutable-bootstrap"u8.Length,
                Hash("immutable-bootstrap"u8)));
    }

    private static byte[] PayloadDescriptor(byte[] bootstrap)
    {
        return PayloadDescriptor(bootstrap.LongLength, Hash(bootstrap));
    }

    private static byte[] PayloadDescriptor(
        long bootstrapSize,
        string bootstrapSha256,
        string? sourceCommit = null)
    {
        sourceCommit ??= new string('c', 40);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "1.0",
            product = "NVT FW Combiner",
            payloadKind = "distribution-launcher-bootstrap",
            launcherSetupProtocolVersion = 1,
            launcherVersion = "1.0.4",
            runtimeIdentifier = "win-x64",
            sourceCommit,
            bootstrap = new
            {
                resourceName = "NvtFwCombiner.DistributionLauncher.Payload.NvtFwCombiner.Bootstrap.exe",
                installedFileName = "NvtFwCombiner.Bootstrap.exe",
                size = bootstrapSize,
                sha256 = bootstrapSha256,
                versionManagementProtocolVersion = 1,
                sourceCommit,
            },
        });
    }

    private static FreshInstallationCandidate Candidate(string parent)
    {
        string sourceRoot = Path.Combine(parent, "source");
        _ = Directory.CreateDirectory(sourceRoot);
        var document = new UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [new(
                "1.0.4",
                "2026-08-29T00:00:00Z",
                "packages/NvtFwCombiner-v1.0.4-win-x64.zip",
                4096,
                HashA,
                HashB,
                "Release 1.0.4")]);
        UpdateCatalogVersionSnapshot package = Assert.IsType<UpdateCatalogSnapshot>(
            UpdateCatalogValidator.Validate(document).Snapshot).Versions[0];
        var identity = new FreshInstallationCandidateIdentity(
            "nvt-fw-combiner-production",
            1,
            HashA,
            1,
            package.Version,
            HashB,
            Path.Combine(sourceRoot, "update-catalog.v1.json"),
            sourceRoot,
            UpdateSourceRegistryEntryStatus.Latest,
            package.PackagePath.Value,
            package.PackageSize,
            package.PackageSha256,
            package.ReleaseManifestSha256);
        return new(
            identity,
            package,
            new VerifiedUpdateCandidate(package.Version, package.Identity, package.ReleaseNotes));
    }

    private static FreshInstallationCandidate RealPackageCandidate(string parent)
    {
        string sourceRoot = Path.Combine(parent, "source-real");
        _ = Directory.CreateDirectory(sourceRoot);
        UpdateCatalogVersionSnapshot package = FileSystemManagedVersionRepositoryTests.CreatePackageForManagedSetup(
            sourceRoot,
            "1.0.4",
            includeManagedLauncher: true);
        var identity = new FreshInstallationCandidateIdentity(
            "nvt-fw-combiner-production",
            1,
            HashA,
            1,
            package.Version,
            HashB,
            Path.Combine(sourceRoot, "update-catalog.v1.json"),
            sourceRoot,
            UpdateSourceRegistryEntryStatus.Latest,
            package.PackagePath.Value,
            package.PackageSize,
            package.PackageSha256,
            package.ReleaseManifestSha256);
        return new(
            identity,
            package,
            new VerifiedUpdateCandidate(package.Version, package.Identity, package.ReleaseNotes));
    }

    private static ManagedVersionAdmission Admission(FreshInstallationCandidate candidate)
    {
        return new(
            candidate.Package.Version,
            candidate.Package.Identity,
            candidate.Package.ReleaseManifestSha256);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private sealed class TestPayloadCapture(
        ManagedDistributionPayloadIdentity identity,
        string launcherContent = "distribution-launcher")
        : IManagedDistributionPayloadCapture, IManagedDistributionPayloadContent
    {
        public ManagedDistributionPayloadIdentity Identity => identity;
        internal int CopyCount { get; private set; }

        public ValueTask CopyBootstrapAsync(string destination, CancellationToken cancellationToken)
        {
            CopyCount++;
            return new(File.WriteAllTextAsync(destination, "immutable-bootstrap", cancellationToken));
        }

        public ValueTask CopyDistributionLauncherAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            CopyCount++;
            return new(File.WriteAllTextAsync(destination, launcherContent, cancellationToken));
        }

        public void Dispose()
        {
        }
    }

    private sealed class OpaquePayloadCapture(ManagedDistributionPayloadIdentity identity)
        : IManagedDistributionPayloadCapture
    {
        public ManagedDistributionPayloadIdentity Identity => identity;

        public void Dispose()
        {
        }
    }

    private sealed class TrackingResource(byte[]? bytes)
    {
        internal bool CanRead { get; init; } = true;
        internal bool CanSeek { get; init; } = true;
        internal int BytesRead { get; private set; }
        internal int LengthReadCount { get; private set; }
        internal int MaximumReadChunk { get; init; } = int.MaxValue;
        internal int OpenCount { get; private set; }
        internal Func<Exception>? OpenException { get; init; }
        internal Func<Exception>? LengthException { get; init; }
        internal Func<Exception>? ReadException { get; init; }
        internal long? ReportedLength { get; init; }

        internal TrackingResourceStream? Open()
        {
            OpenCount++;
            return OpenException is not null
                ? throw OpenException()
                : bytes is null
                    ? null
                    : new TrackingResourceStream(this, bytes);
        }

        internal long ObserveLength(long actual)
        {
            LengthReadCount++;
            return LengthException is not null
                ? throw LengthException()
                : ReportedLength ?? actual;
        }

        internal int Read(Memory<byte> destination, MemoryStream source)
        {
            if (ReadException is not null)
            {
                throw ReadException();
            }
            int requested = Math.Min(destination.Length, MaximumReadChunk);
            int read = source.Read(destination.Span[..requested]);
            BytesRead += read;
            return read;
        }
    }

    private sealed class TrackingResourceStream : Stream
    {
        private readonly TrackingResource _owner;
        private readonly MemoryStream _source;

        internal TrackingResourceStream(TrackingResource owner, byte[] bytes)
        {
            _owner = owner;
            _source = new MemoryStream(bytes, writable: false);
        }

        public override bool CanRead => _owner.CanRead;
        public override bool CanSeek => _owner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _owner.ObserveLength(_source.Length);
        public override long Position
        {
            get => _source.Position;
            set => _source.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _owner.Read(buffer.AsMemory(offset, count), _source);
        }

        public override int Read(Span<byte> buffer)
        {
            var temporary = new byte[buffer.Length];
            int read = _owner.Read(temporary, _source);
            temporary.AsSpan(0, read).CopyTo(buffer);
            return read;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_owner.Read(buffer, _source));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _owner.CanSeek
                ? _source.Seek(offset, origin)
                : throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _source.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private sealed class MaterializingRepository(
        ManagedVersionAdmission admission,
        Action<int, string>? inventoryObserved = null,
        bool createRepositoryStaging = false)
        : IManagedVersionRepository, IWindowsCustodiedManagedVersionRepository
    {
        internal int InstallCount { get; private set; }
        internal int InventoryCount { get; private set; }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InstallCount++;
            if (createRepositoryStaging)
            {
                _ = Directory.CreateDirectory(Path.Combine(
                    managedRoot,
                    FileSystemManagedVersionRepository.StagingDirectoryName));
            }
            string versionRoot = Path.Combine(managedRoot, "versions", package.Version.ToString());
            _ = Directory.CreateDirectory(versionRoot);
            File.WriteAllText(Path.Combine(versionRoot, "payload.txt"), "installed");
            return ValueTask.FromResult(new ManagedVersionInstallResult(
                admission,
                ManagedVersionInstallIssue.None,
                WasAlreadyInstalled: false));
        }

        async ValueTask<ManagedVersionPayloadMaterializationResult>
            IWindowsCustodiedManagedVersionRepository.MaterializeVerifiedPayloadWithinHeldRootAsync(
                WindowsStableRelativeWriteRoot writeRoot,
                string sourceRoot,
                UpdateCatalogVersionSnapshot package,
                Action<string>? afterPackageDirectoryCreated,
                CancellationToken cancellationToken)
        {
            ManagedVersionInstallResult result = await InstallAsync(
                writeRoot.RootPath,
                sourceRoot,
                package,
                cancellationToken);
            return new(result.Admission, result.Issue, result.WasAlreadyInstalled);
        }

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InventoryCount++;
            inventoryObserved?.Invoke(InventoryCount, managedRoot);
            ManagedVersionInventory inventory = ManagedVersionInventory.Create(
                [new(
                    admission.Version,
                    admission.AdmissionIdentity,
                    ManagedVersionIntegrity.Healthy,
                    DamageReason: null,
                    IsActive: true,
                    IsLastKnownGood: true,
                    ManagedVersionAdmissionState.Admitted,
                    admission)]);
            return ValueTask.FromResult(ManagedVersionInventoryReadResult.Success(inventory));
        }

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission target,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TemporaryRoot : IAsyncDisposable
    {
        internal TemporaryRoot()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nfc-distribution-runtime-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public ValueTask DisposeAsync()
        {
            Directory.Delete(Path, recursive: true);
            return ValueTask.CompletedTask;
        }
    }

}
