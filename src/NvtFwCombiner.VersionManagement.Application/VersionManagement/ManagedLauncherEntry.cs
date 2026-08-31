namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Complete local observation of the selected first-install root.</summary>
public enum ManagedInstallationRootStatus
{
    /// <summary>The canonical root and every Setup residue path are absent.</summary>
    Absent,
    /// <summary>A root exists and is not owned by the current uninterrupted transaction.</summary>
    Present,
    /// <summary>A transaction marker or staging residue exists.</summary>
    Residue,
    /// <summary>The selected root is not an admitted local non-reparse destination.</summary>
    InvalidDestination,
    /// <summary>The current user cannot inspect the complete root fact.</summary>
    PermissionDenied,
    /// <summary>The complete root fact could not be observed.</summary>
    Unavailable,
}

/// <summary>Read-only root observation used before any Setup source access.</summary>
public sealed record ManagedInstallationRootObservation(ManagedInstallationRootStatus Status);

/// <summary>
/// Read-only infrastructure port for one exact root observation without discovery.
/// Implementations must return their ValueTask promptly and honor cancellation; the entry
/// coordinator additionally isolates and abandons the wait at its hard local-health cutoff.
/// </summary>
public interface IManagedInstallationRootProbe
{
    /// <summary>Observes only the exact normalized root and known transaction paths.</summary>
    ValueTask<ManagedInstallationRootObservation> ObserveAsync(
        string managedRoot,
        CancellationToken cancellationToken);
}

/// <summary>Exact immutable Root Bootstrap identity embedded by the distribution Launcher.</summary>
public sealed record ManagedImmutableBootstrapIdentity
{
    /// <summary>Maximum admitted executable payload length shared by Launcher and Bootstrap.</summary>
    public const long MaximumExecutableBytes = 200_000_000;
    /// <summary>Creates one closed Bootstrap identity.</summary>
    public ManagedImmutableBootstrapIdentity(string fileName, long length, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) ||
            !IsLowerSha256(sha256))
        {
            throw new ArgumentException("Immutable Bootstrap identity is invalid.", nameof(fileName));
        }
        if (length is <= 0 or > MaximumExecutableBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        FileName = fileName;
        Length = length;
        Sha256 = sha256;
    }

    /// <summary>Gets the exact root filename.</summary>
    public string FileName { get; }

    /// <summary>Gets the exact executable byte length.</summary>
    public long Length { get; }

    /// <summary>Gets the lowercase executable SHA-256.</summary>
    public string Sha256 { get; }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
            value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}

/// <summary>Stable issue returned before or during exact immutable Bootstrap process creation.</summary>
public enum ImmutableBootstrapStartIssue
{
    /// <summary>The exact Bootstrap process started and returned a completion receipt.</summary>
    None,
    /// <summary>Another state writer owns the launch transaction.</summary>
    Busy,
    /// <summary>The exact Bootstrap path, PE, length, or SHA-256 is invalid.</summary>
    Damaged,
    /// <summary>The exact process could not be created.</summary>
    StartFailed,
    /// <summary>The Bootstrap or process result could not be observed safely.</summary>
    Unavailable,
}

/// <summary>Terminal outcome observed after an exact Bootstrap process was started.</summary>
public enum ImmutableBootstrapCompletionOutcome
{
    /// <summary>The selected application completed READY.</summary>
    Ready,
    /// <summary>The Bootstrap completed READY through the admitted LKG rollback.</summary>
    RolledBack,
    /// <summary>The Bootstrap completed without a READY application.</summary>
    Failed,
    /// <summary>The started process result could not be observed safely.</summary>
    Unavailable,
    /// <summary>The outer Bootstrap job could not be proven empty or safely released.</summary>
    TerminationUnconfirmed,
}

/// <summary>Typed outcome observed before Root Bootstrap may continue its READY wait.</summary>
public enum ImmutableBootstrapAdmissionOutcome
{
    /// <summary>Root Bootstrap started the exact version Launcher.</summary>
    Admitted,
    /// <summary>Another writer owns the bounded Bootstrap startup transaction.</summary>
    Busy,
    /// <summary>Installed state or executable evidence requires recovery.</summary>
    RecoveryRequired,
    /// <summary>Root Bootstrap could not start the version Launcher.</summary>
    LaunchFailed,
    /// <summary>The pre-launch health result could not be observed safely.</summary>
    HealthUnavailable,
    /// <summary>The outer Bootstrap job could not be proven empty after failed admission.</summary>
    TerminationUnconfirmed,
}

