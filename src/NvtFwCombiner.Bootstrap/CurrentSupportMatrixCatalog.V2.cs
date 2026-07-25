using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap;

internal static partial class CurrentSupportMatrixCatalog
{
    private static void AddV2Routes(
        IEnumerable<BuiltInV2Registration> registrations,
        List<SupportRouteDescriptor> routes,
        List<SupportUnresolvedScope> unresolved)
    {
        foreach (BuiltInV2Registration registration in registrations)
        {
            IReadOnlyList<FirmwareImageMap> maps =
                registration.GetMapVariants(
                    out IcNumberInputMode? icNumberInputMode,
                    out IReadOnlyList<CompositionIssue> issues);
            if (issues.Count != 0 || maps.Count == 0)
            {
                unresolved.Add(Unresolved(
                    registration.IcId,
                    registration.WorkflowId,
                    registration.ProfileId,
                    $"Declared map variants could not be resolved: {IssueCodes(issues)}."));
                continue;
            }

            foreach (FirmwareImageMap map in maps)
            {
                string? countVariant = TryFormatIcCountVariant(
                    map.Applicability.TopologyRequirement,
                    icNumberInputMode);
                if (countVariant is null)
                {
                    unresolved.Add(Unresolved(
                        registration.IcId,
                        registration.WorkflowId,
                        map.MapId,
                        "Numeric IC Count input has no exact map topology binding."));
                    continue;
                }
                TopologySelection? topology = registration.WorkflowId ==
                    Profiles.IcWorkflowIds.AbMerge
                    ? CreateTopologySelection(
                        map.Applicability.TopologyRequirement,
                        map.MapId)
                    : null;
                registration.TryCompile(
                    map.Applicability.CapacityBytes,
                    topology,
                    out CompiledComposition? composition,
                    out IReadOnlyList<CompositionIssue> compileIssues);
                FirmwareImageMap? compiledMap =
                    composition?.V2Details?.Provenance.ResolvedMap.ImageMap;
                if (compiledMap is null ||
                    !StringComparer.Ordinal.Equals(
                        compiledMap.MapId,
                        map.MapId))
                {
                    unresolved.Add(Unresolved(
                        registration.IcId,
                        registration.WorkflowId,
                        $"{countVariant}:{map.MapId}",
                        "Declared exact route did not compile to its own map: " +
                        IssueCodes(compileIssues) + "."));
                    continue;
                }

                routes.Add(Route(
                    new SupportRouteIdentity(
                        registration.IcId,
                        registration.WorkflowId,
                        countVariant,
                        map.MapId,
                        ResolveIntegrityRouteId(composition!)),
                    registration.IcId,
                    registration.WorkflowId,
                    executionAdmitted: true,
                    $"built-in-v2:{registration.ProfileId}@" +
                    $"{registration.ProfileVersion}:{map.MapId}"));
            }
        }
    }

    private static string ResolveIntegrityRouteId(
        CompiledComposition composition)
    {
        string[] processorRoutes =
        [
            .. composition.Plan.OrderedOperations
                .Where(static operation =>
                    operation.ExternalProcessorInvocation is not null)
                .Select(static operation =>
                    operation.ExternalProcessorInvocation!)
                .Select(static invocation =>
                    $"{invocation.ProcessorId}:{invocation.ToolBindingId}"),
        ];
        return processorRoutes.Length == 0
            ? "not-applicable"
            : string.Join('|', processorRoutes);
    }

    private static TopologySelection? CreateTopologySelection(
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
                $"support-matrix:{mapId}");
    }

    private static string FormatIcCountVariant(
        TopologyRequirement requirement)
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

    private static string? TryFormatIcCountVariant(
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
}
