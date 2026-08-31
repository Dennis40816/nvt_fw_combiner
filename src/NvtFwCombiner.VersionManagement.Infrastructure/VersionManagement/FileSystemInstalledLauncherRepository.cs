using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Verifies one release-coupled launcher against its exact admitted owner manifest.</summary>
internal sealed class FileSystemInstalledLauncherRepository : IInstalledLauncherRepository
{
    private readonly Func<string, int, CancellationToken, ValueTask<byte[]?>> _readBoundedFileAsync;
    private readonly Action<WindowsStableCustodyStage>? _custodyHook;
    private readonly Action? _beforeLeaseCreation;

    internal FileSystemInstalledLauncherRepository()
        : this(ManagedPathSafety.ReadBoundedFileAsync)
    {
    }

    internal FileSystemInstalledLauncherRepository(
        Func<string, int, CancellationToken, ValueTask<byte[]?>> readBoundedFileAsync,
        Action<WindowsStableCustodyStage>? custodyHook = null,
        Action? beforeLeaseCreation = null)
    {
        _readBoundedFileAsync = readBoundedFileAsync ??
            throw new ArgumentNullException(nameof(readBoundedFileAsync));
        _custodyHook = custodyHook;
        _beforeLeaseCreation = beforeLeaseCreation;
    }

    public async ValueTask<InstalledLauncherLaunchResult> AcquireLaunchLeaseAsync(
        string managedRoot,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        WindowsStableCustodyResult acquiredTree = AcquireVersionTree(
            managedRoot,
            admission,
            cancellationToken);
        if (!acquiredTree.IsAcquired)
        {
            return new(null, null, MapCustodyIssue(acquiredTree.Issue));
        }
        WindowsStablePathCustody? custody = acquiredTree.Custody!;
        try
        {
            InstalledLauncherResult verified = await ReadIdentityAsync(
                custody,
                admission,
                verifyExecutableBytes: false,
                cancellationToken).ConfigureAwait(false);
            if (!verified.IsVerified)
            {
                return new(null, null, verified.Issue);
            }
            ManagedLauncherIdentity identity = verified.Identity!;
            _beforeLeaseCreation?.Invoke();
            WindowsStablePathCustody ownedCustody = custody;
            custody = null;
            ManagedExecutableLaunchLeaseResult acquired =
                await StableManagedExecutableLaunchLease.TryCreateAsync(
                    ownedCustody,
                    identity.ExecutableRelativePath,
                    identity.Size,
                    identity.Sha256,
                    cancellationToken).ConfigureAwait(false);
            InstalledLauncherIssue issue = acquired.Issue switch
            {
                ManagedExecutableLaunchIssue.None => InstalledLauncherIssue.None,
                ManagedExecutableLaunchIssue.UnsafePath => InstalledLauncherIssue.UnsafePath,
                ManagedExecutableLaunchIssue.Tampered => InstalledLauncherIssue.Tampered,
                ManagedExecutableLaunchIssue.Unavailable => InstalledLauncherIssue.Unavailable,
                _ => throw new InvalidOperationException(
                    "Managed executable lease returned an undefined issue."),
            };
            return acquired.IsAcquired
                ? new(identity, acquired.Lease, issue)
                : new(null, null, issue);
        }
        finally
        {
            custody?.Dispose();
        }
    }