/// <summary>Exact path-free reason represented by an observed immutable Bootstrap exit code.</summary>
public enum ImmutableBootstrapExitIssue
{
    /// <summary>The process did not report a failure exit.</summary>
    None,
    /// <summary>Another process owns the required state writer.</summary>
    Busy,
    /// <summary>The managed version state is invalid.</summary>
    InvalidState,
    /// <summary>The state is bound to a different managed root.</summary>
    ManagedRootMismatch,
    /// <summary>An application mutation transaction is still pending.</summary>
    MutationPending,
    /// <summary>The installed version Launcher failed immutable verification.</summary>
    DamagedLauncher,
    /// <summary>The installed version Launcher uses an incompatible protocol.</summary>
    ProtocolMismatch,
    /// <summary>The exact version Launcher process could not be started.</summary>
    StartFailed,
    /// <summary>No admitted last-known-good rollback target is available.</summary>
    RollbackUnavailable,
    /// <summary>The managed version state changed during launch.</summary>
    StateChanged,
    /// <summary>The managed version state could not be observed or persisted.</summary>
    StateUnavailable,
    /// <summary>The managed process tree could not be proven terminated.</summary>
    TerminationUnconfirmed,
    /// <summary>The immutable Bootstrap received invalid arguments.</summary>
    InvalidArguments,
    /// <summary>The immutable Bootstrap rejected an internal invariant.</summary>
    InvariantViolation,
    /// <summary>The immutable Bootstrap inherited an incomplete process context.</summary>
    InvalidInheritedContext,
    /// <summary>The inherited start gate did not authorize Bootstrap.</summary>
    StartNotAuthorized,
    /// <summary>The immutable Bootstrap returned its reserved undefined-failure code.</summary>
    UndefinedFailure,
    /// <summary>The immutable Bootstrap returned an unrecognized failure code.</summary>
    Unknown,
}

/// <summary>Single numeric wire codec shared by immutable Bootstrap producers and consumers.</summary>
public static class ImmutableBootstrapExitCodeCodec
{
    /// <summary>Successful completion through the active Launcher.</summary>
    public const int Ready = 0;
    /// <summary>Successful completion through the admitted last-known-good Launcher.</summary>
    public const int RolledBack = 1;

    /// <summary>Encodes one concrete Bootstrap failure issue.</summary>
    public static int EncodeFailure(ImmutableBootstrapExitIssue issue)
    {
        return issue switch
        {
            ImmutableBootstrapExitIssue.Busy => 2,
            ImmutableBootstrapExitIssue.InvalidState => 10,
            ImmutableBootstrapExitIssue.ManagedRootMismatch => 11,
            ImmutableBootstrapExitIssue.MutationPending => 12,
            ImmutableBootstrapExitIssue.DamagedLauncher => 13,
            ImmutableBootstrapExitIssue.ProtocolMismatch => 14,
            ImmutableBootstrapExitIssue.StartFailed => 15,
            ImmutableBootstrapExitIssue.RollbackUnavailable => 16,
            ImmutableBootstrapExitIssue.StateChanged => 17,
            ImmutableBootstrapExitIssue.StateUnavailable => 18,
            ImmutableBootstrapExitIssue.TerminationUnconfirmed => 19,
            ImmutableBootstrapExitIssue.InvalidArguments => 20,
            ImmutableBootstrapExitIssue.InvariantViolation => 21,
            ImmutableBootstrapExitIssue.InvalidInheritedContext => 22,
            ImmutableBootstrapExitIssue.StartNotAuthorized => 23,
            ImmutableBootstrapExitIssue.UndefinedFailure => 99,
            ImmutableBootstrapExitIssue.None or ImmutableBootstrapExitIssue.Unknown =>
                throw new ArgumentOutOfRangeException(nameof(issue), issue, "Issue has no failure encoding."),
            _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, "Issue is undefined."),
        };
    }

    /// <summary>Decodes one observed failure code without treating unknown values as success.</summary>
    public static ImmutableBootstrapExitIssue DecodeFailure(int exitCode)
    {
        return exitCode switch
        {
            2 => ImmutableBootstrapExitIssue.Busy,
            10 => ImmutableBootstrapExitIssue.InvalidState,
            11 => ImmutableBootstrapExitIssue.ManagedRootMismatch,
            12 => ImmutableBootstrapExitIssue.MutationPending,
            13 => ImmutableBootstrapExitIssue.DamagedLauncher,
            14 => ImmutableBootstrapExitIssue.ProtocolMismatch,
            15 => ImmutableBootstrapExitIssue.StartFailed,
            16 => ImmutableBootstrapExitIssue.RollbackUnavailable,
            17 => ImmutableBootstrapExitIssue.StateChanged,
            18 => ImmutableBootstrapExitIssue.StateUnavailable,
            19 => ImmutableBootstrapExitIssue.TerminationUnconfirmed,
            20 => ImmutableBootstrapExitIssue.InvalidArguments,
            21 => ImmutableBootstrapExitIssue.InvariantViolation,
            22 => ImmutableBootstrapExitIssue.InvalidInheritedContext,
            23 => ImmutableBootstrapExitIssue.StartNotAuthorized,
            99 => ImmutableBootstrapExitIssue.UndefinedFailure,
            _ => ImmutableBootstrapExitIssue.Unknown,
        };
    }

    /// <summary>Projects one decoded pre-admission issue into its stable coarse outcome.</summary>
    public static ImmutableBootstrapAdmissionOutcome ClassifyAdmission(
        ImmutableBootstrapExitIssue issue)
    {
        return issue switch
        {
            ImmutableBootstrapExitIssue.Busy => ImmutableBootstrapAdmissionOutcome.Busy,
            ImmutableBootstrapExitIssue.InvalidState or
            ImmutableBootstrapExitIssue.ManagedRootMismatch or
            ImmutableBootstrapExitIssue.MutationPending or
            ImmutableBootstrapExitIssue.DamagedLauncher or
            ImmutableBootstrapExitIssue.ProtocolMismatch =>
                ImmutableBootstrapAdmissionOutcome.RecoveryRequired,
            ImmutableBootstrapExitIssue.StartFailed or
            ImmutableBootstrapExitIssue.RollbackUnavailable or
            ImmutableBootstrapExitIssue.StateChanged =>
                ImmutableBootstrapAdmissionOutcome.LaunchFailed,
            ImmutableBootstrapExitIssue.TerminationUnconfirmed =>
                ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed,
            ImmutableBootstrapExitIssue.None or
            ImmutableBootstrapExitIssue.StateUnavailable or
            ImmutableBootstrapExitIssue.InvalidArguments or
            ImmutableBootstrapExitIssue.InvariantViolation or
            ImmutableBootstrapExitIssue.InvalidInheritedContext or
            ImmutableBootstrapExitIssue.StartNotAuthorized or
            ImmutableBootstrapExitIssue.UndefinedFailure or
            ImmutableBootstrapExitIssue.Unknown =>
                ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, "Issue is undefined."),
        };
    }

    /// <summary>Projects one observed post-admission code into its stable terminal outcome.</summary>
    public static ImmutableBootstrapCompletionOutcome ClassifyCompletion(int exitCode)
    {
        return exitCode switch
        {
            Ready => ImmutableBootstrapCompletionOutcome.Ready,
            RolledBack => ImmutableBootstrapCompletionOutcome.RolledBack,
            _ when DecodeFailure(exitCode) == ImmutableBootstrapExitIssue.TerminationUnconfirmed =>
                ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed,
            _ => ImmutableBootstrapCompletionOutcome.Failed,
        };
    }
}

