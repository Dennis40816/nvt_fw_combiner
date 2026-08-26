namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Exact content identity of one release-coupled launcher.</summary>
internal sealed record ManagedLauncherIdentity
{
    /// <summary>Launcher protocol supported by the first managed distribution.</summary>
    public const int SupportedProtocolVersion = 1;
    /// <summary>Only admitted launcher executable path.</summary>
    public const string ExecutablePath = "launcher/NvtFwCombiner.Launcher.exe";
    /// <summary>Maximum admitted launcher executable length.</summary>
    public const long MaximumExecutableBytes = 80_000_000;

    private ManagedLauncherIdentity(
        ManagedAppVersion ownerAppVersion,
        string ownerAdmissionIdentity,
        string ownerReleaseManifestSha256,
        ManagedAppVersion launcherVersion,
        int protocolVersion,
        string executableRelativePath,
        long size,
        string sha256)
    {
        OwnerAppVersion = ownerAppVersion;
        OwnerAdmissionIdentity = ownerAdmissionIdentity;
        OwnerReleaseManifestSha256 = ownerReleaseManifestSha256;
        LauncherVersion = launcherVersion;
        ProtocolVersion = protocolVersion;
        ExecutableRelativePath = executableRelativePath;
        Size = size;
        Sha256 = sha256;
    }

    public ManagedAppVersion OwnerAppVersion { get; }
    public string OwnerAdmissionIdentity { get; }
    public string OwnerReleaseManifestSha256 { get; }
    public ManagedAppVersion LauncherVersion { get; }
    public int ProtocolVersion { get; }
    public string ExecutableRelativePath { get; }
    public long Size { get; }
    public string Sha256 { get; }

    /// <summary>Creates one validated, exact launcher identity.</summary>
    public static ManagedLauncherIdentity Create(
        ManagedAppVersion ownerAppVersion,
        string ownerAdmissionIdentity,
        string ownerReleaseManifestSha256,
        ManagedAppVersion launcherVersion,
        int protocolVersion,
        string executableRelativePath,
        long size,
        string sha256)
    {
        if (string.IsNullOrWhiteSpace(ownerAdmissionIdentity) || ownerAdmissionIdentity.Length > 2048)
        {
            throw new ArgumentException("Launcher owner admission identity is invalid.", nameof(ownerAdmissionIdentity));
        }
        if (!IsLowerSha256(ownerReleaseManifestSha256))
        {
            throw new ArgumentException("Launcher owner manifest identity is invalid.", nameof(ownerReleaseManifestSha256));
        }
        if (protocolVersion != SupportedProtocolVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(protocolVersion), "Launcher protocol is unsupported.");
        }
        _ = string.Equals(executableRelativePath, ExecutablePath, StringComparison.Ordinal)
            ? true
            : throw new ArgumentException("Launcher executable path is unsupported.", nameof(executableRelativePath));
        return size is <= 0 or > MaximumExecutableBytes
            ? throw new ArgumentOutOfRangeException(nameof(size))
            : !IsLowerSha256(sha256)
            ? throw new ArgumentException("Launcher executable identity is invalid.", nameof(sha256))
            : new(
            ownerAppVersion,
            ownerAdmissionIdentity,
            ownerReleaseManifestSha256,
            launcherVersion,
            protocolVersion,
            executableRelativePath,
            size,
            sha256);
    }

    internal bool MatchesOwner(ManagedVersionAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        return admission.Version == OwnerAppVersion &&
               string.Equals(admission.AdmissionIdentity, OwnerAdmissionIdentity, StringComparison.Ordinal) &&
               string.Equals(admission.ReleaseManifestSha256, OwnerReleaseManifestSha256, StringComparison.Ordinal);
    }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}

internal enum LauncherActivationPhase
{
    Requested,
    CandidateLaunchRecorded,
    RollbackLaunchRecorded,
}

internal sealed record PendingLauncherActivation
{
    private PendingLauncherActivation(
        ManagedLauncherIdentity candidate,
        ManagedLauncherIdentity? previousActive,
        ManagedLauncherIdentity? previousLastKnownGood,
        LauncherActivationPhase phase)
    {
        Candidate = candidate;
        PreviousActive = previousActive;
        PreviousLastKnownGood = previousLastKnownGood;
        Phase = phase;
    }

