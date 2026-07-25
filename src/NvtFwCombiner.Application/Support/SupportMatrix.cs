namespace NvtFwCombiner.Application.Support;

/// <summary>Owner-approved product-publication state, separate from authoring and execution facts.</summary>
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

/// <summary>Whether an exact canonical route has an explicit authoring binding.</summary>
public enum SupportAuthoringAvailability
{
    /// <summary>The available source is too coarse to prove an exact authoring result.</summary>
    Unknown,

    /// <summary>An exact source explicitly permits authoring this route.</summary>
    Available,

    /// <summary>An exact source explicitly excludes authoring this route.</summary>
    Unavailable,
}

/// <summary>Strongest declared evidence class for one exact support route.</summary>
public enum SupportEvidenceStatus
{
    /// <summary>Direct route-specific golden evidence exists.</summary>
    DirectGolden,

    /// <summary>An explicitly scoped and approved alias supplies evidence.</summary>
    ApprovedAlias,

    /// <summary>A route-specific synthetic oracle supplies evidence.</summary>
    SyntheticOracle,

    /// <summary>The route has an admitted contract but no stronger declared evidence.</summary>
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

/// <summary>One exact publication decision keyed by a stable canonical route id.</summary>
public sealed record SupportPublicationDecision(
    string DecisionId,
    string RouteId,
    SupportPublicationStatus Status,
    SupportPublicationProvenance Provenance);

/// <summary>Immutable policy identity and hash supplied by an Infrastructure adapter.</summary>
public sealed record SupportPublicationPolicySnapshot(
    string PolicyId,
    string PolicyVersion,
    string Sha256,
    IReadOnlyList<SupportPublicationDecision> Decisions);

/// <summary>One explicitly declared fact scope for evidence reuse across exact routes.</summary>
public sealed record SupportEvidenceFactScope(
    string FactScopeId,
    string IcId,
    string WorkflowId,
    IReadOnlyList<string> IcCountVariants,
    IReadOnlyList<string> MapVariants);

/// <summary>One declared non-contract evidence candidate from the canonical evidence catalog.</summary>
public sealed record SupportEvidenceDeclaration(
    string DeclarationId,
    SupportEvidenceStatus Status,
    string SourceRouteId,
    string? TargetRouteId = null,
    SupportEvidenceFactScope? FactScope = null);

/// <summary>Immutable identity and declarations supplied by an authoritative evidence adapter.</summary>
public sealed record SupportEvidenceCatalogSnapshot(
    string CatalogId,
    string CatalogVersion,
    string SourceReference,
    IReadOnlyList<SupportEvidenceDeclaration> Declarations);

/// <summary>One exact IC/workflow/IC-count/map route with independent authoring and execution facts.</summary>
public sealed record SupportRouteDescriptor(
    string RouteId,
    string IcId,
    string WorkflowId,
    string IcCountVariant,
    string MapVariant,
    SupportAuthoringAvailability AuthoringAvailability,
    bool ExecutionAdmitted,
    string AuthoringSourceId,
    string ExecutionSourceId)
{
    internal (string IcId, string WorkflowId, string IcCountVariant, string MapVariant) ExactIdentity =>
        (IcId, WorkflowId, IcCountVariant, MapVariant);
}

/// <summary>A catalog declaration that cannot safely bind to an exact route.</summary>
public sealed record SupportUnresolvedScope(
    string SourceId,
    string IcId,
    string WorkflowId,
    string Reason);

/// <summary>The selected evidence class and declaration provenance for one matrix row.</summary>
public sealed record SupportEvidenceResolution(
    SupportEvidenceStatus Status,
    string? SourceDeclarationId,
    string? TargetRouteId,
    string? FactScopeId);

/// <summary>One materialized support matrix row. It never grants execution authority.</summary>
public sealed record SupportMatrixRow(
    SupportRouteDescriptor Route,
    SupportPublicationStatus PublicationStatus,
    SupportPublicationDecision? PublicationDecision,
    SupportEvidenceResolution Evidence);

/// <summary>One retained fail-closed diagnostic for the migration gate.</summary>
public sealed record SupportMatrixDiagnostic(string Code, string Subject, string Message);

/// <summary>Read-only Support Matrix projection over exact routes and a hash-closed publication snapshot.</summary>
public sealed record SupportMatrix(
    SupportPublicationPolicySnapshot Policy,
    SupportEvidenceCatalogSnapshot EvidenceCatalog,
    IReadOnlyList<SupportMatrixRow> Rows,
    IReadOnlyList<SupportMatrixDiagnostic> Diagnostics)
{
    /// <summary>True only when every source is exactly bound and every route has a safe policy relationship.</summary>
    public bool IsMigrationReady => Diagnostics.Count == 0;
}

/// <summary>Application-owned materializer that never infers publication or evidence from firmware facts.</summary>
public static class SupportMatrixMaterializer
{
    /// <summary>Diagnostic code for an exact route missing an owner policy row.</summary>
    public const string UnclassifiedRoute = "support-matrix.unclassified-route";

