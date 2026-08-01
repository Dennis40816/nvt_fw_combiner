using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Stable issue returned by catalog loading, publication, or resolution.</summary>
public sealed record CapabilityCatalogIssue(
    string Code,
    string Message,
    string? Subject = null);

/// <summary>Stable canonical capability issue codes shared by UI and CLI adapters.</summary>
public static class CapabilityCatalogIssueCodes
{
    /// <summary>The trusted source could not be read.</summary>
    public const string SourceUnavailable = "capability.catalog.source-unavailable";

    /// <summary>The trusted source failed hash/schema/contract validation.</summary>
    public const string SourceInvalid = "capability.catalog.source-invalid";

    /// <summary>The loaded candidate failed Application materialization.</summary>
    public const string InvalidCandidate = "capability.catalog.invalid-candidate";

    /// <summary>No valid catalog snapshot exists in the current process.</summary>
    public const string CatalogUnavailable = "capability.catalog.unavailable";

    /// <summary>The requested exact route is absent from the current snapshot.</summary>
    public const string RouteUnavailable = "capability.route.unavailable";

    /// <summary>Selection axes do not identify one exact map variant.</summary>
    public const string RouteAmbiguous = "capability.route.ambiguous";

    /// <summary>The exact route is intentionally unavailable for authoring.</summary>
    public const string AuthoringUnavailable = "capability.authoring.unavailable";

    /// <summary>The compiler has not admitted the exact route for runtime execution.</summary>
    public const string ExecutionUnavailable = "capability.execution.unavailable";

    /// <summary>A support claim has no approved evidence declaration.</summary>
    public const string SupportedWithoutEvidence =
        "capability.certification.supported-without-evidence";
}

/// <summary>Port that loads and compiles a complete candidate without publishing it.</summary>
public interface ICanonicalCapabilityCatalogSource
{
    /// <summary>Loads one complete candidate or typed source issues.</summary>
    CapabilityCatalogLoadResult Load(CancellationToken cancellationToken);
}

/// <summary>Typed source result consumed by the Application catalog.</summary>
public sealed record CapabilityCatalogLoadResult
{
    private CapabilityCatalogLoadResult(
        CanonicalCapabilityCatalogCandidate? candidate,
        IReadOnlyList<CapabilityCatalogIssue> issues)
    {
        Candidate = candidate;
        Issues = issues;
    }

    /// <summary>Loaded candidate when the source completed successfully.</summary>
    public CanonicalCapabilityCatalogCandidate? Candidate { get; }

    /// <summary>Typed source issues when no candidate is available.</summary>
    public IReadOnlyList<CapabilityCatalogIssue> Issues { get; }

    /// <summary>True only when one complete candidate exists.</summary>
    public bool Succeeded => Candidate is not null;

    /// <summary>Creates one successful source result.</summary>
    public static CapabilityCatalogLoadResult Success(
        CanonicalCapabilityCatalogCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new CapabilityCatalogLoadResult(candidate, []);
    }

    /// <summary>Creates one failed source result.</summary>
    public static CapabilityCatalogLoadResult Failure(
        params CapabilityCatalogIssue[] issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return issues.Length == 0 || issues.Any(static issue => issue is null)
            ? throw new ArgumentException(
                "A failed catalog load requires at least one non-null issue.",
                nameof(issues))
            : new CapabilityCatalogLoadResult(null, Array.AsReadOnly([.. issues]));
    }
}

/// <summary>Outcome of an explicit catalog reload and atomic publication attempt.</summary>
public sealed record CapabilityCatalogReloadResult(
    bool Succeeded,
    bool RetainedLastKnownGood,
    CanonicalCapabilityCatalogSnapshot? Snapshot,
    IReadOnlyList<CapabilityCatalogIssue> Issues);

/// <summary>Result of resolving one exact route for authoring and execution.</summary>
public sealed record CapabilityResolutionResult(
    ResolvedCapability? Capability,
    CapabilityCatalogIssue? Issue)
{
    /// <summary>True only when the exact route is authoring- and execution-admitted.</summary>
    public bool Succeeded => Capability is not null && Issue is null;
}

/// <summary>
/// Application-owned catalog session. Reload validates a complete candidate
/// before one atomic publication and otherwise retains the last-known-good snapshot.
/// </summary>
public sealed class CanonicalCapabilityCatalog
{
    private readonly Lock _reloadLock = new();
    private readonly ICanonicalCapabilityCatalogSource _source;
    private CanonicalCapabilityCatalogSnapshot? _current;
    private long _publicationGeneration;

