using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Diagnostics;

/// <summary>Focused current-session query, refresh, and export-snapshot contract.</summary>
public interface ISystemInformationService
{
    /// <summary>Latest immutable observation.</summary>
    SystemInformationSnapshot Current { get; }

    /// <summary>Bounded current-session activity history.</summary>
    IReadOnlyList<SystemActivityEntry> Activity { get; }

    /// <summary>Reprobes runtime state and optionally reloads the canonical catalog first.</summary>
    SystemInformationSnapshot Refresh(bool reloadCatalog, CancellationToken cancellationToken);

    /// <summary>Captures a versioned privacy-filtered export payload.</summary>
    SystemDiagnosticsBundle CreateBundle();

    /// <summary>Records one privacy-filtered user or system activity.</summary>
    void RecordActivity(SystemActivityDraft activity);
}

/// <summary>Owns refreshable System Information separately from immutable run reports.</summary>
public sealed class SystemInformationService : ISystemInformationService
{
    private const int DefaultActivityLimit = 128;
    private readonly Lock _gate = new();
    private readonly Lock _refreshGate = new();
    private readonly string _applicationVersion;
    private readonly ICanonicalSupportMatrixQuery _catalogQuery;
    private readonly ICanonicalCapabilityCatalogReloader _catalogReloader;
    private readonly ISystemRuntimeProbe _runtimeProbe;
    private readonly IExternalProcessorEnvironmentLoader _externalEnvironment;
    private readonly ISystemClock _clock;
    private readonly int _activityLimit;
    private readonly List<SystemActivityEntry> _activity = [];
    private long _activitySequence;
    private SystemInformationSnapshot _current;

    /// <summary>Creates one current-session lifecycle and performs its startup probe.</summary>
    public SystemInformationService(
        string applicationVersion,
        ICanonicalSupportMatrixQuery catalogQuery,
        ICanonicalCapabilityCatalogReloader catalogReloader,
        IExternalProcessorEnvironmentLoader externalEnvironment,
        ISystemRuntimeProbe runtimeProbe,
        ISystemClock clock,
        int activityLimit = DefaultActivityLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
        ArgumentOutOfRangeException.ThrowIfLessThan(activityLimit, 1);
        _applicationVersion = applicationVersion;
        _catalogQuery = catalogQuery ?? throw new ArgumentNullException(nameof(catalogQuery));
        _catalogReloader = catalogReloader ?? throw new ArgumentNullException(nameof(catalogReloader));
        _externalEnvironment = externalEnvironment ??
            throw new ArgumentNullException(nameof(externalEnvironment));
        _runtimeProbe = runtimeProbe ?? throw new ArgumentNullException(nameof(runtimeProbe));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _activityLimit = activityLimit;
        _current = Capture(generation: 1);
        RecordActivityCore(new SystemActivityDraft(
            SystemActivityCodes.ApplicationStarted,
            SystemActivityImportance.Important,
            SystemActivityCategory.Session,
            SystemActivitySeverity.Information,
            _applicationVersion));
        RecordDiagnosticChanges(previous: null, _current);
    }

