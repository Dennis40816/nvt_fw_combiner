using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private const string LogicalPreparationNotAdmitted = "profile.v2.logical.preparation-not-admitted";
    private const string LogicalProfileShapeInvalid = "profile.v2.logical.profile-shape-invalid";
    private const string LogicalOutputCapacityInvalid = "profile.v2.logical.output-capacity-invalid";
    private const string LogicalBindingInvalid = "profile.v2.logical.binding-invalid";
    private const string LogicalMappingInvalid = "profile.v2.logical.mapping-invalid";
    private const string LogicalSourceOutOfBounds = "profile.v2.logical.source-out-of-bounds";
    private const string LogicalTargetOutOfBounds = "profile.v2.logical.target-out-of-bounds";

    /// <summary>Lowers one admitted logical-output General Merge request through the shared plan algebra.</summary>
    internal static V2CompositionPlanCompileResult CompileLogicalOutput(
        V2LogicalOutputPreparationResult preparation,
        V2LogicalOutputCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(request);
        if (!preparation.IsAdmitted || preparation.Selection is null || preparation.Admission is null)
        {
            return V2CompositionPlanCompileResult.Failed([
                new CompositionIssue(
                    LogicalPreparationNotAdmitted,
                    "Logical-output plan lowering requires an admitted trusted preparation.")]);
        }

        CompositionProfileDefinition profile = preparation.Admission.ProfileEntry.Profile;
        var issues = new List<CompositionIssue>();
        if (!IsLogicalOutputProfile(profile))
        {
            issues.Add(new CompositionIssue(
                LogicalProfileShapeInvalid,
                "The admitted profile is not the closed logical-output General Merge shape."));
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        ValidateLogicalRequest(profile, request, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        AddressSpace[] spaces =
        [
            .. request.Bindings.Select(static binding => new AddressSpace(
                binding.BindingId,
                binding.ExactLengthBytes,
                AddressSpaceMutability.Immutable)),
            new AddressSpace(output.SpaceId, request.OutputCapacity, AddressSpaceMutability.Mutable),
        ];
        CompositionOperation[] operations =
        [
            .. request.Mappings.Select(static mapping => CompositionOperation.CopyRange(
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

        var plan = new CompositionPlan(
            [ImageInitialization.Blank(output.SpaceId, request.OutputCapacity, ((BlankProfileInitializer)output.Initializer).FillByte)],
            output.SpaceId,
            spaces,
            operations);
        CompositionProfileInputSlot inputSlot = AssertLogicalInputSlot(profile);
        var provenance = new V2CompilationProvenance(
            preparation.Selection.BundleIdentity,
            preparation.Selection.ProfileEntryIdentity,
            new LogicalOutputV2CompilationContext(
                profile.LogicalOutputBinding.FamilyId,
                profile.LogicalOutputBinding.FamilyVersion,
                profile.LogicalOutputBinding.FamilyContentHash,
                preparation.Admission.MemberId),
            new CompiledProfilePromotion(
                MapPromotionStage(profile.Promotion.Stage),
                profile.Promotion.Blockers.Select(MapPromotionBlocker)),
            profile.EvidenceRefs,
            [],
            []);
        var inputContract = new CompiledInputContract(
            [MapLogicalInputSlot(inputSlot)],
            request.Bindings.Select(binding => new CompiledInputSpaceBinding(
                binding.BindingId,
                binding.SlotId,
                CompiledInputInstancePolicy.PerBinding)));
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
            new V2CompiledCompositionDetails(
                provenance,
                inputContract,
                new CompiledRegionAccessContract([], []),
                outputNaming));
        return V2CompositionPlanCompileResult.Succeeded(CompiledComposition.CreateV2(
            plan,
            identity,
            CompiledIcNumberPolicy.NotApplicable));
    }

    private static bool IsLogicalOutputProfile(CompositionProfileDefinition profile)
    {
        return profile.CompilationContext is LogicalOutputProfileCompilationContext &&
            profile.CompositionKind == CompositionKind.Merge &&
            StringComparer.Ordinal.Equals(profile.Experience.ExperienceId, ExperienceIds.GeneralMerge) &&
            profile.Experience.LayoutPolicy == LayoutPolicy.UserDefined &&
            profile.Experience.InputPolicy == InputPolicy.Extensible &&
            profile.Spaces.Count == 2 &&
            profile.Spaces.OfType<InputArtifactProfileSpace>().SingleOrDefault() is { InstancePolicy: CompositionProfileInstancePolicy.PerBinding } &&
            AssertOutputSpace(profile) is
            {
                Capacity: RuntimeRequestProfileCapacity,
                Initializer: BlankProfileInitializer { FillByte: 0 },
            } &&
            profile.Views.Count == 0 &&
            profile.MetadataBindings.Count == 0 &&
            profile.RegionAccessRules.Count == 0 &&
            profile.Operations.Count == 0 &&
            profile.Validations.Count == 0 &&
            profile.ProcessorStages.Count == 0;
    }

    private static CompositionProfileInputSlot AssertLogicalInputSlot(CompositionProfileDefinition profile)
    {
        return profile.InputSlots.Count == 1 && profile.InputSlots[0] is
        {
            Required: true,
            Cardinality: CompositionProfileSlotCardinality.OneOrMore,
            ArtifactClass: CompositionProfileArtifactClass.Auxiliary,
            LengthRule: BoundedLengthRule { MinimumBytes: 1, MaximumBytes: int.MaxValue },
            Normalization: NoInputNormalization,
        } slot
            ? slot
            : throw new InvalidOperationException("Validated logical-output profile has an invalid input slot.");
    }

    private static CompiledInputSlotRequirement MapLogicalInputSlot(CompositionProfileInputSlot slot)
    {
        return new CompiledInputSlotRequirement(
            slot.SlotId,
            slot.Role,
            CompiledInputArtifactClass.Auxiliary,
            required: true,
            CompiledInputSlotCardinality.OneOrMore,
            slot.AcceptedExtensions,
            new CompiledBoundedInputLengthRequirement(1, int.MaxValue),
            new CompiledNoInputNormalization());
    }

    private static void ValidateLogicalRequest(
        CompositionProfileDefinition profile,
        V2LogicalOutputCompileRequest request,
        List<CompositionIssue> issues)
    {
        if (request.OutputCapacity <= 0)
        {
            issues.Add(new CompositionIssue(
                LogicalOutputCapacityInvalid,
                "Logical-output capacity must be a positive in-memory byte count.",
                CompositionAddressSpaceIds.OutputImage));
        }

        CompositionProfileInputSlot inputSlot = AssertLogicalInputSlot(profile);
        string outputSpaceId = AssertOutputSpace(profile).SpaceId;
        var bindings = new Dictionary<string, V2LogicalOutputInputBinding>(StringComparer.Ordinal);
        foreach (V2LogicalOutputInputBinding? binding in request.Bindings)
        {
            if (binding is null ||
                string.IsNullOrWhiteSpace(binding.BindingId) ||
                !StringComparer.Ordinal.Equals(binding.SlotId, inputSlot.SlotId) ||
                binding.ExactLengthBytes <= 0 ||
                StringComparer.Ordinal.Equals(binding.BindingId, outputSpaceId) ||
                !bindings.TryAdd(binding.BindingId, binding))
            {
                issues.Add(new CompositionIssue(
                    LogicalBindingInvalid,
                    "Logical-output bindings must be unique positive-length instances of the declared auxiliary slot."));
            }
        }

        if (bindings.Count == 0)
        {
            issues.Add(new CompositionIssue(
                LogicalBindingInvalid,
                "Logical-output compilation requires at least one concrete immutable input binding."));
        }

        var mappingIds = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new HashSet<int>();
        var referencedBindings = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExplicitMapping? mapping in request.Mappings)
        {
            if (mapping is null ||
                string.IsNullOrWhiteSpace(mapping.MappingId) ||
                !mappingIds.Add(mapping.MappingId) ||
                !sequences.Add(mapping.Sequence) ||
                mapping.OperationKind != ExplicitMappingOperationKind.CopyRange ||
                mapping.OverlapPolicy != OverlapPolicy.Reject ||
                !StringComparer.Ordinal.Equals(mapping.TargetSpaceId, outputSpaceId) ||
                mapping.TargetRegionId is not null ||
                mapping.SourceRange.Start % mapping.Alignment != 0 ||
                mapping.SourceRange.Length % mapping.Alignment != 0 ||
                mapping.TargetRange.Length % mapping.Alignment != 0)
            {
                issues.Add(new CompositionIssue(
                    LogicalMappingInvalid,
                    "Logical-output mappings must be uniquely ordered aligned CopyRange writes to the logical output with reject overlap.",
                    mapping?.MappingId));
                continue;
            }

            if (!bindings.TryGetValue(mapping.SourceBindingId, out V2LogicalOutputInputBinding? source))
            {
                issues.Add(new CompositionIssue(
                    LogicalMappingInvalid,
                    "Logical-output mapping names an unknown concrete source binding.",
                    mapping.MappingId));
                continue;
            }

            _ = referencedBindings.Add(source.BindingId);
            if (mapping.SourceRange.EndExclusive > source.ExactLengthBytes)
            {
                issues.Add(new CompositionIssue(
                    LogicalSourceOutOfBounds,
                    "Logical-output mapping source range escapes its concrete immutable binding.",
                    mapping.MappingId));
            }

            if (request.OutputCapacity > 0 && mapping.TargetRange.EndExclusive > request.OutputCapacity)
            {
                issues.Add(new CompositionIssue(
                    LogicalTargetOutOfBounds,
                    "Logical-output mapping target range escapes the requested output capacity.",
                    mapping.MappingId));
            }
        }

        if (request.Mappings.Count == 0 || referencedBindings.Count != bindings.Count)
        {
            issues.Add(new CompositionIssue(
                LogicalMappingInvalid,
                "Logical-output compilation requires one or more mappings and every concrete input binding must be referenced.",
                "mappings"));
        }
    }
}
