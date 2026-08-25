namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Content admission persisted for one installed managed version.</summary>
public sealed record ManagedVersionAdmission(
    ManagedAppVersion Version,
    string AdmissionIdentity,
    string ReleaseManifestSha256);

/// <summary>Stable managed-package installation result category.</summary>
public enum ManagedVersionInstallIssue
{
    /// <summary>The version was installed or was already installed identically.</summary>
    None,
    /// <summary>The requested catalog version or source package is unavailable, unreadable, or differs from its catalog length.</summary>
    PackageUnavailable,
    /// <summary>The source package length matches but its SHA-256 digest differs from the catalog.</summary>
    PackageMismatch,
    /// <summary>The archive path or expansion shape is unsafe.</summary>
    UnsafeArchive,
    /// <summary>The source or an already admitted installed closed payload is invalid.</summary>
    InvalidPayload,
    /// <summary>The same version is already installed with another identity.</summary>
    IdentityConflict,
    /// <summary>Staging or atomic promotion could not complete.</summary>
    PromotionFailed,
    /// <summary>An activation or recovery transaction blocks the request, writer/durable state is unavailable, or the mutation journal or commit cannot be saved.</summary>
    StateUnavailable,
}

/// <summary>Fail-closed managed-package installation result.</summary>
public sealed record ManagedVersionInstallResult(
    ManagedVersionAdmission? Admission,
    ManagedVersionInstallIssue Issue,
    bool WasAlreadyInstalled)
{
    /// <summary>Gets whether a complete admitted version is present.</summary>
    public bool IsSuccess => Admission is not null && Issue == ManagedVersionInstallIssue.None;
}

/// <summary>Fail-closed complete package verification result without installation.</summary>
public sealed record ManagedPackageVerificationResult(
    VerifiedUpdateCandidate? Candidate,
    ManagedVersionInstallIssue Issue)
{
    /// <summary>Gets whether the complete package and closed payload verified.</summary>
    public bool IsVerified => Candidate is not null && Issue == ManagedVersionInstallIssue.None;
}

/// <summary>Stable guarded-delete result category.</summary>
public enum ManagedVersionDeleteIssue
{
    /// <summary>The exact admitted non-active directory was deleted.</summary>
    None,
    /// <summary>The requested version is active.</summary>
    ActiveVersion,
    /// <summary>The requested version is not an admitted installed child.</summary>
    NotInstalled,
    /// <summary>The resolved target is unsafe or outside the managed root.</summary>
    UnsafeTarget,
    /// <summary>The exact target could not be removed.</summary>
    DeleteFailed,
}

/// <summary>Filesystem/process-free Application port for managed payload storage.</summary>
public interface IManagedVersionRepository
{
    /// <summary>Fully verifies one catalog package without creating an installed version.</summary>
    /// <param name="sourceRoot">Committed update-source root.</param>
    /// <param name="package">Validated catalog entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete verification result.</returns>
    ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
        string sourceRoot,
        UpdateCatalogVersionSnapshot package,
        CancellationToken cancellationToken);

    /// <summary>Verifies, stages, and atomically promotes one catalog package.</summary>
    /// <param name="managedRoot">Stable launcher-owned managed root.</param>
    /// <param name="sourceRoot">Committed update-source root.</param>
    /// <param name="package">Validated catalog entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete install result.</returns>
    ValueTask<ManagedVersionInstallResult> InstallAsync(
        string managedRoot,
        string sourceRoot,
        UpdateCatalogVersionSnapshot package,
        CancellationToken cancellationToken);

    /// <summary>Inventories and verifies every admitted installed version.</summary>
    /// <param name="managedRoot">Stable launcher-owned managed root.</param>
    /// <param name="admissions">Persisted content admissions.</param>
    /// <param name="activeVersion">Current active version.</param>
    /// <param name="lastKnownGoodVersion">Current fallback version.</param>
    /// <param name="failedActivationVersion">Optional activation-failed version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verified immutable inventory.</returns>
    ValueTask<ManagedVersionInventory> InventoryAsync(
        string managedRoot,
        IReadOnlyList<ManagedVersionAdmission> admissions,
        ManagedAppVersion? activeVersion,
        ManagedAppVersion? lastKnownGoodVersion,
        ManagedAppVersion? failedActivationVersion,
        CancellationToken cancellationToken);

    /// <summary>Deletes one exact admitted non-active managed directory.</summary>
    /// <param name="managedRoot">Stable launcher-owned managed root.</param>
    /// <param name="admission">Exact admitted target identity.</param>
    /// <param name="activeVersion">Current active version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guarded-delete issue.</returns>
    ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
        string managedRoot,
        ManagedVersionAdmission admission,
        ManagedAppVersion? activeVersion,
        CancellationToken cancellationToken);
}
