namespace NvtFwCombiner.Application.Support;

internal sealed class SupportEvidenceResolver
{
    private readonly SupportEvidenceCatalogSnapshot _catalog;
    private readonly IReadOnlyDictionary<string, SupportRouteDescriptor> _routesById;

    private SupportEvidenceResolver(
        SupportEvidenceCatalogSnapshot catalog,
        IReadOnlyDictionary<string, SupportRouteDescriptor> routesById)
    {
        _catalog = catalog;
        _routesById = routesById;
    }

    internal static SupportEvidenceResolver Create(
        SupportEvidenceCatalogSnapshot catalog,
        IReadOnlyDictionary<string, SupportRouteDescriptor> routesById)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(routesById);
        ValidateText(catalog.CatalogId, nameof(catalog.CatalogId));
        ValidateText(catalog.CatalogVersion, nameof(catalog.CatalogVersion));
        ValidateText(catalog.SourceReference, nameof(catalog.SourceReference));
        if (catalog.Declarations.Any(static declaration => declaration is null) ||
            catalog.Declarations.Select(static declaration => declaration.DeclarationId)
                .Distinct(StringComparer.Ordinal).Count() != catalog.Declarations.Count)
        {
            throw new ArgumentException(
                "Evidence declarations must have unique non-null ids.",
                nameof(catalog));
        }

