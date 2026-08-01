using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>One exact processor/tool binding referenced by a compiled composition.</summary>
public sealed record ExternalProcessorDependencyReference
{
    /// <summary>Creates one reference without granting any processor authority.</summary>
    public ExternalProcessorDependencyReference(string processorId, string toolBindingId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        ProcessorId = processorId;
        ToolBindingId = toolBindingId;
    }

    /// <summary>Profile-selected processor identity.</summary>
    public string ProcessorId { get; }

    /// <summary>Profile-selected trusted tool binding identity.</summary>
    public string ToolBindingId { get; }
}

/// <summary>Refresh request bound to one exact capability publication.</summary>
public sealed class RuntimeDependencyReadinessRequest
{
    private readonly ExternalProcessorDependencyReference[] _dependencies;

    /// <summary>Creates one immutable request over compiled dependency references.</summary>
    public RuntimeDependencyReadinessRequest(
        string routeId,
        string capabilityFingerprint,
        string compilationFingerprint,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        IEnumerable<ExternalProcessorDependencyReference> dependencies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilationFingerprint);
        ArgumentNullException.ThrowIfNull(dependencies);
        if (!CapabilityRouteIdentity.IsSha256(capabilityFingerprint))
        {
            throw new ArgumentException(
                "Runtime dependency readiness requires a capability SHA-256 fingerprint.",
                nameof(capabilityFingerprint));
        }

        if (!CapabilityRouteIdentity.IsSha256(compilationFingerprint))
        {
            throw new ArgumentException(
                "Runtime dependency readiness requires a compilation SHA-256 fingerprint.",
                nameof(compilationFingerprint));
        }

        if (string.IsNullOrWhiteSpace(resolutionToken.Value))
        {
            throw new ArgumentException(
                "Runtime dependency readiness requires a non-empty resolution token.",
                nameof(resolutionToken));
        }

        ExternalProcessorDependencyReference[] dependencySnapshot = [.. dependencies];
        if (dependencySnapshot.Any(static dependency => dependency is null))
        {
            throw new ArgumentException(
                "Runtime dependency references must not contain null entries.",
                nameof(dependencies));
        }

        _dependencies =
        [
            .. dependencySnapshot
                .DistinctBy(
                    static dependency => (dependency.ProcessorId, dependency.ToolBindingId))
                .OrderBy(static dependency => dependency.ProcessorId, StringComparer.Ordinal)
                .ThenBy(static dependency => dependency.ToolBindingId, StringComparer.Ordinal),
        ];

        RouteId = routeId;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = compilationFingerprint;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        Dependencies = Array.AsReadOnly(_dependencies);
    }

    /// <summary>Stable exact-route identity.</summary>
    public string RouteId { get; }

    /// <summary>Reviewed capability-definition fingerprint.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact compiled-composition fingerprint.</summary>
    public string CompilationFingerprint { get; }

    /// <summary>Catalog publication identity.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Authoring-input revision evaluated by this request.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Exact compiled processor dependencies in deterministic order.</summary>
    public IReadOnlyList<ExternalProcessorDependencyReference> Dependencies { get; }

    /// <summary>Derives requirements only from external-processor operations in the compiled plan.</summary>
    public static RuntimeDependencyReadinessRequest FromResolvedCapability(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return new RuntimeDependencyReadinessRequest(
            capability.Identity.RouteId,
            capability.CapabilityFingerprint,
            capability.CompiledComposition.CompilationFingerprint,
            capability.ResolutionToken,
            authoringRevision,
            capability.CompiledComposition.Plan.OrderedOperations
                .Select(static operation => operation.ExternalProcessorInvocation)
                .Where(static invocation => invocation is not null)
                .Select(static invocation => new ExternalProcessorDependencyReference(
                    invocation!.ProcessorId,
                    invocation.ToolBindingId)));
    }

}

/// <summary>One refresh-time environment result for a compiled dependency.</summary>
public sealed record RuntimeDependencyEntry
{
    /// <summary>Creates one typed dependency result.</summary>
    public RuntimeDependencyEntry(
        string processorId,
        string toolBindingId,
        ResolvedChildReadiness readiness,
        string? issueCode = null,
        string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        if (!Enum.IsDefined(readiness))
        {
            throw new ArgumentOutOfRangeException(
                nameof(readiness),
                readiness,
                "Runtime dependency readiness must use the closed child vocabulary.");
        }

        bool hasIssue = !string.IsNullOrWhiteSpace(issueCode) &&
            !string.IsNullOrWhiteSpace(message);
        if ((issueCode is null) != (message is null) ||
            (readiness == ResolvedChildReadiness.Blocked && !hasIssue) ||
            (readiness != ResolvedChildReadiness.Blocked &&
             (issueCode is not null || message is not null)))
        {
            throw new ArgumentException(
                "Blocked runtime dependencies require one complete issue; other states cannot carry one.");
        }

        ProcessorId = processorId;
        ToolBindingId = toolBindingId;
        Readiness = readiness;
        IssueCode = issueCode;
        Message = message;
    }

    /// <summary>Profile-selected processor identity.</summary>
    public string ProcessorId { get; }

