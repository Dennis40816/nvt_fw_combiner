namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Recoverable journal for one not-yet-ready activation.</summary>
public sealed record PendingVersionActivation(
    ManagedAppVersion CandidateVersion,
    string CandidateAdmissionIdentity,
    ManagedAppVersion? PreviousActiveVersion,
    ManagedAppVersion? PreviousLastKnownGoodVersion);

/// <summary>Immutable launcher-owned managed-version state.</summary>
public sealed class VersionManagerState
{
    private VersionManagerState(
        string? updateSource,
        ManagedAppVersion? activeVersion,
        ManagedAppVersion? lastKnownGoodVersion,
        IReadOnlyList<ManagedVersionAdmission> admissions,
        PendingVersionActivation? pendingActivation,
        ManagedAppVersion? failedActivationVersion,
        bool retentionReviewDue)
    {
        UpdateSource = updateSource;
        ActiveVersion = activeVersion;
        LastKnownGoodVersion = lastKnownGoodVersion;
        Admissions = admissions;
        PendingActivation = pendingActivation;
        FailedActivationVersion = failedActivationVersion;
        RetentionReviewDue = retentionReviewDue;
    }

    /// <summary>Gets the committed configured update-source folder.</summary>
    public string? UpdateSource { get; }

    /// <summary>Gets the committed active version.</summary>
    public ManagedAppVersion? ActiveVersion { get; }

    /// <summary>Gets the most recent ready version eligible for rollback.</summary>
    public ManagedAppVersion? LastKnownGoodVersion { get; }

    /// <summary>Gets content admissions for installed managed versions.</summary>
    public IReadOnlyList<ManagedVersionAdmission> Admissions { get; }

    /// <summary>Gets the recoverable not-yet-ready activation journal.</summary>
    public PendingVersionActivation? PendingActivation { get; }

    /// <summary>Gets the candidate that most recently failed readiness.</summary>
    public ManagedAppVersion? FailedActivationVersion { get; }

    /// <summary>Gets whether post-update retention review remains due.</summary>
    public bool RetentionReviewDue { get; }

