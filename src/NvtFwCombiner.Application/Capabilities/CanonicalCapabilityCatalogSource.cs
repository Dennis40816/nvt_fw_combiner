using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Typed trusted-policy facts ready to join exact compiler results.</summary>
public sealed record CanonicalCapabilityPolicySnapshot(
    string CatalogId,
    string CatalogVersion,
    string SourceSha256,
    IReadOnlyList<CanonicalCapabilityPolicyRoute> Routes);

/// <summary>One exact-route policy row without duplicated firmware execution facts.</summary>
public sealed record CanonicalCapabilityPolicyRoute(
    CapabilityRouteIdentity Identity,
    string CapabilityFingerprint,
    PinnedCapabilityDecision<CapabilityAuthoringAvailability> Authoring,
    PinnedCapabilityDecision<CapabilityPublicationStatus> Publication,
    PinnedCapabilityDecision<CapabilityEvidenceStatus> Evidence);

/// <summary>Exact compiler-owned artifacts for one static catalog route.</summary>
internal sealed record CanonicalCompiledRoute(
    string CapabilityFingerprint,
    CompiledComposition Composition,
    MetadataPlanDefinition MetadataPlan);

/// <summary>Exact compiler contract for one authoring-bound dynamic route.</summary>
internal sealed record CanonicalDynamicRoute(
    string CapabilityFingerprint,
    CanonicalCapabilityCompilationContract CompilationContract);

