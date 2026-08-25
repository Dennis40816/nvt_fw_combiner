using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Application.Metadata;
using System.Threading.Channels;

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

    internal CapabilityCatalogLoadResult Load(
        ChannelWriter<CanonicalCapabilityCatalogLoadUpdate>? progress,
        CancellationToken cancellationToken)
    {
        return Load(cancellationToken);
    }
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
/// Read-only metadata-plan result that carries no authoring or execution
/// capability.
/// </summary>
public sealed record MetadataPlanResolutionResult(
    ResolvedMetadataPlan? MetadataPlan,
    CapabilityCatalogIssue? Issue)
{
    /// <summary>True only when one exact publication-bound metadata plan was selected.</summary>
    public bool Succeeded => MetadataPlan is not null && Issue is null;
}

/// <summary>
/// Application-owned catalog session. Reload validates a complete candidate
/// before one atomic publication and otherwise retains the last-known-good snapshot.
/// </summary>
public sealed class CanonicalCapabilityCatalog :
    ICanonicalCapabilityCatalogReloader,
    ICanonicalCapabilityCatalogLoader,
    ICanonicalCapabilityQuery,
    ICanonicalSupportMatrixQuery
{
    private readonly Lock _reloadLock = new();
    private readonly ICanonicalCapabilityCatalogSource _source;
    private CanonicalCapabilityCatalogSnapshot? _current;
    private CapabilityCatalogReloadResult? _latestReload;
    private long _publicationGeneration;

    /// <summary>Creates one catalog session over an injected trusted source.</summary>
    public CanonicalCapabilityCatalog(ICanonicalCapabilityCatalogSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Explicitly loads, validates, and atomically publishes one candidate.</summary>
    public CapabilityCatalogReloadResult Reload(
        CancellationToken cancellationToken = default)
    {
        return Load(initialOnly: false, progress: null, cancellationToken);
    }

    void ICanonicalCapabilityCatalogReloader.Reload(
        CancellationToken cancellationToken)
    {
        _ = Reload(cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var updates = Channel.CreateUnbounded<CanonicalCapabilityCatalogLoadUpdate>();
        _ = Task.Run(
            () => PublishLoadUpdates(updates.Writer, cancellationToken),
            CancellationToken.None);
        return updates.Reader.ReadAllAsync(CancellationToken.None);
    }

    /// <summary>Gets the current valid publication after one lazy load attempt.</summary>
    public CanonicalCapabilityCatalogSnapshot GetCurrentSnapshot()
    {
        return TryGetCurrentSnapshot() ??
            throw new InvalidOperationException(
                "Canonical capability publication is unavailable.");
    }

    /// <inheritdoc />
    public CanonicalCapabilityCatalogSnapshot? TryGetCurrentSnapshot()
    {
        _ = EnsureLoaded(CancellationToken.None);
        return Volatile.Read(ref _current);
    }

    /// <summary>Resolves one exact route through the current immutable snapshot.</summary>
    public CapabilityResolutionResult Resolve(string routeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        _ = EnsureLoaded(CancellationToken.None);
        CanonicalCapabilityCatalogSnapshot? snapshot = Volatile.Read(ref _current);
        return snapshot is null
            ? Failure(
                CapabilityCatalogIssueCodes.CatalogUnavailable,
                "No valid canonical capability catalog is loaded.")
            : Resolve(snapshot, routeId);
    }

    /// <inheritdoc />
    public CanonicalSupportMatrixQueryResult Query()
    {
        CapabilityCatalogReloadResult? reload = Volatile.Read(ref _latestReload);
        return reload is null
            ? CanonicalSupportMatrixQueryResult.Loading()
            : CanonicalSupportMatrixQuery.Project(reload);
    }

    private CapabilityCatalogReloadResult EnsureLoaded(
        CancellationToken cancellationToken)
    {
        return Volatile.Read(ref _latestReload) ??
            Load(initialOnly: true, progress: null, cancellationToken);
    }

    private CapabilityCatalogReloadResult Load(
        bool initialOnly,
        ChannelWriter<CanonicalCapabilityCatalogLoadUpdate>? progress,
        CancellationToken cancellationToken)
    {
        lock (_reloadLock)
        {
            if (initialOnly && Volatile.Read(ref _latestReload) is { } current)
            {
                return current;
            }

            cancellationToken.ThrowIfCancellationRequested();
            CapabilityCatalogLoadResult loaded =
                _source.Load(progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            CapabilityCatalogReloadResult result;
            if (!loaded.Succeeded)
            {
                result = Failed(loaded.Issues);
            }
            else
            {
                try
                {
                    CanonicalCapabilityCatalogCandidate candidate = loaded.Candidate!;
                    ValidateCandidate(candidate);
                    cancellationToken.ThrowIfCancellationRequested();
                    long generation = checked(_publicationGeneration + 1);
                    var token = new ResolutionToken(
                        FormattableString.Invariant(
                            $"{candidate.CatalogId}:{candidate.CatalogVersion}:{generation}:{candidate.SourceSha256[..12]}"));
                    var snapshot = new CanonicalCapabilityCatalogSnapshot(
                        candidate,
                        token);
                    cancellationToken.ThrowIfCancellationRequested();
                    _publicationGeneration = generation;
                    Volatile.Write(ref _current, snapshot);
                    result = new CapabilityCatalogReloadResult(
                        Succeeded: true,
                        RetainedLastKnownGood: false,
                        snapshot,
                        []);
                }
                catch (ArgumentException exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result = Failed(
                    [
                        new CapabilityCatalogIssue(
                            CapabilityCatalogIssueCodes.InvalidCandidate,
                            exception.Message),
                    ]);
                }
            }

            Volatile.Write(ref _latestReload, result);
            return result;
        }
    }

    private void PublishLoadUpdates(
        ChannelWriter<CanonicalCapabilityCatalogLoadUpdate> updates,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            CapabilityCatalogReloadResult result =
                Load(initialOnly: false, updates, cancellationToken);
            _ = updates.TryWrite(new(result.Succeeded ? 1 : null, result));
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        _ = updates.TryComplete(failure);
    }

    /// <summary>Resolves a policy-bound dynamic definition before compiling current authoring state.</summary>
    public CapabilityRouteResolutionResult ResolveDynamicRoute(string routeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        _ = EnsureLoaded(CancellationToken.None);
        CanonicalCapabilityCatalogSnapshot? snapshot = Volatile.Read(ref _current);
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
        _ = EnsureLoaded(CancellationToken.None);
        CanonicalCapabilityCatalogSnapshot? snapshot = Volatile.Read(ref _current);
        if (snapshot is null)
        {
            return Failure(
                CapabilityCatalogIssueCodes.CatalogUnavailable,
                "No valid canonical capability catalog is loaded.");
        }

        PublishedRouteSelection selection = SelectUniquePublishedRoute(
            snapshot,
            icId,
            workflowId,
            icCountVariant,
            outputCapacity);
        return selection.Capability is null
            ? new CapabilityResolutionResult(null, selection.Issue)
            : Resolve(snapshot, selection.Capability.Identity.RouteId);
    }

    /// <inheritdoc />
    public MetadataPlanResolutionResult ResolveUniqueMetadataPlan(
        string icId,
        string workflowId,
        string icCountVariant,
        long? outputCapacity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icCountVariant);
        _ = EnsureLoaded(CancellationToken.None);
        CanonicalCapabilityCatalogSnapshot? snapshot = Volatile.Read(ref _current);
        if (snapshot is null)
        {
            return new MetadataPlanResolutionResult(
                null,
                new CapabilityCatalogIssue(
                    CapabilityCatalogIssueCodes.CatalogUnavailable,
                    "No valid canonical capability catalog is loaded."));
        }

        PublishedRouteSelection selection = SelectUniquePublishedRoute(
            snapshot,
            icId,
            workflowId,
            icCountVariant,
            outputCapacity);
        return selection.Capability is null
            ? new MetadataPlanResolutionResult(null, selection.Issue)
            : new MetadataPlanResolutionResult(
                selection.Capability.MetadataPlan,
                null);
    }

    /// <summary>Resolves the sole published map admitted by an exact topology selection.</summary>
    public CapabilityResolutionResult ResolveUniqueTopologyRoute(
        string icId,
        string workflowId,
        TopologySelection? topology)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        _ = EnsureLoaded(CancellationToken.None);
        CanonicalCapabilityCatalogSnapshot? snapshot = Volatile.Read(ref _current);
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
                capability.CompiledComposition.V2Details.Provenance.ResolvedMap
                    .ImageMap.Applicability.TopologyRequirement.Matches(topology)),
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

    /// <inheritdoc />
    public bool HasAuthorableCapability(string icId, string workflowId)
    {
        if (string.IsNullOrWhiteSpace(icId) || string.IsNullOrWhiteSpace(workflowId))
        {
            return false;
        }

        _ = EnsureLoaded(CancellationToken.None);
        CanonicalCapabilityCatalogSnapshot? snapshot = Volatile.Read(ref _current);
        return snapshot is not null &&
            (snapshot.Capabilities.Any(capability =>
                    MatchesAuthorableRoute(
                        capability.Identity,
                        capability.Authoring,
                        icId,
                        workflowId) &&
                    capability.ExecutionAdmitted) ||
                snapshot.DynamicRoutes.Any(route =>
                    MatchesAuthorableRoute(
                        route.Identity,
                        route.Authoring,
                        icId,
                        workflowId)));
    }

    /// <inheritdoc />
    public ResolvedCapability? ResolveCurrentCompilation(
        CompiledComposition composition,
        ResolvedCapability? acceptedCapability = null)
    {
        ArgumentNullException.ThrowIfNull(composition);
        _ = EnsureLoaded(CancellationToken.None);
        CanonicalCapabilityCatalogSnapshot? snapshot = Volatile.Read(ref _current);
        if (snapshot is null || composition.CapabilityFingerprint is null)
        {
            return null;
        }

        if (acceptedCapability is not null)
        {
            if (!ReferenceEquals(acceptedCapability.CompiledComposition, composition) ||
                acceptedCapability.ResolutionToken != snapshot.ResolutionToken ||
                acceptedCapability.Authoring.Value != CapabilityAuthoringAvailability.Available)
            {
                return null;
            }

            CapabilityResolutionResult fixedResolution = Resolve(
                acceptedCapability.Identity.RouteId);
            if (fixedResolution.Succeeded)
            {
                return ReferenceEquals(
                        fixedResolution.Capability!.CompiledComposition,
                        composition) &&
                    fixedResolution.Capability.ResolutionToken ==
                        acceptedCapability.ResolutionToken
                            ? acceptedCapability
                            : null;
            }

            CapabilityRouteResolutionResult dynamicResolution = ResolveDynamicRoute(
                acceptedCapability.Identity.RouteId);
            return dynamicResolution.Succeeded &&
                dynamicResolution.Route!.ResolutionToken ==
                    acceptedCapability.ResolutionToken &&
                StringComparer.Ordinal.Equals(
                    dynamicResolution.Route.CapabilityFingerprint,
                    acceptedCapability.CapabilityFingerprint)
                        ? acceptedCapability
                        : null;
        }

        ResolvedCapability? fixedCapability = snapshot.Capabilities.SingleOrDefault(
            capability => ReferenceEquals(capability.CompiledComposition, composition));
        if (fixedCapability is null)
        {
            return null;
        }

        CapabilityResolutionResult current = Resolve(fixedCapability.Identity.RouteId);
        return current.Succeeded &&
            ReferenceEquals(current.Capability!.CompiledComposition, composition)
                ? current.Capability
                : null;
    }

    private static bool MatchesAuthorableRoute(
        CapabilityRouteIdentity identity,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        string icId,
        string workflowId)
    {
        return StringComparer.Ordinal.Equals(identity.IcId, icId) &&
            StringComparer.Ordinal.Equals(identity.WorkflowId, workflowId) &&
            authoring.Value == CapabilityAuthoringAvailability.Available;
    }

    private static PublishedRouteSelection SelectUniquePublishedRoute(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string icId,
        string workflowId,
        string icCountVariant,
        long? outputCapacity)
    {
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
            0 => new PublishedRouteSelection(
                null,
                new CapabilityCatalogIssue(
                    CapabilityCatalogIssueCodes.RouteUnavailable,
                    "The requested selection is not present in the current catalog.")),
            > 1 => new PublishedRouteSelection(
                null,
                new CapabilityCatalogIssue(
                    CapabilityCatalogIssueCodes.RouteAmbiguous,
                    "The requested selection resolves to more than one map variant.")),
            _ => new PublishedRouteSelection(matches[0], null),
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
        if (candidate.Definitions.Count == 0 &&
            candidate.DynamicDefinitions.Count == 0)
        {
            throw new ArgumentException(
                "A canonical capability candidate must contain at least one exact route.",
                nameof(candidate));
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
        CanonicalCapabilityCatalogSnapshot? current = Volatile.Read(ref _current);
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

    private sealed record PublishedRouteSelection(
        ResolvedCapability? Capability,
        CapabilityCatalogIssue? Issue);
}
