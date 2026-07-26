namespace NvtFwCombiner.Application.Support;

/// <summary>Application-owned materializer that never infers publication from firmware facts.</summary>
public static class SupportMatrixMaterializer
{
    /// <summary>Diagnostic code for a route missing an owner policy row.</summary>
    public const string UnclassifiedRoute = "support-matrix.unclassified-route";

    /// <summary>Diagnostic code for a selectable route lacking execution admission.</summary>
    public const string SelectableNotExecutable = "support-matrix.selectable-not-executable";

    /// <summary>Diagnostic code for a hidden executable route lacking a safe policy state.</summary>
    public const string ExecutableNotSelectable = "support-matrix.executable-not-selectable";

    /// <summary>Diagnostic code for a source too coarse to create an exact route.</summary>
    public const string SourceScopeUnresolved = "support-matrix.source-scope-unresolved";

    /// <summary>Diagnostic code for unresolved exact authoring availability.</summary>
    public const string AuthoringRouteUnresolved = "support-matrix.authoring-route-unresolved";

    /// <summary>Materializes one immutable reporting snapshot and retained diagnostics.</summary>
    public static SupportMatrix Materialize(
        SupportPublicationPolicySnapshot policy,
        IEnumerable<SupportRouteDescriptor> routes,
        SupportEvidenceCatalogSnapshot evidenceCatalog,
        IEnumerable<SupportUnresolvedScope>? unresolvedScopes = null,
        SupportPublicationPolicySnapshot? supersededPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(evidenceCatalog);

        SupportRouteDescriptor[] routeArray = [.. routes];
        SupportPublicationPolicyValidator.Validate(policy, supersededPolicy);
        ValidateRoutes(routeArray);
        Dictionary<string, SupportRouteDescriptor> routesById = routeArray.ToDictionary(
            static route => route.RouteId,
            StringComparer.Ordinal);
        string[] unresolvedPolicyRoutes =
        [
            .. policy.Decisions
                .Where(decision => !routesById.ContainsKey(decision.RouteId))
                .Select(static decision => decision.RouteId)
                .Order(StringComparer.Ordinal),
        ];
        if (unresolvedPolicyRoutes.Length != 0)
        {
            throw new ArgumentException(
                "Every publication policy route must resolve exactly once: " +
                string.Join(", ", unresolvedPolicyRoutes),
                nameof(policy));
        }

        var evidenceResolver =
            SupportEvidenceResolver.Create(evidenceCatalog, routesById);
        var decisions = policy.Decisions.ToDictionary(
            static decision => decision.RouteId,
            StringComparer.Ordinal);

        var rows = new List<SupportMatrixRow>(routeArray.Length);
        var diagnostics = new List<SupportMatrixDiagnostic>();
        foreach (SupportRouteDescriptor route in routeArray.OrderBy(
                     static route => route.RouteId,
                     StringComparer.Ordinal))
        {
            _ = decisions.TryGetValue(
                route.RouteId,
                out SupportPublicationDecision? decision);
            SupportPublicationStatus publication =
                decision?.Status ?? SupportPublicationStatus.Unclassified;
            AddRouteDiagnostics(route, decision, publication, diagnostics);
            rows.Add(new SupportMatrixRow(
                route,
                publication,
                decision,
                evidenceResolver.Resolve(route)));
        }

        AddUnresolvedScopeDiagnostics(unresolvedScopes ?? [], diagnostics);
        return new SupportMatrix(
            policy,
            evidenceCatalog,
            rows,
            diagnostics
                .OrderBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Subject, StringComparer.Ordinal));
    }

    private static void AddRouteDiagnostics(
        SupportRouteDescriptor route,
        SupportPublicationDecision? decision,
        SupportPublicationStatus publication,
        List<SupportMatrixDiagnostic> diagnostics)
    {
        if (publication == SupportPublicationStatus.Unclassified)
        {
            diagnostics.Add(new SupportMatrixDiagnostic(
                UnclassifiedRoute,
                route.RouteId,
                decision is null
                    ? "Exact route has no owner-approved publication decision."
                    : "Owner policy explicitly leaves the exact route unclassified."));
        }

        if (route.AuthoringAvailability == SupportAuthoringAvailability.Unknown)
        {
            diagnostics.Add(new SupportMatrixDiagnostic(
                AuthoringRouteUnresolved,
                route.RouteId,
                "Exact route authoring availability has no exact source binding."));
        }

        if (route.AuthoringAvailability == SupportAuthoringAvailability.Available &&
            !route.ExecutionAdmitted)
        {
            diagnostics.Add(new SupportMatrixDiagnostic(
                SelectableNotExecutable,
                route.RouteId,
                "Selectable exact route is not execution-admitted."));
        }

        if (route.ExecutionAdmitted &&
            route.AuthoringAvailability == SupportAuthoringAvailability.Unavailable &&
            publication is not (
                SupportPublicationStatus.Candidate or
                SupportPublicationStatus.Internal or
                SupportPublicationStatus.TestOnly))
        {
            diagnostics.Add(new SupportMatrixDiagnostic(
                ExecutableNotSelectable,
                route.RouteId,
                "Execution-admitted route is not selectable and lacks an explicit non-public status."));
        }
    }

    private static void AddUnresolvedScopeDiagnostics(
        IEnumerable<SupportUnresolvedScope> unresolvedScopes,
        List<SupportMatrixDiagnostic> diagnostics)
    {
        foreach (SupportUnresolvedScope scope in unresolvedScopes)
        {
            ArgumentNullException.ThrowIfNull(scope);
            ValidateText(scope.SourceId, nameof(scope.SourceId));
            ValidateText(scope.IcId, nameof(scope.IcId));
            ValidateText(scope.WorkflowId, nameof(scope.WorkflowId));
            ValidateText(scope.Reason, nameof(scope.Reason));
            diagnostics.Add(new SupportMatrixDiagnostic(
                SourceScopeUnresolved,
                scope.SourceId,
                scope.Reason));
        }
    }

    private static void ValidateRoutes(SupportRouteDescriptor[] routes)
    {
        if (routes.Any(static route => route is null) ||
            routes.Select(static route => route.RouteId)
                .Distinct(StringComparer.Ordinal).Count() != routes.Length ||
            routes.Select(static route => route.Identity)
                .Distinct().Count() != routes.Length)
        {
            throw new ArgumentException(
                "Support routes must have unique route and exact identities.",
                nameof(routes));
        }

        foreach (SupportRouteDescriptor route in routes)
        {
            ArgumentNullException.ThrowIfNull(route.Identity);
            if (!Enum.IsDefined(route.AuthoringAvailability))
            {
                throw new ArgumentException(
                    "Support route authoring availability is invalid.",
                    nameof(routes));
            }

            ValidateText(route.AuthoringSourceId, nameof(route.AuthoringSourceId));
            ValidateText(route.ExecutionSourceId, nameof(route.ExecutionSourceId));
        }
    }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }
}
