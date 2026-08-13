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

/// <summary>One bounded record of diagnostic activation and resolution.</summary>
public sealed class SystemDiagnosticTransition
{
    internal SystemDiagnosticTransition(
        long generation,
        DateTimeOffset observedAtUtc,
        IEnumerable<string> addedCodes,
        IEnumerable<string> resolvedCodes)
    {
        Generation = generation;
        ObservedAtUtc = observedAtUtc;
        AddedCodes = Array.AsReadOnly([.. addedCodes.Order(StringComparer.Ordinal)]);
        ResolvedCodes = Array.AsReadOnly([.. resolvedCodes.Order(StringComparer.Ordinal)]);
    }

    /// <summary>System Information generation that observed the change.</summary>
    public long Generation { get; }

    /// <summary>UTC observation time.</summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary>Diagnostic codes activated by this observation.</summary>
    public IReadOnlyList<string> AddedCodes { get; }

    /// <summary>Diagnostic codes resolved by this observation.</summary>
    public IReadOnlyList<string> ResolvedCodes { get; }
}

/// <summary>Versioned privacy-filtered payload supplied to the host exporter.</summary>
public sealed class SystemDiagnosticsBundle
{
    /// <summary>Current JSON contract identity.</summary>
    public const string CurrentSchemaVersion = "system-diagnostics-v1";

    internal SystemDiagnosticsBundle(
        SystemInformationSnapshot current,
        IEnumerable<SystemDiagnosticTransition> transitions)
    {
        SchemaVersion = CurrentSchemaVersion;
        Current = current;
        Transitions = Array.AsReadOnly([.. transitions]);
    }

    /// <summary>Stable schema version.</summary>
    public string SchemaVersion { get; }

    /// <summary>Latest immutable System Information snapshot.</summary>
    public SystemInformationSnapshot Current { get; }

    /// <summary>Bounded current-session activation/resolution history.</summary>
    public IReadOnlyList<SystemDiagnosticTransition> Transitions { get; }
}
