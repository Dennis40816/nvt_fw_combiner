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

    /// <summary>Lowers one admitted map-bound General Replace request through the shared plan algebra.</summary>
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
                    "The admitted profile is not the closed map-bound runtime reference-replace General Replace shape.")]);
        }

        RuntimeReferenceReplaceProfileShape shape = AssertRuntimeReferenceReplaceProfileShape(profile);
        LoweredRegionAccess regionAccess = LowerRegionAccess(
            profile,
            resolvedMap,
            new Dictionary<string, ResolvedView>(StringComparer.Ordinal),
            issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        ValidateRuntimeReferenceReplaceRequest(shape, resolvedMap, request, regionAccess, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        var bindings = request.Bindings.ToDictionary(
            static binding => binding.BindingId,
            StringComparer.Ordinal);
        AddressSpace[] spaces =
        [
            .. bindings.Values.Select(static binding => new AddressSpace(
                binding.BindingId,
                binding.ExactLengthBytes,
                AddressSpaceMutability.Immutable)),
            new AddressSpace(
                shape.Output.SpaceId,
                resolvedMap.CapacityBytes,
                AddressSpaceMutability.Mutable),
        ];
        CompositionOperation[] operations =
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
        ValidateOperationOverlaps(operations, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        V2RuntimeReferenceReplaceInputBinding referenceBinding = bindings.Values.Single(binding =>
            StringComparer.Ordinal.Equals(binding.SlotId, shape.ReferenceSlot.SlotId));
        var plan = new CompositionPlan(
            [ImageInitialization.Reference(shape.Output.SpaceId, referenceBinding.BindingId, resolvedMap.CapacityBytes)],
            shape.Output.SpaceId,
            spaces,
            operations);
        var promotion = new CompiledProfilePromotion(
            MapPromotionStage(profile.Promotion.Stage),
            profile.Promotion.Blockers.Select(MapPromotionBlocker));
        var provenance = new V2CompilationProvenance(
            preparation.Selection.BundleIdentity,
            preparation.Selection.ProfileEntryIdentity,
            new RuntimeReferenceReplaceV2CompilationContext(resolvedMap),
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
        return profile.CompilationContext is RuntimeReferenceReplaceProfileCompilationContext &&
            profile.CompositionKind == CompositionKind.Replace &&
            StringComparer.Ordinal.Equals(profile.Experience.ExperienceId, ExperienceIds.GeneralReplace) &&
            profile.Experience.LayoutPolicy == LayoutPolicy.UserDefined &&
            profile.Experience.InputPolicy == InputPolicy.Extensible &&
            profile.Views.Count == 0 &&
            profile.MetadataBindings.Count == 0 &&
            profile.RegionAccessRules.Count != 0 &&
            profile.Operations.Count == 0 &&
            profile.Validations.Count == 0 &&
            profile.ProcessorStages.Count == 0;
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
                ArtifactClass: CompositionProfileArtifactClass.Auxiliary,
                Cardinality: CompositionProfileSlotCardinality.OneOrMore,
                LengthRule: BoundedLengthRule { MinimumBytes: 1, MaximumBytes: int.MaxValue },
                Normalization: NoInputNormalization,
            } ||
            referenceSpace.InstancePolicy != CompositionProfileInstancePolicy.Singleton ||
            sourceSpace.InstancePolicy != CompositionProfileInstancePolicy.PerBinding
            ? throw new InvalidOperationException("Validated runtime reference-replace profile has an invalid input contract.")
            : new RuntimeReferenceReplaceProfileShape(reference, source, output);
    }

    private static void ValidateRuntimeReferenceReplaceRequest(
        RuntimeReferenceReplaceProfileShape shape,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        V2RuntimeReferenceReplaceCompileRequest request,
        LoweredRegionAccess regionAccess,
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
                    "Runtime reference-replace bindings must be unique declared reference or auxiliary source instances with valid exact lengths."));
                continue;
            }

            referenceCount += isReference ? 1 : 0;
            sourceCount += isSource ? 1 : 0;
        }

        if (referenceCount != 1 || sourceCount == 0)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceBindingInvalid,
                "Runtime reference-replace compilation requires exactly one map-capacity reference binding and one or more auxiliary source bindings."));
        }

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
                    "Runtime reference-replace mappings must read one declared auxiliary source binding, never the reference image.",
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

            _ = TryAuthorizeTargetWrite(
                mapping.MappingId,
                "runtime-request-target",
                new ResolvedView(shape.Output.SpaceId, mapping.TargetRange, governingRegionChain),
                regionAccess,
                issues);
        }

        if (request.Mappings.Count == 0 || referencedSourceBindingIds.Count != sourceCount)
        {
            issues.Add(new CompositionIssue(
                RuntimeReferenceMappingInvalid,
                "Runtime reference-replace compilation requires mappings for every concrete auxiliary source binding.",
                "mappings"));
        }
    }

    private sealed record RuntimeReferenceReplaceProfileShape(
        CompositionProfileInputSlot ReferenceSlot,
        CompositionProfileInputSlot SourceSlot,
        MutableCompositionProfileSpace Output);
}