    /// <summary>Diagnostic code for a selectable exact route lacking execution admission.</summary>
    public const string SelectableNotExecutable = "support-matrix.selectable-not-executable";

    /// <summary>Diagnostic code for an executable hidden route lacking a non-public policy state.</summary>
    public const string ExecutableNotSelectable = "support-matrix.executable-not-selectable";

    /// <summary>Diagnostic code for a policy decision that has no exact canonical route.</summary>
    public const string PolicyRouteUnresolved = "support-matrix.policy-route-unresolved";

    /// <summary>Diagnostic code for an input catalog too coarse to create an exact route.</summary>
    public const string SourceScopeUnresolved = "support-matrix.source-scope-unresolved";

    /// <summary>Diagnostic code for an exact route whose authoring availability remains unresolved.</summary>
    public const string AuthoringRouteUnresolved = "support-matrix.authoring-route-unresolved";

    /// <summary>Materializes the reporting projection while retaining every unsafe or unresolved relationship.</summary>
    public static SupportMatrix Materialize(
        SupportPublicationPolicySnapshot policy,
        IEnumerable<SupportRouteDescriptor> routes,
        SupportEvidenceCatalogSnapshot evidenceCatalog,
        IEnumerable<SupportUnresolvedScope>? unresolvedScopes = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(evidenceCatalog);
        SupportRouteDescriptor[] routeArray = [.. routes];
        ValidatePolicy(policy);
        ValidateRoutes(routeArray);
        Dictionary<string, SupportRouteDescriptor> routesById = routeArray.ToDictionary(
            static route => route.RouteId,
            StringComparer.Ordinal);
        ValidateEvidenceCatalog(evidenceCatalog, routesById);

        var decisions = policy.Decisions.ToDictionary(
            static decision => decision.RouteId,
            StringComparer.Ordinal);
        var diagnostics = new List<SupportMatrixDiagnostic>();
        var rows = new List<SupportMatrixRow>(routeArray.Length);
        foreach (SupportRouteDescriptor route in routeArray.OrderBy(static route => route.RouteId, StringComparer.Ordinal))
        {
            _ = decisions.TryGetValue(route.RouteId, out SupportPublicationDecision? decision);
            SupportPublicationStatus publication = decision?.Status ?? SupportPublicationStatus.Unclassified;
            if (publication == SupportPublicationStatus.Unclassified)
            {
                diagnostics.Add(new SupportMatrixDiagnostic(
                    UnclassifiedRoute,
                    route.RouteId,
                    "Exact route has no owner-approved publication decision."));
            }

            if (route.AuthoringAvailability == SupportAuthoringAvailability.Unknown)
            {
                diagnostics.Add(new SupportMatrixDiagnostic(
                    AuthoringRouteUnresolved,
                    route.RouteId,
                    "Exact route authoring availability has no exact source binding."));
            }

            if (route.AuthoringAvailability == SupportAuthoringAvailability.Available && !route.ExecutionAdmitted)
            {
                diagnostics.Add(new SupportMatrixDiagnostic(
                    SelectableNotExecutable,
                    route.RouteId,
                    "Selectable exact route is not execution-admitted."));
            }

            if (route.ExecutionAdmitted && route.AuthoringAvailability == SupportAuthoringAvailability.Unavailable &&
                publication is not (SupportPublicationStatus.Candidate or SupportPublicationStatus.Internal or SupportPublicationStatus.TestOnly))
            {
                diagnostics.Add(new SupportMatrixDiagnostic(
                    ExecutableNotSelectable,
                    route.RouteId,
                    "Execution-admitted exact route is not selectable and lacks an explicit non-public classification."));
            }

            rows.Add(new SupportMatrixRow(route, publication, decision, ResolveEvidence(route, evidenceCatalog)));
        }

        foreach (SupportPublicationDecision decision in policy.Decisions.Where(decision =>
                     !routeArray.Any(route => StringComparer.Ordinal.Equals(route.RouteId, decision.RouteId))))
        {
            diagnostics.Add(new SupportMatrixDiagnostic(
                PolicyRouteUnresolved,
                decision.RouteId,
                "Publication policy route does not resolve to one exact canonical route."));
        }

        foreach (SupportUnresolvedScope scope in unresolvedScopes ?? [])
        {
            ValidateText(scope.SourceId, nameof(scope.SourceId));
            ValidateText(scope.IcId, nameof(scope.IcId));
            ValidateText(scope.WorkflowId, nameof(scope.WorkflowId));
            ValidateText(scope.Reason, nameof(scope.Reason));
            diagnostics.Add(new SupportMatrixDiagnostic(SourceScopeUnresolved, scope.SourceId, scope.Reason));
        }

        return new SupportMatrix(
            policy,
            evidenceCatalog,
            Array.AsReadOnly(rows.ToArray()),
            Array.AsReadOnly(diagnostics.OrderBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Subject, StringComparer.Ordinal)
                .ToArray()));
    }