    public ManagedLauncherIdentity Candidate { get; }
    public ManagedLauncherIdentity? PreviousActive { get; }
    public ManagedLauncherIdentity? PreviousLastKnownGood { get; }
    public LauncherActivationPhase Phase { get; }

    public static PendingLauncherActivation Create(
        ManagedLauncherIdentity candidate,
        ManagedLauncherIdentity? previousActive,
        ManagedLauncherIdentity? previousLastKnownGood,
        LauncherActivationPhase phase)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return !Enum.IsDefined(phase)
            ? throw new ArgumentOutOfRangeException(nameof(phase))
            : new(candidate, previousActive, previousLastKnownGood, phase);
    }

    internal PendingLauncherActivation WithPhase(LauncherActivationPhase phase)
    {
        return Create(Candidate, PreviousActive, PreviousLastKnownGood, phase);
    }
}

/// <summary>Strict durable launcher activation snapshot protected by the app-state lease.</summary>
internal sealed class LauncherBootstrapState
{
    private LauncherBootstrapState(
        string managedRootIdentity,
        ManagedLauncherIdentity? active,
        ManagedLauncherIdentity? lastKnownGood,
        PendingLauncherActivation? pending,
        ManagedLauncherIdentity? failed)
    {
        ManagedRootIdentity = managedRootIdentity;
        Active = active;
        LastKnownGood = lastKnownGood;
        Pending = pending;
        Failed = failed;
    }

    public string ManagedRootIdentity { get; }
    public ManagedLauncherIdentity? Active { get; }
    public ManagedLauncherIdentity? LastKnownGood { get; }
    public PendingLauncherActivation? Pending { get; }
    public ManagedLauncherIdentity? Failed { get; }

    public static LauncherBootstrapState Create(
        string managedRootIdentity,
        ManagedLauncherIdentity? active,
        ManagedLauncherIdentity? lastKnownGood,
        PendingLauncherActivation? pending,
        ManagedLauncherIdentity? failed)
    {
        string root = ManagedRootPathIdentity.Normalize(managedRootIdentity);
        return (active is null) != (lastKnownGood is null)
            ? throw new ArgumentException("Launcher active and last-known-good must be absent together.")
            : pending is not null &&
            (pending.PreviousActive != active || pending.PreviousLastKnownGood != lastKnownGood)
            ? throw new ArgumentException("Launcher pending transaction does not preserve exact prior state.", nameof(pending))
            : new(root, active, lastKnownGood, pending, failed);
    }

    internal bool IsBoundToManagedRoot(string managedRoot)
    {
        return ManagedRootPathIdentity.Equals(ManagedRootIdentity, managedRoot);
    }

    internal LauncherBootstrapState Begin(ManagedLauncherIdentity candidate)
    {
        return Pending is not null || candidate == Active
            ? throw new InvalidOperationException("Launcher candidate cannot begin from current state.")
            : Create(
            ManagedRootIdentity,
            Active,
            LastKnownGood,
            PendingLauncherActivation.Create(
                candidate,
                Active,
                LastKnownGood,
                LauncherActivationPhase.Requested),
            Failed);
    }

    internal LauncherBootstrapState RecordCandidateLaunch()
    {
        PendingLauncherActivation pending = Pending is { Phase: LauncherActivationPhase.Requested } value
            ? value
            : throw new InvalidOperationException("Launcher candidate is not requested.");
        return Create(
            ManagedRootIdentity,
            Active,
            LastKnownGood,
            pending.WithPhase(LauncherActivationPhase.CandidateLaunchRecorded),
            Failed);
    }

    internal LauncherBootstrapState RecordRollbackLaunch()
    {
        PendingLauncherActivation pending = Pending is
        { Phase: LauncherActivationPhase.CandidateLaunchRecorded } value
                ? value
                : throw new InvalidOperationException("Launcher candidate launch is not recorded.");
        return Create(
            ManagedRootIdentity,
            Active,
            LastKnownGood,
            pending.WithPhase(LauncherActivationPhase.RollbackLaunchRecorded),
            Failed);
    }

