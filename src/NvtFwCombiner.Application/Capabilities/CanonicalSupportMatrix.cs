namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Lifecycle of the catalog publication represented by a Support Matrix query.</summary>
public enum CanonicalSupportMatrixCatalogState
{
    /// <summary>The first complete catalog publication is still being prepared.</summary>
    Loading,

    /// <summary>The matrix represents the current successfully published catalog.</summary>
    Current,

    /// <summary>A failed reload retained the prior immutable publication.</summary>
    LastKnownGood,

    /// <summary>No valid catalog publication exists after the initial load attempt.</summary>
    ColdStartBlocked,
}

/// <summary>Execution admission represented without compiling a dynamic route for display.</summary>
public enum CanonicalSupportMatrixExecutionState
{
    /// <summary>The published route already owns one compiler-admitted composition.</summary>
    Admitted,

    /// <summary>The reviewed route compiles one exact composition from current authoring state.</summary>
    RequiresAuthoringCompilation,

    /// <summary>The published fixed route is not compiler-admitted.</summary>
    Unavailable,
}

/// <summary>Independent typed reason that prevents authoring, execution, or certification.</summary>
public enum CanonicalSupportMatrixBlockerKind
{
    /// <summary>Exact-route authoring policy intentionally excludes the route.</summary>
    AuthoringUnavailable,

    /// <summary>The compiler did not admit a fixed route for execution.</summary>
    ExecutionUnavailable,

    /// <summary>Independent publication and evidence facts conflict for certification.</summary>
    CertificationInconsistency,
}

/// <summary>One typed blocker with provenance from the canonical decision that produced it.</summary>
public sealed record CanonicalSupportMatrixBlocker(
    CanonicalSupportMatrixBlockerKind Kind,
    string Code,
    string SourceReference);

/// <summary>One read-only exact-route projection from a canonical catalog publication.</summary>
public sealed record CanonicalSupportMatrixRow
{
    /// <summary>Creates one route row without copying firmware definitions.</summary>
    public CanonicalSupportMatrixRow(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        CanonicalSupportMatrixExecutionState executionState,
        IEnumerable<CanonicalSupportMatrixBlocker> blockers)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        ArgumentNullException.ThrowIfNull(authoring);
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(blockers);

        Identity = identity;
        CapabilityFingerprint = capabilityFingerprint;
        Authoring = authoring;
        Publication = publication;
        Evidence = evidence;
        ExecutionState = executionState;
        Blockers = Array.AsReadOnly([.. blockers]);
    }

    /// <summary>Stable logical route axes.</summary>
    public CapabilityRouteIdentity Identity { get; }

    /// <summary>Reviewed complete capability-definition fingerprint.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact fingerprint-pinned authoring policy.</summary>
    public PinnedCapabilityDecision<CapabilityAuthoringAvailability> Authoring { get; }

    /// <summary>Independent fingerprint-pinned publication decision.</summary>
    public PinnedCapabilityDecision<CapabilityPublicationStatus> Publication { get; }

    /// <summary>Independent fingerprint-pinned evidence declaration.</summary>
    public PinnedCapabilityDecision<CapabilityEvidenceStatus> Evidence { get; }

    /// <summary>Compiler-admission state without per-run compilation.</summary>
    public CanonicalSupportMatrixExecutionState ExecutionState { get; }

    /// <summary>Owned typed blockers in stable priority order.</summary>
    public IReadOnlyList<CanonicalSupportMatrixBlocker> Blockers { get; }
}

/// <summary>Immutable enumerated reporting projection over one canonical publication.</summary>
public sealed record CanonicalSupportMatrixSnapshot
{
    /// <summary>Creates one owned matrix snapshot.</summary>
    public CanonicalSupportMatrixSnapshot(
        string catalogId,
        string catalogVersion,
        string sourceSha256,
        ResolutionToken resolutionToken,
        IEnumerable<CanonicalSupportMatrixRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSha256);
        ArgumentNullException.ThrowIfNull(rows);
        resolutionToken.EnsureValid(nameof(resolutionToken));

        CatalogId = catalogId;
        CatalogVersion = catalogVersion;
        SourceSha256 = sourceSha256;
        ResolutionToken = resolutionToken;
        Rows = Array.AsReadOnly([.. rows]);
    }

    /// <summary>Stable catalog id.</summary>
    public string CatalogId { get; }

    /// <summary>Published catalog version.</summary>
    public string CatalogVersion { get; }

    /// <summary>Hash of the exact trusted source catalog.</summary>
    public string SourceSha256 { get; }

    /// <summary>Opaque identity for this exact in-process publication.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Exact routes in stable route-id order.</summary>
    public IReadOnlyList<CanonicalSupportMatrixRow> Rows { get; }
}

/// <summary>One query result that keeps catalog lifecycle separate from route facts.</summary>
public sealed record CanonicalSupportMatrixQueryResult
{
    /// <summary>Creates one immutable query result.</summary>
    public CanonicalSupportMatrixQueryResult(
        CanonicalSupportMatrixCatalogState state,
        CanonicalSupportMatrixSnapshot? matrix,
        IEnumerable<CapabilityCatalogIssue>? reloadIssues = null)
    {
        State = state;
        Matrix = matrix;
        ReloadIssues = Array.AsReadOnly([.. reloadIssues ?? []]);
    }

    /// <summary>Catalog lifecycle represented by this result.</summary>
    public CanonicalSupportMatrixCatalogState State { get; }

