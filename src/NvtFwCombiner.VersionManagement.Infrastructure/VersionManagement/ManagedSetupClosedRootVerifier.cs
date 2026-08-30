using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>
/// Reuses the one complete first-install root proof for Setup completion and exact recovery.
/// </summary>
internal sealed class ManagedSetupClosedRootVerifier(IManagedVersionRepository repository)
{
    private readonly IManagedVersionRepository _repository = repository ??
        throw new ArgumentNullException(nameof(repository));

    internal ValueTask<bool> VerifyAsync(
        string root,
        ManagedDistributionPayloadIdentity payload,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(admission);
        return VerifyCoreAsync(
            root,
            payload.LauncherSize,
            payload.LauncherSha256,
            payload.Bootstrap.FileName,
            payload.Bootstrap.Length,
            payload.Bootstrap.Sha256,
            admission,
            cancellationToken);
    }

    internal ValueTask<bool> VerifyAsync(
        string root,
        ManagedSetupRecoveryPayloadIdentity payload,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(admission);
        return VerifyCoreAsync(
            root,
            payload.LauncherSize,
            payload.LauncherSha256,
            payload.BootstrapFileName,
            payload.BootstrapSize,
            payload.BootstrapSha256,
            admission,
            cancellationToken);
    }

    private async ValueTask<bool> VerifyCoreAsync(
        string root,
        long launcherSize,
        string launcherSha256,
        string bootstrapFileName,
        long bootstrapSize,
        string bootstrapSha256,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        if (!await VerifyPayloadAndSeedAsync(
                root,
                launcherSize,
                launcherSha256,
                bootstrapFileName,
                bootstrapSize,
                bootstrapSha256,
                admission,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        ManagedVersionInventoryReadResult inventory = await _repository.InventoryAsync(
            root,
            [admission],
            admission.Version,
            admission.Version,
            failedActivationVersion: null,
            cancellationToken).ConfigureAwait(false);
        ManagedVersionInventory? installed = inventory.Inventory;
        return inventory.IsSuccess && installed is not null &&
            installed.Versions.Count == 1 &&
            installed.HealthyCount == 1 &&
            installed.DamagedCount == 0;
    }

    internal static async ValueTask<bool> VerifyPayloadAndSeedAsync(
        string root,
        ManagedDistributionPayloadIdentity payload,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return await VerifyPayloadAndSeedAsync(
            root,
            payload.LauncherSize,
            payload.LauncherSha256,
            payload.Bootstrap.FileName,
            payload.Bootstrap.Length,
            payload.Bootstrap.Sha256,
            admission,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> VerifyPayloadAndSeedAsync(
        string root,
        long launcherSize,
        string launcherSha256,
        string bootstrapFileName,
        long bootstrapSize,
        string bootstrapSha256,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        var seedStore = new JsonVersionManagerStateStore(
            Path.Combine(root, FileSystemManagedFirstInstallationRootMaterializer.SeedFileName),
            allowUnboundSeedTemplate: true);
        VersionManagerStateLoadResult seed = await seedStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        VersionManagerState? seedState = seed.State;
        return seed.IsSuccess && seedState is not null &&
            ManagedVersionSeedPolicy.IsCanonicalFirstRunSeed(seedState) &&
            seedState.Admissions is [var only] &&
            only == admission &&
            HasClosedTopLevelInventory(root) &&
            await MatchesAsync(
                Path.Combine(
                    root,
                    FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName),
                launcherSize,
                launcherSha256,
                cancellationToken).ConfigureAwait(false) &&
            string.Equals(
                bootstrapFileName,
                FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
                StringComparison.Ordinal) &&
            await MatchesAsync(
                Path.Combine(
                    root,
                    FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName),
                bootstrapSize,
                bootstrapSha256,
                cancellationToken).ConfigureAwait(false);
    }

    private static bool HasClosedTopLevelInventory(string root)
    {
        string[] expected =
        [
            FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
            FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName,
            FileSystemManagedFirstInstallationRootMaterializer.SeedFileName,
            FileSystemManagedVersionRepository.VersionsDirectoryName,
        ];
        try
        {
            string[] actual =
            [.. Directory.EnumerateFileSystemEntries(root).Select(
                static path => Path.GetFileName(path) ?? string.Empty)];
            return actual.Order(StringComparer.Ordinal).SequenceEqual(
                    expected.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal) &&
                expected.All(name => !ManagedPathSafety.IsReparsePoint(Path.Combine(root, name))) &&
                File.Exists(Path.Combine(
                    root,
                    FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName)) &&
                File.Exists(Path.Combine(
                    root,
                    FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName)) &&
                File.Exists(Path.Combine(
                    root,
                    FileSystemManagedFirstInstallationRootMaterializer.SeedFileName)) &&
                Directory.Exists(Path.Combine(
                    root,
                    FileSystemManagedVersionRepository.VersionsDirectoryName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static async ValueTask<bool> MatchesAsync(
        string path,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || ManagedPathSafety.IsReparsePoint(path))
        {
            return false;
        }
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length != expectedSize)
        {
            return false;
        }
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return string.Equals(Convert.ToHexStringLower(hash), expectedSha256, StringComparison.Ordinal);
    }
}