    internal LauncherBootstrapState CommitReady()
    {
        ManagedLauncherIdentity candidate = Pending is
        { Phase: LauncherActivationPhase.CandidateLaunchRecorded } value
                ? value.Candidate
                : throw new InvalidOperationException("Launcher candidate readiness is not committable.");
        return Create(ManagedRootIdentity, candidate, candidate, pending: null, failed: null);
    }

    internal LauncherBootstrapState CommitRollback()
    {
        PendingLauncherActivation pending = Pending is
        { Phase: LauncherActivationPhase.RollbackLaunchRecorded } value
                ? value
                : throw new InvalidOperationException("Launcher rollback is not committable.");
        ManagedLauncherIdentity rollback = pending.PreviousLastKnownGood ??
            throw new InvalidOperationException("Launcher rollback target is absent.");
        return Create(ManagedRootIdentity, rollback, rollback, pending: null, pending.Candidate);
    }

    internal LauncherBootstrapState FailCandidate()
    {
        ManagedLauncherIdentity candidate = Pending?.Candidate ??
            throw new InvalidOperationException("Launcher candidate is absent.");
        return Create(ManagedRootIdentity, Active, LastKnownGood, pending: null, candidate);
    }
}

internal enum LauncherBootstrapStateLoadIssue { None, Missing, Invalid, Unavailable }
internal sealed record LauncherBootstrapStateLoadResult(
    LauncherBootstrapState? State,
    LauncherBootstrapStateLoadIssue Issue)
{
    public bool IsSuccess => State is not null && Issue == LauncherBootstrapStateLoadIssue.None;
}
internal enum LauncherBootstrapStateSaveIssue { None, Unavailable }
internal readonly record struct LauncherBootstrapStateSaveResult(LauncherBootstrapStateSaveIssue Issue)
{
    public bool IsSuccess => Issue == LauncherBootstrapStateSaveIssue.None;
}

/// <summary>Persistence port that intentionally exposes no second writer lease.</summary>
internal interface ILauncherBootstrapStateStore
{
    ValueTask<LauncherBootstrapStateLoadResult> LoadAsync(CancellationToken cancellationToken);
    ValueTask<LauncherBootstrapStateSaveResult> TrySaveAsync(
        LauncherBootstrapState state,
        CancellationToken cancellationToken);
}

internal enum InstalledLauncherIssue { None, Unavailable, InvalidManifest, Tampered, ProtocolMismatch, UnsafePath }
internal sealed record InstalledLauncherResult(
    ManagedLauncherIdentity? Identity,
    InstalledLauncherIssue Issue)
{
    public bool IsVerified => Identity is not null && Issue == InstalledLauncherIssue.None;
}

internal interface IInstalledLauncherRepository
{
    ValueTask<InstalledLauncherResult> VerifyAsync(
        string managedRoot,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken);
}

internal enum LauncherProcessStartOutcome { Ready, StartFailed, ExitedBeforeReady, ReadyTimeout, InvalidReadySignal }
internal sealed record LauncherProcessStartResult(
    LauncherProcessStartOutcome Outcome,
    int? ExitCode,
    ManagedVersionAdmission? ReadyAdmission = null);

internal interface IManagedLauncherProcess
{
    ValueTask<LauncherProcessStartResult> StartUntilReadyAsync(
        string managedRoot,
        string statePath,
        ManagedLauncherIdentity launcher,
        TimeSpan readyDeadline,
        CancellationToken cancellationToken);
}

internal enum LauncherBootstrapOutcome
{
    Ready,
    RolledBack,
    InvalidState,
    ManagedRootMismatch,
    AppMutationPending,
    DamagedLauncher,
    ProtocolMismatch,
    StartFailed,
    RollbackUnavailable,
    StateChanged,
    StateUnavailable,
    Busy,
}

internal sealed record LauncherBootstrapResult(
    LauncherBootstrapOutcome Outcome,
    ManagedLauncherIdentity? RunningLauncher,
    ManagedLauncherIdentity? FailedLauncher);
