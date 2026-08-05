using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Diagnostics;

/// <summary>Focused current-session query, refresh, and export-snapshot contract.</summary>
public interface ISystemInformationService
{
    /// <summary>Latest immutable observation.</summary>
    SystemInformationSnapshot Current { get; }

    /// <summary>Bounded activation/resolution transitions for this process session.</summary>
    IReadOnlyList<SystemDiagnosticTransition> Transitions { get; }

    /// <summary>Reprobes runtime state and optionally reloads the canonical catalog first.</summary>
    SystemInformationSnapshot Refresh(bool reloadCatalog, CancellationToken cancellationToken);

    /// <summary>Captures a versioned privacy-filtered export payload.</summary>
    SystemDiagnosticsBundle CreateBundle();
}

/// <summary>Owns refreshable System Information separately from immutable run reports.</summary>
public sealed class SystemInformationService : ISystemInformationService
{
    private const int DefaultTransitionLimit = 32;
    private readonly Lock _gate = new();
    private readonly Lock _refreshGate = new();
    private readonly string _applicationVersion;
    private readonly ICanonicalSupportMatrixQuery _catalogQuery;
    private readonly ICanonicalCapabilityCatalogReloader _catalogReloader;
    private readonly ISystemRuntimeProbe _runtimeProbe;
    private readonly ISystemClock _clock;
    private readonly int _transitionLimit;
    private readonly List<SystemDiagnosticTransition> _transitions = [];
    private SystemInformationSnapshot _current;

    /// <summary>Creates one current-session lifecycle and performs its startup probe.</summary>
    public SystemInformationService(
        string applicationVersion,
        ICanonicalSupportMatrixQuery catalogQuery,
        ICanonicalCapabilityCatalogReloader catalogReloader,
        ISystemRuntimeProbe runtimeProbe,
        ISystemClock clock,
        int transitionLimit = DefaultTransitionLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
        ArgumentOutOfRangeException.ThrowIfLessThan(transitionLimit, 1);
        _applicationVersion = applicationVersion;
        _catalogQuery = catalogQuery ?? throw new ArgumentNullException(nameof(catalogQuery));
        _catalogReloader = catalogReloader ?? throw new ArgumentNullException(nameof(catalogReloader));
        _runtimeProbe = runtimeProbe ?? throw new ArgumentNullException(nameof(runtimeProbe));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _transitionLimit = transitionLimit;
        _current = Capture(generation: 1);
        RecordTransition(previous: null, _current);
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
    public IReadOnlyList<SystemDiagnosticTransition> Transitions
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly([.. _transitions]);
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
                RecordTransition(_current, next);
                _current = next;
                return next;
            }
        }
    }

    /// <inheritdoc />
    public SystemDiagnosticsBundle CreateBundle()
    {
        lock (_gate)
        {
            return new SystemDiagnosticsBundle(_current, _transitions);
        }
    }

    private SystemInformationSnapshot Capture(long generation)
    {
        CanonicalSupportMatrixQueryResult catalog = _catalogQuery.Query();
        CanonicalSupportMatrixSnapshot? matrix = catalog.Matrix;
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
            DiagnosticsFor(catalog.State));
    }

    private static IReadOnlyList<ActionableSystemDiagnostic> DiagnosticsFor(
        CanonicalSupportMatrixCatalogState state)
    {
        return state switch
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
    }

    private void RecordTransition(
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
        if (added.Length == 0 && resolved.Length == 0)
        {
            return;
        }

        _transitions.Add(new SystemDiagnosticTransition(
            current.Generation,
            current.ObservedAtUtc,
            added,
            resolved));
        while (_transitions.Count > _transitionLimit)
        {
            _transitions.RemoveAt(0);
        }
    }
}
