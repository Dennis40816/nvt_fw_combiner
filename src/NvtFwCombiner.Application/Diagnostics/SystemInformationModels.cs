using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Application.Diagnostics;

/// <summary>Stable diagnostic category shared by UI and CLI adapters.</summary>
public enum SystemDiagnosticCategory
{
    /// <summary>Canonical capability catalog load or publication state.</summary>
    CapabilityCatalog,

    /// <summary>Bounded external processor environment discovery.</summary>
    ExternalProcessorEnvironment,
}

/// <summary>Severity of one active current-session system diagnostic.</summary>
public enum SystemDiagnosticSeverity
{
    /// <summary>A retained safe state needs operator review.</summary>
    Warning,

    /// <summary>The current system state blocks Build.</summary>
    Blocking,
}

/// <summary>Stable diagnostic codes emitted by the System Information lifecycle.</summary>
public static class SystemDiagnosticCodes
{
    /// <summary>No canonical publication exists after a load attempt.</summary>
    public const string CapabilityCatalogUnavailable = "system.catalog.unavailable";

    /// <summary>A failed reload retained the prior immutable publication.</summary>
    public const string CapabilityCatalogLastKnownGood = "system.catalog.last-known-good";

    /// <summary>No valid external processor environment is available.</summary>
    public const string ExternalProcessorEnvironmentUnavailable =
        "system.external-environment.unavailable";

    /// <summary>An external environment refresh retained its prior publication.</summary>
    public const string ExternalProcessorEnvironmentLastKnownGood =
        "system.external-environment.last-known-good";
}

/// <summary>One path-free diagnostic with operator-safe text shared by every adapter.</summary>
public sealed record ActionableSystemDiagnostic(
    string Code,
    SystemDiagnosticCategory Category,
    SystemDiagnosticSeverity Severity,
    string Message,
    string Action);

/// <summary>Non-sensitive facts about the current managed process.</summary>
public sealed record SystemRuntimeFacts(
    string FrameworkDescription,
    string OperatingSystemDescription,
    string ProcessArchitecture);

/// <summary>Immutable current-session System Information snapshot.</summary>
public sealed class SystemInformationSnapshot
{
    internal SystemInformationSnapshot(
        long generation,
        DateTimeOffset observedAtUtc,
        string applicationVersion,
        SystemRuntimeFacts runtime,
        CanonicalSupportMatrixCatalogState catalogState,
        string? catalogId,
        string? catalogVersion,
        string? catalogSourceSha256,
        string? publicationToken,
        IEnumerable<string> catalogIssueCodes,
        ExternalProcessorEnvironmentStatus externalEnvironment,
        IEnumerable<ActionableSystemDiagnostic> activeDiagnostics)
    {
        Generation = generation;
        ObservedAtUtc = observedAtUtc;
        ApplicationVersion = applicationVersion;
        Runtime = runtime;
        CatalogState = catalogState;
        CatalogId = catalogId;
        CatalogVersion = catalogVersion;
        CatalogSourceSha256 = catalogSourceSha256;
        PublicationToken = publicationToken;
        CatalogIssueCodes = Array.AsReadOnly([
            .. catalogIssueCodes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
        ]);
        ExternalEnvironment = externalEnvironment ??
            throw new ArgumentNullException(nameof(externalEnvironment));
        ActiveDiagnostics = Array.AsReadOnly([.. activeDiagnostics]);
    }

    /// <summary>Monotonic generation within this process session.</summary>
    public long Generation { get; }

    /// <summary>UTC observation time.</summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary>Application informational version.</summary>
    public string ApplicationVersion { get; }

    /// <summary>Current non-sensitive process facts.</summary>
    public SystemRuntimeFacts Runtime { get; }

    /// <summary>Canonical catalog lifecycle state.</summary>
    public CanonicalSupportMatrixCatalogState CatalogState { get; }

    /// <summary>Published catalog identity when current or retained.</summary>
    public string? CatalogId { get; }

    /// <summary>Published catalog version when current or retained.</summary>
    public string? CatalogVersion { get; }

    /// <summary>SHA-256 of the exact published source.</summary>
    public string? CatalogSourceSha256 { get; }

    /// <summary>Opaque publication identity, safe for correlation within diagnostics.</summary>
    public string? PublicationToken { get; }

    /// <summary>Stable source issue codes only; raw messages and paths are intentionally excluded.</summary>
    public IReadOnlyList<string> CatalogIssueCodes { get; }

    /// <summary>Latest bounded external environment lifecycle observation.</summary>
    public ExternalProcessorEnvironmentStatus ExternalEnvironment { get; }

    /// <summary>Currently active diagnostics. Resolved diagnostics never remain here.</summary>
    public IReadOnlyList<ActionableSystemDiagnostic> ActiveDiagnostics { get; }

    /// <summary>True when an active global diagnostic disables Build.</summary>
    public bool IsBuildBlocked => ActiveDiagnostics.Any(static diagnostic =>
        diagnostic.Severity == SystemDiagnosticSeverity.Blocking);
}

/// <summary>Disclosure level for one current-session activity entry.</summary>
public enum SystemActivityImportance
{
    /// <summary>Shown in the default operator history.</summary>
    Important,

    /// <summary>Shown only after the operator explicitly expands Debug activity.</summary>
    Debug,
}

/// <summary>Stable category for one current-session activity entry.</summary>
public enum SystemActivityCategory
{
    /// <summary>Application process-session lifecycle.</summary>
    Session,
    /// <summary>System diagnostic lifecycle.</summary>
    Diagnostics,
    /// <summary>Shell and modal navigation.</summary>
    Navigation,
    /// <summary>Workflow context selection.</summary>
    Workflow,
    /// <summary>Firmware input selection.</summary>
    Input,
    /// <summary>Preview and Build execution.</summary>
    Composition,
}

