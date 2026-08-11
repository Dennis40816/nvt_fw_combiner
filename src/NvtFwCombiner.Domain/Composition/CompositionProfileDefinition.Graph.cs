using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

internal sealed partial class CompositionProfileDefinition
{
    private void ValidateReferenceGraph()
    {
        Dictionary<string, CompositionInputSlotDefinition> slots = _inputSlots.ToDictionary(
            static slot => slot.SlotId,
            StringComparer.Ordinal);
        Dictionary<string, CompositionProfileSpace> spaces = _spaces.ToDictionary(
            static space => space.SpaceId,
            StringComparer.Ordinal);
        Dictionary<string, CompositionProfileView> views = _views.ToDictionary(
            static view => view.ViewId,
            StringComparer.Ordinal);
        Dictionary<string, CompositionProfileMetadataBinding> metadataBindings =
            _metadataBindings.ToDictionary(static binding => binding.BindingId, StringComparer.Ordinal);
        Dictionary<string, CompositionProfileProcessorStage> processors =
            _processorStages.ToDictionary(static processor => processor.ProcessorStageId, StringComparer.Ordinal);

        ValidateIcNumberInputMode();
        ValidateOutputSpace();
        ValidateInputPolicy();
        ValidateInputSelectionGroups(slots);
        ValidateSpaces(slots);
        if (Header.CompilationContextKind == V2CompilationContextKind.LogicalOutput)
        {
            ValidateLogicalOutputShape();
            return;
        }

        if (Header.CompilationContextKind == V2CompilationContextKind.RuntimeReferenceReplace)
        {
            ValidateRuntimeReferenceReplaceShape();
            ValidateViews(spaces);
            ValidateRegionAccess();
            ValidateOperations(views, spaces, processors);
            ValidateProcessors(views, spaces);
            return;
        }

        ValidateViews(spaces);
        ValidateMetadataBindings(spaces);
        ValidateRegionAccess();
        ValidateOperations(views, spaces, processors);
        ValidateProcessors(views, spaces);
        ValidateValidations(views, metadataBindings);
    }

    private void ValidateLogicalOutputShape()
    {
        DomainInvariant.Require(
            CompositionKind == CompositionKind.Merge &&
            Header.LayoutPolicy == LayoutPolicy.UserDefined &&
            Header.InputPolicy == InputPolicy.Extensible &&
            _metadataBindings.Length == 0 &&
            _regionAccessRules.Length == 0 &&
            _operations.Length == 0 &&
            _validations.Length == 0 &&
            _processorStages.Length == 0 &&
            _views.Length == 0,
            "Logical-output profiles are restricted to the General Merge declarative shape without physical-map declarations.");

        InputArtifactProfileSpace[] inputSpaces = [.. _spaces.OfType<InputArtifactProfileSpace>()];
        MutableCompositionProfileSpace output = _spaces.OfType<MutableCompositionProfileSpace>().Single(space =>
            space.Kind == CompositionProfileSpaceKind.OutputImage);
        DomainInvariant.Require(
            _inputSlots.Length == 1 &&
            inputSpaces.Length == 1 &&
            inputSpaces[0].InstancePolicy == CompiledInputInstancePolicy.PerBinding &&
            output.Capacity is RuntimeRequestProfileCapacity &&
            output.Initializer is BlankProfileInitializer { FillByte: 0 } &&
            _spaces.Length == 2,
            "Logical-output profiles require one per-binding input template and one zero-filled runtime-request output.");

        CompositionInputSlotDefinition slot = _inputSlots[0];
        DomainInvariant.Require(
            slot.Required &&
            slot.Cardinality == CompiledInputSlotCardinality.OneOrMore &&
            slot.ArtifactClass == CompiledInputArtifactClass.Auxiliary &&
            slot.Normalization is CompiledNoInputNormalization &&
            slot.LengthRequirement is CompiledBoundedInputLengthRequirement
            {
                MinimumBytes: 1,
                MaximumBytes: int.MaxValue,
            } &&
            StringComparer.Ordinal.Equals(inputSpaces[0].SlotId, slot.SlotId),
            "Logical-output profiles require one unnormalized auxiliary one-or-more input slot bounded to the in-memory composition limit.");
        DomainInvariant.Require(
            Promotion.Stage < CompiledProfilePromotionStage.Supported,
            "Logical-output profiles cannot be marked supported before runtime per-binding admission exists.");
    }