/// <summary>One typed cross-process Root Bootstrap admission receipt.</summary>
public sealed record ImmutableBootstrapAdmissionResult(
    ImmutableBootstrapAdmissionOutcome Outcome,
    int? ExitCode = null,
    ImmutableBootstrapExitIssue ExitIssue = ImmutableBootstrapExitIssue.None)
{
    /// <summary>Gets whether outcome, optional code, and typed issue describe one receipt.</summary>
    public bool HasValidShape =>
        Enum.IsDefined(Outcome) &&
        Enum.IsDefined(ExitIssue) &&
        (Outcome switch
        {
            ImmutableBootstrapAdmissionOutcome.Admitted =>
                ExitCode is null && ExitIssue == ImmutableBootstrapExitIssue.None,
            ImmutableBootstrapAdmissionOutcome.LaunchFailed when ExitCode is null =>
                ExitIssue == ImmutableBootstrapExitIssue.StartFailed,
            ImmutableBootstrapAdmissionOutcome.HealthUnavailable when ExitCode is null =>
                ExitIssue == ImmutableBootstrapExitIssue.None,
            ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed when ExitCode is null =>
                ExitIssue == ImmutableBootstrapExitIssue.TerminationUnconfirmed,
            ImmutableBootstrapAdmissionOutcome.Busy or
            ImmutableBootstrapAdmissionOutcome.RecoveryRequired or
            ImmutableBootstrapAdmissionOutcome.LaunchFailed or
            ImmutableBootstrapAdmissionOutcome.HealthUnavailable or
            ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed when ExitCode is int exitCode =>
                exitCode is not
                    ImmutableBootstrapExitCodeCodec.Ready and not
                    ImmutableBootstrapExitCodeCodec.RolledBack &&
                ExitIssue == ImmutableBootstrapExitCodeCodec.DecodeFailure(exitCode) &&
                Outcome == ImmutableBootstrapExitCodeCodec.ClassifyAdmission(ExitIssue),
            _ => false,
        });
}

