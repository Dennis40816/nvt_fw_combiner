using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// One-way bridge that joins the canonical policy to the existing V2 compiler
/// until registrations are retired by the capability migration.
/// </summary>
internal sealed class CanonicalCapabilityCatalogMigrationSource :
    ICanonicalCapabilityCatalogSource
{
    private readonly Func<CanonicalCapabilityPolicySnapshot> _loadPolicy;

    internal CanonicalCapabilityCatalogMigrationSource()
        : this(BuiltInCanonicalCapabilityPolicy.Load)
    {
    }

    internal CanonicalCapabilityCatalogMigrationSource(
        Func<CanonicalCapabilityPolicySnapshot> loadPolicy)
    {
        ArgumentNullException.ThrowIfNull(loadPolicy);
        _loadPolicy = loadPolicy;
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
                    .Where(static route =>
                        !CanonicalDynamicRouteInventory.IsDynamic(route.Identity))
                    .Select(Materialize),
            ];
            CanonicalDynamicCapabilityDefinition[] dynamicDefinitions =
            [
                .. policy.Routes
                    .Where(static route =>
                        CanonicalDynamicRouteInventory.IsDynamic(route.Identity))
                    .Select(MaterializeDynamic),
            ];
            return CapabilityCatalogLoadResult.Success(
                new CanonicalCapabilityCatalogCandidate(
                    policy.CatalogId,
                    policy.CatalogVersion,
                    policy.SourceSha256,
                    definitions,
                    dynamicDefinitions));
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
    }

    private static CanonicalDynamicCapabilityDefinition MaterializeDynamic(
        CanonicalCapabilityPolicyRoute route)
    {
        CanonicalDynamicRoute definition =
            CanonicalDynamicRouteInventory.Resolve(route.Identity);
        return !StringComparer.Ordinal.Equals(
                definition.CapabilityFingerprint,
                route.CapabilityFingerprint)
            ? throw new InvalidDataException(
                $"Capability definition fingerprint does not match dynamic route '{route.Identity.RouteId}' " +
                $"(current {definition.CapabilityFingerprint}, policy {route.CapabilityFingerprint}).")
            : new CanonicalDynamicCapabilityDefinition(
                route.Identity,
                definition.CapabilityFingerprint,
                definition.CompilationContract,
                route.Authoring,
                route.Publication,
                route.Evidence);
    }

    private static CanonicalCapabilityDefinition Materialize(
        CanonicalCapabilityPolicyRoute route)
    {
        CanonicalCompiledRoute compiled =
            CanonicalCompiledRouteInventory.Resolve(route.Identity);
        CompiledComposition composition = compiled.Composition
            .BindCapabilityFingerprint(compiled.CapabilityFingerprint);

        string mapId = composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId;
        return !StringComparer.Ordinal.Equals(mapId, route.Identity.MapVariant)
            ? throw new InvalidDataException(
                $"Compiler map '{mapId}' does not match route '{route.Identity.MapVariant}'.")
            : !StringComparer.Ordinal.Equals(
                compiled.CapabilityFingerprint,
                route.CapabilityFingerprint)
            ? throw new InvalidDataException(
                $"Capability definition fingerprint does not match route '{route.Identity.RouteId}' " +
                $"(current {compiled.CapabilityFingerprint}, policy {route.CapabilityFingerprint}).")
            : new CanonicalCapabilityDefinition(
            route.Identity,
            compiled.CapabilityFingerprint,
            composition,
            route.Authoring,
            route.Publication,
            route.Evidence,
            compiled.MetadataPlan);
    }

    private static CapabilityCatalogLoadResult Failure(
        string code,
        string message)
    {
        return CapabilityCatalogLoadResult.Failure(
            new CapabilityCatalogIssue(code, message));
    }
}
