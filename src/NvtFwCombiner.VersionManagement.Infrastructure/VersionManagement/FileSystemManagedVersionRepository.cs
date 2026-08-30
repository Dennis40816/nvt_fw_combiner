using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Stages, inventories, and deletes exact launcher-owned managed payloads.</summary>
public sealed partial class FileSystemManagedVersionRepository :
    IManagedVersionRepository,
    IWindowsCustodiedManagedVersionRepository
{
    internal const string VersionsDirectoryName = "versions";
    internal const string StagingDirectoryName = ".staging";
    internal const string AdmissionFileName = ".managed-admission.v1.json";
    internal const int MaximumArchiveEntries = 4096;
    internal const long MaximumExpandedBytes = 512L * 1024 * 1024;
    internal const int MaximumManifestBytes = 1024 * 1024;
    internal const int MaximumAdmissionBytes = 4096;
    internal const int MaximumInstalledFiles = MaximumArchiveEntries + 1;
    internal const int MaximumInstalledDirectories = MaximumArchiveEntries;
    internal const long MaximumInstalledBytes = MaximumExpandedBytes + MaximumAdmissionBytes;

    private static readonly JsonSerializerOptions AdmissionJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };
    private static readonly VersionManagementJsonContext AdmissionJsonContext = new(AdmissionJsonOptions);
    private readonly Func<string, bool> _directoryExists;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;
    private readonly Action<WindowsStableCustodyStage>? _custodyHook;
    private readonly Action? _beforeLeaseCreation;
    private readonly Action<string>? _beforePackagePromotion;
    private readonly Action<string>? _afterPackageDirectoryCreated;
    private readonly long _maximumExpandedBytes;

    /// <inheritdoc />
    public async ValueTask<ManagedExecutableLaunchLeaseResult> AcquireApplicationLaunchLeaseAsync(
        string managedRoot,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentNullException.ThrowIfNull(admission);
        WindowsStablePathCustody? custody = null;
        try
        {
            if (!Path.IsPathFullyQualified(managedRoot))
            {
                return new(null, ManagedExecutableLaunchIssue.UnsafePath);
            }
            string versionsRoot = Path.Combine(Path.GetFullPath(managedRoot), VersionsDirectoryName);
            string versionRoot = ManagedPathSafety.GetExactVersionDirectory(versionsRoot, admission.Version);
            WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireImmutableTree(
                versionRoot,
                _custodyHook,
                WindowsStableTreeLimits.ForInstalledVersion(_maximumExpandedBytes),
                cancellationToken);
            if (!acquired.IsAcquired)
            {
                return new(null, MapCustodyIssue(acquired.Issue));
            }
            custody = acquired.Custody!;
            ManagedVersionDamageReason? damage = await ManagedPackageVerifier.VerifyInstalledAsync(
                versionRoot,
                admission,
                _maximumExpandedBytes,
                cancellationToken).ConfigureAwait(false);
            if (damage is not null)
            {
                return new(null, ManagedExecutableLaunchIssue.Tampered);
            }
            byte[]? manifestBytes = await ReadBoundedFileAsync(
                custody,
                "RELEASE-MANIFEST.json",
                MaximumManifestBytes,
                cancellationToken).ConfigureAwait(false);
            if (manifestBytes is null ||
                !string.Equals(
                    Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
                    admission.ReleaseManifestSha256,
                    StringComparison.Ordinal))
            {
                return new(null, ManagedExecutableLaunchIssue.Tampered);
            }
            ReleaseManifestDocument? manifest = JsonSerializer.Deserialize(
                manifestBytes,
                AdmissionJsonContext.ReleaseManifestDocument);
            ReleaseManifestFileDocument[] applications =
            [.. manifest?.Files?.Where(file =>
                string.Equals(file.Path, "NvtFwCombiner.exe", StringComparison.Ordinal) &&
                string.Equals(file.Role, "application", StringComparison.Ordinal)) ?? []];
            if (applications is not [var application])
            {
                return new(null, ManagedExecutableLaunchIssue.Tampered);
            }
            _beforeLeaseCreation?.Invoke();
            WindowsStablePathCustody ownedCustody = custody;
            custody = null;
            return await StableManagedExecutableLaunchLease.TryCreateFromVerifiedTreeAsync(
                ownedCustody,
                application.Path,
                application.Size,
                application.Sha256,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new(null, ManagedExecutableLaunchIssue.Unavailable);
        }
        finally
        {
            custody?.Dispose();
        }
    }

    /// <summary>Creates the production repository with the owner-approved 512 MiB expanded-byte ceiling.</summary>
    public FileSystemManagedVersionRepository()
        : this(MaximumExpandedBytes, Directory.EnumerateDirectories, Directory.Exists)
    {
    }

    internal FileSystemManagedVersionRepository(long maximumExpandedBytes)
        : this(maximumExpandedBytes, Directory.EnumerateDirectories, Directory.Exists)
    {
    }

    internal FileSystemManagedVersionRepository(
        long maximumExpandedBytes,
        Func<string, IEnumerable<string>> enumerateDirectories,
        Func<string, bool>? directoryExists = null,
        Action<WindowsStableCustodyStage>? custodyHook = null,
        Action? beforeLeaseCreation = null,
        Action<string>? beforePackagePromotion = null,
        Action<string>? afterPackageDirectoryCreated = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedBytes);
        _enumerateDirectories = enumerateDirectories ??
            throw new ArgumentNullException(nameof(enumerateDirectories));
        _directoryExists = directoryExists ?? Directory.Exists;
        _custodyHook = custodyHook;
        _beforeLeaseCreation = beforeLeaseCreation;
        _beforePackagePromotion = beforePackagePromotion;
        _afterPackageDirectoryCreated = afterPackageDirectoryCreated;
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
                {
                    HasSupportedManagedLauncher = plan.Plan!.HasSupportedManagedLauncher,
                }
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
    public async ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
        string managedRoot,
        IReadOnlyList<ManagedVersionAdmission> admissions,
        ManagedAppVersion? activeVersion,
        ManagedAppVersion? lastKnownGoodVersion,
        ManagedAppVersion? failedActivationVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentNullException.ThrowIfNull(admissions);
        try
        {
            string versionsRoot = Path.Combine(Path.GetFullPath(managedRoot), VersionsDirectoryName);
            bool versionsRootExists;
            try
            {
                FileAttributes attributes = File.GetAttributes(versionsRoot);
                versionsRootExists = (attributes & FileAttributes.Directory) != 0;
                if (!versionsRootExists)
                {
                    return ManagedVersionInventoryReadResult.Unavailable();
                }
            }
            catch (FileNotFoundException)
            {
                versionsRootExists = false;
            }
            catch (DirectoryNotFoundException)
            {
                versionsRootExists = false;
            }
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

            if (versionsRootExists)
            {
                if (!ManagedPathSafety.IsSafeExistingDirectory(versionsRoot))
                {
                    return ManagedVersionInventoryReadResult.Unavailable();
                }

                string[] directories = [.. _enumerateDirectories(versionsRoot)];
                HashSet<ManagedAppVersion> known = [.. admissions.Select(admission => admission.Version)];
                foreach (string directory in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string name = Path.GetFileName(directory);
                    if (ManagedAppVersion.TryParse(name, out ManagedAppVersion version) && known.Add(version))
                    {
                        ManagedVersionAdmission? observed = await ReadAdmissionAsync(
                            directory,
                            cancellationToken).ConfigureAwait(false);
                        if (observed is null && !_directoryExists(directory))
                        {
                            return ManagedVersionInventoryReadResult.Unavailable();
                        }
                        bool hasMatchingSelfAdmission = observed?.Version == version;
                        ManagedVersionDamageReason? damage = hasMatchingSelfAdmission
                            ? await ManagedPackageVerifier.VerifyInstalledAsync(
                                directory,
                                observed!,
                                _maximumExpandedBytes,
                                cancellationToken).ConfigureAwait(false)
                            : ManagedVersionDamageReason.UnexpectedPath;
                        if (!_directoryExists(directory))
                        {
                            return ManagedVersionInventoryReadResult.Unavailable();
                        }
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

            return ManagedVersionInventoryReadResult.Success(ManagedVersionInventory.Create(rows));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ManagedVersionInventoryReadResult.Unavailable();
        }
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

    private static async ValueTask<byte[]?> ReadBoundedFileAsync(
        WindowsStablePathCustody custody,
        string relativePath,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using FileStream stream = custody.OpenReadOnlyFile(relativePath);
        if (stream.Length < 0 || stream.Length > maximumBytes)
        {
            return null;
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    private static ManagedExecutableLaunchIssue MapCustodyIssue(WindowsStableCustodyIssue issue)
    {
        return issue switch
        {
            WindowsStableCustodyIssue.InvalidPath or WindowsStableCustodyIssue.ReparsePoint or
            WindowsStableCustodyIssue.Changed => ManagedExecutableLaunchIssue.UnsafePath,
            WindowsStableCustodyIssue.AccessDenied or WindowsStableCustodyIssue.Contended or
            WindowsStableCustodyIssue.Unavailable => ManagedExecutableLaunchIssue.Unavailable,
            WindowsStableCustodyIssue.None => throw new InvalidOperationException(
                "Successful custody did not return its owner."),
            _ => throw new InvalidOperationException("Stable custody returned an undefined issue."),
        };
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

}

internal sealed record ManagedVersionAdmissionFileDocument(
    string Version,
    string AdmissionIdentity,
    string ReleaseManifestSha256);
