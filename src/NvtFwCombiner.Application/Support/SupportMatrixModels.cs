namespace NvtFwCombiner.Application.Support;

/// <summary>Owner-approved publication state, independent from authoring and execution.</summary>
public enum SupportPublicationStatus
{
    /// <summary>No exact owner publication decision exists.</summary>
    Unclassified,

    /// <summary>The owner has approved this route as supported.</summary>
    Supported,

    /// <summary>The owner has classified this route as a candidate.</summary>
    Candidate,

    /// <summary>The route is internal rather than publicly supported.</summary>
    Internal,

    /// <summary>The route is retained for test-only use.</summary>
    TestOnly,
}

/// <summary>Whether an exact route has an explicit authoring binding.</summary>
public enum SupportAuthoringAvailability
{
    /// <summary>The available source is too coarse to prove an exact result.</summary>
    Unknown,

    /// <summary>An exact source permits authoring this route.</summary>
    Available,

    /// <summary>An exact source excludes authoring this route.</summary>
    Unavailable,
}

/// <summary>Strongest declared evidence class for one exact route.</summary>
public enum SupportEvidenceStatus
{
    /// <summary>Direct route-specific golden evidence exists.</summary>
    DirectGolden,

    /// <summary>An explicitly scoped approved alias supplies evidence.</summary>
    ApprovedAlias,

    /// <summary>A route-specific synthetic oracle supplies evidence.</summary>
    SyntheticOracle,

    /// <summary>An admitted contract exists without stronger evidence.</summary>
    ContractOnly,

    /// <summary>No eligible evidence declaration exists.</summary>
    Missing,
}

/// <summary>Traceable owner provenance for one publication decision.</summary>
public sealed record SupportPublicationProvenance(
    string AuthorityKind,
    string RecordedOn,
    string RecordRef,
    string Rationale);

/// <summary>One exact publication decision keyed by a stable route id.</summary>
public sealed record SupportPublicationDecision
{
    /// <summary>Initializes an immutable decision and optional supersession history.</summary>
    public SupportPublicationDecision(
        string decisionId,
        string routeId,
        SupportPublicationStatus status,
        SupportPublicationProvenance provenance,
        IEnumerable<string>? supersedesDecisionIds = null)
    {
        DecisionId = decisionId;
        RouteId = routeId;
        Status = status;
        Provenance = provenance;
        SupersedesDecisionIds = SupportSnapshot.Copy(supersedesDecisionIds ?? []);
    }

    /// <summary>Stable owner decision identity.</summary>
    public string DecisionId { get; }

    /// <summary>Exact canonical route governed by the decision.</summary>
    public string RouteId { get; }

    /// <summary>Owner-approved publication status.</summary>
    public SupportPublicationStatus Status { get; }

    /// <summary>Traceable owner provenance.</summary>
    public SupportPublicationProvenance Provenance { get; }

    /// <summary>Prior decision ids intentionally superseded by this decision.</summary>
    public IReadOnlyList<string> SupersedesDecisionIds { get; }
}

/// <summary>Immutable policy identity and decisions supplied by Infrastructure.</summary>
public sealed record SupportPublicationPolicySnapshot
{
    /// <summary>Initializes one owned policy snapshot.</summary>
    public SupportPublicationPolicySnapshot(
        string policyId,
        string policyVersion,
        string sha256,
        IEnumerable<SupportPublicationDecision> decisions,
        string? supersedesPolicyVersion = null)
    {
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        Sha256 = sha256;
        Decisions = SupportSnapshot.Copy(decisions);
        SupersedesPolicyVersion = supersedesPolicyVersion;
    }

    /// <summary>Stable policy identity.</summary>
    public string PolicyId { get; }

    /// <summary>Loaded policy version.</summary>
    public string PolicyVersion { get; }

    /// <summary>Canonical SHA-256 of the loaded source bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Owned exact-route decisions.</summary>
    public IReadOnlyList<SupportPublicationDecision> Decisions { get; }

    /// <summary>Optional previous policy version superseded by this snapshot.</summary>
    public string? SupersedesPolicyVersion { get; }
}

/// <summary>One explicitly declared fact scope for evidence reuse.</summary>
public sealed record SupportEvidenceFactScope
{
    /// <summary>Initializes an owned fact scope.</summary>
    public SupportEvidenceFactScope(
        string factScopeId,
        string icId,
        string workflowId,
        IEnumerable<string> icCountVariants,
        IEnumerable<string> mapVariants)
    {
        FactScopeId = factScopeId;
        IcId = icId;
        WorkflowId = workflowId;
        IcCountVariants = SupportSnapshot.Copy(icCountVariants);
        MapVariants = SupportSnapshot.Copy(mapVariants);
    }

