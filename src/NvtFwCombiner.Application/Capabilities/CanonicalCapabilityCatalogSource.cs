using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using System.Threading.Channels;

#pragma warning disable CS1591 // Infrastructure adapter contracts are not end-user API.

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
public sealed record CanonicalCompiledRoute(
    string CapabilityFingerprint,
    CompiledComposition Composition,
    MetadataPlanDefinition MetadataPlan);

/// <summary>Exact compiler contract for one authoring-bound dynamic route.</summary>
public sealed record CanonicalDynamicRoute(
    string CapabilityFingerprint,
    CanonicalCapabilityCompilationContract CompilationContract);

/// <summary>Joins one trusted policy snapshot to exact compiler outputs before publication.</summary>
internal sealed class CanonicalCapabilityCatalogSource(
    Func<CanonicalCapabilityPolicySnapshot> loadPolicy,
    Func<CapabilityRouteIdentity, bool> isDynamicRoute,
    Func<CapabilityRouteIdentity, CanonicalCompiledRoute> resolveCompiledRoute,
    Func<CapabilityRouteIdentity, CanonicalDynamicRoute> resolveDynamicRoute,
    Func<
        IReadOnlyList<CanonicalCapabilityDefinition>,
        IReadOnlyList<CanonicalDynamicCapabilityDefinition>,
        CanonicalCapabilityDisclosure> loadDisclosure) :
    ICanonicalCapabilityCatalogSource
{
    public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
    {
        return Load(progress: null, cancellationToken);
    }

    CapabilityCatalogLoadResult ICanonicalCapabilityCatalogSource.Load(
        ChannelWriter<CanonicalCapabilityCatalogLoadUpdate>? progress,
        CancellationToken cancellationToken)
    {
        return Load(progress, cancellationToken);
    }

    private CapabilityCatalogLoadResult Load(
        ChannelWriter<CanonicalCapabilityCatalogLoadUpdate>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CanonicalCapabilityPolicySnapshot policy = loadPolicy();
            cancellationToken.ThrowIfCancellationRequested();
            int totalRoutes = policy.Routes.Count;
            int completedRoutes = 0;
            _ = progress?.TryWrite(new(0, Result: null));
            List<CanonicalCapabilityDefinition> definitions = [];
            List<CanonicalDynamicCapabilityDefinition> dynamicDefinitions = [];
            ILookup<bool, CanonicalCapabilityPolicyRoute> classifiedRoutes =
                policy.Routes.ToLookup(route => isDynamicRoute(route.Identity));
            foreach (CanonicalCapabilityPolicyRoute route in classifiedRoutes[false])
            {
                cancellationToken.ThrowIfCancellationRequested();
                definitions.Add(Materialize(route));
                cancellationToken.ThrowIfCancellationRequested();
                _ = progress?.TryWrite(new((double)++completedRoutes / (totalRoutes + 1), Result: null));
            }
            foreach (CanonicalCapabilityPolicyRoute route in classifiedRoutes[true])
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamicDefinitions.Add(MaterializeDynamic(route));
                cancellationToken.ThrowIfCancellationRequested();
                _ = progress?.TryWrite(new((double)++completedRoutes / (totalRoutes + 1), Result: null));
            }
            cancellationToken.ThrowIfCancellationRequested();
            CanonicalCapabilityDisclosure disclosure = loadDisclosure(
                definitions,
                dynamicDefinitions);
            cancellationToken.ThrowIfCancellationRequested();
            return CapabilityCatalogLoadResult.Success(
                new CanonicalCapabilityCatalogCandidate(
                    policy.CatalogId,
                    policy.CatalogVersion,
                    policy.SourceSha256,
                    definitions,
                    dynamicDefinitions)
                    .WithDisclosure(disclosure));
        }
        catch (Exception exception) when (TryGetSourceIssue(exception, out CapabilityCatalogIssue issue))
        {
            return CapabilityCatalogLoadResult.Failure(issue);
        }
    }

    private CanonicalDynamicCapabilityDefinition MaterializeDynamic(
        CanonicalCapabilityPolicyRoute policy)
    {
        CanonicalDynamicRoute route = resolveDynamicRoute(policy.Identity);
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
        CanonicalCompiledRoute route = resolveCompiledRoute(policy.Identity);
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

    private static bool TryGetSourceIssue(
        Exception exception,
        out CapabilityCatalogIssue issue)
    {
        while (exception is TypeInitializationException { InnerException: { } inner })
        {
            exception = inner;
        }

        string? code = exception switch
        {
            IOException or UnauthorizedAccessException => CapabilityCatalogIssueCodes.SourceUnavailable,
            InvalidDataException or ArgumentException or System.Text.Json.JsonException => CapabilityCatalogIssueCodes.SourceInvalid,
            _ => null,
        };
        issue = new CapabilityCatalogIssue(code ?? string.Empty, exception.Message);
        return code is not null;
    }
}
