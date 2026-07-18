using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private const string RuntimeReferencePreparationNotAdmitted = "profile.v2.runtime-reference-replace.preparation-not-admitted";
    private const string RuntimeReferenceProfileShapeInvalid = "profile.v2.runtime-reference-replace.profile-shape-invalid";
    private const string RuntimeReferenceBindingInvalid = "profile.v2.runtime-reference-replace.binding-invalid";
    private const string RuntimeReferenceMappingInvalid = "profile.v2.runtime-reference-replace.mapping-invalid";
    private const string RuntimeReferenceSourceOutOfBounds = "profile.v2.runtime-reference-replace.source-out-of-bounds";
    private const string RuntimeReferenceTargetOutOfBounds = "profile.v2.runtime-reference-replace.target-out-of-bounds";
    private const string RuntimeReferenceCtrlRamTargetInvalid = "profile.v2.runtime-reference-replace.ctrlram-target-invalid";
    private const string RuntimeReferenceProcessorRequired = "profile.v2.runtime-reference-replace.processor-required";
    private const string RuntimeReferenceProcessorOrderInvalid = "profile.v2.runtime-reference-replace.processor-order-invalid";

    /// <summary>Lowers one admitted map-bound runtime reference Replace request through the shared plan algebra.</summary>
    internal static V2CompositionPlanCompileResult CompileRuntimeReferenceReplace(
        V2CompositionPreparationResult preparation,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(request);
        if (!preparation.IsAdmitted || preparation.Selection is null || preparation.Admission is null)
        {
            return V2CompositionPlanCompileResult.Failed([
                new CompositionIssue(
                    RuntimeReferencePreparationNotAdmitted,
                    "Runtime reference-replace plan lowering requires an admitted trusted preparation.")]);
        }

        CompositionProfileDefinition profile = preparation.Admission.Profile;
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = preparation.Admission.ResolvedMap;
        var issues = new List<CompositionIssue>();
        if (!IsRuntimeReferenceReplaceProfile(profile))
        {
            return V2CompositionPlanCompileResult.Failed([
                new CompositionIssue(
                    RuntimeReferenceProfileShapeInvalid,
                    "The admitted profile is not a closed map-bound runtime reference-replace shape.")]);
        }

        RuntimeReferenceReplaceProfileShape shape = AssertRuntimeReferenceReplaceProfileShape(profile);
        Dictionary<string, V2RuntimeReferenceReplaceInputBinding> bindings =
            ValidateRuntimeReferenceReplaceBindings(shape, resolvedMap, request, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        var spaces = bindings.Values.ToDictionary(
            static binding => binding.BindingId,
            static binding => new AddressSpace(
                binding.BindingId,
                binding.ExactLengthBytes,
                AddressSpaceMutability.Immutable),
            StringComparer.Ordinal);
        spaces.Add(
            shape.Output.SpaceId,
            new AddressSpace(
                shape.Output.SpaceId,
                resolvedMap.CapacityBytes,
                AddressSpaceMutability.Mutable));
        Dictionary<string, ResolvedView> views = LowerViews(profile, resolvedMap, spaces, issues);
        LoweredRegionAccess regionAccess = LowerRegionAccess(profile, resolvedMap, views, issues);
        bool touchesTp = ValidateRuntimeReferenceReplaceMappings(
            shape,
            resolvedMap,
            request,
            bindings,
            regionAccess,
            issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        CompositionOperation[] mappingOperations =
        [
            .. request.Mappings.Select(static mapping => CompositionOperation.ReplaceRange(
                mapping.MappingId,
                mapping.Sequence,
                mapping.SourceBindingId,
                mapping.SourceRange,
                mapping.TargetSpaceId,
                mapping.TargetRange,
                mapping.OverlapPolicy,
                mapping.Reason,
                mapping.Provenance)),
        ];
        ValidateOperationOverlaps(mappingOperations, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        CompositionOperation[] declaredProcessorOperations = LowerOperations(
            profile,
            spaces,
            views,
            regionAccess,
            issues,
            useProcessorWriteAuthority: true);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        CompositionOperation[] processorOperations = touchesTp
            ? NarrowRuntimeReferenceProcessorAuthority(
                resolvedMap,
                mappingOperations,
                declaredProcessorOperations)
            : [];
        CompositionOperation[] operations = [.. mappingOperations, .. processorOperations];

        V2RuntimeReferenceReplaceInputBinding referenceBinding = bindings.Values.Single(binding =>
            StringComparer.Ordinal.Equals(binding.SlotId, shape.ReferenceSlot.SlotId));
        var plan = new CompositionPlan(
            [ImageInitialization.Reference(shape.Output.SpaceId, referenceBinding.BindingId, resolvedMap.CapacityBytes)],
            shape.Output.SpaceId,
            spaces.Values,
            operations);
        var promotion = new CompiledProfilePromotion(
            MapPromotionStage(profile.Promotion.Stage),
            profile.Promotion.Blockers.Select(MapPromotionBlocker));
        var provenance = new V2CompilationProvenance(
            preparation.Selection.BundleIdentity,
            preparation.Selection.ProfileEntryIdentity,
            new RuntimeReferenceReplaceV2CompilationContext(
                resolvedMap,
                ((RuntimeReferenceReplaceProfileCompilationContext)profile.CompilationContext)
                    .AllowsConditionalProcessor),
            promotion,
            profile.EvidenceRefs,
            [],
            preparation.Admission.RequiredCapabilities.Select(static capability => new CompiledCapabilityAdmission(
                capability.RequiredCapabilityId,
                capability.Binding)));
        var inputContract = new CompiledInputContract(
            profile.InputSlots.Select(slot => MapInputSlot(slot, resolvedMap)),
            bindings.Values.Select(binding => new CompiledInputSpaceBinding(
                binding.BindingId,
                binding.SlotId,
                StringComparer.Ordinal.Equals(binding.SlotId, shape.ReferenceSlot.SlotId)
                    ? CompiledInputInstancePolicy.Singleton
                    : CompiledInputInstancePolicy.PerBinding)));
        var outputNaming = new CompiledOutputNamingRequirement(
            profile.Output.FileNameTemplate,
            profile.Output.AllowOverride,
            MapOutputPolicy(profile.Output.InvalidCharacterPolicy),
            profile.Output.RequiredTokenIds);
        var identity = new V2CompiledCompositionIdentity(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.Experience.ExperienceId,
            profile.CompositionKind,
            new V2CompiledCompositionDetails(provenance, inputContract, regionAccess.Contract, outputNaming));
        return V2CompositionPlanCompileResult.Succeeded(CompiledComposition.CreateV2(
            plan,
            identity,
            CompiledIcNumberPolicies.From(profile.IcNumberInputMode)));
    }

    private static bool IsRuntimeReferenceReplaceProfile(CompositionProfileDefinition profile)
    {
        bool isGeneralReplace = StringComparer.Ordinal.Equals(
            profile.Experience.ExperienceId,
            ExperienceIds.GeneralReplace);
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            profile.Experience.ExperienceId,
            ExperienceIds.CtrlRamReplace);
        return profile.CompilationContext is RuntimeReferenceReplaceProfileCompilationContext &&
            profile.CompositionKind == CompositionKind.Replace &&
            ((isGeneralReplace &&
              profile.Experience.LayoutPolicy == LayoutPolicy.UserDefined &&
              profile.Experience.InputPolicy == InputPolicy.Extensible) ||
             (isCtrlRamReplace &&
              profile.Experience.LayoutPolicy == LayoutPolicy.Fixed &&
              profile.Experience.InputPolicy == InputPolicy.Fixed)) &&
            profile.MetadataBindings.Count == 0 &&
            profile.RegionAccessRules.Count != 0 &&
            profile.Validations.Count == 0 &&
            ((!isCtrlRamReplace && profile.ProcessorStages.Count == 0) ||
             profile.CompilationContext is RuntimeReferenceReplaceProfileCompilationContext
             {
                 AllowsConditionalProcessor: true,
             });
    }

    internal static bool TryGetRuntimeReferenceReplaceReferenceSlotId(
        CompositionProfileDefinition profile,
        out string referenceSlotId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        referenceSlotId = string.Empty;
        if (!IsRuntimeReferenceReplaceProfile(profile))
        {
            return false;
        }

        try
        {
            referenceSlotId = AssertRuntimeReferenceReplaceProfileShape(profile).ReferenceSlot.SlotId;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static RuntimeReferenceReplaceProfileShape AssertRuntimeReferenceReplaceProfileShape(
        CompositionProfileDefinition profile)
    {
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            profile.Experience.ExperienceId,
            ExperienceIds.CtrlRamReplace);
        CompositionProfileArtifactClass expectedSourceClass = isCtrlRamReplace
            ? CompositionProfileArtifactClass.CtrlRamReplacement
            : CompositionProfileArtifactClass.Auxiliary;
        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        CloneProfileInitializer clone = output.Capacity is RuntimeRequestProfileCapacity &&
            output.Initializer is CloneProfileInitializer initializer
            ? initializer
            : throw new InvalidOperationException("Validated runtime reference-replace profile has an invalid output space.");
        CompositionProfileInputSlot reference = profile.InputSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, clone.SourceSlotId));
        CompositionProfileInputSlot source = profile.InputSlots.Single(slot =>
            !StringComparer.Ordinal.Equals(slot.SlotId, clone.SourceSlotId));
        InputArtifactProfileSpace referenceSpace = profile.Spaces.OfType<InputArtifactProfileSpace>().Single(space =>
            StringComparer.Ordinal.Equals(space.SlotId, reference.SlotId));
        InputArtifactProfileSpace sourceSpace = profile.Spaces.OfType<InputArtifactProfileSpace>().Single(space =>
            StringComparer.Ordinal.Equals(space.SlotId, source.SlotId));
        return reference is not
        {
            Required: true,
            ArtifactClass: CompositionProfileArtifactClass.ReferenceImage,
            Cardinality: CompositionProfileSlotCardinality.ExactlyOne,
            LengthRule: ExactResolvedMapCapacityLengthRule,
            Normalization: NoInputNormalization,
        } ||
            source is not
            {
                Required: true,
                Cardinality: CompositionProfileSlotCardinality.OneOrMore,
                LengthRule: BoundedLengthRule { MinimumBytes: 1, MaximumBytes: int.MaxValue },
                Normalization: NoInputNormalization,
            } ||
            source.ArtifactClass != expectedSourceClass ||
            referenceSpace.InstancePolicy != CompositionProfileInstancePolicy.Singleton ||
            sourceSpace.InstancePolicy != CompositionProfileInstancePolicy.PerBinding
            ? throw new InvalidOperationException("Validated runtime reference-replace profile has an invalid input contract.")
            : new RuntimeReferenceReplaceProfileShape(
                reference,
                source,
                output,
                profile.Operations.OfType<RunProcessorProfileOperation>().SingleOrDefault());
    }

    private static Dictionary<string, V2RuntimeReferenceReplaceInputBinding> ValidateRuntimeReferenceReplaceBindings(
        RuntimeReferenceReplaceProfileShape shape,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        V2RuntimeReferenceReplaceCompileRequest request,
        List<CompositionIssue> issues)
    {
        var bindings = new Dictionary<string, V2RuntimeReferenceReplaceInputBinding>(StringComparer.Ordinal);
        int referenceCount = 0;
        int sourceCount = 0;
        foreach (V2RuntimeReferenceReplaceInputBinding? binding in request.Bindings)
        {
            bool isReference = binding is not null && StringComparer.Ordinal.Equals(binding.SlotId, shape.ReferenceSlot.SlotId);
            bool isSource = binding is not null && StringComparer.Ordinal.Equals(binding.SlotId, shape.SourceSlot.SlotId);
            bool valid = binding is not null &&
                !string.IsNullOrWhiteSpace(binding.BindingId) &&
                !StringComparer.Ordinal.Equals(binding.BindingId, shape.Output.SpaceId) &&
                (isReference || isSource) &&
                binding.ExactLengthBytes > 0 &&
                (isReference
                    ? binding.ExactLengthBytes == resolvedMap.CapacityBytes
                    : binding.ExactLengthBytes <= int.MaxValue) &&
                bindings.TryAdd(binding.BindingId, binding);
            if (!valid)
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceBindingInvalid,
                    "Runtime reference-replace bindings must be unique declared reference or source instances with valid exact lengths."));
                continue;
            }

            referenceCount += isReference ? 1 : 0;
            sourceCount += isSource ? 1 : 0;
        }

        if (referenceCount != 1 || sourceCount == 0)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceBindingInvalid,
                "Runtime reference-replace compilation requires exactly one map-capacity reference binding and one or more experience-owned source bindings."));
        }

        return bindings;
    }

    private static bool ValidateRuntimeReferenceReplaceMappings(
        RuntimeReferenceReplaceProfileShape shape,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        V2RuntimeReferenceReplaceCompileRequest request,
        Dictionary<string, V2RuntimeReferenceReplaceInputBinding> bindings,
        LoweredRegionAccess regionAccess,
        List<CompositionIssue> issues)
    {
        bool touchesTp = false;
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            resolvedMap.ModeId,
            ExperienceIds.CtrlRamReplace);

        var mappingIds = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new HashSet<int>();
        var referencedSourceBindingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExplicitMapping? mapping in request.Mappings)
        {
            if (mapping is null ||
                string.IsNullOrWhiteSpace(mapping.MappingId) ||
                !mappingIds.Add(mapping.MappingId) ||
                !sequences.Add(mapping.Sequence) ||
                mapping.OperationKind != ExplicitMappingOperationKind.ReplaceRange ||
                mapping.OverlapPolicy != OverlapPolicy.Reject ||
                !StringComparer.Ordinal.Equals(mapping.TargetSpaceId, shape.Output.SpaceId) ||
                mapping.TargetRegionId is not null ||
                mapping.Alignment != 1 ||
                mapping.SourceRange.Length != mapping.TargetRange.Length)
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceMappingInvalid,
                    "Runtime reference-replace mappings must be uniquely ordered unaligned ReplaceRange writes to the output without caller-owned region authority.",
                    mapping?.MappingId));
                continue;
            }

            if (!bindings.TryGetValue(mapping.SourceBindingId, out V2RuntimeReferenceReplaceInputBinding? source) ||
                !StringComparer.Ordinal.Equals(source.SlotId, shape.SourceSlot.SlotId))
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceMappingInvalid,
                    "Runtime reference-replace mappings must read one declared source binding, never the reference image.",
                    mapping.MappingId));
                continue;
            }

            _ = referencedSourceBindingIds.Add(source.BindingId);
            if (mapping.SourceRange.EndExclusive > source.ExactLengthBytes)
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceSourceOutOfBounds,
                    "Runtime reference-replace mapping source range escapes its concrete immutable binding.",
                    mapping.MappingId));
            }

            if (mapping.TargetRange.EndExclusive > resolvedMap.CapacityBytes)
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceTargetOutOfBounds,
                    "Runtime reference-replace mapping target range escapes the resolved physical image map.",
                    mapping.MappingId));
                continue;
            }

            if (!TryResolveGoverningRegionChain(
                    mapping.TargetRange,
                    regionAccess.RegionsById,
                    out FirmwareRegion[] governingRegionChain))
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceTargetOutOfBounds,
                    "Runtime reference-replace mapping target range is not contained by one canonical physical region chain.",
                    mapping.MappingId));
                continue;
            }

            FirmwareRegion governingRegion = governingRegionChain[^1];
            if (isCtrlRamReplace &&
                (governingRegion.Owner != FirmwareRegionOwner.Tp ||
                 governingRegion.Kind != FirmwareRegionKind.CtrlRam))
            {
                issues.Add(new CompositionIssue(
                    RuntimeReferenceCtrlRamTargetInvalid,
                    "CtrlRAM Replace mappings must target one canonical TP-owned CtrlRAM region.",
                    mapping.MappingId));
                continue;
            }

            touchesTp |= resolvedMap.ImageMap.Regions.Any(region =>
                region.Owner == FirmwareRegionOwner.Tp &&
                region.Range.Overlaps(mapping.TargetRange));

            _ = TryAuthorizeTargetWrite(
                mapping.MappingId,
                "runtime-request-target",
                new ResolvedView(shape.Output.SpaceId, mapping.TargetRange, governingRegionChain),
                regionAccess,
                issues);
        }

        int sourceBindingCount = bindings.Values.Count(binding =>
            StringComparer.Ordinal.Equals(binding.SlotId, shape.SourceSlot.SlotId));
        if (request.Mappings.Count == 0 || referencedSourceBindingIds.Count != sourceBindingCount)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceMappingInvalid,
                "Runtime reference-replace compilation requires mappings for every concrete auxiliary source binding.",
                "mappings"));
        }

        if (touchesTp && shape.ProcessorOperation is null)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceProcessorRequired,
                "A runtime reference Replace mapping touches a TP-owned canonical region, but the selected profile has no approved Legacy Combiner refresh stage.",
                "mappings"));
        }
        else if (touchesTp && request.Mappings.Any(mapping =>
                     mapping is not null && mapping.Sequence >= shape.ProcessorOperation!.Sequence))
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceProcessorOrderInvalid,
                "Every runtime reference Replace mapping must run before the profile-owned Legacy Combiner refresh stage.",
                shape.ProcessorOperation!.OperationId));
        }

        return touchesTp;
    }

    private static CompositionOperation[] NarrowRuntimeReferenceProcessorAuthority(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        IReadOnlyList<CompositionOperation> mappingOperations,
        CompositionOperation[] processorOperations)
    {
        if (!StringComparer.Ordinal.Equals(resolvedMap.ModeId, ExperienceIds.CtrlRamReplace) ||
            processorOperations.Length == 0)
        {
            return [.. processorOperations];
        }

        CompositionOperation processor = processorOperations.Single();
        ExternalProcessorInvocation declared = processor.ExternalProcessorInvocation!;
        ByteRange[] allowedWrites =
        [
            .. declared.AllowedWriteRanges.SelectMany(range =>
                IsCanonicalCtrlRamRange(resolvedMap.ImageMap, range)
                    ? mappingOperations
                        .Select(mapping => mapping.TargetRange.Intersect(range))
                        .Where(static overlap => overlap is not null)
                        .Select(static overlap => overlap!.Value)
                    : [range]),
        ];
        var invocation = new ExternalProcessorInvocation(
            declared.ProcessorId,
            declared.ToolBindingId,
            declared.AllowedReadRanges,
            allowedWrites,
            declared.StagedSourceBindings,
            declared.AllowedWriteRangeSections.Where(section =>
                allowedWrites.Any(range => range.Contains(section.Range))),
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

    private sealed record RuntimeReferenceReplaceProfileShape(
        CompositionProfileInputSlot ReferenceSlot,
        CompositionProfileInputSlot SourceSlot,
        MutableCompositionProfileSpace Output,
        RunProcessorProfileOperation? ProcessorOperation);
}