    /// <summary>Profile-selected tool binding identity.</summary>
    public string ToolBindingId { get; }

    /// <summary>Current machine result using the shared child vocabulary.</summary>
    public ResolvedChildReadiness Readiness { get; }

    /// <summary>Stable issue code when blocked.</summary>
    public string? IssueCode { get; }

    /// <summary>Operator-safe dependency detail when blocked.</summary>
    public string? Message { get; }

    /// <summary>Creates a ready dependency result.</summary>
    public static RuntimeDependencyEntry Ready(string processorId, string toolBindingId)
    {
        return new RuntimeDependencyEntry(
            processorId,
            toolBindingId,
            ResolvedChildReadiness.Ready);
    }

    /// <summary>Creates a blocked dependency result.</summary>
    public static RuntimeDependencyEntry Blocked(
        string processorId,
        string toolBindingId,
        string issueCode,
        string message)
    {
        return new RuntimeDependencyEntry(
            processorId,
            toolBindingId,
            ResolvedChildReadiness.Blocked,
            issueCode,
            message);
    }
}

/// <summary>Immutable refresh generation for one exact capability publication.</summary>
public sealed class RuntimeDependencyReadinessSnapshot
{
    private readonly RuntimeDependencyEntry[] _entries;

    /// <summary>Creates one deterministic environment snapshot.</summary>
    public RuntimeDependencyReadinessSnapshot(
        string routeId,
        string capabilityFingerprint,
        string compilationFingerprint,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        long generation,
        DateTimeOffset checkedAtUtc,
        IEnumerable<RuntimeDependencyEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilationFingerprint);
        ArgumentOutOfRangeException.ThrowIfLessThan(generation, 1);
        ArgumentNullException.ThrowIfNull(entries);
        if (!CapabilityRouteIdentity.IsSha256(capabilityFingerprint))
        {
            throw new ArgumentException(
                "Runtime dependency snapshots require a capability SHA-256 fingerprint.",
                nameof(capabilityFingerprint));
        }

        if (!CapabilityRouteIdentity.IsSha256(compilationFingerprint))
        {
            throw new ArgumentException(
                "Runtime dependency snapshots require a compilation SHA-256 fingerprint.",
                nameof(compilationFingerprint));
        }

        if (checkedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Runtime dependency timestamps must be normalized to UTC.",
                nameof(checkedAtUtc));
        }

        if (string.IsNullOrWhiteSpace(resolutionToken.Value))
        {
            throw new ArgumentException(
                "Runtime dependency snapshots require a non-empty resolution token.",
                nameof(resolutionToken));
        }

        RuntimeDependencyEntry[] entrySnapshot = [.. entries];
        if (entrySnapshot.Any(static entry => entry is null))
        {
            throw new ArgumentException(
                "Runtime dependency entries must be non-null and unique.",
                nameof(entries));
        }

        _entries =
        [
            .. entrySnapshot
                .OrderBy(static entry => entry.ProcessorId, StringComparer.Ordinal)
                .ThenBy(static entry => entry.ToolBindingId, StringComparer.Ordinal),
        ];
        if (_entries.Select(static entry => (entry.ProcessorId, entry.ToolBindingId))
                .Distinct().Count() != _entries.Length)
        {
            throw new ArgumentException(
                "Runtime dependency entries must be non-null and unique.",
                nameof(entries));
        }

        RouteId = routeId;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = compilationFingerprint;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        Generation = generation;
        CheckedAtUtc = checkedAtUtc;
        Entries = Array.AsReadOnly(_entries);
    }

    /// <summary>Stable exact-route identity.</summary>
    public string RouteId { get; }

    /// <summary>Reviewed capability-definition fingerprint evaluated by this refresh.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact compiled-composition fingerprint evaluated by this refresh.</summary>
    public string CompilationFingerprint { get; }

    /// <summary>Catalog publication evaluated by this refresh.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Authoring-input revision evaluated by this refresh.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Monotonic refresh generation owned by the environment adapter.</summary>
    public long Generation { get; }

    /// <summary>Injected UTC observation time.</summary>
    public DateTimeOffset CheckedAtUtc { get; }

    /// <summary>Per-dependency results in deterministic order.</summary>
    public IReadOnlyList<RuntimeDependencyEntry> Entries { get; }

    /// <summary>True when every declared dependency is ready or not applicable.</summary>
    public bool IsReady => Entries.All(static entry =>
        entry.Readiness is ResolvedChildReadiness.Ready or
            ResolvedChildReadiness.NotApplicable);

    internal bool Matches(
        CapabilityAdmissionSnapshot admission,
        long currentGeneration)
    {
        return StringComparer.Ordinal.Equals(RouteId, admission.RouteId) &&
            StringComparer.Ordinal.Equals(
                CapabilityFingerprint,
                admission.CapabilityFingerprint) &&
            StringComparer.Ordinal.Equals(
                CompilationFingerprint,
                admission.CompilationFingerprint) &&
            ResolutionToken == admission.ResolutionToken &&
            AuthoringRevision == admission.AuthoringRevision &&
            Generation == currentGeneration;
    }
}
