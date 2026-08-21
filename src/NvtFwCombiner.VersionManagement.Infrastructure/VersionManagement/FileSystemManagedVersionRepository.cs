using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Stages, inventories, and deletes exact launcher-owned managed payloads.</summary>
public sealed class FileSystemManagedVersionRepository : IManagedVersionRepository
{
    internal const string VersionsDirectoryName = "versions";
    internal const string StagingDirectoryName = ".staging";
    internal const string AdmissionFileName = ".managed-admission.v1.json";
    internal const int MaximumArchiveEntries = 4096;
    internal const long MaximumExpandedBytes = 512L * 1024 * 1024;
    internal const int MaximumManifestBytes = 1024 * 1024;
    internal const int MaximumAdmissionBytes = 4096;

    private static readonly JsonSerializerOptions AdmissionJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };
    private static readonly VersionManagementJsonContext AdmissionJsonContext = new(AdmissionJsonOptions);
    private readonly long _maximumExpandedBytes;

    /// <summary>Creates the production repository with the owner-approved 512 MiB expanded-byte ceiling.</summary>
    public FileSystemManagedVersionRepository()
        : this(MaximumExpandedBytes)
    {
    }

    internal FileSystemManagedVersionRepository(long maximumExpandedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedBytes);
        _maximumExpandedBytes = maximumExpandedBytes;
    }

    /// <inheritdoc />
    public async ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
        string sourceRoot,
        UpdateCatalogVersionSnapshot package,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentNullException.ThrowIfNull(package);
        try
        {
            string source = Path.GetFullPath(sourceRoot);
            if (!ManagedPathSafety.IsSafeExistingDirectory(source) ||
                !ManagedPathSafety.TryResolveRelativeFile(source, package.PackagePath.Value, out string packagePath))
            {
                return new(null, ManagedVersionInstallIssue.PackageUnavailable);
            }

            await using FileStream packageStream = OpenStablePackage(packagePath, package.PackageSize);
            string actualPackageHash = await HashAsync(packageStream, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualPackageHash, package.PackageSha256, StringComparison.Ordinal))
            {
                return new(null, ManagedVersionInstallIssue.PackageMismatch);
            }
            packageStream.Position = 0;
            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
            ManagedPackagePlanResult plan = await ManagedPackageVerifier.CreatePlanAsync(
                archive,
                package,
                _maximumExpandedBytes,
                cancellationToken).ConfigureAwait(false);
            return plan.IsSuccess
                ? new(new(package.Version, package.Identity, package.ReleaseNotes), ManagedVersionInstallIssue.None)
                : new(null, plan.Issue);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new(null, ManagedVersionInstallIssue.PackageUnavailable);
        }
    }

    /// <inheritdoc />
    public async ValueTask<ManagedVersionInstallResult> InstallAsync(
        string managedRoot,
        string sourceRoot,
        UpdateCatalogVersionSnapshot package,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentNullException.ThrowIfNull(package);
        cancellationToken.ThrowIfCancellationRequested();

        string? stagingDirectory = null;
        try
        {
            string root = Path.GetFullPath(managedRoot);
            string source = Path.GetFullPath(sourceRoot);
            if (!ManagedPathSafety.IsSafeExistingDirectory(source) ||
                !ManagedPathSafety.TryResolveRelativeFile(source, package.PackagePath.Value, out string packagePath))
            {
                return Failure(ManagedVersionInstallIssue.PackageUnavailable);
            }

            await using FileStream packageStream = OpenStablePackage(packagePath, package.PackageSize);
            string actualPackageHash = await HashAsync(packageStream, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualPackageHash, package.PackageSha256, StringComparison.Ordinal))
            {
                return Failure(ManagedVersionInstallIssue.PackageMismatch);
            }
            packageStream.Position = 0;

            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
            ManagedPackagePlanResult planResult = await ManagedPackageVerifier.CreatePlanAsync(
                archive,
                package,
                _maximumExpandedBytes,
                cancellationToken).ConfigureAwait(false);
            if (!planResult.IsSuccess)
            {
                return Failure(planResult.Issue);
            }

            ManagedPackagePlan plan = planResult.Plan!;
            var admission = new ManagedVersionAdmission(
                package.Version,
                package.Identity,
                package.ReleaseManifestSha256);
            string versionsRoot = Path.Combine(root, VersionsDirectoryName);
            string stagingRoot = Path.Combine(root, StagingDirectoryName);
            string target = ManagedPathSafety.GetExactVersionDirectory(versionsRoot, package.Version);
            _ = Directory.CreateDirectory(root);
            if (!ManagedPathSafety.IsSafeExistingDirectory(root))
            {
                return Failure(ManagedVersionInstallIssue.PromotionFailed);
            }
            _ = Directory.CreateDirectory(versionsRoot);
            _ = Directory.CreateDirectory(stagingRoot);
            if (!ManagedPathSafety.IsSafeExistingDirectory(versionsRoot) ||
                !ManagedPathSafety.IsSafeExistingDirectory(stagingRoot))
            {
                return Failure(ManagedVersionInstallIssue.PromotionFailed);
            }

            if (Directory.Exists(target))
            {
                ManagedVersionAdmission? installedAdmission = await ReadAdmissionAsync(
                    target,
                    cancellationToken).ConfigureAwait(false);
                if (installedAdmission != admission)
                {
                    return Failure(ManagedVersionInstallIssue.IdentityConflict);
                }

                ManagedVersionDamageReason? damage = await ManagedPackageVerifier.VerifyInstalledAsync(
                    target,
                    admission,
                    _maximumExpandedBytes,
                    cancellationToken).ConfigureAwait(false);
                return damage is null
                    ? new(admission, ManagedVersionInstallIssue.None, WasAlreadyInstalled: true)
                    : Failure(ManagedVersionInstallIssue.IdentityConflict);
            }

            stagingDirectory = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(stagingDirectory);
            await ManagedPackageVerifier.ExtractAsync(
                plan,
                stagingDirectory,
                _maximumExpandedBytes,
                cancellationToken).ConfigureAwait(false);
            await WriteAdmissionAsync(stagingDirectory, admission, cancellationToken).ConfigureAwait(false);
            ManagedVersionDamageReason? stagedDamage = await ManagedPackageVerifier.VerifyInstalledAsync(
                stagingDirectory,
                admission,
                _maximumExpandedBytes,
                cancellationToken).ConfigureAwait(false);
            if (stagedDamage is not null)
            {
                return Failure(ManagedVersionInstallIssue.InvalidPayload);
            }

            Directory.Move(stagingDirectory, target);
            stagingDirectory = null;
            return new(admission, ManagedVersionInstallIssue.None, WasAlreadyInstalled: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return Failure(ManagedVersionInstallIssue.PackageUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(ManagedVersionInstallIssue.PromotionFailed);
        }
        catch (InvalidDataException)
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }
        catch (IOException)
        {
            return Failure(ManagedVersionInstallIssue.PromotionFailed);
        }
        finally
        {
            if (stagingDirectory is not null && Directory.Exists(stagingDirectory))
            {
                TryDeleteOwnedDirectory(stagingDirectory);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<ManagedVersionInventory> InventoryAsync(
        string managedRoot,
        IReadOnlyList<ManagedVersionAdmission> admissions,
        ManagedAppVersion? activeVersion,
        ManagedAppVersion? lastKnownGoodVersion,
        ManagedAppVersion? failedActivationVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentNullException.ThrowIfNull(admissions);
        string versionsRoot = Path.Combine(Path.GetFullPath(managedRoot), VersionsDirectoryName);
        var rows = new List<InstalledVersionSnapshot>();
        foreach (ManagedVersionAdmission admission in admissions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string target = ManagedPathSafety.GetExactVersionDirectory(versionsRoot, admission.Version);
            ManagedVersionDamageReason? damage = failedActivationVersion == admission.Version
                ? ManagedVersionDamageReason.FailedActivation
                : await ManagedPackageVerifier.VerifyInstalledAsync(
                    target,
                    admission,
                    _maximumExpandedBytes,
                    cancellationToken).ConfigureAwait(false);
            rows.Add(new(
                admission.Version,
                admission.AdmissionIdentity,
                damage is null ? ManagedVersionIntegrity.Healthy : ManagedVersionIntegrity.Damaged,
                damage,
                activeVersion == admission.Version,
                lastKnownGoodVersion == admission.Version,
                ManagedVersionAdmissionState.Admitted,
                admission));
        }

        if (Directory.Exists(versionsRoot) && ManagedPathSafety.IsSafeExistingDirectory(versionsRoot))
        {
            HashSet<ManagedAppVersion> known = [.. admissions.Select(admission => admission.Version)];
            foreach (string directory in Directory.EnumerateDirectories(versionsRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileName(directory);
                if (ManagedAppVersion.TryParse(name, out ManagedAppVersion version) && known.Add(version))
                {
                    ManagedVersionAdmission? observed = await ReadAdmissionAsync(
                        directory,
                        cancellationToken).ConfigureAwait(false);
                    bool hasMatchingSelfAdmission = observed?.Version == version;
                    ManagedVersionDamageReason? damage = hasMatchingSelfAdmission
                        ? await ManagedPackageVerifier.VerifyInstalledAsync(
                            directory,
                            observed!,
                            _maximumExpandedBytes,
                            cancellationToken).ConfigureAwait(false)
                        : ManagedVersionDamageReason.UnexpectedPath;
                    rows.Add(new(
                        version,
                        observed?.AdmissionIdentity ?? $"unadmitted:{version}",
                        damage is null ? ManagedVersionIntegrity.Healthy : ManagedVersionIntegrity.Damaged,
                        damage,
                        activeVersion == version,
                        lastKnownGoodVersion == version,
                        ManagedVersionAdmissionState.Unadmitted,
                        observed));
                }
            }
        }

        return ManagedVersionInventory.Create(rows);
    }

    /// <inheritdoc />
    public async ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
        string managedRoot,
        ManagedVersionAdmission admission,
        ManagedAppVersion? activeVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentNullException.ThrowIfNull(admission);
        cancellationToken.ThrowIfCancellationRequested();
        if (activeVersion == admission.Version)
        {
            return ManagedVersionDeleteIssue.ActiveVersion;
        }

        try
        {
            string versionsRoot = Path.Combine(Path.GetFullPath(managedRoot), VersionsDirectoryName);
            string target = ManagedPathSafety.GetExactVersionDirectory(versionsRoot, admission.Version);
            if (!Directory.Exists(target))
            {
                return ManagedVersionDeleteIssue.NotInstalled;
            }
            if (!ManagedPathSafety.IsSafeOwnedTree(target))
            {
                return ManagedVersionDeleteIssue.UnsafeTarget;
            }

            ManagedVersionAdmission? installed = await ReadAdmissionAsync(target, cancellationToken).ConfigureAwait(false);
            if (installed != admission)
            {
                return ManagedVersionDeleteIssue.UnsafeTarget;
            }

            Directory.Delete(target, recursive: true);
            return ManagedVersionDeleteIssue.None;
        }
        catch (UnauthorizedAccessException)
        {
            return ManagedVersionDeleteIssue.DeleteFailed;
        }
        catch (IOException)
        {
            return ManagedVersionDeleteIssue.DeleteFailed;
        }
    }

    internal static async ValueTask<ManagedVersionAdmission?> ReadAdmissionAsync(
        string versionRoot,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(versionRoot, AdmissionFileName);
        byte[]? bytes = await ManagedPathSafety.ReadBoundedFileAsync(
            path,
            MaximumAdmissionBytes,
            cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return null;
        }
        try
        {
            ManagedVersionAdmissionFileDocument? document =
                JsonSerializer.Deserialize(bytes, AdmissionJsonContext.ManagedVersionAdmissionFileDocument);
            return document is not null &&
                   ManagedAppVersion.TryParse(document.Version, out ManagedAppVersion version) &&
                   !string.IsNullOrWhiteSpace(document.AdmissionIdentity) &&
                   IsLowerSha256(document.ReleaseManifestSha256)
                ? new(version, document.AdmissionIdentity, document.ReleaseManifestSha256)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async ValueTask WriteAdmissionAsync(
        string versionRoot,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        var document = new ManagedVersionAdmissionFileDocument(
            admission.Version.ToString(),
            admission.AdmissionIdentity,
            admission.ReleaseManifestSha256);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            document,
            AdmissionJsonContext.ManagedVersionAdmissionFileDocument);
        await File.WriteAllBytesAsync(
            Path.Combine(versionRoot, AdmissionFileName),
            bytes,
            cancellationToken).ConfigureAwait(false);
    }

    private static FileStream OpenStablePackage(string path, long admittedLength)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length != admittedLength)
        {
            stream.Dispose();
            throw new FileNotFoundException("Package length differs from the admitted catalog entry.", path);
        }
        return stream;
    }

    private static async ValueTask<string> HashAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = GC.AllocateUninitializedArray<byte>(64 * 1024);
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static ManagedVersionInstallResult Failure(ManagedVersionInstallIssue issue)
    {
        return new(null, issue, WasAlreadyInstalled: false);
    }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    private static void TryDeleteOwnedDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

}

internal sealed record ManagedVersionAdmissionFileDocument(
    string Version,
    string AdmissionIdentity,
    string ReleaseManifestSha256);