    private void ValidateRuntimeReferenceReplaceShape()
    {
        MutableCompositionProfileSpace output = _spaces.OfType<MutableCompositionProfileSpace>().Single(space =>
            space.Kind == CompositionProfileSpaceKind.OutputImage);
        bool processorFree = _views.Length == 0 && _operations.Length == 0 && _processorStages.Length == 0;
        bool conditionalProcessor = Header.AllowsConditionalProcessor &&
            HasValidRuntimeReferenceProcessorShape(output);
        bool userDefinedAuthoring = Header.LayoutPolicy == LayoutPolicy.UserDefined &&
            Header.InputPolicy == InputPolicy.Extensible;
        bool fixedProcessorAuthoring = Header.LayoutPolicy == LayoutPolicy.Fixed &&
            Header.InputPolicy == InputPolicy.Fixed &&
            conditionalProcessor;
        DomainInvariant.Require(
            CompositionKind == CompositionKind.Replace &&
            (userDefinedAuthoring || fixedProcessorAuthoring) &&
            _metadataBindings.Length == 0 &&
            _validations.Length == 0 &&
            _regionAccessRules.Length != 0 &&
            (processorFree || conditionalProcessor),
            "Runtime reference-replace profiles require a closed user-defined source or fixed processor mapping shape.");

        InputArtifactProfileSpace[] inputs = [.. _spaces.OfType<InputArtifactProfileSpace>()];
        DomainInvariant.Require(
            _inputSlots.Length == 2 && inputs.Length == 2 && _spaces.Length == 3 &&
            output.Capacity is RuntimeRequestProfileCapacity &&
            output.Initializer is CloneProfileInitializer,
            "Runtime reference-replace profiles require two immutable inputs and one runtime-capacity output cloned from the reference slot.");
        var clone = (CloneProfileInitializer)output.Initializer;

        CompositionInputSlotDefinition? reference = _inputSlots.SingleOrDefault(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, clone.SourceSlotId));
        CompositionInputSlotDefinition? source = _inputSlots.SingleOrDefault(slot =>
            !StringComparer.Ordinal.Equals(slot.SlotId, clone.SourceSlotId));
        InputArtifactProfileSpace? referenceSpace = inputs.SingleOrDefault(space =>
            StringComparer.Ordinal.Equals(space.SlotId, clone.SourceSlotId));
        InputArtifactProfileSpace? sourceSpace = inputs.SingleOrDefault(space =>
            !StringComparer.Ordinal.Equals(space.SlotId, clone.SourceSlotId));
        bool sourcePolicyIsValid =
            (userDefinedAuthoring && source is
            {
                ArtifactClass: CompiledInputArtifactClass.Auxiliary,
                Normalization: CompiledNoInputNormalization,
            }) ||
            (fixedProcessorAuthoring && source is
            {
                ArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement,
                Normalization: CompiledTruncateCtrlRamInputNormalization,
            });
        DomainInvariant.Require(
            reference is
            {
                Required: true,
                ArtifactClass: CompiledInputArtifactClass.ReferenceImage,
                Cardinality: CompiledInputSlotCardinality.ExactlyOne,
                LengthRequirement: ResolvedMapCapacityInputLengthDefinition,
                Normalization: CompiledNoInputNormalization,
            } &&
            source is
            {
                Required: true,
                Cardinality: CompiledInputSlotCardinality.OneOrMore,
                LengthRequirement: CompiledBoundedInputLengthRequirement { MinimumBytes: 1, MaximumBytes: int.MaxValue },
            } &&
            sourcePolicyIsValid &&
            referenceSpace is { InstancePolicy: CompiledInputInstancePolicy.Singleton } &&
            sourceSpace is { InstancePolicy: CompiledInputInstancePolicy.PerBinding },
            "Runtime reference-replace profiles require one exact singleton reference and one typed per-binding source with its closed normalization policy.");
        DomainInvariant.Require(
            Promotion.Stage < CompiledProfilePromotionStage.Supported,
            "Runtime reference-replace profiles cannot be marked supported before runtime request routing and owner evidence are complete.");
    }

    private bool HasValidRuntimeReferenceProcessorShape(MutableCompositionProfileSpace output)
    {
        if (_operations is not [var operation] ||
            operation.Kind != CompositionOperationKind.RunExternalProcessor ||
            _processorStages is not [LegacyCombinerProfileProcessorStage processor] ||
            operation.Sequence != int.MaxValue ||
            operation.OverlapPolicy != OverlapPolicy.ReplaceExisting ||
            !StringComparer.Ordinal.Equals(operation.ProcessorStageId, processor.ProcessorStageId) ||
            !StringComparer.Ordinal.Equals(processor.TargetSpaceId, output.SpaceId) ||
            processor.Purpose != CompositionProfileProcessorPurpose.HeaderAndIntegrity ||
            processor.IntegrityDisposition != CompositionProfileIntegrityDisposition.RecalculateAndWrite ||
            processor.TargetViewId is null ||
            processor.StagedSourceBindings.Count != 0 ||
            processor.StagedArtifactBindings.Count != 0 ||
            _views.Any(view => !StringComparer.Ordinal.Equals(view.SpaceId, output.SpaceId)))
        {
            return false;
        }

        var referencedViewIds = new HashSet<string>(processor.AllowedReadViewIds, StringComparer.Ordinal);
        referencedViewIds.UnionWith(processor.AllowedWriteViewIds);
        _ = referencedViewIds.Add(processor.TargetViewId);
        return referencedViewIds.SetEquals(_views.Select(static view => view.ViewId));
    }

    private void ValidateIcNumberInputMode()
    {
        DomainInvariant.Require(
            CompositionKind != CompositionKind.Merge || IcNumberInputMode is null,
            "Merge profiles cannot declare an IC-number input mode.");
        DomainInvariant.Require(
            CompositionKind != CompositionKind.Replace || IcNumberInputMode is not null,
            "Replace profiles require an IC-number input mode.");
    }

    private void ValidateOutputSpace()
    {
        MutableCompositionProfileSpace[] outputs =
        [
            .. _spaces.OfType<MutableCompositionProfileSpace>()
                .Where(static space => space.Kind == CompositionProfileSpaceKind.OutputImage),
        ];
        DomainInvariant.Require(outputs.Length == 1, "Profiles require exactly one output-image space.");

        bool initializerMatchesComposition = CompositionKind == CompositionKind.Merge
            ? outputs[0].Initializer is BlankProfileInitializer
            : outputs[0].Initializer is CloneProfileInitializer;
        DomainInvariant.Require(
            initializerMatchesComposition,
            "The output initializer must match merge versus replace semantics.");
    }

    private void ValidateInputPolicy()
    {
        bool extractsDeclaredPrefix = _inputSlots.Any(static slot =>
            slot.LengthRequirement is SourceViewCoverageInputLengthDefinition { RequiredEndExclusive: not null });
        DomainInvariant.Require(
            !extractsDeclaredPrefix || CompositionKind == CompositionKind.Merge,
            "Declared-prefix input authority requires Merge composition.");

        bool padsInput = _inputSlots.Any(static slot =>
            slot.Normalization is CompiledPadShorterInputNormalization);
        DomainInvariant.Require(
            !padsInput ||
            (CompositionKind == CompositionKind.Replace && _processorStages.Length == 0),
            "Short-input padding requires Replace composition without processor stages.");

        bool truncatesCtrlRam = _inputSlots.Any(static slot =>
            slot.Normalization is CompiledTruncateCtrlRamInputNormalization);
        DomainInvariant.Require(
            !truncatesCtrlRam ||
            (CompositionKind == CompositionKind.Replace &&
             !_inputSlots.Any(static slot =>
                 slot.Normalization is CompiledTruncateCtrlRamInputNormalization &&
                 slot.ArtifactClass != CompiledInputArtifactClass.CtrlRamReplacement)),
            "CtrlRAM truncation requires a typed CtrlRAM replacement source.");
    }

    private void ValidateInputSelectionGroups(
        IReadOnlyDictionary<string, CompositionInputSlotDefinition> slots)
    {
        var groupedSlotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (InputSelectionGroupDefinition group in _inputSelectionGroups)
        {
            foreach (string memberSlotId in group.MemberSlotIds)
            {
                CompositionInputSlotDefinition slot = RequireReference(
                    slots,
                    memberSlotId,
                    "Input selection group references an unknown slot.");
                DomainInvariant.Require(
                    !slot.Required && slot.Cardinality == CompiledInputSlotCardinality.ZeroOrOne,
                    "Input selection groups may reference only optional zero-or-one slots.");
                DomainInvariant.Require(
                    groupedSlotIds.Add(memberSlotId),
                    "An input slot may belong to only one selection group.");
            }
        }
    }

    internal bool IsInputSlotApplicable(
        string slotId,
        IReadOnlySet<string> availableRegionIds)
    {
        ArgumentNullException.ThrowIfNull(availableRegionIds);
        var inputSpaceIds = _spaces
            .OfType<InputArtifactProfileSpace>()
            .Where(space => StringComparer.Ordinal.Equals(space.SlotId, slotId))
            .Select(static space => space.SpaceId)
            .ToHashSet(StringComparer.Ordinal);
        return _views
            .Where(view => inputSpaceIds.Contains(view.SpaceId))
            .All(view => view.MapRegionId is not string regionId || availableRegionIds.Contains(regionId));
    }

    internal bool AreSelectedOptionalInputSlotsApplicable(
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlySet<string> availableRegionIds)
    {
        ArgumentNullException.ThrowIfNull(selectedSlotIds);
        var selected = selectedSlotIds.ToHashSet(StringComparer.Ordinal);
        return _inputSelectionGroups
            .SelectMany(static group => group.MemberSlotIds)
            .Where(selected.Contains)
            .All(slotId => IsInputSlotApplicable(slotId, availableRegionIds));
    }

    private void ValidateSpaces(IReadOnlyDictionary<string, CompositionInputSlotDefinition> slots)
    {
        HashSet<string> referencedSlotIds = new(StringComparer.Ordinal);
        foreach (CompositionProfileSpace space in _spaces)
        {
            switch (space)
            {
                case InputArtifactProfileSpace input:
                    _ = RequireReference(slots, input.SlotId, "Input space references an unknown slot.");
                    _ = referencedSlotIds.Add(input.SlotId);
                    break;
                case MutableCompositionProfileSpace
                {
                    Initializer: CloneProfileInitializer clone,
                }:
                    CompositionInputSlotDefinition sourceSlot = RequireReference(
                        slots,
                        clone.SourceSlotId,
                        "Clone initializer references an unknown slot.");
                    DomainInvariant.Require(
                        sourceSlot.Required &&
                        sourceSlot.Cardinality == CompiledInputSlotCardinality.ExactlyOne,
                        "Clone initializer source slots must require exactly one artifact.");
                    DomainInvariant.Require(
                        space.Kind != CompositionProfileSpaceKind.OutputImage ||
                        CompositionKind != CompositionKind.Replace ||
                        sourceSlot.ArtifactClass == CompiledInputArtifactClass.ReferenceImage,
                        "Replace output must clone a reference-image slot.");

                    _ = referencedSlotIds.Add(clone.SourceSlotId);
                    break;
                case MutableCompositionProfileSpace:
                    break;
                default:
                    throw new InvalidOperationException("Unknown composition profile space shape.");
            }
        }

        DomainInvariant.Require(
            _inputSlots.All(slot => referencedSlotIds.Contains(slot.SlotId)),
            "Every input slot must feed an input or clone space.");
    }

    private void ValidateViews(IReadOnlyDictionary<string, CompositionProfileSpace> spaces)
    {
        foreach (CompositionProfileView view in _views)
        {
            _ = RequireReference(spaces, view.SpaceId, "View references an unknown space.");
            DomainInvariant.Require(
                view.MapRegionId is not { } regionId ||
                MapBinding.RequiredRegionIds.Contains(regionId, StringComparer.Ordinal) ||
                MapBinding.OptionalRegionIds.Contains(regionId, StringComparer.Ordinal),
                "Map-region views must declare their region in mapBinding required or optional regions.");
        }
    }

    private void ValidateMetadataBindings(IReadOnlyDictionary<string, CompositionProfileSpace> spaces)
    {
        foreach (CompositionProfileMetadataBinding binding in _metadataBindings)
        {
            _ = RequireReference(spaces, binding.SpaceId, "Metadata binding references an unknown space.");
            DomainInvariant.Require(
                MapBinding.RequiredMetadataStructureIds.Contains(binding.StructureId, StringComparer.Ordinal),
                "Metadata bindings must declare their structure in mapBinding requirements.");
        }
    }

    private void ValidateRegionAccess()
    {
        DomainInvariant.Require(
            _regionAccessRules.All(rule =>
                MapBinding.RequiredRegionIds.Contains(rule.RegionId, StringComparer.Ordinal) ||
                MapBinding.OptionalRegionIds.Contains(rule.RegionId, StringComparer.Ordinal)),
            "Region access rules must declare their region in mapBinding required or optional regions.");
    }

    private void ValidateOperations(
        IReadOnlyDictionary<string, CompositionProfileView> views,
        IReadOnlyDictionary<string, CompositionProfileSpace> spaces,
        IReadOnlyDictionary<string, CompositionProfileProcessorStage> processors)
    {
        foreach (CompositionOperationDefinition operation in _operations)
        {
            switch (operation.Kind)
            {
                case CompositionOperationKind.CopyRange:
                case CompositionOperationKind.ReplaceRange:
                    _ = RequireReference(views, operation.SourceViewId, "Operation references an unknown source view.");
                    RequireMutableTarget(operation.TargetViewId, views, spaces);
                    break;
                case CompositionOperationKind.FillRange:
                case CompositionOperationKind.PatchScalar:
                    RequireMutableTarget(operation.TargetViewId, views, spaces);
                    break;
                case CompositionOperationKind.TransformScalar:
                    _ = RequireReference(
                        views,
                        operation.SourceViewId,
                        "Transform references an unknown source view.");
                    RequireMutableTarget(operation.TargetViewId, views, spaces);
                    break;
                case CompositionOperationKind.RunExternalProcessor:
                    _ = RequireReference(
                        processors,
                        operation.ProcessorStageId,
                        "Operation references an unknown processor stage.");
                    break;
                default:
                    throw new InvalidOperationException("Unknown canonical operation kind.");
            }
        }

        DomainInvariant.Require(
            _processorStages.All(stage => _operations.Any(operation =>
                operation.Kind == CompositionOperationKind.RunExternalProcessor &&
                StringComparer.Ordinal.Equals(operation.ProcessorStageId, stage.ProcessorStageId))),
            "Every processor stage must be invoked by an operation.");
    }

    private void ValidateProcessors(
        IReadOnlyDictionary<string, CompositionProfileView> views,
        IReadOnlyDictionary<string, CompositionProfileSpace> spaces)
    {
        foreach (CompositionProfileProcessorStage processor in _processorStages)
        {
            CompositionProfileSpace targetSpace = RequireReference(
                spaces,
                processor.TargetSpaceId,
                "Processor references an unknown target space.");
            DomainInvariant.Require(
                targetSpace.Kind != CompositionProfileSpaceKind.InputArtifact ||
                processor is not LegacyCombinerProfileProcessorStage,
                "Transform processors cannot target immutable input spaces.");

            foreach (string viewId in processor.AllowedReadViewIds.Concat(processor.AllowedWriteViewIds))
            {
                RequireViewInSpace(viewId, processor.TargetSpaceId, views, "Processor authority view");
            }

            if (processor is not LegacyCombinerProfileProcessorStage legacy)
            {
                continue;
            }

            if (legacy.TargetViewId is { } targetViewId)
            {
                RequireViewInSpace(
                    targetViewId,
                    processor.TargetSpaceId,
                    views,
                    "Legacy Combiner target view");
            }

            foreach (CompositionProfileStagedSourceBinding binding in legacy.StagedSourceBindings)
            {
                _ = RequireReference(views, binding.SourceViewId, "Staged source references an unknown view.");
                RequireViewInSpace(
                    binding.TargetViewId,
                    processor.TargetSpaceId,
                    views,
                    "Staged target view");
            }

            foreach (CompositionProfileStagedArtifactBinding binding in legacy.StagedArtifactBindings)
            {
                _ = RequireReference(views, binding.SourceViewId, "Staged artifact source references an unknown view.");
            }
        }
    }

    private void ValidateValidations(
        IReadOnlyDictionary<string, CompositionProfileView> views,
        IReadOnlyDictionary<string, CompositionProfileMetadataBinding> metadataBindings)
    {
        foreach (ValidationRequirementDefinition validation in _validations)
        {
            if (validation is CompiledValidationRequirement compiled)
            {
                _ = CanonicalValidationDefinitionRules.RequireProfileDefinition(compiled);
            }

            switch (validation)
            {
                case CompiledMetadataValueValidation metadataValue:
                    ValidateFieldReference(metadataValue.Field, metadataBindings);
                    break;
                case CompiledPidSanityValidation pid:
                    ValidateFieldReference(pid.Field, metadataBindings);
                    break;
                case CompiledMetadataEqualityValidation equality:
                    ValidateFieldReference(equality.Left, metadataBindings);
                    ValidateFieldReference(equality.Right, metadataBindings);
                    break;
                case CompiledRejectMetadataBytePatternValidation rejected:
                    ValidateFieldReference(rejected.Field, metadataBindings);
                    break;
                case CompiledViewByteAssertionValidation assertion:
                    _ = RequireReference(views, assertion.ViewId, "Validation references an unknown view.");
                    break;
                case SourceViewNonUniformValidationDefinition nonUniform:
                    _ = RequireReference(views, nonUniform.ViewId, "Validation references an unknown view.");
                    break;
                default:
                    throw new InvalidOperationException("Unknown composition profile validation shape.");
            }
        }
    }

    private static void ValidateFieldReference(
        CompiledValidationFieldReference field,
        IReadOnlyDictionary<string, CompositionProfileMetadataBinding> bindings)
    {
        CompositionProfileMetadataBinding binding = RequireReference(
            bindings,
            field.BindingId,
            "Validation references an unknown metadata binding.");
        DomainInvariant.Require(
            binding.TargetReferences.Any(target =>
                target.Kind == FirmwareMetadataReferenceTargetKind.Field &&
                StringComparer.Ordinal.Equals(target.TargetId, field.FieldId)),
            "Validation references a field not selected by its metadata binding.");
    }

    private static void RequireMutableTarget(
        string targetViewId,
        IReadOnlyDictionary<string, CompositionProfileView> views,
        IReadOnlyDictionary<string, CompositionProfileSpace> spaces)
    {
        CompositionProfileView targetView = RequireReference(
            views,
            targetViewId,
            "Operation references an unknown target view.");
        CompositionProfileSpace targetSpace = spaces[targetView.SpaceId];
        DomainInvariant.Require(
            targetSpace.Kind != CompositionProfileSpaceKind.InputArtifact,
            "Operations cannot target immutable input spaces.");
    }

    private static void RequireViewInSpace(
        string viewId,
        string spaceId,
        IReadOnlyDictionary<string, CompositionProfileView> views,
        string subject)
    {
        CompositionProfileView view = RequireReference(views, viewId, $"{subject} is unknown.");
        DomainInvariant.Require(
            StringComparer.Ordinal.Equals(view.SpaceId, spaceId),
            $"{subject} must belong to processor target space '{spaceId}'.");
    }

    private static TValue RequireReference<TValue>(
        IReadOnlyDictionary<string, TValue> values,
        string id,
        string message)
    {
        return values.TryGetValue(id, out TValue? value)
            ? value
            : throw new ArgumentException($"{message} Unknown id: '{id}'.");
    }
}