    private static void ValidatePolicy(SupportPublicationPolicySnapshot policy)
    {
        ValidateText(policy.PolicyId, nameof(policy.PolicyId));
        ValidateText(policy.PolicyVersion, nameof(policy.PolicyVersion));
        ValidateText(policy.Sha256, nameof(policy.Sha256));
        ArgumentNullException.ThrowIfNull(policy.Decisions);
        if (policy.Decisions.Any(static decision => decision is null) ||
            policy.Decisions.Select(static decision => decision.DecisionId).Distinct(StringComparer.Ordinal).Count() != policy.Decisions.Count ||
            policy.Decisions.Select(static decision => decision.RouteId).Distinct(StringComparer.Ordinal).Count() != policy.Decisions.Count)
        {
            throw new ArgumentException("Publication policy decisions must have unique ids and route ids.", nameof(policy));
        }

        foreach (SupportPublicationDecision decision in policy.Decisions)
        {
            ValidateText(decision.DecisionId, nameof(decision.DecisionId));
            ValidateText(decision.RouteId, nameof(decision.RouteId));
            if (!Enum.IsDefined(decision.Status) || decision.Provenance is null)
            {
                throw new ArgumentException("Publication policy decision is invalid.", nameof(policy));
            }

            ValidateText(decision.Provenance.AuthorityKind, nameof(decision.Provenance.AuthorityKind));
            ValidateText(decision.Provenance.RecordedOn, nameof(decision.Provenance.RecordedOn));
            ValidateText(decision.Provenance.RecordRef, nameof(decision.Provenance.RecordRef));
            ValidateText(decision.Provenance.Rationale, nameof(decision.Provenance.Rationale));
        }
    }

    private static void ValidateRoutes(SupportRouteDescriptor[] routes)
    {
        if (routes.Any(static route => route is null) ||
            routes.Select(static route => route.RouteId).Distinct(StringComparer.Ordinal).Count() != routes.Length ||
            routes.Select(static route => route.ExactIdentity).Distinct().Count() != routes.Length)
        {
            throw new ArgumentException("Support routes must have unique route and exact identities.", nameof(routes));
        }

        foreach (SupportRouteDescriptor route in routes)
        {
            ValidateText(route.RouteId, nameof(route.RouteId));
            ValidateText(route.IcId, nameof(route.IcId));
            ValidateText(route.WorkflowId, nameof(route.WorkflowId));
            ValidateText(route.IcCountVariant, nameof(route.IcCountVariant));
            ValidateText(route.MapVariant, nameof(route.MapVariant));
            if (!Enum.IsDefined(route.AuthoringAvailability))
            {
                throw new ArgumentException("Support route authoring availability is invalid.", nameof(routes));
            }

            ValidateText(route.AuthoringSourceId, nameof(route.AuthoringSourceId));
            ValidateText(route.ExecutionSourceId, nameof(route.ExecutionSourceId));
        }
    }

    private static void ValidateEvidenceCatalog(
        SupportEvidenceCatalogSnapshot evidenceCatalog,
        Dictionary<string, SupportRouteDescriptor> routesById)
    {
        ValidateText(evidenceCatalog.CatalogId, nameof(evidenceCatalog.CatalogId));
        ValidateText(evidenceCatalog.CatalogVersion, nameof(evidenceCatalog.CatalogVersion));
        ValidateText(evidenceCatalog.SourceReference, nameof(evidenceCatalog.SourceReference));
        ArgumentNullException.ThrowIfNull(evidenceCatalog.Declarations);
        if (evidenceCatalog.Declarations.Any(static declaration => declaration is null) ||
            evidenceCatalog.Declarations.Select(static declaration => declaration.DeclarationId)
                .Distinct(StringComparer.Ordinal).Count() != evidenceCatalog.Declarations.Count)
        {
            throw new ArgumentException("Evidence catalog declarations must have unique ids.", nameof(evidenceCatalog));
        }

        foreach (SupportEvidenceDeclaration declaration in evidenceCatalog.Declarations)
        {
            ValidateText(declaration.DeclarationId, nameof(declaration.DeclarationId));
            ValidateText(declaration.SourceRouteId, nameof(declaration.SourceRouteId));
            if (!routesById.TryGetValue(declaration.SourceRouteId, out SupportRouteDescriptor? sourceRoute))
            {
                throw new ArgumentException("Evidence declaration source route is not an exact canonical route.", nameof(evidenceCatalog));
            }

            if (declaration.Status is not (SupportEvidenceStatus.DirectGolden or
                SupportEvidenceStatus.ApprovedAlias or SupportEvidenceStatus.SyntheticOracle))
            {
                throw new ArgumentException("Evidence catalog may declare only direct golden, approved alias, or synthetic oracle evidence.", nameof(evidenceCatalog));
            }

            if (declaration.Status == SupportEvidenceStatus.ApprovedAlias)
            {
                ValidateAliasDeclaration(declaration, sourceRoute, routesById, evidenceCatalog.Declarations);
                continue;
            }

            if (declaration.TargetRouteId is not null || declaration.FactScope is not null)
            {
                throw new ArgumentException("Exact evidence declarations cannot carry alias target or fact scope data.", nameof(evidenceCatalog));
            }
        }
    }

