namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Terminal reason selected by the Application Launcher entry coordinator.</summary>
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
    /// <summary>A completed observation reported unavailable facts or a process result.</summary>
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

/// <summary>Existing entry-sequence step at which Application made its terminal decision.</summary>
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
    BootstrapAdmission,
    /// <summary>Late or cancelled admission is consuming its reserved cleanup observation.</summary>
    AdmissionCleanup,
    /// <summary>The admitted Bootstrap is being observed for application READY or rollback.</summary>
    BootstrapCompletion,
}
