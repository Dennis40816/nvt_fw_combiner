using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static void ValidateRuntimeReferenceReplaceInputRequirements(
        CompositionPlan plan,
        CompositionKind compositionKind,
        string experienceId,
        V2CompiledCompositionDetails details)
    {
        var runtimeContext = details.Provenance.Context as RuntimeReferenceReplaceV2CompilationContext;
        bool isGeneralReplace = StringComparer.Ordinal.Equals(
            runtimeContext?.ModeId,
            ExperienceIds.GeneralReplace);
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            runtimeContext?.ModeId,
            ExperienceIds.CtrlRamReplace);
        if (compositionKind != CompositionKind.Replace ||
            runtimeContext is null ||
            (!isGeneralReplace && !isCtrlRamReplace) ||
            !StringComparer.Ordinal.Equals(experienceId, runtimeContext.ModeId) ||
            plan.OutputInitialization.Kind != ImageInitializationKind.Reference ||
            plan.OutputInitialization.ReferenceSpaceId is null ||
            details.RegionAccessContract.Requirements.Count == 0 ||
            (!runtimeContext.AllowsConditionalProcessor &&
             details.RegionAccessContract.ResolvedViews.Count != 0))
        {
            throw new ArgumentException(
                "Map-bound runtime reference-replace artifacts require a matching General or CtrlRAM Replace experience, a reference-cloned output, declared physical access, and only contract-authorized processor views.",
                nameof(details));
        }

        CompiledInputArtifactClass expectedSourceClass = isCtrlRamReplace
            ? CompiledInputArtifactClass.CtrlRamReplacement
            : CompiledInputArtifactClass.Auxiliary;

        CompiledInputSlotRequirement[] referenceSlots =
        [
            .. details.InputContract.Slots.Where(static slot =>
                slot.ArtifactClass == CompiledInputArtifactClass.ReferenceImage),
        ];
        CompiledInputSlotRequirement[] sourceSlots =
        [
            .. details.InputContract.Slots.Where(slot =>
                slot.ArtifactClass == expectedSourceClass),
        ];
        if (details.InputContract.Slots.Count != 2 || referenceSlots.Length != 1 || sourceSlots.Length != 1 ||
            referenceSlots[0] is not
            {
                Required: true,
                Cardinality: CompiledInputSlotCardinality.ExactlyOne,
                LengthRequirement: CompiledExactResolvedMapCapacityInputLengthRequirement,
                Normalization: CompiledNoInputNormalization,
            } ||
            sourceSlots[0] is not
            {
                Required: true,
                Cardinality: CompiledInputSlotCardinality.OneOrMore,
                LengthRequirement: CompiledBoundedInputLengthRequirement
                {
                    MinimumBytes: 1,
                    MaximumBytes: int.MaxValue,
                },
                Normalization: CompiledNoInputNormalization,
            })
        {
            throw new ArgumentException(
                "Map-bound runtime reference-replace artifacts require one exact reference slot and one unnormalized per-binding experience-owned source slot.",
                nameof(details));
        }

        CompiledInputSpaceBinding[] referenceBindings =
        [
            .. details.InputContract.SpaceBindings.Where(binding =>
                StringComparer.Ordinal.Equals(binding.SlotId, referenceSlots[0].SlotId)),
        ];
        CompiledInputSpaceBinding[] sourceBindings =
        [
            .. details.InputContract.SpaceBindings.Where(binding =>
                StringComparer.Ordinal.Equals(binding.SlotId, sourceSlots[0].SlotId)),
        ];
        if (referenceBindings.Length != 1 || sourceBindings.Length == 0 ||
            referenceBindings[0].InstancePolicy != CompiledInputInstancePolicy.Singleton ||
            sourceBindings.Any(static binding => binding.InstancePolicy != CompiledInputInstancePolicy.PerBinding) ||
            !StringComparer.Ordinal.Equals(
                plan.OutputInitialization.ReferenceSpaceId,
                referenceBindings[0].AddressSpaceId))
        {
            throw new ArgumentException(
                "Runtime reference-replace bindings must contain exactly one singleton output reference and one or more per-binding auxiliary sources.",
                nameof(details));
        }

        var spaces = plan.AddressSpaces.ToDictionary(static space => space.AddressSpaceId, StringComparer.Ordinal);
        string[] immutableAddressSpaceIds =
        [
            .. plan.AddressSpaces
                .Where(static space => space.Mutability == AddressSpaceMutability.Immutable)
                .Select(static space => space.AddressSpaceId)
                .Order(StringComparer.Ordinal),
        ];
        string[] bindingAddressSpaceIds =
        [
            .. details.InputContract.SpaceBindings
                .Select(static binding => binding.AddressSpaceId)
                .Order(StringComparer.Ordinal),
        ];
        if (!immutableAddressSpaceIds.SequenceEqual(bindingAddressSpaceIds, StringComparer.Ordinal) ||
            plan.AddressSpaces.Count != bindingAddressSpaceIds.Length + 1 ||
            plan.OutputInitialization.Capacity != details.Provenance.ResolvedMap.CapacityBytes)
        {
            throw new ArgumentException(
                "Runtime reference-replace artifacts must bind every immutable plan space once and declare only the reference-cloned output as mutable.",
                nameof(details));
        }

        if (!spaces.TryGetValue(referenceBindings[0].AddressSpaceId, out AddressSpace? referenceSpace) ||
            referenceSpace.Mutability != AddressSpaceMutability.Immutable ||
            referenceSpace.Length != details.Provenance.ResolvedMap.CapacityBytes ||
            referenceSpace.InputPaddingByte is not null ||
            referenceSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
            referenceSpace.AllowedInputLengths.Count != 0 ||
            referenceSpace.ExpectedInputLengths.Count != 0)
        {
            throw new ArgumentException(
                "Runtime reference-replace output must clone one exact unnormalized immutable resolved-map reference.",
                nameof(details));
        }

        var sourceAddressSpaceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CompiledInputSpaceBinding sourceBinding in sourceBindings)
        {
            if (!spaces.TryGetValue(sourceBinding.AddressSpaceId, out AddressSpace? sourceSpace) ||
                sourceSpace.Mutability != AddressSpaceMutability.Immutable ||
                sourceSpace.Length is < 1 or > int.MaxValue ||
                sourceSpace.InputPaddingByte is not null ||
                sourceSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
                sourceSpace.AllowedInputLengths.Count != 0 ||
                sourceSpace.ExpectedInputLengths.Count != 0 ||
                !sourceAddressSpaceIds.Add(sourceBinding.AddressSpaceId))
            {
                throw new ArgumentException(
                    "Runtime reference-replace auxiliary bindings must be unique unnormalized immutable in-memory sources.",
                    nameof(details));
            }
        }

        ValidateRuntimeReferenceReplaceViews(
            plan,
            runtimeContext,
            details.RegionAccessContract.ResolvedViews);

        CompositionOperation[] mappingOperations = [.. plan.OrderedOperations.Where(static operation => operation.Kind == CompositionOperationKind.ReplaceRange)];
        CompositionOperation[] processorOperations = [.. plan.OrderedOperations.Where(static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor)];
        if (mappingOperations.Length == 0 ||
            mappingOperations.Length + processorOperations.Length != plan.OrderedOperations.Count ||
            mappingOperations.Any(operation =>
                operation.OverlapPolicy != OverlapPolicy.Reject ||
                !StringComparer.Ordinal.Equals(operation.TargetSpaceId, plan.OutputSpaceId) ||
                operation.SourceSpaceId is null ||
                !sourceAddressSpaceIds.Contains(operation.SourceSpaceId)))
        {
            throw new ArgumentException(
                "Runtime reference-replace plans require only reject-overlap ReplaceRange operations from declared sources into the output.",
                nameof(plan));
        }

        if (isCtrlRamReplace && mappingOperations.Any(mapping =>
                runtimeContext.ResolvedMap.ImageMap.Regions
                    .Where(region => region.Range.Contains(mapping.TargetRange))
                    .OrderBy(static region => region.Range.Length)
                    .ThenBy(static region => region.RegionId, StringComparer.Ordinal)
                    .FirstOrDefault() is not
                    {
                        Owner: FirmwareRegionOwner.Tp,
                        Kind: FirmwareRegionKind.CtrlRam,
                    }))
        {
            throw new ArgumentException(
                "CtrlRAM runtime reference-replace mappings must target canonical TP-owned CtrlRAM regions.",
                nameof(plan));
        }

        var referencedSourceAddressSpaceIds = mappingOperations
            .Select(static operation => operation.SourceSpaceId)
            .ToHashSet(StringComparer.Ordinal);
        if (!referencedSourceAddressSpaceIds.SetEquals(sourceAddressSpaceIds))
        {
            throw new ArgumentException(
                "Every runtime reference-replace source binding must participate in at least one operation.",
                nameof(plan));
        }

        ValidateRuntimeReferenceReplaceProcessor(
            plan,
            runtimeContext,
            mappingOperations,
            processorOperations,
            details.RegionAccessContract.ResolvedViews);
    }

    private static void ValidateRuntimeReferenceReplaceViews(
        CompositionPlan plan,
        RuntimeReferenceReplaceV2CompilationContext runtimeContext,
        IReadOnlyList<CompiledResolvedPhysicalView> resolvedViews)
    {
        AddressSpace output = plan.AddressSpaces.Single(space =>
            StringComparer.Ordinal.Equals(space.AddressSpaceId, plan.OutputSpaceId));
        if ((!runtimeContext.AllowsConditionalProcessor && resolvedViews.Count != 0) ||
            resolvedViews.Any(view =>
                !StringComparer.Ordinal.Equals(view.AddressSpaceId, plan.OutputSpaceId) ||
                !output.Contains(view.Range)))
        {
            throw new ArgumentException(
                "Runtime reference Replace processor views must be profile-owned physical ranges inside the cloned output image.",
                nameof(resolvedViews));
        }
    }

    private static void ValidateRuntimeReferenceReplaceProcessor(
        CompositionPlan plan,
        RuntimeReferenceReplaceV2CompilationContext runtimeContext,
        CompositionOperation[] mappingOperations,
        CompositionOperation[] processorOperations,
        IReadOnlyList<CompiledResolvedPhysicalView> resolvedViews)
    {
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            runtimeContext.ModeId,
            ExperienceIds.CtrlRamReplace);
        bool touchesTp = mappingOperations.Any(mapping =>
            runtimeContext.ResolvedMap.ImageMap.Regions.Any(region =>
                region.Owner == FirmwareRegionOwner.Tp &&
                region.Range.Overlaps(mapping.TargetRange)));
        if (processorOperations.Length != (touchesTp ? 1 : 0) ||
            (touchesTp && !runtimeContext.AllowsConditionalProcessor))
        {
            throw new ArgumentException(
                "Runtime reference Replace requires exactly one approved processor after TP mappings and no processor for mappings outside TP regions.",
                nameof(processorOperations));
        }

        if (processorOperations.Length == 0)
        {
            return;
        }

        CompositionOperation processor = processorOperations[0];
        ExternalProcessorInvocation invocation = processor.ExternalProcessorInvocation!;
        if (processor.Sequence != int.MaxValue ||
            processor.OverlapPolicy != OverlapPolicy.ReplaceExisting ||
            !ReferenceEquals(processor, plan.OrderedOperations[^1]) ||
            !StringComparer.Ordinal.Equals(processor.TargetSpaceId, plan.OutputSpaceId) ||
            processor.TargetRange.Start != 0 ||
            invocation.StagedSourceBindings.Count != 0 ||
            invocation.StagedArtifactBindings.Count != 0)
        {
            throw new ArgumentException(
                "The runtime reference Replace processor must be the single final profile-owned output refresh with no staged source artifacts.",
                nameof(processorOperations));
        }

        ByteRange[] processorRanges =
        [
            processor.TargetRange,
            .. invocation.AllowedReadRanges,
            .. invocation.AllowedWriteRanges,
        ];
        bool everyProcessorRangeHasProvenance = processorRanges.All(range =>
            resolvedViews.Any(view => isCtrlRamReplace
                ? view.Range.Contains(range)
                : view.Range == range));
        bool ctrlRamWritesMatchMappings = !isCtrlRamReplace ||
            (mappingOperations.All(mapping => invocation.AllowedWriteRanges.Any(range =>
                 range.Contains(mapping.TargetRange))) &&
             invocation.AllowedWriteRanges
                 .Where(range => IsCanonicalCtrlRamRange(runtimeContext.ResolvedMap.ImageMap, range))
                 .All(range => mappingOperations.Any(mapping => mapping.TargetRange.Contains(range))));
        if (resolvedViews.Count == 0 ||
            !everyProcessorRangeHasProvenance ||
            !ctrlRamWritesMatchMappings ||
            (!isCtrlRamReplace && resolvedViews.Any(view => !processorRanges.Contains(view.Range))))
        {
            throw new ArgumentException(
                "Every runtime reference Replace processor target, read, and write range must retain profile-owned physical-view provenance.",
                nameof(resolvedViews));
        }
    }

    private static bool IsCanonicalCtrlRamRange(FirmwareImageMap map, ByteRange range)
    {
        return map.Regions
            .Where(region => region.Range.Contains(range))
            .OrderBy(static region => region.Range.Length)
            .ThenBy(static region => region.RegionId, StringComparer.Ordinal)
            .FirstOrDefault() is
        {
            Owner: FirmwareRegionOwner.Tp,
            Kind: FirmwareRegionKind.CtrlRam,
        };
    }
}
