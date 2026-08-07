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
        ProfileBundleIdentity bundleIdentity,
        TrustedCompositionProfileCatalogEntry profileEntry,
        string memberId,
        V2LogicalOutputCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(bundleIdentity);
        ArgumentNullException.ThrowIfNull(profileEntry);
        ArgumentNullException.ThrowIfNull(request);
        CompositionProfileDefinition profile = profileEntry.Profile;
        if (profile.Header.CompilationContextKind != V2CompilationContextKind.LogicalOutput)
        {
            return V2CompositionPlanCompileResult.Failed([
                new CompositionIssue(
                    LogicalProfileShapeInvalid,
                    "The selected trusted V2 profile is not a logical-output General Merge declaration.")]);
        }

        if (string.IsNullOrWhiteSpace(memberId) ||
            !profile.LogicalOutputMemberIds.Contains(memberId, StringComparer.Ordinal))
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
        CompositionInputSlotDefinition inputSlot = profile.InputSlots[0];
        return Succeed(
            profile,
            bundleIdentity,
            profileEntry.EntryIdentity,
            new LogicalOutputV2CompilationContext(
                profile.Header.FamilyId,
                profile.Header.FamilyVersion,
                profile.Header.FamilyContentHash,
                memberId),
            plan,
            [MapLogicalInputSlot(inputSlot)],
            request.Bindings.Select(binding => new CompiledInputSpaceBinding(
                binding.BindingId,
                binding.SlotId,
                CompiledInputInstancePolicy.PerBinding)),
            new CompiledRegionAccessContract([], []));
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
        CompositionInputSlotDefinition inputSlot = profile.InputSlots[0];
        string outputSpaceId = AssertOutputSpace(profile).SpaceId;
        var bindings = new Dictionary<string, V2ExplicitMappingInputBinding>(StringComparer.Ordinal);
        foreach (V2ExplicitMappingInputBinding? binding in request.Bindings)
        {
            if (binding is null ||
                string.IsNullOrWhiteSpace(binding.BindingId) ||
                !StringComparer.Ordinal.Equals(binding.SlotId, inputSlot.SlotId) ||
                binding.ExactLengthBytes is <= 0 or > int.MaxValue ||
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
                !mappingIds.Add(mapping.MappingId) ||
                !sequences.Add(mapping.Sequence) ||
                mapping.OperationKind != ExplicitMappingOperationKind.CopyRange ||
                mapping.OverlapPolicy != OverlapPolicy.Reject ||
                !StringComparer.Ordinal.Equals(
                    mapping.TargetSpaceId,
                    CompositionAddressSpaceIds.OutputImage) ||
                mapping.TargetRegionId is not null ||
                mapping.SourceRange.Start % mapping.Alignment != 0 ||
                mapping.SourceRange.Length % mapping.Alignment != 0)
            {
                issues.Add(new CompositionIssue(
                    LogicalMappingInvalid,
                    "Logical-output mappings must be uniquely ordered aligned CopyRange writes to the logical output with reject overlap.",
                    mapping?.MappingId));
                continue;
            }

            if (!bindings.TryGetValue(mapping.SourceBindingId, out V2ExplicitMappingInputBinding? source))
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