    /// <summary>Creates validated launcher state without inferring missing identities.</summary>
    /// <param name="updateSource">Committed source configuration.</param>
    /// <param name="activeVersion">Committed active version.</param>
    /// <param name="lastKnownGoodVersion">Committed fallback version.</param>
    /// <param name="admissions">Installed content admissions.</param>
    /// <param name="pendingActivation">Optional activation journal.</param>
    /// <param name="failedActivationVersion">Optional failed candidate.</param>
    /// <param name="retentionReviewDue">Whether retention review is due.</param>
    /// <returns>Validated immutable state.</returns>
    public static VersionManagerState Create(
        string? updateSource,
        ManagedAppVersion? activeVersion,
        ManagedAppVersion? lastKnownGoodVersion,
        IEnumerable<ManagedVersionAdmission> admissions,
        PendingVersionActivation? pendingActivation,
        ManagedAppVersion? failedActivationVersion,
        bool retentionReviewDue)
    {
        ArgumentNullException.ThrowIfNull(admissions);
        ManagedVersionAdmission[] installed = [.. admissions];
        if (installed.GroupBy(admission => admission.Version).Any(group => group.Count() != 1) ||
            installed.Any(admission =>
                string.IsNullOrWhiteSpace(admission.AdmissionIdentity) ||
                !IsLowerSha256(admission.ReleaseManifestSha256)))
        {
            throw new ArgumentException("Installed admissions are inconsistent.", nameof(admissions));
        }

        HashSet<ManagedAppVersion> versions = [.. installed.Select(admission => admission.Version)];
        if ((activeVersion is not null && !versions.Contains(activeVersion.Value)) ||
            (lastKnownGoodVersion is not null && !versions.Contains(lastKnownGoodVersion.Value)) ||
            (failedActivationVersion is not null && !versions.Contains(failedActivationVersion.Value)) ||
            (pendingActivation is not null && !versions.Contains(pendingActivation.CandidateVersion)))
        {
            throw new ArgumentException("State references a version without an installed admission.");
        }

        if (pendingActivation is not null)
        {
            ManagedVersionAdmission candidate = installed.Single(
                admission => admission.Version == pendingActivation.CandidateVersion);
            if (!string.Equals(
                    candidate.AdmissionIdentity,
                    pendingActivation.CandidateAdmissionIdentity,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException("Pending activation identity differs from installed admission.");
            }
        }

        Array.Sort(installed, static (left, right) => right.Version.CompareTo(left.Version));
        return new(
            string.IsNullOrWhiteSpace(updateSource) ? null : updateSource,
            activeVersion,
            lastKnownGoodVersion,
            installed,
            pendingActivation,
            failedActivationVersion,
            retentionReviewDue);
    }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    internal VersionManagerState Rebuild(
        ManagedAppVersion? activeVersion,
        ManagedAppVersion? lastKnownGoodVersion,
        PendingVersionActivation? pendingActivation,
        ManagedAppVersion? failedActivationVersion)
    {
        return Create(
            UpdateSource,
            activeVersion,
            lastKnownGoodVersion,
            Admissions,
            pendingActivation,
            failedActivationVersion,
            RetentionReviewDue);
    }

    internal VersionManagerState WithRetentionReviewDue(bool retentionReviewDue)
    {
        return Create(
            UpdateSource,
            ActiveVersion,
            LastKnownGoodVersion,
            Admissions,
            PendingActivation,
            FailedActivationVersion,
            retentionReviewDue);
    }
}

/// <summary>State and optional exact fallback selected after activation failure.</summary>
public sealed record ActivationRecoveryDecision(
    VersionManagerState State,
    ManagedAppVersion? RollbackVersion);

/// <summary>Pure Application owner for ready commit and bounded rollback transitions.</summary>
public static class VersionActivationPolicy
{
    /// <summary>Begins one recoverable activation for an admitted installed version.</summary>
    /// <param name="state">Current launcher state.</param>
    /// <param name="candidateVersion">Exact admitted candidate.</param>
    /// <returns>State with a pending activation journal.</returns>
    public static VersionManagerState BeginActivation(
        VersionManagerState state,
        ManagedAppVersion candidateVersion)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.PendingActivation is not null)
        {
            throw new InvalidOperationException("Another managed-version activation is already pending.");
        }
        ManagedVersionAdmission candidate = state.Admissions.SingleOrDefault(
            admission => admission.Version == candidateVersion) ??
            throw new InvalidOperationException("Activation candidate is not installed and admitted.");
        return state.Rebuild(
            state.ActiveVersion,
            state.LastKnownGoodVersion,
            new(
                candidate.Version,
                candidate.AdmissionIdentity,
                state.ActiveVersion,
                state.LastKnownGoodVersion),
            failedActivationVersion: null);
    }

    /// <summary>Commits a pending candidate only after its authenticated ready signal.</summary>
    /// <param name="state">State with a pending activation.</param>
    /// <param name="readyVersion">Version that reported ready.</param>
    /// <returns>Committed state.</returns>
    public static VersionManagerState CommitReady(
        VersionManagerState state,
        ManagedAppVersion readyVersion)
    {
        ArgumentNullException.ThrowIfNull(state);
        _ = state.PendingActivation?.CandidateVersion == readyVersion
            ? true
            : throw new InvalidOperationException("Ready signal does not match the pending candidate.");
        return state.Rebuild(
            readyVersion,
            readyVersion,
            pendingActivation: null,
            failedActivationVersion: null);
    }

    /// <summary>Fails one matching pending activation and selects its prior LKG at most once.</summary>
    /// <param name="state">Current launcher state.</param>
    /// <param name="failedVersion">Candidate that failed start, exit, integrity, or deadline.</param>
    /// <returns>Restored state and the exact fallback to launch once.</returns>
    public static ActivationRecoveryDecision FailActivation(
        VersionManagerState state,
        ManagedAppVersion failedVersion)
    {
        ArgumentNullException.ThrowIfNull(state);
        PendingVersionActivation? pending = state.PendingActivation;
        if (pending?.CandidateVersion != failedVersion)
        {
            return new(state, RollbackVersion: null);
        }

        ManagedAppVersion? rollback = pending.PreviousLastKnownGoodVersion;
        bool rollbackAdmitted = rollback is not null &&
                                state.Admissions.Any(admission => admission.Version == rollback.Value) &&
                                rollback.Value != failedVersion;
        ManagedAppVersion? restoredActive = rollbackAdmitted
            ? rollback
            : pending.PreviousActiveVersion;
        VersionManagerState restored = state.Rebuild(
            restoredActive,
            rollbackAdmitted ? rollback : state.LastKnownGoodVersion,
            pendingActivation: null,
            failedActivationVersion: failedVersion);
        return new(restored, rollbackAdmitted ? rollback : null);
    }
}