    /// <summary>Current or last-known-good immutable matrix, when one exists.</summary>
    public CanonicalSupportMatrixSnapshot? Matrix { get; }

    /// <summary>Typed issues from the latest failed catalog load or reload.</summary>
    public IReadOnlyList<CapabilityCatalogIssue> ReloadIssues { get; }

    /// <summary>True when a failed reload retained an older coherent publication.</summary>
    public bool IsStale => State == CanonicalSupportMatrixCatalogState.LastKnownGood;

    /// <summary>True when a coherent publication contains no exact routes.</summary>
    public bool IsEmpty => Matrix is { Rows.Count: 0 };

    /// <summary>Creates the transient first-load state without fabricating route facts.</summary>
    public static CanonicalSupportMatrixQueryResult Loading()
    {
        return new CanonicalSupportMatrixQueryResult(
            CanonicalSupportMatrixCatalogState.Loading,
            matrix: null);
    }
}

/// <summary>Focused Application query consumed by Settings and later support surfaces.</summary>
public interface ICanonicalSupportMatrixQuery
{
    /// <summary>Returns one immutable matrix/lifecycle result.</summary>
    CanonicalSupportMatrixQueryResult Query();
}

/// <summary>Projects only the current canonical catalog reload result.</summary>
public sealed class CanonicalSupportMatrixQuery(
    Func<CapabilityCatalogReloadResult?> reloadResultProvider) :
    ICanonicalSupportMatrixQuery
{
    private readonly Func<CapabilityCatalogReloadResult?> _reloadResultProvider =
        reloadResultProvider ?? throw new ArgumentNullException(nameof(reloadResultProvider));

    /// <inheritdoc />
    public CanonicalSupportMatrixQueryResult Query()
    {
        CapabilityCatalogReloadResult? reload = _reloadResultProvider();
        return reload is null
            ? CanonicalSupportMatrixQueryResult.Loading()
            : Project(reload);
    }

    /// <summary>Projects one catalog reload result without reading another catalog.</summary>
    public static CanonicalSupportMatrixQueryResult Project(
        CapabilityCatalogReloadResult reload)
    {
        ArgumentNullException.ThrowIfNull(reload);
        return reload.Snapshot is null
            ? new CanonicalSupportMatrixQueryResult(
                CanonicalSupportMatrixCatalogState.ColdStartBlocked,
                matrix: null,
                reload.Issues)
            : new CanonicalSupportMatrixQueryResult(
                reload.Succeeded
                    ? CanonicalSupportMatrixCatalogState.Current
                    : CanonicalSupportMatrixCatalogState.LastKnownGood,
                Project(reload.Snapshot),
                reload.Issues);
    }

    private static CanonicalSupportMatrixSnapshot Project(
        CanonicalCapabilityCatalogSnapshot snapshot)
    {
        ILookup<string?, CapabilityCatalogIssue> certificationByRoute =
            snapshot.CertificationIssues.ToLookup(
                static issue => issue.Subject,
                StringComparer.Ordinal);
        CanonicalSupportMatrixRow[] rows =
        [
            .. snapshot.Capabilities.Select(capability => Row(
                capability.Identity,
                capability.CapabilityFingerprint,
                capability.Authoring,
                capability.Publication,
                capability.Evidence,
                capability.ExecutionAdmitted
                    ? CanonicalSupportMatrixExecutionState.Admitted
                    : CanonicalSupportMatrixExecutionState.Unavailable,
                certificationByRoute[capability.Identity.RouteId])),
            .. snapshot.DynamicRoutes.Select(route => Row(
                route.Identity,
                route.CapabilityFingerprint,
                route.Authoring,
                route.Publication,
                route.Evidence,
                CanonicalSupportMatrixExecutionState.RequiresAuthoringCompilation,
                certificationByRoute[route.Identity.RouteId])),
        ];
        return new CanonicalSupportMatrixSnapshot(
            snapshot.CatalogId,
            snapshot.CatalogVersion,
            snapshot.SourceSha256,
            snapshot.ResolutionToken,
            rows.OrderBy(static row => row.Identity.RouteId, StringComparer.Ordinal));
    }

    private static CanonicalSupportMatrixRow Row(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        CanonicalSupportMatrixExecutionState executionState,
        IEnumerable<CapabilityCatalogIssue> certificationIssues)
    {
        var blockers = new List<CanonicalSupportMatrixBlocker>();
        if (authoring.Value == CapabilityAuthoringAvailability.Unavailable)
        {
            blockers.Add(new CanonicalSupportMatrixBlocker(
                CanonicalSupportMatrixBlockerKind.AuthoringUnavailable,
                CapabilityCatalogIssueCodes.AuthoringUnavailable,
                authoring.SourceReference));
        }

        if (executionState == CanonicalSupportMatrixExecutionState.Unavailable)
        {
            blockers.Add(new CanonicalSupportMatrixBlocker(
                CanonicalSupportMatrixBlockerKind.ExecutionUnavailable,
                CapabilityCatalogIssueCodes.ExecutionUnavailable,
                $"{identity.RouteId}:{capabilityFingerprint}"));
        }

        blockers.AddRange(certificationIssues.Select(issue =>
            new CanonicalSupportMatrixBlocker(
                CanonicalSupportMatrixBlockerKind.CertificationInconsistency,
                issue.Code,
                evidence.SourceReference)));
        return new CanonicalSupportMatrixRow(
            identity,
            capabilityFingerprint,
            authoring,
            publication,
            evidence,
            executionState,
            blockers);
    }
}