        var resolver = new SupportEvidenceResolver(catalog, routesById);
        resolver.ValidateDeclarations();
        return resolver;
    }

    internal SupportEvidenceResolution Resolve(SupportRouteDescriptor route)
    {
        SupportEvidenceDeclaration? strongest = _catalog.Declarations
            .Where(declaration => StringComparer.Ordinal.Equals(declaration.SourceRouteId, route.RouteId))
            .OrderBy(static declaration => Precedence(declaration.Status))
            .ThenBy(static declaration => declaration.DeclarationId, StringComparer.Ordinal)
            .FirstOrDefault();
        return strongest is not null
            ? new SupportEvidenceResolution(
                strongest.Status,
                strongest.DeclarationId,
                strongest.TargetRouteId,
                strongest.FactScope?.FactScopeId)
            : route.ExecutionAdmitted
            ? new SupportEvidenceResolution(
                SupportEvidenceStatus.ContractOnly,
                route.ExecutionSourceId,
                null,
                null)
            : new SupportEvidenceResolution(SupportEvidenceStatus.Missing, null, null, null);
    }

    private void ValidateDeclarations()
    {
        var scopesById = new Dictionary<string, SupportEvidenceFactScope>(StringComparer.Ordinal);
        foreach (SupportEvidenceDeclaration declaration in _catalog.Declarations)
        {
            ValidateText(declaration.DeclarationId, nameof(declaration.DeclarationId));
            ValidateText(declaration.SourceRouteId, nameof(declaration.SourceRouteId));
            if (!_routesById.TryGetValue(
                    declaration.SourceRouteId,
                    out SupportRouteDescriptor? sourceRoute))
            {
                throw new ArgumentException(
                    "Evidence source route is not an exact catalog route.",
                    nameof(_catalog));
            }

            if (declaration.Status is not (SupportEvidenceStatus.DirectGolden or
                SupportEvidenceStatus.ApprovedAlias or
                SupportEvidenceStatus.SyntheticOracle))
            {
                throw new ArgumentException(
                    "Evidence catalogs may declare only direct golden, approved alias, or synthetic oracle evidence.",
                    nameof(_catalog));
            }

            if (declaration.Status == SupportEvidenceStatus.ApprovedAlias)
            {
                ValidateAlias(declaration, sourceRoute, scopesById);
            }
            else if (declaration.TargetRouteId is not null || declaration.FactScope is not null)
            {
                throw new ArgumentException(
                    "Exact evidence cannot carry alias target or fact-scope data.",
                    nameof(_catalog));
            }
        }
    }

    private void ValidateAlias(
        SupportEvidenceDeclaration declaration,
        SupportRouteDescriptor sourceRoute,
        Dictionary<string, SupportEvidenceFactScope> scopesById)
    {
        if (string.IsNullOrWhiteSpace(declaration.TargetRouteId) ||
            declaration.FactScope is null ||
            !_routesById.ContainsKey(declaration.TargetRouteId))
        {
            throw new ArgumentException(
                "Approved alias evidence requires an exact target route and fact scope.",
                nameof(declaration));
        }

        ValidateFactScope(declaration.FactScope, scopesById);
        if (!FactScopeAppliesTo(declaration.FactScope, sourceRoute.Identity))
        {
            throw new ArgumentException(
                "Approved alias fact scope does not apply to its exact source route.",
                nameof(declaration));
        }

        if (!_catalog.Declarations.Any(candidate =>
                candidate.Status == SupportEvidenceStatus.DirectGolden &&
                StringComparer.Ordinal.Equals(
                    candidate.SourceRouteId,
                    declaration.TargetRouteId)))
        {
            throw new ArgumentException(
                "Approved alias target has no direct golden declaration.",
                nameof(declaration));
        }
    }

    private static void ValidateFactScope(
        SupportEvidenceFactScope factScope,
        Dictionary<string, SupportEvidenceFactScope> scopesById)
    {
        ValidateText(factScope.FactScopeId, nameof(factScope.FactScopeId));
        ValidateText(factScope.IcId, nameof(factScope.IcId));
        ValidateText(factScope.WorkflowId, nameof(factScope.WorkflowId));
        ValidateVariants(factScope.IcCountVariants, nameof(factScope.IcCountVariants));
        ValidateVariants(factScope.MapVariants, nameof(factScope.MapVariants));
        if (scopesById.TryGetValue(
                factScope.FactScopeId,
                out SupportEvidenceFactScope? existing) &&
            !Equivalent(existing, factScope))
        {
            throw new ArgumentException(
                $"Evidence fact-scope id '{factScope.FactScopeId}' has conflicting definitions.",
                nameof(factScope));
        }

        _ = scopesById.TryAdd(factScope.FactScopeId, factScope);
    }

    private static bool Equivalent(
        SupportEvidenceFactScope left,
        SupportEvidenceFactScope right)
    {
        return StringComparer.Ordinal.Equals(left.IcId, right.IcId) &&
            StringComparer.Ordinal.Equals(left.WorkflowId, right.WorkflowId) &&
            left.IcCountVariants.SequenceEqual(right.IcCountVariants, StringComparer.Ordinal) &&
            left.MapVariants.SequenceEqual(right.MapVariants, StringComparer.Ordinal);
    }

    private static bool FactScopeAppliesTo(
        SupportEvidenceFactScope factScope,
        SupportRouteIdentity route)
    {
        return StringComparer.Ordinal.Equals(factScope.IcId, route.IcId) &&
            StringComparer.Ordinal.Equals(factScope.WorkflowId, route.WorkflowId) &&
            factScope.IcCountVariants.Contains(route.IcCountVariant, StringComparer.Ordinal) &&
            factScope.MapVariants.Contains(route.MapVariant, StringComparer.Ordinal);
    }

    private static void ValidateVariants(
        IReadOnlyList<string> variants,
        string parameterName)
    {
        if (variants.Count == 0 ||
            variants.Any(string.IsNullOrWhiteSpace) ||
            variants.Distinct(StringComparer.Ordinal).Count() != variants.Count)
        {
            throw new ArgumentException(
                "Fact-scope variants must be non-empty and unique.",
                parameterName);
        }
    }

    private static int Precedence(SupportEvidenceStatus status)
    {
        return status switch
        {
            SupportEvidenceStatus.DirectGolden => 0,
            SupportEvidenceStatus.ApprovedAlias => 1,
            SupportEvidenceStatus.SyntheticOracle => 2,
            SupportEvidenceStatus.ContractOnly or SupportEvidenceStatus.Missing =>
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "Only declared evidence may be ranked."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Only declared evidence may be ranked."),
        };
    }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }
}
