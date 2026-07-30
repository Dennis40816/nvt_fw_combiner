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

            IReadOnlyList<string> selectionGroupMemberSlotIds =
                registration.InputSelectionGroupMemberSlotIds;
            IEnumerable<FirmwareImageMap> publishedRouteMaps =
                selectionGroupMemberSlotIds.Count == 0
                    ? maps
                    : maps.TakeLast(1);
            foreach (FirmwareImageMap map in publishedRouteMaps)
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
                    selectionGroupMemberSlotIds.Count == 0
                        ? null
                        : selectionGroupMemberSlotIds,
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
        string[] integrityOperations =
        [
            .. composition.Plan.OrderedOperations
                .Where(static operation =>
                    operation.Kind is
                        CompositionOperationKind.RunExternalProcessor or
                        CompositionOperationKind.TransformScalar)
                .Select(DescribeIntegrityOperation),
        ];
        if (integrityOperations.Length == 0)
        {
            return composition.IntegrityFingerprint is null
                ? "not-applicable"
                : throw new InvalidOperationException(
                    "A compiled integrity fingerprint must have a declared integrity operation.");
        }

        string fingerprint = composition.IntegrityFingerprint ??
            throw new InvalidOperationException(
                "Declared integrity operations require a compiled integrity fingerprint.");
        return $"{string.Join('|', integrityOperations)}|fingerprint:{fingerprint}";
    }

    private static string DescribeIntegrityOperation(
        CompositionOperation operation)
    {
        return operation.Kind switch
        {
            CompositionOperationKind.RunExternalProcessor =>
                DescribeExternalProcessor(operation),
            CompositionOperationKind.TransformScalar =>
                DescribeScalarTransform(operation),
            CompositionOperationKind.CopyRange or
            CompositionOperationKind.ReplaceRange or
            CompositionOperationKind.FillRange or
            CompositionOperationKind.PatchScalar =>
                throw new InvalidOperationException(
                    "The operation is not an integrity operation."),
            _ => throw new InvalidOperationException(
                "Unknown compiled integrity operation."),
        };
    }

    private static string DescribeExternalProcessor(
        CompositionOperation operation)
    {
        ExternalProcessorInvocation invocation =
            operation.ExternalProcessorInvocation ??
            throw new InvalidOperationException(
                "An external-processor operation requires its invocation.");
        return
            $"external-processor:{FrameIntegrityValue(invocation.ProcessorId)}:" +
            FrameIntegrityValue(invocation.ToolBindingId);
    }

    private static string DescribeScalarTransform(
        CompositionOperation operation)
    {
        _ = operation.ScalarTransform ??
            throw new InvalidOperationException(
                "A scalar-transform operation requires its transform.");
        return $"transform-scalar:{FrameIntegrityValue(operation.OperationId)}";
    }

    private static string FrameIntegrityValue(string value)
    {
        return FormattableString.Invariant($"{value.Length}:{value}");
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
