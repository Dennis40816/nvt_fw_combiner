using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Resolves one exact static V2 route from declarative registration and map
/// facts. Dynamic map-set, General, and CtrlRAM authoring routes join the same
/// published catalog through <see cref="CanonicalDynamicRouteInventory"/>.
/// </summary>
internal static class CanonicalCompiledRouteInventory
{
    internal static CanonicalCompiledRoute Resolve(
        CapabilityRouteIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        BuiltInV2Registration registration = ResolveRegistration(identity) ??
            throw new InvalidDataException(
                $"No migration compiler registration matches route '{identity.RouteId}'.");

        IReadOnlyList<FirmwareImageMap> maps = registration.GetMapVariants(
            out IcNumberInputMode? inputMode,
            out IReadOnlyList<CompositionIssue> mapIssues);
        FirmwareImageMap selectedMap = mapIssues.Count == 0
            ? maps.SingleOrDefault(map => StringComparer.Ordinal.Equals(
                map.MapId,
                identity.MapVariant)) ??
              throw new InvalidDataException(
                  $"No trusted map matches route '{identity.RouteId}'.")
            : throw new InvalidDataException(
                $"Trusted maps for route '{identity.RouteId}' were rejected: " +
                string.Join(", ", mapIssues.Select(static issue => issue.Code)));

        string? countVariant = HeadlessRouteSelection.TryFormatIcCountVariant(
            selectedMap.Applicability.TopologyRequirement,
            inputMode);
        if (!StringComparer.Ordinal.Equals(countVariant, identity.IcCountVariant))
        {
            throw new InvalidDataException(
                $"Declared IC Count variant '{identity.IcCountVariant}' does not match map '{selectedMap.MapId}'.");
        }

        TopologySelection? topology = identity.WorkflowId == IcWorkflowIds.AbMerge
            ? HeadlessRouteSelection.CreateTopologySelection(
                selectedMap.Applicability.TopologyRequirement,
                selectedMap.MapId)
            : null;
        IReadOnlyList<string> selectedSlots =
            registration.InputSelectionGroupMemberSlotIds;
        registration.TryCompile(
            selectedMap.CapacityBytes,
            topology,
            selectedSlots.Count == 0 ? null : selectedSlots,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> compileIssues);
        CompiledComposition accepted =
            composition?.V2Details?.Provenance.ResolvedMap.ImageMap is { } compiledMap &&
            StringComparer.Ordinal.Equals(compiledMap.MapId, selectedMap.MapId) &&
            compileIssues.Count == 0
                ? composition
                : throw new InvalidDataException(
                $"Compiler rejected route '{identity.RouteId}': " +
                string.Join(", ", compileIssues.Select(static issue => issue.Code)));

        return new CanonicalCompiledRoute(
            CapabilityDefinitionFingerprint.Compute(
                identity,
                registration.ProfileId,
                registration.ProfileVersion,
                registration.BundleContentHash,
                [selectedMap.MapId],
                CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId,
                registration.InputSelectionGroupMemberSlotIds),
            accepted,
            registration.CreateMetadataPlan(accepted));
    }

    private static BuiltInV2Registration? ResolveRegistration(
        CapabilityRouteIdentity identity)
    {
        BuiltInV2Registration? registration = identity.WorkflowId switch
        {
            IcWorkflowIds.StandardMerge =>
                BuiltInV2RegistrationRegistry.StandardMergeByIc
                    .GetValueOrDefault(identity.IcId),
            IcWorkflowIds.AbMerge =>
                BuiltInV2RegistrationRegistry.AbMergeByIc
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
}

/// <summary>One compiler-owned artifact and its canonical metadata references.</summary>
internal sealed record CanonicalCompiledRoute(
    string CapabilityFingerprint,
    CompiledComposition Composition,
    MetadataPlanDefinition MetadataPlan);

/// <summary>Pure formatting of declared topology facts into exact route axes.</summary>
internal static class HeadlessRouteSelection
{
    internal static string? TryFormatIcCountVariant(
        TopologyRequirement requirement,
        IcNumberInputMode? inputMode)
    {
        return requirement.Kind != TopologyRequirementKind.None
            ? FormatIcCountVariant(requirement)
            : inputMode switch
            {
                null => "selector-free",
                IcNumberInputMode.SingleSelector => "1-ic",
                IcNumberInputMode.CascadeSelector => "2-plus-ic",
                IcNumberInputMode.NumericSelector => null,
                _ => throw new InvalidOperationException(
                    "Unknown IC Count input mode."),
            };
    }

    internal static string FormatIcCountVariant(TopologyRequirement requirement)
    {
        return requirement.Kind switch
        {
            TopologyRequirementKind.None => "selector-free",
            TopologyRequirementKind.SingleChip => "1-ic",
            TopologyRequirementKind.ExactCount =>
                FormattableString.Invariant(
                    $"{requirement.ExactChipCount}-ic"),
            TopologyRequirementKind.Cascade
                when requirement.MaximumChipCount is { } maximum =>
                FormattableString.Invariant(
                    $"{requirement.MinimumChipCount}-{maximum}-ic"),
            TopologyRequirementKind.Cascade =>
                FormattableString.Invariant(
                    $"{requirement.MinimumChipCount}-plus-ic"),
            _ => throw new InvalidOperationException(
                "Unknown topology requirement kind."),
        };
    }

    internal static TopologySelection? CreateTopologySelection(
        TopologyRequirement requirement,
        string mapId)
    {
        int? count = requirement.Kind switch
        {
            TopologyRequirementKind.None => null,
            TopologyRequirementKind.SingleChip => 1,
            TopologyRequirementKind.ExactCount => requirement.ExactChipCount,
            TopologyRequirementKind.Cascade => requirement.MinimumChipCount,
            _ => throw new InvalidOperationException(
                "Unknown topology requirement kind."),
        };
        return count is null
            ? null
            : new TopologySelection(
                count.Value,
                $"{count.Value} IC",
                TopologySelectionSource.Requested,
                $"canonical-route:{mapId}");
    }
}
