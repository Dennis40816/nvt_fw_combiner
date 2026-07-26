using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Profiles;

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
                .. policy.Routes.Select(Materialize),
            ];
            return CapabilityCatalogLoadResult.Success(
                new CanonicalCapabilityCatalogCandidate(
                    policy.CatalogId,
                    policy.CatalogVersion,
                    policy.SourceSha256,
                    definitions));
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

    private static CanonicalCapabilityDefinition Materialize(
        CanonicalCapabilityPolicyRoute route)
    {
        BuiltInV2Registration registration =
            ResolveRegistration(route.Identity) ??
            throw new InvalidDataException(
                $"No migration compiler registration matches route '{route.Identity.RouteId}'.");

        IReadOnlyList<FirmwareImageMap> mapVariants =
            registration.GetMapVariants(
                out _,
                out IReadOnlyList<CompositionIssue> mapIssues);
        FirmwareImageMap selectedMap = (mapIssues.Count == 0
            ? mapVariants.SingleOrDefault(map =>
                StringComparer.Ordinal.Equals(
                    map.MapId,
                    route.Identity.MapVariant))
            : null) ??
            throw new InvalidDataException(
                $"No trusted map matches route '{route.Identity.RouteId}': " +
                string.Join(
                    ", ",
                    mapIssues.Select(static issue => issue.Code)));

        registration.TryCompile(
            inputLength: selectedMap.CapacityBytes,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        if (composition is null || issues.Count != 0)
        {
            throw new InvalidDataException(
                $"Compiler rejected route '{route.Identity.RouteId}': " +
                string.Join(
                    ", ",
                    issues.Select(static issue => issue.Code)));
        }

        string? mapId = composition.V2Details?.Provenance.ResolvedMap.ImageMap.MapId;
        return !StringComparer.Ordinal.Equals(mapId, route.Identity.MapVariant)
            ? throw new InvalidDataException(
                $"Compiler map '{mapId}' does not match route '{route.Identity.MapVariant}'.")
            : !StringComparer.Ordinal.Equals(
                composition.CompilationFingerprint,
                route.CapabilityFingerprint)
            ? throw new InvalidDataException(
                $"Compiler fingerprint does not match route '{route.Identity.RouteId}'.")
            : new CanonicalCapabilityDefinition(
            route.Identity,
            composition,
            route.Authoring,
            route.Publication,
            route.Evidence,
            registration.CreateMetadataPlan(composition));
    }

    private static BuiltInV2Registration? ResolveRegistration(
        CapabilityRouteIdentity identity)
    {
        BuiltInV2Registration? registration =
            identity.WorkflowId switch
            {
                IcWorkflowIds.StandardMerge =>
                    BuiltInV2RegistrationRegistry.StandardMergeByIc
                        .GetValueOrDefault(identity.IcId),
                IcWorkflowIds.DpReplace =>
                    BuiltInV2RegistrationRegistry.DpReplaceByIc.Value
                        .GetValueOrDefault(identity.IcId),
                _ => null,
            };
        return registration is not null &&
            StringComparer.Ordinal.Equals(
                registration.WorkflowId,
                identity.WorkflowId)
                ? registration
                : null;
    }

    private static CapabilityCatalogLoadResult Failure(
        string code,
        string message)
    {
        return CapabilityCatalogLoadResult.Failure(
            new CapabilityCatalogIssue(code, message));
    }
}