/// <summary>Typed terminal result from one already-started immutable Bootstrap.</summary>
public sealed record ImmutableBootstrapCompletionResult(
    ImmutableBootstrapCompletionOutcome Outcome,
    int? ExitCode = null,
    ImmutableBootstrapExitIssue ExitIssue = ImmutableBootstrapExitIssue.None)
{
    /// <summary>Gets whether outcome, optional code, and typed issue describe one receipt.</summary>
    public bool HasValidShape =>
        Enum.IsDefined(Outcome) &&
        Enum.IsDefined(ExitIssue) &&
        (Outcome switch
        {
            ImmutableBootstrapCompletionOutcome.Ready =>
                ExitCode == ImmutableBootstrapExitCodeCodec.Ready &&
                ExitIssue == ImmutableBootstrapExitIssue.None,
            ImmutableBootstrapCompletionOutcome.RolledBack =>
                ExitCode == ImmutableBootstrapExitCodeCodec.RolledBack &&
                ExitIssue == ImmutableBootstrapExitIssue.None,
            ImmutableBootstrapCompletionOutcome.Failed when ExitCode is int exitCode =>
                exitCode is not
                    ImmutableBootstrapExitCodeCodec.Ready and not
                    ImmutableBootstrapExitCodeCodec.RolledBack &&
                ImmutableBootstrapExitCodeCodec.ClassifyCompletion(exitCode) ==
                    ImmutableBootstrapCompletionOutcome.Failed &&
                ExitIssue == ImmutableBootstrapExitCodeCodec.DecodeFailure(exitCode),
            ImmutableBootstrapCompletionOutcome.Unavailable =>
                ExitCode is null && ExitIssue == ImmutableBootstrapExitIssue.None,
            ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed when ExitCode is null =>
                ExitIssue == ImmutableBootstrapExitIssue.TerminationUnconfirmed,
            ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed when ExitCode is int exitCode =>
                ImmutableBootstrapExitCodeCodec.ClassifyCompletion(exitCode) ==
                    ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed &&
                ExitIssue == ImmutableBootstrapExitCodeCodec.DecodeFailure(exitCode),
            _ => false,
        });
}

/// <summary>Remaining monotonic operation and total budgets for one process wait.</summary>
public readonly record struct ImmutableBootstrapWaitBudget
{
    /// <summary>Creates a fail-closed budget whose total includes cleanup observation.</summary>
    public ImmutableBootstrapWaitBudget(TimeSpan remainingOperation, TimeSpan remainingTotal)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(remainingOperation, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(remainingTotal, remainingOperation);
        RemainingOperation = remainingOperation;
        RemainingTotal = remainingTotal;
    }

    /// <summary>Gets remaining time in which the operation may reach its terminal signal.</summary>
    public TimeSpan RemainingOperation { get; }

    /// <summary>Gets remaining caller-visible time including bounded cleanup observation.</summary>
    public TimeSpan RemainingTotal { get; }
}

/// <summary>Receipt for one already-started immutable Bootstrap process.</summary>
public interface IImmutableBootstrapLaunch : IDisposable
{
    /// <summary>Waits for exact Launcher admission within the remaining absolute budget.</summary>
    ValueTask<ImmutableBootstrapAdmissionResult> WaitForAdmissionAsync(
        ImmutableBootstrapWaitBudget budget,
        CancellationToken cancellationToken);

    /// <summary>Waits for Bootstrap/READY completion within the remaining absolute budget.</summary>
    ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
        ImmutableBootstrapWaitBudget budget,
        CancellationToken cancellationToken);
}

/// <summary>Typed result from bounded stable verification and process creation.</summary>
public sealed record ImmutableBootstrapStartResult(
    IImmutableBootstrapLaunch? Launch,
    ImmutableBootstrapStartIssue Issue)
{
    /// <summary>Gets whether exact process creation completed and returned its receipt.</summary>
    public bool IsStarted => Launch is not null && Issue == ImmutableBootstrapStartIssue.None;

    /// <summary>Gets whether receipt custody and the typed issue describe one valid result shape.</summary>
    public bool HasValidShape =>
        Enum.IsDefined(Issue) &&
        (Launch is not null) == (Issue == ImmutableBootstrapStartIssue.None);
}

/// <summary>Stable process seam shared by healthy entry and post-install launch.</summary>
public interface IImmutableBootstrapHandoff
{
    /// <summary>Verifies and starts the exact Root Bootstrap without waiting for child READY.</summary>
    ValueTask<ImmutableBootstrapStartResult> StartAsync(
        string managedRoot,
        ManagedImmutableBootstrapIdentity expectedIdentity,
        CancellationToken cancellationToken);
}

/// <summary>
/// Setup-only process seam that consumes already verified promoted-tree launch custody.
/// </summary>
public interface IImmutableBootstrapLeaseHandoff
{
    /// <summary>
    /// Consumes <paramref name="ownedLease"/> on every path and starts the exact Root Bootstrap
    /// without reacquiring custody from the promoted managed path.
    /// </summary>
    ValueTask<ImmutableBootstrapStartResult> StartAsync(
        string managedRoot,
        ManagedImmutableBootstrapIdentity expectedIdentity,
        IManagedExecutableLaunchLease ownedLease,
        CancellationToken cancellationToken);
}