    public async ValueTask<InstalledLauncherResult> VerifyAsync(
        string managedRoot,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        WindowsStableCustodyResult acquired = AcquireVersionTree(
            managedRoot,
            admission,
            cancellationToken);
        if (!acquired.IsAcquired)
        {
            return Failure(MapCustodyIssue(acquired.Issue));
        }
        using WindowsStablePathCustody custody = acquired.Custody!;
        return await ReadIdentityAsync(
            custody,
            admission,
            verifyExecutableBytes: true,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<InstalledLauncherResult> ReadIdentityAsync(
        WindowsStablePathCustody custody,
        ManagedVersionAdmission admission,
        bool verifyExecutableBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(custody);
        ArgumentNullException.ThrowIfNull(admission);
        try
        {
            string manifestPath = custody.GetAbsoluteFilePath("RELEASE-MANIFEST.json");
            byte[]? manifestBytes = await _readBoundedFileAsync(
                manifestPath,
                FileSystemManagedVersionRepository.MaximumManifestBytes,
                cancellationToken).ConfigureAwait(false);
            if (manifestBytes is null ||
                !string.Equals(
                    Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
                    admission.ReleaseManifestSha256,
                    StringComparison.Ordinal))
            {
                return Failure(InstalledLauncherIssue.InvalidManifest);
            }

            if (!ManagedPackageVerifier.TryReadCanonicalManifest(
                    manifestBytes,
                    admission.Version,
                    archivePaths: null,
                    out ReleaseManifestDocument? manifest))
            {
                return Failure(InstalledLauncherIssue.InvalidManifest);
            }
            if (!string.Equals(manifest.SchemaVersion, "1.2", StringComparison.Ordinal) ||
                manifest.Launcher is not
                {
                    ProtocolVersion: { } launcherProtocolVersion,
                    ExecutableRelativePath: { } launcherPath,
                    Size: { } launcherSize,
                    Sha256: { } launcherExpectedSha256,
                } launcher ||
                !ManagedAppVersion.TryParse(launcher.LauncherVersion, out ManagedAppVersion launcherVersion))
            {
                return Failure(InstalledLauncherIssue.ProtocolMismatch);
            }
            if (!HasExactInstalledTopology(custody, manifest.Files!))
            {
                return Failure(InstalledLauncherIssue.Tampered);
            }

            ManagedLauncherIdentity identity = ManagedLauncherIdentity.Create(
                admission.Version,
                admission.AdmissionIdentity,
                admission.ReleaseManifestSha256,
                launcherVersion,
                launcherProtocolVersion,
                launcherPath,
                launcherSize,
                launcherExpectedSha256);
            if (verifyExecutableBytes)
            {
                ManagedVersionDamageReason? packageDamage = await ManagedPackageVerifier.VerifyInstalledAsync(
                    custody.RootPath,
                    admission,
                    FileSystemManagedVersionRepository.MaximumExpandedBytes,
                    cancellationToken).ConfigureAwait(false);
                if (packageDamage is not null)
                {
                    return Failure(InstalledLauncherIssue.Tampered);
                }
            }
            else
            {
                return new(identity, InstalledLauncherIssue.None);
            }

            using FileStream launcherStream = custody.OpenReadOnlyFile(
                ManagedLauncherIdentity.ExecutablePath);
            if (launcherStream.Length != launcher.Size)
            {
                return Failure(InstalledLauncherIssue.Tampered);
            }
            string launcherSha256 = Convert.ToHexStringLower(
                await SHA256.HashDataAsync(launcherStream, cancellationToken).ConfigureAwait(false));
            return string.Equals(launcherSha256, launcher.Sha256, StringComparison.Ordinal)
                ? new(identity, InstalledLauncherIssue.None)
                : Failure(InstalledLauncherIssue.Tampered);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Failure(InstalledLauncherIssue.Unavailable);
        }
    }

    private static InstalledLauncherResult Failure(InstalledLauncherIssue issue)
    {
        return new(null, issue);
    }

    private static bool HasExactInstalledTopology(
        WindowsStablePathCustody custody,
        IReadOnlyList<ReleaseManifestFileDocument> declaredFiles)
    {
        if (!custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot))
        {
            return false;
        }

        HashSet<string> expectedFiles =
            ManagedPackageVerifier.BuildExpectedInstalledFilePaths(declaredFiles);
        if (!expectedFiles.SetEquals(snapshot!.Files.Keys))
        {
            return false;
        }

        var expectedDirectories = new HashSet<string>(ManagedPathSafety.PathComparer)
        {
            string.Empty,
        };
        foreach (string file in expectedFiles)
        {
            int separator = file.LastIndexOf('/');
            while (separator >= 0)
            {
                string directory = file[..separator];
                _ = expectedDirectories.Add(directory);
                separator = directory.LastIndexOf('/');
            }
        }
        return expectedDirectories.SetEquals(snapshot.Directories.Keys);
    }

    private WindowsStableCustodyResult AcquireVersionTree(
        string managedRoot,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentNullException.ThrowIfNull(admission);
        if (!Path.IsPathFullyQualified(managedRoot))
        {
            return WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.InvalidPath);
        }
        string versionRoot = ManagedPathSafety.GetExactVersionDirectory(
            Path.Combine(
                Path.GetFullPath(managedRoot),
                FileSystemManagedVersionRepository.VersionsDirectoryName),
            admission.Version);
        return WindowsStablePathCustody.TryAcquireImmutableTree(
            versionRoot,
            _custodyHook,
            cancellationToken);
    }

    private static InstalledLauncherIssue MapCustodyIssue(WindowsStableCustodyIssue issue)
    {
        return issue switch
        {
            WindowsStableCustodyIssue.InvalidPath or WindowsStableCustodyIssue.ReparsePoint or
            WindowsStableCustodyIssue.Changed => InstalledLauncherIssue.UnsafePath,
            WindowsStableCustodyIssue.AccessDenied or WindowsStableCustodyIssue.Contended or
            WindowsStableCustodyIssue.Unavailable => InstalledLauncherIssue.Unavailable,
            WindowsStableCustodyIssue.None => throw new InvalidOperationException(
                "Successful custody did not return its owner."),
            _ => throw new InvalidOperationException("Stable custody returned an undefined issue."),
        };
    }

}
