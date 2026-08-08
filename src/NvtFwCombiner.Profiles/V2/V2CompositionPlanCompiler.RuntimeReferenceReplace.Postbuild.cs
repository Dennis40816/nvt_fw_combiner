using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static CompositionOperation[] NarrowRuntimeReferenceProcessorAuthority(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        bool narrowsCtrlRamProcessorAuthority,
        IReadOnlyList<CompositionOperation> mappingOperations,
        IReadOnlyList<ByteRange> postbuildFirmwareVersionWrites,
        V2RuntimeReferenceReplacePostbuildPolicy? postbuildPolicy,
        IReadOnlyList<ExternalProcessorWriteRangeSection> postbuildWriteRangeSections,
        CompositionOperation[] processorOperations)
    {
        if (!narrowsCtrlRamProcessorAuthority || processorOperations.Length == 0)
        {
            return [.. processorOperations];
        }

        CompositionOperation processor = processorOperations.Single();
        ExternalProcessorInvocation declared = processor.ExternalProcessorInvocation!;
        ByteRange[] canonicalCtrlRamRanges =
        [
            .. resolvedMap.ImageMap.Regions
                .Where(static region =>
                    region.Owner == FirmwareRegionOwner.Tp &&
                    region.Kind == FirmwareRegionKind.CtrlRam)
                .Select(static region => region.Range)
                .Distinct()
                .OrderBy(static range => range.Start)
                .ThenBy(static range => range.Length),
        ];
        var allowedWrites = new List<ByteRange>();
        foreach (ByteRange declaredRange in declared.AllowedWriteRanges)
        {
            IReadOnlyList<ByteRange> nonCtrlRam = [declaredRange];
            foreach (ByteRange ctrlRamRange in canonicalCtrlRamRanges)
            {
                nonCtrlRam =
                [
                    .. nonCtrlRam.SelectMany(range => range.Subtract([ctrlRamRange])),
                ];
            }

            allowedWrites.AddRange(nonCtrlRam);
            allowedWrites.AddRange(mappingOperations
                .Select(mapping => mapping.TargetRange.Intersect(declaredRange))
                .Where(static overlap => overlap is not null)
                .Select(static overlap => overlap!.Value));
        }

        allowedWrites.AddRange(postbuildFirmwareVersionWrites);
        if (postbuildPolicy is not null)
        {
            allowedWrites =
            [
                .. allowedWrites.SelectMany(range =>
                    range.Subtract([postbuildPolicy.ResolvedProcessorAuthority])),
            ];
            allowedWrites.Add(postbuildPolicy.ResolvedProcessorAuthority);
        }

        ByteRange[] resolvedAllowedWrites =
        [
            .. allowedWrites,
        ];
        var invocation = new ExternalProcessorInvocation(
            declared.ProcessorId,
            declared.ToolBindingId,
            declared.AllowedReadRanges,
            resolvedAllowedWrites,
            declared.StagedSourceBindings,
            declared.AllowedWriteRangeSections
                .Concat(postbuildWriteRangeSections)
                .Where(section =>
                    resolvedAllowedWrites.Any(range => range.Contains(section.Range)))
                .DistinctBy(section => (section.SectionId, section.Range, section.SourceRange)),
            declared.StagedArtifactBindings,
            declared.OutputAssertions);
        return
        [
            CompositionOperation.RunExternalProcessor(
                processor.OperationId,
                processor.Sequence,
                processor.TargetSpaceId,
                processor.TargetRange,
                invocation,
                processor.OverlapPolicy,
                processor.Reason,
                processor.Provenance),
        ];
    }

    private static void ValidatePostbuildPolicy(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        bool truncatesCtrlRamSources,
        V2RuntimeReferenceReplacePostbuildPolicy? postbuildPolicy,
        Dictionary<string, V2ExplicitMappingInputBinding> bindings,
        IReadOnlyList<CompositionOperation> mappingOperations,
        CompositionOperation[] processorOperations,
        List<CompositionIssue> issues)
    {
        if (postbuildPolicy is null)
        {
            return;
        }

        bool sourceValidationIsValid =
            postbuildPolicy.SourceAddressSpaceId is null ||
            (bindings.TryGetValue(
                 postbuildPolicy.SourceAddressSpaceId,
                 out V2ExplicitMappingInputBinding? sourceBinding) &&
             postbuildPolicy.RequiredNonuniformSourceRanges.All(range =>
                 range.EndExclusive <= sourceBinding.ExactLengthBytes &&
                 mappingOperations.Any(mapping =>
                     StringComparer.Ordinal.Equals(
                         mapping.SourceSpaceId,
                         postbuildPolicy.SourceAddressSpaceId) &&
                     mapping.SourceRange == range)));
        bool validShape =
            sourceValidationIsValid &&
            truncatesCtrlRamSources &&
            resolvedMap.TopologySelection is { ChipCount: >= 2 } &&
            resolvedMap.CapacityBytes >= postbuildPolicy.DeclaredProcessorAuthority.EndExclusive &&
            mappingOperations.All(mapping =>
                !mapping.TargetRange.Overlaps(postbuildPolicy.ResolvedProcessorAuthority)) &&
            processorOperations.Length == 1 &&
            postbuildPolicy.DeclaredProcessorAuthority.Subtract(
                processorOperations[0].ExternalProcessorInvocation!.AllowedWriteRanges).Count == 0;
        if (!validShape)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceProcessorRequired,
                $"Postbuild policy '{postbuildPolicy.PolicyId}' has stale, overlapping, or undeclared processor authority.",
                postbuildPolicy.PolicyId));
        }
    }

}
