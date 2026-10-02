namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>
/// Terminal entry reason selected by the Application coordinator. First-installation launch
/// issues describe post-promotion failures only; entry also decides payload/state/root health,
/// Setup, success, and separate health, admission, and completion timeouts.
/// For malformed or termination-uncertain process receipts, caller cancellation takes precedence
/// over the stage's operation timeout, which takes precedence over the receipt reason. Optional
/// upstream members retain the receipt facts. Accepted READY/rollback retains success, and
/// ordinary caller cancellation still propagates as OperationCanceledException.
/// </summary>
public enum ManagedLauncherEntryReason
{
    /// <summary>An external caller constructed a legacy result without a reason.</summary>
    NotSpecified,
    /// <summary>The application reached READY, directly or through admitted LKG rollback.</summary>
    Success,
    /// <summary>State and root are genuinely absent and Setup may be shown.</summary>
    SetupRequired,
    /// <summary>The payload or its running Launcher version failed admission.</summary>
    PayloadInvalid,
    /// <summary>Observed state, root, or Bootstrap facts require recovery.</summary>
    RecoveryRequired,
    /// <summary>Another writer owns the bounded launch transaction.</summary>
    Busy,
    /// <summary>
    /// An observation reported unavailable facts or a process result. At ApplicationReady this
    /// also covers cancellation unrelated to the caller or completion deadline, caught after
    /// the independent admission deadline expired; it does not imply a completion timeout.
    /// </summary>
    Unavailable,
    /// <summary>The exact-root observation reported permission denial.</summary>
    PermissionDenied,
    /// <summary>The exact Bootstrap process could not be created.</summary>
    StartFailed,
    /// <summary>Bootstrap admission or application READY failed.</summary>
    LaunchFailed,
    /// <summary>A start, admission, or completion receipt has a contradictory or undefined shape.</summary>
    InvalidReceipt,
    /// <summary>The receipt could not prove the started process tree terminated or safely released.</summary>
    TerminationUnconfirmed,
    /// <summary>The shared local-health deadline expired before observation completed.</summary>
    HealthDeadlineExceeded,
    /// <summary>The local admission operation budget expired.</summary>
    AdmissionTimeout,
    /// <summary>The independent Bootstrap completion operation budget expired.</summary>
    CompletionTimeout,
    /// <summary>The caller cancelled entry while bounded cleanup still required a terminal result.</summary>
    CallerCancelled,
}

/// <summary>
/// Entry step at which Application made its terminal decision. First-installation launch stages
/// cannot represent pre-launch payload/state/root decisions or reserved admission cleanup;
/// shared BootstrapStart, LauncherAdmission, and ApplicationReady steps use the same names.
/// </summary>
public enum ManagedLauncherEntryStage
{
    /// <summary>An external caller constructed a legacy result without a stage.</summary>
    NotSpecified,
    /// <summary>The embedded payload descriptor and running Launcher version are being admitted.</summary>
    PayloadAdmission,
    /// <summary>The canonical per-user state is being loaded and its root binding checked.</summary>
    StateLoad,
    /// <summary>The exact default or state-bound managed root is being observed.</summary>
    RootObservation,
    /// <summary>The exact immutable Bootstrap is being verified and started.</summary>
    BootstrapStart,
    /// <summary>The started Bootstrap is being observed for Launcher admission.</summary>
    LauncherAdmission,
    /// <summary>Late or cancelled admission is consuming its reserved cleanup observation.</summary>
    AdmissionCleanup,
    /// <summary>The admitted Bootstrap is being observed for application READY or rollback.</summary>
    ApplicationReady,
}
