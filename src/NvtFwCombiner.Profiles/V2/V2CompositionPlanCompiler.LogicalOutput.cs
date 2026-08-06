using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private const string LogicalProfileShapeInvalid = "profile.v2.logical.profile-shape-invalid";
    private const string LogicalMemberNotAdmitted = "profile.v2.logical.member-not-admitted";
    private const string LogicalBindingInvalid = "profile.v2.logical.binding-invalid";
    private const string LogicalMappingInvalid = "profile.v2.logical.mapping-invalid";
    private const string LogicalSourceOutOfBounds = "profile.v2.logical.source-out-of-bounds";
    private const string LogicalTargetOutOfBounds = "profile.v2.logical.target-out-of-bounds";

    /// <summary>Lowers one admitted logical-output General Merge request through the shared plan algebra.</summary>
    internal static V2CompositionPlanCompileResult CompileLogicalOutput(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        TrustedCompositionProfileCatalogEntry profileEntry,
        string memberId,
        V2LogicalOutputCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(profileEntry);
        ArgumentNullException.ThrowIfNull(request);
        CompositionProfileDefinition profile = profileEntry.Profile;
        if (!IsLogicalOutputProfile(profile))
        {
            return V2CompositionPlanCompileResult.Failed([
                new CompositionIssue(
                    LogicalProfileShapeInvalid,
                    "The selected trusted V2 profile is not a logical-output General Merge declaration.")]);
        }

        if (string.IsNullOrWhiteSpace(memberId) ||
            !profile.LogicalOutputBinding.MemberIds.Contains(memberId, StringComparer.Ordinal))
        {
            return V2CompositionPlanCompileResult.Failed([
                new CompositionIssue(
                    LogicalMemberNotAdmitted,
                    "The requested member is not admitted by the selected logical-output profile.")]);
        }

        var issues = new List<CompositionIssue>();
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
            new AddressSpace(
                output.SpaceId,
                request.OutputInitializer.Capacity,
                AddressSpaceMutability.Mutable),
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
            [request.OutputInitializer.ToImageInitialization(output.SpaceId)],
            output.SpaceId,
            spaces,
            operations);
        CompositionInputSlotDefinition inputSlot = AssertLogicalInputSlot(profile);
        return Succeed(
            profile,
            selection,
            new LogicalOutputV2CompilationContext(
                profile.LogicalOutputBinding.FamilyId,
                profile.LogicalOutputBinding.FamilyVersion,
                profile.LogicalOutputBinding.FamilyContentHash,
                memberId),
            plan,
            [MapLogicalInputSlot(inputSlot)],
            request.Bindings.Select(binding => new CompiledInputSpaceBinding(
                binding.BindingId,
                binding.SlotId,
                CompiledInputInstancePolicy.PerBinding)),
            new CompiledRegionAccessContract([], []),
            CompiledIcNumberPolicy.NotApplicable);
    }

    private static bool IsLogicalOutputProfile(CompositionProfileDefinition profile)
    {
        return profile.CompilationContext is LogicalOutputProfileCompilationContext &&
            profile.CompositionKind == CompositionKind.Merge &&
            StringComparer.Ordinal.Equals(profile.Experience.ExperienceId, ExperienceIds.GeneralMerge) &&
            profile.Experience.LayoutPolicy == LayoutPolicy.UserDefined &&
            profile.Experience.InputPolicy == InputPolicy.Extensible &&
            profile.Spaces.Count == 2 &&
            profile.Spaces.OfType<InputArtifactProfileSpace>().SingleOrDefault() is { InstancePolicy: CompiledInputInstancePolicy.PerBinding } &&
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

    private static CompositionInputSlotDefinition AssertLogicalInputSlot(CompositionProfileDefinition profile)
    {
        return profile.InputSlots.Count == 1 && profile.InputSlots[0] is
        {
            Required: true,
            Cardinality: CompiledInputSlotCardinality.OneOrMore,
            ArtifactClass: CompiledInputArtifactClass.Auxiliary,
            LengthRequirement: CompiledBoundedInputLengthRequirement { MinimumBytes: 1, MaximumBytes: int.MaxValue },
            Normalization: CompiledNoInputNormalization,
        } slot
            ? slot
            : throw new InvalidOperationException("Validated logical-output profile has an invalid input slot.");
    }

    private static CompiledInputSlotRequirement MapLogicalInputSlot(CompositionInputSlotDefinition slot)
    {
        return new CompiledInputSlotRequirement(
            slot,
            (CompiledBoundedInputLengthRequirement)slot.LengthRequirement);
    }

    private static void ValidateLogicalRequest(
        CompositionProfileDefinition profile,
        V2LogicalOutputCompileRequest request,
        List<CompositionIssue> issues)
    {
        CompositionInputSlotDefinition inputSlot = AssertLogicalInputSlot(profile);
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
                !StringComparer.Ordinal.Equals(
                    mapping.TargetSpaceId,
                    CompositionAddressSpaceIds.OutputImage) ||
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

            if (mapping.TargetRange.EndExclusive >
                request.OutputInitializer.Capacity)
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