    /// <inheritdoc />
    public IReadOnlyList<SystemActivityEntry> Activity
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly([.. _activity]);
            }
        }
    }

    /// <inheritdoc />
    public SystemInformationSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public SystemInformationSnapshot Refresh(
        bool reloadCatalog,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_refreshGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reloadCatalog)
            {
                _catalogReloader.Reload(cancellationToken);
            }

            lock (_gate)
            {
                SystemInformationSnapshot next = Capture(checked(_current.Generation + 1));
                RecordDiagnosticChanges(_current, next);
                _current = next;
                RecordActivityCore(new SystemActivityDraft(
                    SystemActivityCodes.SystemRefreshed,
                    SystemActivityImportance.Debug,
                    SystemActivityCategory.Diagnostics,
                    SystemActivitySeverity.Information,
                    reloadCatalog ? "catalog-and-runtime" : "runtime"));
                return next;
            }
        }
    }

    /// <inheritdoc />
    public SystemDiagnosticsBundle CreateBundle()
    {
        lock (_gate)
        {
            return new SystemDiagnosticsBundle(_current, _activity);
        }
    }

    /// <inheritdoc />
    public void RecordActivity(SystemActivityDraft activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ValidateActivity(activity);
        lock (_gate)
        {
            RecordActivityCore(activity);
        }
    }

    private SystemInformationSnapshot Capture(long generation)
    {
        CanonicalSupportMatrixQueryResult catalog = _catalogQuery.Query();
        CanonicalSupportMatrixSnapshot? matrix = catalog.Matrix;
        ExternalProcessorEnvironmentStatus externalEnvironment = _externalEnvironment.Current;
        return new SystemInformationSnapshot(
            generation,
            _clock.UtcNow,
            _applicationVersion,
            _runtimeProbe.Probe(),
            catalog.State,
            matrix?.CatalogId,
            matrix?.CatalogVersion,
            matrix?.SourceSha256,
            matrix?.ResolutionToken.Value,
            catalog.ReloadIssues.Select(static issue => issue.Code),
            externalEnvironment,
            DiagnosticsFor(catalog.State, externalEnvironment.State));
    }

    private static List<ActionableSystemDiagnostic> DiagnosticsFor(
        CanonicalSupportMatrixCatalogState state,
        ExternalProcessorEnvironmentState externalState)
    {
        List<ActionableSystemDiagnostic> diagnostics = state switch
        {
            CanonicalSupportMatrixCatalogState.LastKnownGood =>
            [
                new ActionableSystemDiagnostic(
                    SystemDiagnosticCodes.CapabilityCatalogLastKnownGood,
                    SystemDiagnosticCategory.CapabilityCatalog,
                    SystemDiagnosticSeverity.Warning,
                    "Capability catalog reload failed; the last-known-good publication remains active.",
                    "Review the catalog source and reload."),
            ],
            CanonicalSupportMatrixCatalogState.ColdStartBlocked =>
            [
                new ActionableSystemDiagnostic(
                    SystemDiagnosticCodes.CapabilityCatalogUnavailable,
                    SystemDiagnosticCategory.CapabilityCatalog,
                    SystemDiagnosticSeverity.Blocking,
                    "The capability catalog is unavailable, so Build is disabled.",
                    "Correct the catalog source and reload."),
            ],
            CanonicalSupportMatrixCatalogState.Loading or
            CanonicalSupportMatrixCatalogState.Current => [],
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
        if (externalState == ExternalProcessorEnvironmentState.LastKnownGood)
        {
            diagnostics.Add(new(
                SystemDiagnosticCodes.ExternalProcessorEnvironmentLastKnownGood,
                SystemDiagnosticCategory.ExternalProcessorEnvironment,
                SystemDiagnosticSeverity.Warning,
                "External tool refresh failed; the last-known-good environment remains active.",
                "Review external tool manifests and refresh."));
        }
        else if (externalState == ExternalProcessorEnvironmentState.Unavailable)
        {
            diagnostics.Add(new(
                SystemDiagnosticCodes.ExternalProcessorEnvironmentUnavailable,
                SystemDiagnosticCategory.ExternalProcessorEnvironment,
                SystemDiagnosticSeverity.Warning,
                "The external tool environment is unavailable.",
                "Review external tool manifests and refresh."));
        }
        return diagnostics;
    }

    private void RecordDiagnosticChanges(
        SystemInformationSnapshot? previous,
        SystemInformationSnapshot current)
    {
        HashSet<string> before = previous?.ActiveDiagnostics
            .Select(static diagnostic => diagnostic.Code)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        var after = current.ActiveDiagnostics
            .Select(static diagnostic => diagnostic.Code)
            .ToHashSet(StringComparer.Ordinal);
        string[] added = [.. after.Except(before, StringComparer.Ordinal)];
        string[] resolved = [.. before.Except(after, StringComparer.Ordinal)];
        foreach (string code in added.Order(StringComparer.Ordinal))
        {
            RecordActivityCore(new SystemActivityDraft(
                SystemActivityCodes.DiagnosticActivated,
                SystemActivityImportance.Important,
                SystemActivityCategory.Diagnostics,
                SystemActivitySeverity.Warning,
                code));
        }

        foreach (string code in resolved.Order(StringComparer.Ordinal))
        {
            RecordActivityCore(new SystemActivityDraft(
                SystemActivityCodes.DiagnosticResolved,
                SystemActivityImportance.Important,
                SystemActivityCategory.Diagnostics,
                SystemActivitySeverity.Success,
                code));
        }
    }

    private void RecordActivityCore(SystemActivityDraft activity)
    {
        ValidateActivity(activity);
        _activity.Add(new SystemActivityEntry(
            checked(++_activitySequence),
            _clock.UtcNow,
            activity));
        while (_activity.Count > _activityLimit)
        {
            _activity.RemoveAt(0);
        }
    }

    private static void ValidateActivity(SystemActivityDraft activity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activity.Code);
        if (!Enum.IsDefined(activity.Importance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(activity),
                activity.Importance,
                "Activity importance must use the closed disclosure vocabulary.");
        }
        if (!Enum.IsDefined(activity.Category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(activity),
                activity.Category,
                "Activity category must use the closed system vocabulary.");
        }
        if (!Enum.IsDefined(activity.Severity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(activity),
                activity.Severity,
                "Activity severity must use the closed presentation vocabulary.");
        }
        ValidateToken(activity.Code, nameof(activity.Code));
        ValidateToken(activity.SubjectId, nameof(activity.SubjectId));
        ValidateToken(activity.ContextId, nameof(activity.ContextId));
    }

    private static void ValidateToken(string? value, string parameterName)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 128 ||
            value.IndexOfAny(['/', '\\', '\r', '\n']) >= 0 ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Activity identifiers must be short, single-line, path-free tokens.",
                parameterName);
        }
    }
}