    /// <summary>Stable declared fact-scope identity.</summary>
    public string FactScopeId { get; }

    /// <summary>IC to which the scope applies.</summary>
    public string IcId { get; }

    /// <summary>Workflow to which the scope applies.</summary>
    public string WorkflowId { get; }

    /// <summary>Owned applicable IC Count variants.</summary>
    public IReadOnlyList<string> IcCountVariants { get; }

    /// <summary>Owned applicable canonical map variants.</summary>
    public IReadOnlyList<string> MapVariants { get; }
}

/// <summary>One declared non-contract evidence candidate.</summary>
public sealed record SupportEvidenceDeclaration(
    string DeclarationId,
    SupportEvidenceStatus Status,
    string SourceRouteId,
    string? TargetRouteId = null,
    SupportEvidenceFactScope? FactScope = null);

/// <summary>Immutable identity and declarations supplied by an evidence adapter.</summary>
public sealed record SupportEvidenceCatalogSnapshot
{
    /// <summary>Initializes one owned evidence snapshot.</summary>
    public SupportEvidenceCatalogSnapshot(
        string catalogId,
        string catalogVersion,
        string sourceReference,
        IEnumerable<SupportEvidenceDeclaration> declarations)
    {
        CatalogId = catalogId;
        CatalogVersion = catalogVersion;
        SourceReference = sourceReference;
        Declarations = SupportSnapshot.Copy(declarations);
    }

    /// <summary>Stable evidence catalog identity.</summary>
    public string CatalogId { get; }

    /// <summary>Evidence catalog version.</summary>
    public string CatalogVersion { get; }

    /// <summary>Traceable source reference.</summary>
    public string SourceReference { get; }

    /// <summary>Owned evidence declarations.</summary>
    public IReadOnlyList<SupportEvidenceDeclaration> Declarations { get; }
}

/// <summary>One exact route with independent authoring and execution facts.</summary>
public sealed record SupportRouteDescriptor(
    SupportRouteIdentity Identity,
    SupportAuthoringAvailability AuthoringAvailability,
    bool ExecutionAdmitted,
    string AuthoringSourceId,
    string ExecutionSourceId)
{
    /// <summary>Stable route id derived from the exact route identity.</summary>
    public string RouteId => Identity.RouteId;
}

/// <summary>A catalog declaration that cannot safely bind to an exact route.</summary>
public sealed record SupportUnresolvedScope(
    string SourceId,
    string IcId,
    string WorkflowId,
    string Reason);

/// <summary>The selected evidence class and declaration provenance.</summary>
public sealed record SupportEvidenceResolution(
    SupportEvidenceStatus Status,
    string? SourceDeclarationId,
    string? TargetRouteId,
    string? FactScopeId);

/// <summary>One materialized matrix row. It never grants execution authority.</summary>
public sealed record SupportMatrixRow(
    SupportRouteDescriptor Route,
    SupportPublicationStatus PublicationStatus,
    SupportPublicationDecision? PublicationDecision,
    SupportEvidenceResolution Evidence);

/// <summary>One retained fail-closed migration diagnostic.</summary>
public sealed record SupportMatrixDiagnostic(string Code, string Subject, string Message);

/// <summary>Read-only Support Matrix projection over exact route snapshots.</summary>
public sealed record SupportMatrix
{
    /// <summary>Initializes one owned reporting projection.</summary>
    public SupportMatrix(
        SupportPublicationPolicySnapshot policy,
        SupportEvidenceCatalogSnapshot evidenceCatalog,
        IEnumerable<SupportMatrixRow> rows,
        IEnumerable<SupportMatrixDiagnostic> diagnostics)
    {
        Policy = policy;
        EvidenceCatalog = evidenceCatalog;
        Rows = SupportSnapshot.Copy(rows);
        Diagnostics = SupportSnapshot.Copy(diagnostics);
    }

    /// <summary>Hash-closed owner policy snapshot.</summary>
    public SupportPublicationPolicySnapshot Policy { get; }

    /// <summary>Evidence snapshot used for resolution.</summary>
    public SupportEvidenceCatalogSnapshot EvidenceCatalog { get; }

    /// <summary>Owned materialized rows.</summary>
    public IReadOnlyList<SupportMatrixRow> Rows { get; }

    /// <summary>Owned fail-closed diagnostics.</summary>
    public IReadOnlyList<SupportMatrixDiagnostic> Diagnostics { get; }

    /// <summary>True only when every source is safely bound and classified.</summary>
    public bool IsMigrationReady => Diagnostics.Count == 0;
}

internal static class SupportSnapshot
{
    internal static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}
