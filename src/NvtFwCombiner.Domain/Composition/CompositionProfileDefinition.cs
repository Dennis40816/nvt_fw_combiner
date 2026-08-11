namespace NvtFwCombiner.Domain.Composition;

/// <summary>Complete immutable map-independent composition-profile-v2 definition.</summary>
internal sealed partial class CompositionProfileDefinition
{
    private readonly CompositionInputSlotDefinition[] _inputSlots;
    private readonly InputSelectionGroupDefinition[] _inputSelectionGroups;
    private readonly CompositionProfileSpace[] _spaces;
    private readonly CompositionProfileView[] _views;
    private readonly CompositionProfileMetadataBinding[] _metadataBindings;
    private readonly CompositionProfileRegionAccess[] _regionAccessRules;
    private readonly CompositionOperationDefinition[] _operations;
    private readonly ValidationRequirementDefinition[] _validations;
    private readonly CompositionProfileProcessorStage[] _processorStages;

    internal CompositionProfileDefinition(
        string profileId,
        string profileVersion,
        CompiledProfilePromotion promotion,
        CompositionKind compositionKind,
        IcNumberInputMode? icNumberInputMode,
        CompositionProfileHeader header,
        IEnumerable<CompositionInputSlotDefinition> inputSlots,
        IEnumerable<CompositionProfileSpace> spaces,
        IEnumerable<CompositionProfileView> views,
        IEnumerable<CompositionProfileMetadataBinding> metadataBindings,
        IEnumerable<CompositionProfileRegionAccess> regionAccessRules,
        IEnumerable<CompositionOperationDefinition> operations,
        IEnumerable<ValidationRequirementDefinition> validations,
        IEnumerable<CompositionProfileProcessorStage> processorStages,
        CompiledOutputNamingRequirement output,
        IEnumerable<string> evidenceRefs,
        IEnumerable<InputSelectionGroupDefinition>? inputSelectionGroups = null)
    {
        ProfileId = CanonicalPolicyValueRules.RequireCanonicalId(profileId, nameof(profileId));
        ProfileVersion = CanonicalProfileValueRules.RequireSemanticVersion(
            profileVersion,
            nameof(profileVersion));
        ArgumentNullException.ThrowIfNull(promotion);
        ClosedEnum.ThrowIfUndefined(compositionKind, "Unknown composition kind.");

        if (icNumberInputMode is { } declaredIcNumberInputMode && !ClosedEnum.IsDefined(declaredIcNumberInputMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(icNumberInputMode),
                declaredIcNumberInputMode,
                "Unknown IC-number input mode.");
        }

        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(output);

        _inputSlots = SnapshotUnique(
            inputSlots,
            static slot => slot.SlotId,
            nameof(inputSlots),
            requireValue: true);
        _inputSelectionGroups = SnapshotUnique(
            inputSelectionGroups ?? [],
            static group => group.GroupId,
            nameof(inputSelectionGroups),
            requireValue: false);
        _spaces = SnapshotUnique(
            spaces,
            static space => space.SpaceId,
            nameof(spaces),
            requireValue: true);
        DomainInvariant.Reject(_spaces.Length < 2, "Profiles require at least two address spaces.", nameof(spaces));

        bool isRuntimeLowered = header.CompilationContextKind is
            V2CompilationContextKind.LogicalOutput or
            V2CompilationContextKind.RuntimeReferenceReplace;
        _views = SnapshotUnique(
            views,
            static view => view.ViewId,
            nameof(views),
            requireValue: !isRuntimeLowered);
        _metadataBindings = SnapshotUnique(
            metadataBindings,
            static binding => binding.BindingId,
            nameof(metadataBindings),
            requireValue: false);
        _regionAccessRules = SnapshotUnique(
            regionAccessRules,
            static rule => rule.RegionId,
            nameof(regionAccessRules),
            requireValue: false);
        _operations = SnapshotUnique(
            operations,
            static operation => operation.OperationId,
            nameof(operations),
            requireValue: !isRuntimeLowered);
        DomainInvariant.Reject(
            _operations.Select(static operation => operation.Sequence).Distinct().Count() != _operations.Length,
            "Operation sequences must be unique.", nameof(operations));

        Array.Sort(_operations, CompareOperations);
        _validations = SnapshotUnique(
            validations,
            static validation => validation.RuleId,
            nameof(validations),
            requireValue: false);
        _processorStages = SnapshotUnique(
            processorStages,
            static processor => processor.ProcessorStageId,
            nameof(processorStages),
            requireValue: false);
        string[] evidenceRefsSnapshot = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            evidenceRefs,
            nameof(evidenceRefs),
            requireValue: true);

