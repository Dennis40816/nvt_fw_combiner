using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Complete immutable map-independent composition-profile-v2 definition.</summary>
internal sealed partial class CompositionProfileDefinition
{
    private readonly CompositionProfileInputSlot[] _inputSlots;
    private readonly CompositionProfileInputSelectionGroup[] _inputSelectionGroups;
    private readonly CompositionProfileSpace[] _spaces;
    private readonly CompositionProfileView[] _views;
    private readonly CompositionProfileMetadataBinding[] _metadataBindings;
    private readonly CompositionProfileRegionAccess[] _regionAccessRules;
    private readonly CompositionProfileOperation[] _operations;
    private readonly CompositionProfileValidation[] _validations;
    private readonly CompositionProfileProcessorStage[] _processorStages;
    private readonly string[] _evidenceRefs;

    internal CompositionProfileDefinition(
        string profileId,
        string profileVersion,
        CompiledProfilePromotion promotion,
        CompositionKind compositionKind,
        IcNumberInputMode? icNumberInputMode,
        CompositionProfileExperience experience,
        CompositionProfileMapBinding mapBinding,
        IEnumerable<CompositionProfileInputSlot> inputSlots,
        IEnumerable<CompositionProfileSpace> spaces,
        IEnumerable<CompositionProfileView> views,
        IEnumerable<CompositionProfileMetadataBinding> metadataBindings,
        IEnumerable<CompositionProfileRegionAccess> regionAccessRules,
        IEnumerable<CompositionProfileOperation> operations,
        IEnumerable<CompositionProfileValidation> validations,
        IEnumerable<CompositionProfileProcessorStage> processorStages,
        CompositionProfileOutput output,
        IEnumerable<string> evidenceRefs,
        IEnumerable<CompositionProfileInputSelectionGroup>? inputSelectionGroups = null)
        : this(
            profileId,
            profileVersion,
            promotion,
            compositionKind,
            icNumberInputMode,
            experience,
            new ResolvedMapProfileCompilationContext(mapBinding),
            inputSlots,
            spaces,
            views,
            metadataBindings,
            regionAccessRules,
            operations,
            validations,
            processorStages,
            output,
            evidenceRefs,
            inputSelectionGroups)
    {
    }

    internal CompositionProfileDefinition(
        string profileId,
        string profileVersion,
        CompiledProfilePromotion promotion,
        CompositionKind compositionKind,
        IcNumberInputMode? icNumberInputMode,
        CompositionProfileExperience experience,
        CompositionProfileCompilationContext compilationContext,
        IEnumerable<CompositionProfileInputSlot> inputSlots,
        IEnumerable<CompositionProfileSpace> spaces,
        IEnumerable<CompositionProfileView> views,
        IEnumerable<CompositionProfileMetadataBinding> metadataBindings,
        IEnumerable<CompositionProfileRegionAccess> regionAccessRules,
        IEnumerable<CompositionProfileOperation> operations,
        IEnumerable<CompositionProfileValidation> validations,
        IEnumerable<CompositionProfileProcessorStage> processorStages,
        CompositionProfileOutput output,
        IEnumerable<string> evidenceRefs,
        IEnumerable<CompositionProfileInputSelectionGroup>? inputSelectionGroups = null)
    {
        ProfileId = CompositionProfileValueRules.RequireId(profileId, nameof(profileId));
        ProfileVersion = CompositionProfileValueRules.RequireSemanticVersion(
            profileVersion,
            nameof(profileVersion));
        ArgumentNullException.ThrowIfNull(promotion);
        if (!Enum.IsDefined(compositionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(compositionKind), compositionKind, "Unknown composition kind.");
        }

        if (icNumberInputMode is { } declaredIcNumberInputMode && !Enum.IsDefined(declaredIcNumberInputMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(icNumberInputMode),
                declaredIcNumberInputMode,
                "Unknown IC-number input mode.");
        }

        ArgumentNullException.ThrowIfNull(experience);
        ArgumentNullException.ThrowIfNull(compilationContext);
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
        if (_spaces.Length < 2)
        {
            throw new ArgumentException("Profiles require at least two address spaces.", nameof(spaces));
        }

        bool isRuntimeLowered = compilationContext is
            LogicalOutputProfileCompilationContext or
            RuntimeReferenceReplaceProfileCompilationContext;
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
        if (_operations.Select(static operation => operation.Sequence).Distinct().Count() != _operations.Length)
        {
            throw new ArgumentException("Operation sequences must be unique.", nameof(operations));
        }

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
        _evidenceRefs = CompositionProfileValueRules.SnapshotIds(
            evidenceRefs,
            nameof(evidenceRefs),
            requireValue: true);

        Promotion = promotion;
        CompositionKind = compositionKind;
        IcNumberInputMode = icNumberInputMode;
        Experience = experience;
        CompilationContext = compilationContext;
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
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);

        ValidateReferenceGraph();
    }

    internal string ProfileId { get; }

    internal string ProfileVersion { get; }

    internal CompiledProfilePromotion Promotion { get; }

    internal CompositionKind CompositionKind { get; }

    /// <summary>Replace-only profile authority for the caller's IC-number selection.</summary>
    internal IcNumberInputMode? IcNumberInputMode { get; }

    internal CompositionProfileExperience Experience { get; }

    internal CompositionProfileCompilationContext CompilationContext { get; }

    /// <summary>Exact map binding for map-bound profiles only.</summary>
    internal CompositionProfileMapBinding MapBinding => CompilationContext switch
    {
        ResolvedMapProfileCompilationContext mapBound => mapBound.MapBinding,
        RuntimeReferenceReplaceProfileCompilationContext runtimeReferenceReplace => runtimeReferenceReplace.MapBinding,
        _ => throw new InvalidOperationException("Logical-output profiles do not declare a physical map binding."),
    };

    /// <summary>Logical-output binding for General Merge profiles only.</summary>
    internal LogicalOutputProfileCompilationContext LogicalOutputBinding => CompilationContext as LogicalOutputProfileCompilationContext
        ?? throw new InvalidOperationException("This profile is not admitted through the logical-output context.");

    internal IReadOnlyList<CompositionProfileInputSlot> InputSlots { get; }

    internal IReadOnlyList<CompositionProfileInputSelectionGroup> InputSelectionGroups { get; }

    internal IReadOnlyList<CompositionProfileSpace> Spaces { get; }

    internal IReadOnlyList<CompositionProfileView> Views { get; }

    internal IReadOnlyList<CompositionProfileMetadataBinding> MetadataBindings { get; }

    internal IReadOnlyList<CompositionProfileRegionAccess> RegionAccessRules { get; }

    internal IReadOnlyList<CompositionProfileOperation> Operations { get; }

    internal IReadOnlyList<CompositionProfileValidation> Validations { get; }

    internal IReadOnlyList<CompositionProfileProcessorStage> ProcessorStages { get; }

    internal CompositionProfileOutput Output { get; }

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
        CompositionProfileOperation left,
        CompositionProfileOperation right)
    {
        int sequenceComparison = left.Sequence.CompareTo(right.Sequence);
        return sequenceComparison != 0
            ? sequenceComparison
            : StringComparer.Ordinal.Compare(left.OperationId, right.OperationId);
    }
}