/// <summary>Terminal single-entry decision rendered by the distribution Launcher.</summary>
public enum ManagedLauncherEntryOutcome
{
    /// <summary>The exact distribution payload could not be observed completely.</summary>
    PayloadUnavailable,
    /// <summary>The distribution descriptor or embedded Bootstrap metadata is invalid.</summary>
    PayloadInvalid,
    /// <summary>A healthy installed version was started, directly or through LKG rollback.</summary>
    LaunchInstalled,
    /// <summary>Root and durable state are genuinely absent, so Setup may be shown.</summary>
    SetupRequired,
    /// <summary>Installed or residual facts require the separate recovery capability.</summary>
    RecoveryRequired,
    /// <summary>Another process owns the bounded launch transaction.</summary>
    Busy,
    /// <summary>Local health could not be established before its hard deadline.</summary>
    HealthUnavailable,
    /// <summary>The exact Bootstrap was admitted but process creation or READY failed.</summary>
    LaunchFailed,
    /// <summary>The Root Bootstrap tree could not be proven terminated or safely released.</summary>
    TerminationUnconfirmed,
}

/// <summary>Immutable entry result including local admission duration.</summary>
public sealed record ManagedLauncherEntryResult(
    ManagedLauncherEntryOutcome Outcome,
    string? ManagedRoot,
    TimeSpan AdmissionElapsed,
    TimeSpan TotalElapsed);

/// <summary>Single Application owner for Launcher-to-Setup-or-Bootstrap routing.</summary>
public sealed class ManagedLauncherEntryCoordinator
{
    /// <summary>Accepted P95 target for local-only healthy-installation classification.</summary>
    public static readonly TimeSpan HealthyRoutingP95Target = TimeSpan.FromMilliseconds(100);

    /// <summary>Delay before Presentation may show non-blocking startup progress.</summary>
    public static readonly TimeSpan ProgressDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>Hard cutoff for state and exact-root health observation.</summary>
    public static readonly TimeSpan DefaultHealthObservationDeadline = ProgressDelay;

    /// <summary>
    /// Admission work cutoff, including exact Bootstrap hashing on cold or scanned storage,
    /// while leaving bounded time to prove outer-tree cleanup.
    /// </summary>
    public static readonly TimeSpan DefaultAdmissionOperationCutoff = TimeSpan.FromSeconds(5);

    /// <summary>Completion work cutoff, leaving bounded time to prove outer-tree cleanup.</summary>
    public static readonly TimeSpan DefaultCompletionOperationCutoff = TimeSpan.FromMilliseconds(44500);

    /// <summary>Maximum caller-visible cleanup observation after either operation cutoff.</summary>
    public static readonly TimeSpan DefaultCleanupObservationBudget = TimeSpan.FromMilliseconds(500);

    /// <summary>Hard deadline for bounded local entry admission including cleanup observation.</summary>
    public static readonly TimeSpan DefaultAdmissionDeadline =
        DefaultAdmissionOperationCutoff + DefaultCleanupObservationBudget;

    /// <summary>Independent Bootstrap/READY deadline including cleanup observation.</summary>
    public static readonly TimeSpan DefaultCompletionDeadline =
        DefaultCompletionOperationCutoff + DefaultCleanupObservationBudget;

