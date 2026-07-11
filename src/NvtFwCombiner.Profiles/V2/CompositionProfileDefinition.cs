using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Complete immutable map-independent composition-profile-v2 definition.</summary>
internal sealed partial class CompositionProfileDefinition
{
    private readonly CompositionProfileInputSlot[] _inputSlots;
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
        CompositionProfilePromotion promotion,
        CompositionKind compositionKind,
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
        IEnumerable<string> evidenceRefs)
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

        ArgumentNullException.ThrowIfNull(experience);
        ArgumentNullException.ThrowIfNull(mapBinding);
        ArgumentNullException.ThrowIfNull(output);

        _inputSlots = SnapshotUnique(
            inputSlots,
            static slot => slot.SlotId,
            nameof(inputSlots),
            requireValue: true);
        _spaces = SnapshotUnique(
            spaces,
            static space => space.SpaceId,
            nameof(spaces),
            requireValue: true);
        if (_spaces.Length < 2)
        {
            throw new ArgumentException("Profiles require at least two address spaces.", nameof(spaces));
        }

        _views = SnapshotUnique(
            views,
            static view => view.ViewId,
            nameof(views),
            requireValue: true);
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
            requireValue: true);
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
        Experience = experience;
        MapBinding = mapBinding;
        InputSlots = Array.AsReadOnly(_inputSlots);
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

    internal CompositionProfilePromotion Promotion { get; }

    internal CompositionKind CompositionKind { get; }

    internal CompositionProfileExperience Experience { get; }

    internal CompositionProfileMapBinding MapBinding { get; }

    internal IReadOnlyList<CompositionProfileInputSlot> InputSlots { get; }

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
        ArgumentNullException.ThrowIfNull(values, parameterName);
        T[] snapshot = [.. values];
        return requireValue && snapshot.Length == 0
            ? throw new ArgumentException("At least one value is required.", parameterName)
            : snapshot.Any(static value => value is null)
            ? throw new ArgumentException("Values cannot contain null.", parameterName)
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