/// <summary>Visual and filtering severity for one current-session activity entry.</summary>
public enum SystemActivitySeverity
{
    /// <summary>Neutral information.</summary>
    Information,
    /// <summary>Successful completion.</summary>
    Success,
    /// <summary>Operator attention is useful.</summary>
    Warning,
    /// <summary>An operation failed.</summary>
    Error,
}

/// <summary>Stable event codes owned by the System Information lifecycle.</summary>
public static class SystemActivityCodes
{
    /// <summary>The application process started.</summary>
    public const string ApplicationStarted = "activity.session.started";
    /// <summary>The required managed startup path became interactive.</summary>
    public const string StartupReady = "activity.session.ready";
    /// <summary>A system diagnostic became active.</summary>
    public const string DiagnosticActivated = "activity.diagnostic.activated";
    /// <summary>A system diagnostic was resolved.</summary>
    public const string DiagnosticResolved = "activity.diagnostic.resolved";
    /// <summary>System information was reprobed.</summary>
    public const string SystemRefreshed = "activity.diagnostic.refreshed";
    /// <summary>The user changed shell page.</summary>
    public const string UserNavigated = "activity.navigation.changed";
    /// <summary>The user opened Settings.</summary>
    public const string SettingsOpened = "activity.navigation.settings-opened";
    /// <summary>The user opened Message Center.</summary>
    public const string MessageCenterOpened = "activity.navigation.message-center-opened";
    /// <summary>The user selected a workflow mode.</summary>
    public const string ModeSelected = "activity.workflow.mode-selected";
    /// <summary>The user selected an IC.</summary>
    public const string IcSelected = "activity.workflow.ic-selected";
    /// <summary>The user selected a firmware number.</summary>
    public const string NumberSelected = "activity.workflow.number-selected";
    /// <summary>The user selected a firmware input.</summary>
    public const string InputSelected = "activity.input.selected";
    /// <summary>A Preview attempt started.</summary>
    public const string PreviewStarted = "activity.composition.preview-started";
    /// <summary>A Build attempt started.</summary>
    public const string BuildStarted = "activity.composition.build-started";
    /// <summary>A Preview attempt completed.</summary>
    public const string PreviewCompleted = "activity.composition.preview-completed";
    /// <summary>A Build attempt completed.</summary>
    public const string BuildCompleted = "activity.composition.build-completed";
    /// <summary>A Preview attempt failed.</summary>
    public const string PreviewFailed = "activity.composition.preview-failed";
    /// <summary>A Build attempt failed.</summary>
    public const string BuildFailed = "activity.composition.build-failed";
    /// <summary>The user requested a diagnostic refresh.</summary>
    public const string DiagnosticsRefreshRequested = "activity.diagnostic.refresh-requested";
    /// <summary>A diagnostics bundle was exported.</summary>
    public const string DiagnosticsExported = "activity.diagnostic.exported";
    /// <summary>A diagnostics export failed.</summary>
    public const string DiagnosticsExportFailed = "activity.diagnostic.export-failed";
}

/// <summary>Privacy-filtered request to append one activity entry.</summary>
public sealed record SystemActivityDraft(
    string Code,
    SystemActivityImportance Importance,
    SystemActivityCategory Category,
    SystemActivitySeverity Severity,
    string? SubjectId = null,
    string? ContextId = null);

/// <summary>One immutable, bounded, privacy-filtered current-session activity entry.</summary>
public sealed class SystemActivityEntry
{
    internal SystemActivityEntry(
        long sequence,
        DateTimeOffset observedAtUtc,
        SystemActivityDraft draft)
    {
        Sequence = sequence;
        ObservedAtUtc = observedAtUtc;
        Code = draft.Code;
        Importance = draft.Importance;
        Category = draft.Category;
        Severity = draft.Severity;
        SubjectId = draft.SubjectId;
        ContextId = draft.ContextId;
    }

    /// <summary>Monotonic sequence within the current process.</summary>
    public long Sequence { get; }

    /// <summary>UTC observation time.</summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary>Stable activity code.</summary>
    public string Code { get; }

    /// <summary>Disclosure level.</summary>
    public SystemActivityImportance Importance { get; }

    /// <summary>Stable category.</summary>
    public SystemActivityCategory Category { get; }

    /// <summary>Filtering and visual severity.</summary>
    public SystemActivitySeverity Severity { get; }

    /// <summary>Optional privacy-safe primary token.</summary>
    public string? SubjectId { get; }

    /// <summary>Optional privacy-safe context token.</summary>
    public string? ContextId { get; }
}

/// <summary>Versioned privacy-filtered payload supplied to the host exporter.</summary>
public sealed class SystemDiagnosticsBundle
{
    /// <summary>Current JSON contract identity.</summary>
    public const string CurrentSchemaVersion = "system-diagnostics-v2";

    internal SystemDiagnosticsBundle(
        SystemInformationSnapshot current,
        IEnumerable<SystemActivityEntry> activities)
    {
        SchemaVersion = CurrentSchemaVersion;
        Current = current;
        Activities = Array.AsReadOnly([.. activities]);
    }

    /// <summary>Stable schema version.</summary>
    public string SchemaVersion { get; }

    /// <summary>Latest immutable System Information snapshot.</summary>
    public SystemInformationSnapshot Current { get; }

    /// <summary>Bounded current-session activity history; no raw path or report data is allowed.</summary>
    public IReadOnlyList<SystemActivityEntry> Activities { get; }
}