    private readonly TimeSpan _admissionOperationCutoff;
    private readonly TimeSpan _completionOperationCutoff;
    private readonly TimeSpan _healthObservationDeadline;
    private readonly string _defaultManagedRoot;
    private readonly IImmutableBootstrapHandoff _handoff;
    private readonly IManagedDistributionPayloadSource _payloadSource;
    private readonly IManagedInstallationRootProbe _rootProbe;
    private readonly ManagedAppVersion _runningLauncherVersion;
    private readonly IVersionManagerStateStore _stateStore;
    private readonly TimeProvider _timeProvider;
    /// <summary>Creates the one bounded local entry coordinator.</summary>
    public ManagedLauncherEntryCoordinator(
        string defaultManagedRoot,
        IVersionManagerStateStore stateStore,
        IManagedInstallationRootProbe rootProbe,
        IManagedDistributionPayloadSource payloadSource,
        ManagedAppVersion runningLauncherVersion,
        IImmutableBootstrapHandoff handoff,
        TimeSpan? admissionOperationCutoff = null,
        TimeSpan? completionOperationCutoff = null,
        TimeProvider? timeProvider = null,
        TimeSpan? healthObservationDeadline = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultManagedRoot);
        _defaultManagedRoot = ManagedRootPathIdentity.Normalize(defaultManagedRoot);
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _rootProbe = rootProbe ?? throw new ArgumentNullException(nameof(rootProbe));
        _payloadSource = payloadSource ?? throw new ArgumentNullException(nameof(payloadSource));
        _runningLauncherVersion = runningLauncherVersion;
        _handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
        _admissionOperationCutoff = admissionOperationCutoff ?? DefaultAdmissionOperationCutoff;
        _completionOperationCutoff = completionOperationCutoff ?? DefaultCompletionOperationCutoff;
        _healthObservationDeadline = healthObservationDeadline ?? DefaultHealthObservationDeadline;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_admissionOperationCutoff, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_completionOperationCutoff, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_healthObservationDeadline, TimeSpan.Zero);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }
    /// <summary>Routes one invocation without Registry, Catalog, package, or full inventory access.</summary>
    public async ValueTask<ManagedLauncherEntryResult> RunAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long started = _timeProvider.GetTimestamp();
        using var deadline = new CancellationTokenSource(_admissionOperationCutoff, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);
        bool bootstrapReceiptAcquired = false;
        string? observedManagedRoot = null;
        try
        {
            LocalHealthObservation localHealth;
            using (var healthDeadline = new CancellationTokenSource(
                _healthObservationDeadline,
                _timeProvider))
            using (var healthLinked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                deadline.Token,
                healthDeadline.Token))
            {
                try
                {
                    localHealth = await AwaitIsolatedReadOnlyObservationAsync(
                        ObserveLocalHealthAsync,
                        healthLinked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    healthDeadline.IsCancellationRequested &&
                    !deadline.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested)
                {
                    return Result(
                        ManagedLauncherEntryOutcome.HealthUnavailable,
                        managedRoot: null,
                        started);
                }
            }

            if (localHealth.Outcome is { } localOutcome)
            {
                return Result(localOutcome, localHealth.ManagedRoot, started);
            }
            string rootIdentity = localHealth.ManagedRoot!;
            observedManagedRoot = rootIdentity;

            ImmutableBootstrapStartResult handoff = await _handoff.StartAsync(
                rootIdentity,
                localHealth.Bootstrap!,
                linked.Token).ConfigureAwait(false);
            using IImmutableBootstrapLaunch? launch = handoff.Launch;
            bootstrapReceiptAcquired = launch is not null;
            if (!handoff.HasValidShape)
            {
                return Result(
                    ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                    rootIdentity,
                    started);
            }
            if (!handoff.IsStarted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (deadline.IsCancellationRequested)
                {
                    linked.Token.ThrowIfCancellationRequested();
                }
                return Result(
                    handoff.Issue switch
                    {
                        ImmutableBootstrapStartIssue.Busy => ManagedLauncherEntryOutcome.Busy,
                        ImmutableBootstrapStartIssue.Damaged =>
                            ManagedLauncherEntryOutcome.RecoveryRequired,
                        ImmutableBootstrapStartIssue.StartFailed =>
                            ManagedLauncherEntryOutcome.LaunchFailed,
                        ImmutableBootstrapStartIssue.Unavailable =>
                            ManagedLauncherEntryOutcome.HealthUnavailable,
                        ImmutableBootstrapStartIssue.None => throw new InvalidOperationException(
                            "A successful Bootstrap start did not return its launch receipt."),
                        _ => throw new InvalidOperationException(
                            "Bootstrap start returned an invalid result."),
                    },
                    rootIdentity,
                    started);
            }
            IImmutableBootstrapLaunch admittedLaunch = launch!;

            if (_timeProvider.GetElapsedTime(started) >= _admissionOperationCutoff)
            {
                deadline.Cancel();
            }
            ImmutableBootstrapAdmissionResult admission = await admittedLaunch
                .WaitForAdmissionAsync(
                    Budget(
                        _admissionOperationCutoff,
                        _timeProvider.GetElapsedTime(started)),
                    linked.Token).ConfigureAwait(false);
            if (!admission.HasValidShape)
            {
                return Result(
                    ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                    rootIdentity,
                    started);
            }
            if (admission.Outcome == ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed)
            {
                return Result(
                    ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                    rootIdentity,
                    started);
            }
            if (admission.Outcome == ImmutableBootstrapAdmissionOutcome.Admitted &&
                (cancellationToken.IsCancellationRequested ||
                    _timeProvider.GetElapsedTime(started) >= _admissionOperationCutoff))
            {
                deadline.Cancel();
                TimeSpan cleanupRemaining = Remaining(
                    _admissionOperationCutoff + DefaultCleanupObservationBudget,
                    _timeProvider.GetElapsedTime(started));
                if (cleanupRemaining == TimeSpan.Zero)
                {
                    return Result(
                        ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                        rootIdentity,
                        started);
                }
                using var cleanupDeadline = new CancellationTokenSource(
                    cleanupRemaining,
                    _timeProvider);
                ImmutableBootstrapCompletionResult cleanup;
                try
                {
                    cleanup = await admittedLaunch
                        .WaitForCompletionAsync(
                            new ImmutableBootstrapWaitBudget(TimeSpan.Zero, cleanupRemaining),
                            cleanupDeadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cleanupDeadline.IsCancellationRequested)
                {
                    return Result(
                        ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                        rootIdentity,
                        started);
                }
                if (!cleanup.HasValidShape)
                {
                    return Result(
                        ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                        rootIdentity,
                        started);
                }
                if (cleanup.Outcome == ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed)
                {
                    return Result(
                        ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                        rootIdentity,
                        started);
                }
                cancellationToken.ThrowIfCancellationRequested();
                return Result(
                    ManagedLauncherEntryOutcome.HealthUnavailable,
                    rootIdentity,
                    started);
            }
            if (admission.Outcome != ImmutableBootstrapAdmissionOutcome.Admitted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Result(
                    admission.Outcome switch
                    {
                        ImmutableBootstrapAdmissionOutcome.Busy => ManagedLauncherEntryOutcome.Busy,
                        ImmutableBootstrapAdmissionOutcome.RecoveryRequired =>
                            ManagedLauncherEntryOutcome.RecoveryRequired,
                        ImmutableBootstrapAdmissionOutcome.LaunchFailed =>
                            ManagedLauncherEntryOutcome.LaunchFailed,
                        ImmutableBootstrapAdmissionOutcome.HealthUnavailable =>
                            ManagedLauncherEntryOutcome.HealthUnavailable,
                        ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed =>
                            ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                        ImmutableBootstrapAdmissionOutcome.Admitted => throw new InvalidOperationException(
                            "An admitted Bootstrap was handled as an admission failure."),
                        _ => throw new InvalidOperationException(
                            "Bootstrap admission returned an undefined outcome."),
                    },
                    rootIdentity,
                    started);
            }

            TimeSpan admissionElapsed = _timeProvider.GetElapsedTime(started);
            long completionStarted = _timeProvider.GetTimestamp();
            using var completionDeadline = new CancellationTokenSource(
                _completionOperationCutoff,
                _timeProvider);
            using var completionLinked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                completionDeadline.Token);
            ImmutableBootstrapCompletionResult completion;
            try
            {
                completion = await admittedLaunch
                    .WaitForCompletionAsync(
                        new ImmutableBootstrapWaitBudget(
                            _completionOperationCutoff,
                            _completionOperationCutoff + DefaultCleanupObservationBudget),
                        completionLinked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                completionDeadline.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                return new(
                    ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                    rootIdentity,
                    admissionElapsed,
                    _timeProvider.GetElapsedTime(started));
            }
            if (!completion.HasValidShape)
            {
                return new(
                    ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                    rootIdentity,
                    admissionElapsed,
                    _timeProvider.GetElapsedTime(started));
            }
            if (completion.Outcome == ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed)
            {
                return new(
                    ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                    rootIdentity,
                    admissionElapsed,
                    _timeProvider.GetElapsedTime(started));
            }
            if (_timeProvider.GetElapsedTime(completionStarted) >= _completionOperationCutoff)
            {
                return new(
                    ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                    rootIdentity,
                    admissionElapsed,
                    _timeProvider.GetElapsedTime(started));
            }
            if (completion.Outcome is
                ImmutableBootstrapCompletionOutcome.Ready or
                ImmutableBootstrapCompletionOutcome.RolledBack)
            {
                return new(
                    ManagedLauncherEntryOutcome.LaunchInstalled,
                    rootIdentity,
                    admissionElapsed,
                    _timeProvider.GetElapsedTime(started));
            }
            cancellationToken.ThrowIfCancellationRequested();
            ManagedLauncherEntryOutcome outcome = completion.Outcome switch
            {
                ImmutableBootstrapCompletionOutcome.Ready or
                ImmutableBootstrapCompletionOutcome.RolledBack =>
                    ManagedLauncherEntryOutcome.LaunchInstalled,
                ImmutableBootstrapCompletionOutcome.Failed =>
                    ManagedLauncherEntryOutcome.LaunchFailed,
                ImmutableBootstrapCompletionOutcome.Unavailable =>
                    ManagedLauncherEntryOutcome.HealthUnavailable,
                ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed =>
                    ManagedLauncherEntryOutcome.TerminationUnconfirmed,
                _ => throw new InvalidOperationException(
                    "Bootstrap completion returned an undefined outcome."),
            };
            return new(
                outcome,
                rootIdentity,
                admissionElapsed,
                _timeProvider.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (
            deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Result(
                bootstrapReceiptAcquired
                    ? ManagedLauncherEntryOutcome.TerminationUnconfirmed
                    : ManagedLauncherEntryOutcome.HealthUnavailable,
                observedManagedRoot,
                started);
        }
    }

    private async ValueTask<LocalHealthObservation> ObserveLocalHealthAsync(
        CancellationToken cancellationToken)
    {
        ManagedDistributionPayloadEntryAdmissionResult payload = await _payloadSource
            .AdmitEntryAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!payload.IsSuccess)
        {
            return new(
                payload.Issue switch
                {
                    ManagedDistributionPayloadIssue.Unavailable =>
                        ManagedLauncherEntryOutcome.PayloadUnavailable,
                    ManagedDistributionPayloadIssue.Invalid or ManagedDistributionPayloadIssue.Changed =>
                        ManagedLauncherEntryOutcome.PayloadInvalid,
                    ManagedDistributionPayloadIssue.None => throw new InvalidOperationException(
                        "Successful payload admission returned no Bootstrap identity."),
                    _ => throw new InvalidOperationException(
                        "Payload admission returned an undefined issue."),
                },
                ManagedRoot: null,
                Bootstrap: null);
        }
        if (payload.LauncherVersion != _runningLauncherVersion)
        {
            return new(ManagedLauncherEntryOutcome.PayloadInvalid, ManagedRoot: null, Bootstrap: null);
        }
        ManagedImmutableBootstrapIdentity bootstrap = payload.Bootstrap!;

        VersionManagerStateLoadResult loaded = await _stateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (loaded.Issue == VersionManagerStateLoadIssue.Missing)
        {
            ManagedInstallationRootObservation root = await _rootProbe.ObserveAsync(
                _defaultManagedRoot,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new(
                root.Status switch
                {
                    ManagedInstallationRootStatus.Absent =>
                        ManagedLauncherEntryOutcome.SetupRequired,
                    ManagedInstallationRootStatus.Present or
                    ManagedInstallationRootStatus.Residue or
                    ManagedInstallationRootStatus.InvalidDestination =>
                        ManagedLauncherEntryOutcome.RecoveryRequired,
                    ManagedInstallationRootStatus.PermissionDenied or
                    ManagedInstallationRootStatus.Unavailable =>
                        ManagedLauncherEntryOutcome.HealthUnavailable,
                    _ => throw new InvalidOperationException(
                        "Root probe returned an undefined status."),
                },
                _defaultManagedRoot,
                bootstrap);
        }
        if (!loaded.IsSuccess)
        {
            return new(
                loaded.Issue is VersionManagerStateLoadIssue.Invalid or
                    VersionManagerStateLoadIssue.ManagedRootMismatch
                    ? ManagedLauncherEntryOutcome.RecoveryRequired
                    : ManagedLauncherEntryOutcome.HealthUnavailable,
                ManagedRoot: null,
                Bootstrap: bootstrap);
        }

        VersionManagerState state = loaded.State!;
        if (state.ManagedRootIdentity is null)
        {
            return new(
                ManagedLauncherEntryOutcome.RecoveryRequired,
                ManagedRoot: null,
                Bootstrap: bootstrap);
        }
        string rootIdentity = ManagedRootPathIdentity.Normalize(state.ManagedRootIdentity);
        ManagedInstallationRootObservation installedRoot = await _rootProbe.ObserveAsync(
            rootIdentity,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return installedRoot.Status == ManagedInstallationRootStatus.Present
            ? new(Outcome: null, rootIdentity, bootstrap)
            : new(
                installedRoot.Status is ManagedInstallationRootStatus.Absent or
                    ManagedInstallationRootStatus.Residue or
                    ManagedInstallationRootStatus.InvalidDestination
                    ? ManagedLauncherEntryOutcome.RecoveryRequired
                    : ManagedLauncherEntryOutcome.HealthUnavailable,
                rootIdentity,
                bootstrap);
    }

    private static async ValueTask<T> AwaitIsolatedReadOnlyObservationAsync<T>(
        Func<CancellationToken, ValueTask<T>> observe,
        CancellationToken cancellationToken)
    {
        Task<T> pending = Task.Run(
            async () => await observe(cancellationToken).ConfigureAwait(false),
            CancellationToken.None);
        try
        {
            return await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = ObserveAbandonedReadOnlyTaskAsync(pending);
            throw;
        }
    }

    private static async Task ObserveAbandonedReadOnlyTaskAsync(Task pending)
    {
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The caller has already returned a typed timeout/cancellation result.
        }
    }

    private sealed record LocalHealthObservation(
        ManagedLauncherEntryOutcome? Outcome,
        string? ManagedRoot,
        ManagedImmutableBootstrapIdentity? Bootstrap);

    private ManagedLauncherEntryResult Result(
        ManagedLauncherEntryOutcome outcome,
        string? managedRoot,
        long started)
    {
        TimeSpan elapsed = _timeProvider.GetElapsedTime(started);
        return new(outcome, managedRoot, elapsed, elapsed);
    }

    private static TimeSpan Remaining(TimeSpan total, TimeSpan elapsed)
    {
        return elapsed >= total ? TimeSpan.Zero : total - elapsed;
    }

    private static ImmutableBootstrapWaitBudget Budget(TimeSpan operation, TimeSpan elapsed)
    {
        return Budget(operation, elapsed, operation + DefaultCleanupObservationBudget);
    }

    private static ImmutableBootstrapWaitBudget Budget(
        TimeSpan operation,
        TimeSpan elapsed,
        TimeSpan total)
    {
        return new(Remaining(operation, elapsed), Remaining(total, elapsed));
    }
}