        Promotion = promotion;
        CompositionKind = compositionKind;
        IcNumberInputMode = icNumberInputMode;
        Header = header;
        InputSlots = Array.AsReadOnly(_inputSlots);
        InputSelectionGroups = Array.AsReadOnly(_inputSelectionGroups);
        Spaces = Array.AsReadOnly(_spaces);
        Views = Array.AsReadOnly(_views);
        MetadataBindings = Array.AsReadOnly(_metadataBindings);
        RegionAccessRules = Array.AsReadOnly(_regionAccessRules);
        Operations = Array.AsReadOnly(_operations);
        Validations = Array.AsReadOnly(_validations);
        ProcessorStages = Array.AsReadOnly(_processorStages);
        Output = output;
        EvidenceRefs = Array.AsReadOnly(evidenceRefsSnapshot);

        ValidateReferenceGraph();
    }

    internal string ProfileId { get; }

    internal string ProfileVersion { get; }

    internal CompiledProfilePromotion Promotion { get; }

    internal CompositionKind CompositionKind { get; }

    /// <summary>Replace-only profile authority for the caller's IC-number selection.</summary>
    internal IcNumberInputMode? IcNumberInputMode { get; }

    internal CompositionProfileHeader Header { get; }

    /// <summary>Exact map binding for map-bound profiles only.</summary>
    internal CompositionProfileMapBinding MapBinding => Header.MapBinding
        ?? throw new InvalidOperationException("Logical-output profiles do not declare a physical map binding.");

    /// <summary>Logical-output family members for General Merge profiles only.</summary>
    internal IReadOnlyList<string> LogicalOutputMemberIds =>
        Header.CompilationContextKind == V2CompilationContextKind.LogicalOutput
            ? Header.LogicalOutputMemberIds
            : throw new InvalidOperationException("This profile is not admitted through the logical-output context.");

    internal IReadOnlyList<CompositionInputSlotDefinition> InputSlots { get; }

    internal IReadOnlyList<InputSelectionGroupDefinition> InputSelectionGroups { get; }

    internal IReadOnlyList<CompositionProfileSpace> Spaces { get; }

    internal IReadOnlyList<CompositionProfileView> Views { get; }

    internal IReadOnlyList<CompositionProfileMetadataBinding> MetadataBindings { get; }

    internal IReadOnlyList<CompositionProfileRegionAccess> RegionAccessRules { get; }

    internal IReadOnlyList<CompositionOperationDefinition> Operations { get; }

    internal IReadOnlyList<ValidationRequirementDefinition> Validations { get; }

    internal IReadOnlyList<CompositionProfileProcessorStage> ProcessorStages { get; }

    internal CompiledOutputNamingRequirement Output { get; }

    internal IReadOnlyList<string> EvidenceRefs { get; }

    private static T[] SnapshotUnique<T>(
        IEnumerable<T> values,
        Func<T, string> idSelector,
        string parameterName,
        bool requireValue)
        where T : class
    {
        T[] snapshot = ImmutableReferenceSnapshot.Create(
            values,
            "Values cannot contain null.",
            parameterName: parameterName);
        return requireValue && snapshot.Length == 0
            ? throw new ArgumentException("At least one value is required.", parameterName)
            : snapshot.Select(idSelector).Distinct(StringComparer.Ordinal).Count() != snapshot.Length
            ? throw new ArgumentException("Value identifiers must be ordinally unique.", parameterName)
            : snapshot;
    }

    private static int CompareOperations(
        CompositionOperationDefinition left,
        CompositionOperationDefinition right)
    {
        int sequenceComparison = left.Sequence.CompareTo(right.Sequence);
        return sequenceComparison != 0
            ? sequenceComparison
            : StringComparer.Ordinal.Compare(left.OperationId, right.OperationId);
    }
}