    private static void ValidateAliasDeclaration(
        SupportEvidenceDeclaration declaration,
        SupportRouteDescriptor sourceRoute,
        Dictionary<string, SupportRouteDescriptor> routesById,
        IReadOnlyList<SupportEvidenceDeclaration> declarations)
    {
        if (string.IsNullOrWhiteSpace(declaration.TargetRouteId) || declaration.FactScope is null ||
            !routesById.ContainsKey(declaration.TargetRouteId))
        {
            throw new ArgumentException("Approved alias evidence requires an exact target route and fact scope.", nameof(declaration));
        }

        ValidateFactScope(declaration.FactScope);
        if (!FactScopeAppliesTo(declaration.FactScope, sourceRoute))
        {
            throw new ArgumentException("Approved alias fact scope does not apply to its exact source route.", nameof(declaration));
        }

        if (!declarations.Any(candidate =>
                candidate.Status == SupportEvidenceStatus.DirectGolden &&
                StringComparer.Ordinal.Equals(candidate.SourceRouteId, declaration.TargetRouteId)))
        {
            throw new ArgumentException("Approved alias target route has no direct golden declaration.", nameof(declaration));
        }
    }

    private static void ValidateFactScope(SupportEvidenceFactScope factScope)
    {
        ValidateText(factScope.FactScopeId, nameof(factScope.FactScopeId));
        ValidateText(factScope.IcId, nameof(factScope.IcId));
        ValidateText(factScope.WorkflowId, nameof(factScope.WorkflowId));
        ValidateVariants(factScope.IcCountVariants, nameof(factScope.IcCountVariants));
        ValidateVariants(factScope.MapVariants, nameof(factScope.MapVariants));
    }

    private static void ValidateVariants(IReadOnlyList<string> variants, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(variants);
        if (variants.Count == 0 || variants.Any(string.IsNullOrWhiteSpace) ||
            variants.Distinct(StringComparer.Ordinal).Count() != variants.Count)
        {
            throw new ArgumentException("Fact-scope variants must be non-empty and unique.", parameterName);
        }
    }

    private static bool FactScopeAppliesTo(SupportEvidenceFactScope factScope, SupportRouteDescriptor route)
    {
        return StringComparer.Ordinal.Equals(factScope.IcId, route.IcId) &&
            StringComparer.Ordinal.Equals(factScope.WorkflowId, route.WorkflowId) &&
            factScope.IcCountVariants.Contains(route.IcCountVariant, StringComparer.Ordinal) &&
            factScope.MapVariants.Contains(route.MapVariant, StringComparer.Ordinal);
    }

    private static SupportEvidenceResolution ResolveEvidence(
        SupportRouteDescriptor route,
        SupportEvidenceCatalogSnapshot evidenceCatalog)
    {
        SupportEvidenceDeclaration? strongest = evidenceCatalog.Declarations
            .Where(declaration => StringComparer.Ordinal.Equals(declaration.SourceRouteId, route.RouteId))
            .OrderBy(static declaration => declaration.Status)
            .ThenBy(static declaration => declaration.DeclarationId, StringComparer.Ordinal)
            .FirstOrDefault();
        return strongest is not null
            ? new SupportEvidenceResolution(
                strongest.Status,
                strongest.DeclarationId,
                strongest.TargetRouteId,
                strongest.FactScope?.FactScopeId)
            : route.ExecutionAdmitted
            ? new SupportEvidenceResolution(SupportEvidenceStatus.ContractOnly, route.ExecutionSourceId, null, null)
            : new SupportEvidenceResolution(SupportEvidenceStatus.Missing, null, null, null);
    }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }
}