    /// <summary>Creates one catalog session over an injected trusted source.</summary>
    public CanonicalCapabilityCatalog(ICanonicalCapabilityCatalogSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Current immutable snapshot, or null before the first successful load.</summary>
    public CanonicalCapabilityCatalogSnapshot? CurrentSnapshot =>
        Volatile.Read(ref _current);

    /// <summary>Explicitly loads, validates, and atomically publishes one candidate.</summary>
    public CapabilityCatalogReloadResult Reload(
        CancellationToken cancellationToken = default)
    {
        lock (_reloadLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapabilityCatalogLoadResult loaded = _source.Load(cancellationToken);
            if (!loaded.Succeeded)
            {
                return Failed(loaded.Issues);
            }

            try
            {
                CanonicalCapabilityCatalogCandidate candidate = loaded.Candidate!;
                ValidateCandidate(candidate);
                long generation = checked(++_publicationGeneration);
                var token = new ResolutionToken(
                    FormattableString.Invariant(
                        $"{candidate.CatalogId}:{candidate.CatalogVersion}:{generation}:{candidate.SourceSha256[..12]}"));
                var snapshot = new CanonicalCapabilityCatalogSnapshot(
                    candidate,
                    token);
                Volatile.Write(ref _current, snapshot);
                return new CapabilityCatalogReloadResult(
                    Succeeded: true,
                    RetainedLastKnownGood: false,
                    snapshot,
                    []);
            }
            catch (ArgumentException exception)
            {
                return Failed(
                [
                    new CapabilityCatalogIssue(
                        CapabilityCatalogIssueCodes.InvalidCandidate,
                        exception.Message),
                ]);
            }
        }
    }

    /// <summary>Resolves one exact route through the current immutable snapshot.</summary>
    public CapabilityResolutionResult Resolve(string routeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        CanonicalCapabilityCatalogSnapshot? snapshot = CurrentSnapshot;
        return snapshot is null
            ? Failure(
                CapabilityCatalogIssueCodes.CatalogUnavailable,
                "No valid canonical capability catalog is loaded.")
            : Resolve(snapshot, routeId);
    }

    /// <summary>Resolves a policy-bound dynamic definition before compiling current authoring state.</summary>
    public CapabilityRouteResolutionResult ResolveDynamicRoute(string routeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        CanonicalCapabilityCatalogSnapshot? snapshot = CurrentSnapshot;
        return snapshot is null
            ? DynamicFailure(
                CapabilityCatalogIssueCodes.CatalogUnavailable,
                "No valid canonical capability catalog is loaded.")
            : !snapshot.TryGetDynamic(routeId, out ResolvedCapabilityRoute? route)
            ? DynamicFailure(
                CapabilityCatalogIssueCodes.RouteUnavailable,
                "The requested dynamic route is not present in the current catalog.",
                routeId)
            : route!.Authoring.Value == CapabilityAuthoringAvailability.Unavailable
            ? DynamicFailure(
                CapabilityCatalogIssueCodes.AuthoringUnavailable,
                "The requested dynamic route is unavailable for authoring.",
                routeId)
            : new CapabilityRouteResolutionResult(route, null);
    }

    /// <summary>
    /// Resolves the sole published map variant for selection axes that do not
    /// restate a firmware map fact.
    /// </summary>
    public CapabilityResolutionResult ResolveUniqueRoute(
        string icId,
        string workflowId,
        string icCountVariant)
    {
        return ResolveUniqueRoute(
            icId,
            workflowId,
            icCountVariant,
            outputCapacity: null);
    }

    /// <summary>
    /// Resolves the sole published map whose compiled output capacity matches
    /// an already observed container length.
    /// </summary>
    public CapabilityResolutionResult ResolveUniqueRoute(
        string icId,
        string workflowId,
        string icCountVariant,
        long? outputCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icCountVariant);
        CanonicalCapabilityCatalogSnapshot? snapshot = CurrentSnapshot;
        if (snapshot is null)
        {
            return Failure(
                CapabilityCatalogIssueCodes.CatalogUnavailable,
                "No valid canonical capability catalog is loaded.");
        }

        ResolvedCapability[] matches =
        [
            .. snapshot.Capabilities.Where(capability =>
                StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    workflowId) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.IcCountVariant,
                    icCountVariant) &&
                (outputCapacity is null ||
                 capability.CompiledComposition.Plan.OutputInitialization.Capacity ==
                 outputCapacity.Value)),
        ];
        return matches.Length switch
        {
            0 => Failure(
                CapabilityCatalogIssueCodes.RouteUnavailable,
                "The requested selection is not present in the current catalog."),
            > 1 => Failure(
                CapabilityCatalogIssueCodes.RouteAmbiguous,
                "The requested selection resolves to more than one map variant."),
            _ => Resolve(snapshot, matches[0].Identity.RouteId),
        };
    }

    /// <summary>Resolves the sole published map admitted by an exact topology selection.</summary>
    public CapabilityResolutionResult ResolveUniqueTopologyRoute(
        string icId,
        string workflowId,
        TopologySelection? topology)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        CanonicalCapabilityCatalogSnapshot? snapshot = CurrentSnapshot;
        if (snapshot is null)
        {
            return Failure(
                CapabilityCatalogIssueCodes.CatalogUnavailable,
                "No valid canonical capability catalog is loaded.");
        }

        ResolvedCapability[] matches =
        [
            .. snapshot.Capabilities.Where(capability =>
                StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    workflowId) &&
                capability.CompiledComposition.V2Details?.Provenance.ResolvedMap
                    .ImageMap.Applicability.TopologyRequirement.Matches(topology) == true),
        ];
        return matches.Length switch
        {
            0 => Failure(
                CapabilityCatalogIssueCodes.RouteUnavailable,
                "The requested topology is not present in the current catalog."),
            > 1 => Failure(
                CapabilityCatalogIssueCodes.RouteAmbiguous,
                "The requested topology resolves to more than one map variant."),
            _ => Resolve(snapshot, matches[0].Identity.RouteId),
        };
    }

    private static CapabilityResolutionResult Resolve(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string routeId)
    {
        return !snapshot.TryGet(routeId, out ResolvedCapability? capability)
            ? Failure(
                CapabilityCatalogIssueCodes.RouteUnavailable,
                "The requested exact route is not present in the current catalog.",
                routeId)
            : capability!.Authoring.Value == CapabilityAuthoringAvailability.Unavailable
            ? Failure(
                CapabilityCatalogIssueCodes.AuthoringUnavailable,
                "The requested exact route is unavailable for authoring.",
                routeId)
            : !capability.ExecutionAdmitted
            ? Failure(
                CapabilityCatalogIssueCodes.ExecutionUnavailable,
                "The requested exact route is not compiler-admitted for execution.",
                routeId)
            : new CapabilityResolutionResult(capability, null);
    }

    private static void ValidateCandidate(
        CanonicalCapabilityCatalogCandidate candidate)
    {
        if (candidate.Definitions.Count == 0)
        {
            if (candidate.DynamicDefinitions.Count == 0)
            {
                throw new ArgumentException(
                    "A canonical capability candidate must contain at least one exact route.",
                    nameof(candidate));
            }
        }

        if (candidate.Definitions.Any(static definition => definition is null) ||
            candidate.DynamicDefinitions.Any(static definition => definition is null) ||
            candidate.Definitions.Select(static definition => definition.Identity.RouteId)
                .Concat(candidate.DynamicDefinitions.Select(
                    static definition => definition.Identity.RouteId))
                .Distinct(StringComparer.Ordinal).Count() !=
                    candidate.Definitions.Count + candidate.DynamicDefinitions.Count)
        {
            throw new ArgumentException(
                "Canonical capability routes must be non-null and unique.",
                nameof(candidate));
        }
    }

    private CapabilityCatalogReloadResult Failed(
        IReadOnlyList<CapabilityCatalogIssue> issues)
    {
        CanonicalCapabilityCatalogSnapshot? current = CurrentSnapshot;
        return new CapabilityCatalogReloadResult(
            Succeeded: false,
            RetainedLastKnownGood: current is not null,
            current,
            Array.AsReadOnly([.. issues]));
    }

    private static CapabilityResolutionResult Failure(
        string code,
        string message,
        string? subject = null)
    {
        return new CapabilityResolutionResult(
            null,
            new CapabilityCatalogIssue(code, message, subject));
    }

    private static CapabilityRouteResolutionResult DynamicFailure(
        string code,
        string message,
        string? subject = null)
    {
        return new CapabilityRouteResolutionResult(
            null,
            new CapabilityCatalogIssue(code, message, subject));
    }
}
