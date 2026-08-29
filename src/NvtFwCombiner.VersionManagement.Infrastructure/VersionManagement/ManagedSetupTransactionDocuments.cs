using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal sealed record ManagedSetupTransactionDocument(
    string SchemaVersion,
    string Product,
    int LauncherSetupProtocolVersion,
    string TransactionId,
    string ManagedRootIdentity,
    string StatePathIdentity,
    ManagedSetupContentIdentityDocument DistributionLauncherExecutable,
    ManagedSetupPayloadAdmissionDocument PayloadAdmission,
    ManagedSetupCandidateDocument Candidate,
    string[] OwnedPaths,
    string Phase)
{
    internal static ManagedSetupTransactionDocument Create(
        string transactionId,
        string managedRoot,
        string statePathIdentity,
        string stagingPath,
        ManagedDistributionPayloadIdentity payload,
        FreshInstallationCandidate candidate,
        string phase)
    {
        string? parent = Path.GetDirectoryName(managedRoot);
        string? stagingContainer = Path.GetDirectoryName(stagingPath);
        if (parent is null ||
            stagingContainer is null ||
            !ManagedPathSafety.PathComparer.Equals(parent, Path.GetDirectoryName(stagingContainer)))
        {
            throw new InvalidOperationException("Setup owned paths do not share one parent.");
        }
        string rootName = Path.GetFileName(managedRoot);
        string markerName = Path.GetFileName(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(managedRoot));
        string stagingRelative = string.Join(
            '/',
            Path.GetFileName(stagingContainer),
            Path.GetFileName(stagingPath));
        return new(
            "1.0",
            "NVT FW Combiner",
            1,
            transactionId,
            managedRoot,
            statePathIdentity,
            new(payload.LauncherSize, payload.LauncherSha256),
            new(
                payload.DescriptorSize,
                payload.DescriptorSha256,
                payload.Bootstrap.FileName,
                payload.Bootstrap.Length,
                payload.Bootstrap.Sha256),
            new(
                candidate.Identity.RegistryRevision,
                candidate.Identity.RegistryDigest,
                candidate.Identity.CatalogSchemaVersion,
                candidate.Identity.CatalogLatestVersion.ToString(),
                candidate.Identity.CatalogDigest,
                candidate.Identity.CatalogPath,
                candidate.Identity.RegistryId,
                candidate.Identity.SourceRoot,
                FormatSourceStatus(candidate.Identity.SourceStatus),
                candidate.Package.Version.ToString(),
                candidate.Identity.PackagePath,
                candidate.Identity.PackageSize,
                candidate.Identity.PackageSha256,
                candidate.Package.ReleaseManifestSha256,
                candidate.Package.Identity),
            [rootName, markerName, stagingRelative],
            phase);
    }

    internal static bool Equivalent(
        ManagedSetupTransactionDocument left,
        ManagedSetupTransactionDocument right)
    {
        return left.SchemaVersion == right.SchemaVersion &&
            left.Product == right.Product &&
            left.LauncherSetupProtocolVersion == right.LauncherSetupProtocolVersion &&
            left.TransactionId == right.TransactionId &&
            ManagedPathSafety.PathComparer.Equals(
                left.ManagedRootIdentity,
                right.ManagedRootIdentity) &&
            ManagedPathSafety.PathComparer.Equals(
                left.StatePathIdentity,
                right.StatePathIdentity) &&
            left.DistributionLauncherExecutable == right.DistributionLauncherExecutable &&
            left.PayloadAdmission == right.PayloadAdmission &&
            left.Candidate == right.Candidate &&
            left.OwnedPaths.SequenceEqual(right.OwnedPaths, StringComparer.Ordinal) &&
            left.Phase == right.Phase;
    }

    private static string FormatSourceStatus(UpdateSourceRegistryEntryStatus status)
    {
        return status switch
        {
            UpdateSourceRegistryEntryStatus.Latest => "latest",
            UpdateSourceRegistryEntryStatus.Available => "available",
            UpdateSourceRegistryEntryStatus.Deprecated => "deprecated",
            _ => throw new InvalidOperationException("Candidate source status is undefined."),
        };
    }
}

internal sealed record ManagedSetupContentIdentityDocument(long Size, string Sha256);

internal sealed record ManagedSetupPayloadAdmissionDocument(
    long DescriptorSize,
    string DescriptorSha256,
    string BootstrapInstalledFileName,
    long BootstrapSize,
    string BootstrapSha256);

internal sealed record ManagedSetupCandidateDocument(
    long RegistryRevision,
    string RegistryDigest,
    int CatalogSchemaVersion,
    string CatalogLatestVersion,
    string CatalogDigest,
    string CatalogPath,
    string RegistryId,
    string SourceRoot,
    string SourceStatus,
    string Version,
    string PackagePath,
    long PackageSize,
    string PackageSha256,
    string ReleaseManifestSha256,
    string EntryIdentity);

internal sealed record ManagedSetupPayloadAdmissionDescriptorDocument(
    string SchemaVersion,
    string Product,
    string PayloadKind,
    int LauncherSetupProtocolVersion,
    string LauncherVersion,
    string RuntimeIdentifier,
    string SourceCommit,
    ManagedSetupEmbeddedBootstrapDocument Bootstrap);

internal sealed record ManagedSetupEmbeddedBootstrapDocument(
    string ResourceName,
    string InstalledFileName,
    long Size,
    string Sha256,
    int VersionManagementProtocolVersion,
    string SourceCommit);
