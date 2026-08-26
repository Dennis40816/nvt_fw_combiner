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

/// <summary>Stable whole-inventory read result category.</summary>
public enum ManagedVersionInventoryReadIssue
{
    /// <summary>The complete managed-version inventory was observed.</summary>
    None,
    /// <summary>The complete inventory could not be observed without returning partial facts.</summary>
    Unavailable,
}

/// <summary>Fail-closed result for one complete managed-version inventory read.</summary>
public sealed record ManagedVersionInventoryReadResult
{
    private ManagedVersionInventoryReadResult(
        ManagedVersionInventory? inventory,
        ManagedVersionInventoryReadIssue issue)
    {
        Inventory = inventory;
        Issue = issue;
    }

    /// <summary>Gets the complete inventory when the read succeeded.</summary>
    public ManagedVersionInventory? Inventory { get; }

    /// <summary>Gets the terminal whole-inventory read issue.</summary>
    public ManagedVersionInventoryReadIssue Issue { get; }

    /// <summary>Gets whether the complete inventory is available.</summary>
    public bool IsSuccess =>
        Inventory is not null && Issue == ManagedVersionInventoryReadIssue.None;

    /// <summary>Creates one complete successful inventory result.</summary>
    public static ManagedVersionInventoryReadResult Success(ManagedVersionInventory inventory)
    {
        return new(
            inventory ?? throw new ArgumentNullException(nameof(inventory)),
            ManagedVersionInventoryReadIssue.None);
    }

    /// <summary>Creates one whole-inventory unavailable result with no partial facts.</summary>
    public static ManagedVersionInventoryReadResult Unavailable()
    {
        return new(null, ManagedVersionInventoryReadIssue.Unavailable);
    }
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

/// <summary>Stable result category for acquiring one verified executable launch lease.</summary>
public enum ManagedExecutableLaunchIssue
{
    /// <summary>The exact verified executable is held against write/delete through start.</summary>
    None,
    /// <summary>The executable or its owning manifest could not be observed safely.</summary>
    Unavailable,
    /// <summary>The executable no longer matches its admitted manifest identity.</summary>
    Tampered,
    /// <summary>The executable path is unsafe or outside its admitted managed tree.</summary>
    UnsafePath,
}

/// <summary>Repository-owned stable executable identity held through process creation.</summary>
public interface IManagedExecutableLaunchLease : IDisposable
{
    /// <summary>Gets the exact stable executable path.</summary>
    string ExecutablePath { get; }

    /// <summary>Gets the exact stable working directory.</summary>
    string WorkingDirectory { get; }
}

/// <summary>Typed fail-closed executable launch-lease result.</summary>
public sealed record ManagedExecutableLaunchLeaseResult(
    IManagedExecutableLaunchLease? Lease,
    ManagedExecutableLaunchIssue Issue)
{
    /// <summary>Gets whether the exact verified executable is held for launch.</summary>
    public bool IsAcquired => Lease is not null && Issue == ManagedExecutableLaunchIssue.None;
}

/// <summary>Filesystem/process-free Application port for managed payload storage.</summary>
public interface IManagedVersionRepository
{
    /// <summary>Verifies and holds the exact admitted application executable against replacement.</summary>
    ValueTask<ManagedExecutableLaunchLeaseResult> AcquireApplicationLaunchLeaseAsync(
        string managedRoot,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ManagedExecutableLaunchLeaseResult(
            null,
            ManagedExecutableLaunchIssue.Unavailable));
    }

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
    /// <returns>The complete verified inventory, or a typed unavailable result without partial facts.</returns>
    ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
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