/// <summary>Joins one trusted policy snapshot to exact compiler outputs before publication.</summary>
internal sealed class CanonicalCapabilityCatalogSource :
    ICanonicalCapabilityCatalogSource
{
    private readonly Func<CanonicalCapabilityPolicySnapshot> _loadPolicy;
    private readonly Func<CapabilityRouteIdentity, bool> _isDynamicRoute;
    private readonly Func<CapabilityRouteIdentity, CanonicalCompiledRoute>
        _resolveCompiledRoute;
    private readonly Func<CapabilityRouteIdentity, CanonicalDynamicRoute>
        _resolveDynamicRoute;
    private readonly Func<
        IReadOnlyList<CanonicalCapabilityDefinition>,
        IReadOnlyList<CanonicalDynamicCapabilityDefinition>,
        CanonicalCapabilityDisclosure> _loadDisclosure;

    internal CanonicalCapabilityCatalogSource(
        Func<CanonicalCapabilityPolicySnapshot> loadPolicy,
        Func<CapabilityRouteIdentity, bool> isDynamicRoute,
        Func<CapabilityRouteIdentity, CanonicalCompiledRoute> resolveCompiledRoute,
        Func<CapabilityRouteIdentity, CanonicalDynamicRoute> resolveDynamicRoute,
        Func<
            IReadOnlyList<CanonicalCapabilityDefinition>,
            IReadOnlyList<CanonicalDynamicCapabilityDefinition>,
            CanonicalCapabilityDisclosure> loadDisclosure)
    {
        _loadPolicy = loadPolicy ?? throw new ArgumentNullException(nameof(loadPolicy));
        _isDynamicRoute = isDynamicRoute ?? throw new ArgumentNullException(nameof(isDynamicRoute));
        _resolveCompiledRoute = resolveCompiledRoute ??
            throw new ArgumentNullException(nameof(resolveCompiledRoute));
        _resolveDynamicRoute = resolveDynamicRoute ??
            throw new ArgumentNullException(nameof(resolveDynamicRoute));
        _loadDisclosure = loadDisclosure ??
            throw new ArgumentNullException(nameof(loadDisclosure));
    }

    public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            CanonicalCapabilityPolicySnapshot policy = _loadPolicy();
            CanonicalCapabilityDefinition[] definitions =
            [
                .. policy.Routes
                    .Where(route => !_isDynamicRoute(route.Identity))
                    .Select(Materialize),
            ];
            CanonicalDynamicCapabilityDefinition[] dynamicDefinitions =
            [
                .. policy.Routes
                    .Where(route => _isDynamicRoute(route.Identity))
                    .Select(MaterializeDynamic),
            ];
            return CapabilityCatalogLoadResult.Success(
                new CanonicalCapabilityCatalogCandidate(
                    policy.CatalogId,
                    policy.CatalogVersion,
                    policy.SourceSha256,
                    definitions,
                    dynamicDefinitions)
                    .WithDisclosure(_loadDisclosure(
                        definitions,
                        dynamicDefinitions)));
        }
        catch (InvalidDataException exception)
        {
            return Failure(CapabilityCatalogIssueCodes.SourceInvalid, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Failure(CapabilityCatalogIssueCodes.SourceInvalid, exception.Message);
        }
        catch (IOException exception)
        {
            return Failure(CapabilityCatalogIssueCodes.SourceUnavailable, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(CapabilityCatalogIssueCodes.SourceUnavailable, exception.Message);
        }
        catch (TypeInitializationException exception)
        {
            Exception? sourceFailure = FindSourceFailure(exception);
            if (sourceFailure is null)
            {
                throw;
            }

            string code = sourceFailure is IOException or UnauthorizedAccessException
                ? CapabilityCatalogIssueCodes.SourceUnavailable
                : CapabilityCatalogIssueCodes.SourceInvalid;
            return Failure(code, sourceFailure.Message);
        }
    }

    private CanonicalDynamicCapabilityDefinition MaterializeDynamic(
        CanonicalCapabilityPolicyRoute policy)
    {
        CanonicalDynamicRoute route = _resolveDynamicRoute(policy.Identity);
        return !StringComparer.Ordinal.Equals(
                route.CapabilityFingerprint,
                policy.CapabilityFingerprint)
            ? throw new InvalidDataException(
                $"Capability definition fingerprint does not match dynamic route '{policy.Identity.RouteId}' " +
                $"(current {route.CapabilityFingerprint}, policy {policy.CapabilityFingerprint}).")
            : new CanonicalDynamicCapabilityDefinition(
                policy.Identity,
                route.CapabilityFingerprint,
                route.CompilationContract,
                policy.Authoring,
                policy.Publication,
                policy.Evidence);
    }

    private CanonicalCapabilityDefinition Materialize(
        CanonicalCapabilityPolicyRoute policy)
    {
        CanonicalCompiledRoute route = _resolveCompiledRoute(policy.Identity);
        CompiledComposition composition = route.Composition
            .BindCapabilityFingerprint(route.CapabilityFingerprint);
        string mapId = composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId;
        return !StringComparer.Ordinal.Equals(mapId, policy.Identity.MapVariant)
            ? throw new InvalidDataException(
                $"Compiler map '{mapId}' does not match route '{policy.Identity.MapVariant}'.")
            : !StringComparer.Ordinal.Equals(
                route.CapabilityFingerprint,
                policy.CapabilityFingerprint)
            ? throw new InvalidDataException(
                $"Capability definition fingerprint does not match route '{policy.Identity.RouteId}' " +
                $"(current {route.CapabilityFingerprint}, policy {policy.CapabilityFingerprint}).")
            : new CanonicalCapabilityDefinition(
                policy.Identity,
                route.CapabilityFingerprint,
                composition,
                policy.Authoring,
                policy.Publication,
                policy.Evidence,
                route.MetadataPlan);
    }

    private static CapabilityCatalogLoadResult Failure(string code, string message)
    {
        return CapabilityCatalogLoadResult.Failure(
            new CapabilityCatalogIssue(code, message));
    }

    private static Exception? FindSourceFailure(TypeInitializationException exception)
    {
        Exception? current = exception.InnerException;
        while (current is TypeInitializationException nested)
        {
            current = nested.InnerException;
        }

        return current is System.Text.Json.JsonException or
            InvalidDataException or
            ArgumentException or
            IOException or
            UnauthorizedAccessException
                ? current
                : null;
    }
}
