using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal sealed partial class CompositionProfileDefinition
{
    private void ValidateReferenceGraph()
    {
        Dictionary<string, CompositionProfileInputSlot> slots = _inputSlots.ToDictionary(
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
        if (CompilationContext is LogicalOutputProfileCompilationContext)
        {
            ValidateLogicalOutputShape();
            ValidateOutputNaming(metadataBindings);
            return;
        }

        if (CompilationContext is RuntimeReferenceReplaceProfileCompilationContext)
        {
            ValidateRuntimeReferenceReplaceShape();
            ValidateViews(spaces);
            ValidateRegionAccess();
            ValidateOperations(views, spaces, processors);
            ValidateProcessors(views, spaces);
            ValidateOutputNaming(metadataBindings);
            return;
        }

        ValidateViews(spaces);
        ValidateMetadataBindings(spaces);
        ValidateRegionAccess();
        ValidateOperations(views, spaces, processors);
        ValidateProcessors(views, spaces);
        ValidateValidations(views, metadataBindings);
        ValidateOutputNaming(metadataBindings);
    }

    private void ValidateLogicalOutputShape()
    {
        if (CompositionKind != CompositionKind.Merge ||
            !StringComparer.Ordinal.Equals(Experience.ExperienceId, ExperienceIds.GeneralMerge) ||
            Experience.LayoutPolicy != LayoutPolicy.UserDefined ||
            Experience.InputPolicy != InputPolicy.Extensible ||
            _metadataBindings.Length != 0 ||
            _regionAccessRules.Length != 0 ||
            _operations.Length != 0 ||
            _validations.Length != 0 ||
            _processorStages.Length != 0 ||
            _views.Length != 0)
        {
            throw new ArgumentException(
                "Logical-output profiles are restricted to the General Merge declarative shape without physical-map declarations.");
        }

        InputArtifactProfileSpace[] inputSpaces = [.. _spaces.OfType<InputArtifactProfileSpace>()];
        MutableCompositionProfileSpace output = _spaces.OfType<MutableCompositionProfileSpace>().Single(space =>
            space.Kind == CompositionProfileSpaceKind.OutputImage);
        if (_inputSlots.Length != 1 ||
            inputSpaces.Length != 1 ||
            inputSpaces[0].InstancePolicy != CompositionProfileInstancePolicy.PerBinding ||
            output.Capacity is not RuntimeRequestProfileCapacity ||
            output.Initializer is not BlankProfileInitializer { FillByte: 0 } ||
            _spaces.Length != 2)
        {
            throw new ArgumentException(
                "Logical-output profiles require one per-binding input template and one zero-filled runtime-request output.");
        }

        CompositionProfileInputSlot slot = _inputSlots[0];
        if (!slot.Required ||
            slot.Cardinality != CompositionProfileSlotCardinality.OneOrMore ||
            slot.ArtifactClass != CompositionProfileArtifactClass.Auxiliary ||
            slot.Normalization is not NoInputNormalization ||
            slot.LengthRule is not BoundedLengthRule
            {
                MinimumBytes: 1,
                MaximumBytes: int.MaxValue,
            } ||
            !StringComparer.Ordinal.Equals(inputSpaces[0].SlotId, slot.SlotId))
        {
            throw new ArgumentException(
                "Logical-output profiles require one unnormalized auxiliary one-or-more input slot bounded to the in-memory composition limit.");
        }

        if (Promotion.Stage >= CompositionProfilePromotionStage.Supported)
        {
            throw new ArgumentException("Logical-output profiles cannot be marked supported before runtime per-binding admission exists.");
        }
    }

    private void ValidateRuntimeReferenceReplaceShape()
    {
        var context =
            (RuntimeReferenceReplaceProfileCompilationContext)CompilationContext;
        bool isGeneralReplace = StringComparer.Ordinal.Equals(
            Experience.ExperienceId,
            ExperienceIds.GeneralReplace);
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(
            Experience.ExperienceId,
            ExperienceIds.CtrlRamReplace);
        CompositionProfileArtifactClass expectedSourceClass = isCtrlRamReplace
            ? CompositionProfileArtifactClass.CtrlRamReplacement
            : CompositionProfileArtifactClass.Auxiliary;
        MutableCompositionProfileSpace output = _spaces.OfType<MutableCompositionProfileSpace>().Single(space =>
            space.Kind == CompositionProfileSpaceKind.OutputImage);
        bool processorFree = _views.Length == 0 && _operations.Length == 0 && _processorStages.Length == 0;
        bool conditionalProcessor = context.AllowsConditionalProcessor &&
            HasValidRuntimeReferenceProcessorShape(output);
        if (CompositionKind != CompositionKind.Replace ||
            (!isGeneralReplace && !isCtrlRamReplace) ||
            (isGeneralReplace &&
             (Experience.LayoutPolicy != LayoutPolicy.UserDefined ||
              Experience.InputPolicy != InputPolicy.Extensible)) ||
            (isCtrlRamReplace &&
             (Experience.LayoutPolicy != LayoutPolicy.Fixed ||
              Experience.InputPolicy != InputPolicy.Fixed ||
              !conditionalProcessor)) ||
            _metadataBindings.Length != 0 ||
            _validations.Length != 0 ||
            _regionAccessRules.Length == 0 ||
            (!processorFree && !conditionalProcessor))
        {
            throw new ArgumentException(
                "Runtime reference-replace profiles require the closed General Replace or CtrlRAM Replace mapping shape; CtrlRAM Replace requires one final Legacy Combiner stage.");
        }

        InputArtifactProfileSpace[] inputs = [.. _spaces.OfType<InputArtifactProfileSpace>()];
        if (_inputSlots.Length != 2 || inputs.Length != 2 || _spaces.Length != 3 ||
            output.Capacity is not RuntimeRequestProfileCapacity ||
            output.Initializer is not CloneProfileInitializer clone)
        {
            throw new ArgumentException(
                "Runtime reference-replace profiles require two immutable inputs and one runtime-capacity output cloned from the reference slot.");
        }

        CompositionProfileInputSlot? reference = _inputSlots.SingleOrDefault(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, clone.SourceSlotId));
        CompositionProfileInputSlot? source = _inputSlots.SingleOrDefault(slot =>
            !StringComparer.Ordinal.Equals(slot.SlotId, clone.SourceSlotId));
        InputArtifactProfileSpace? referenceSpace = inputs.SingleOrDefault(space =>
            StringComparer.Ordinal.Equals(space.SlotId, clone.SourceSlotId));
        InputArtifactProfileSpace? sourceSpace = inputs.SingleOrDefault(space =>
            !StringComparer.Ordinal.Equals(space.SlotId, clone.SourceSlotId));
        bool sourceNormalizationIsValid = isCtrlRamReplace
            ? source?.Normalization is TruncateCtrlRamInputNormalization
            : source?.Normalization is NoInputNormalization;
        if (reference is not
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
            } ||
            source.ArtifactClass != expectedSourceClass ||
            !sourceNormalizationIsValid ||
            referenceSpace is not { InstancePolicy: CompositionProfileInstancePolicy.Singleton } ||
            sourceSpace is not { InstancePolicy: CompositionProfileInstancePolicy.PerBinding })
        {
            throw new ArgumentException(
                "Runtime reference-replace profiles require one exact singleton reference and one experience-owned per-binding source with its closed normalization policy.");
        }

        if (Promotion.Stage >= CompositionProfilePromotionStage.Supported)
        {
            throw new ArgumentException(
                "Runtime reference-replace profiles cannot be marked supported before runtime request routing and owner evidence are complete.");
        }
    }

    private bool HasValidRuntimeReferenceProcessorShape(MutableCompositionProfileSpace output)
    {
        if (_operations is not [RunProcessorProfileOperation operation] ||
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
        if (CompositionKind == CompositionKind.Merge && IcNumberInputMode is not null)
        {
            throw new ArgumentException("Merge profiles cannot declare an IC-number input mode.");
        }

        if (CompositionKind == CompositionKind.Replace && IcNumberInputMode is null)
        {
            throw new ArgumentException("Replace profiles require an IC-number input mode.");
        }
    }

    private void ValidateOutputSpace()
    {
        MutableCompositionProfileSpace[] outputs =
        [
            .. _spaces.OfType<MutableCompositionProfileSpace>()
                .Where(static space => space.Kind == CompositionProfileSpaceKind.OutputImage),
        ];
        if (outputs.Length != 1)
        {
            throw new ArgumentException("Profiles require exactly one output-image space.");
        }

        bool initializerMatchesComposition = CompositionKind == CompositionKind.Merge
            ? outputs[0].Initializer is BlankProfileInitializer
            : outputs[0].Initializer is CloneProfileInitializer;
        if (!initializerMatchesComposition)
        {
            throw new ArgumentException("The output initializer must match merge versus replace semantics.");
        }
    }

    private void ValidateInputPolicy()
    {
        bool extractsDeclaredPrefix = _inputSlots.Any(static slot =>
            slot.LengthRule is SourceViewCoverageLengthRule { RequiredEndExclusive: not null });
        if (extractsDeclaredPrefix && CompositionKind != CompositionKind.Merge)
        {
            throw new ArgumentException("Declared-prefix input authority requires Merge composition.");
        }

        bool padsInput = _inputSlots.Any(static slot =>
            slot.Normalization is PadShorterInputNormalization);
        if (padsInput &&
            (CompositionKind != CompositionKind.Replace ||
             !StringComparer.Ordinal.Equals(Experience.ExperienceId, ExperienceIds.DpReplace) ||
             _processorStages.Length != 0))
        {
            throw new ArgumentException("Short-input padding requires DP Replace without processor stages.");
        }

        bool truncatesCtrlRam = _inputSlots.Any(static slot =>
            slot.Normalization is TruncateCtrlRamInputNormalization);
        if (truncatesCtrlRam &&
            (CompositionKind != CompositionKind.Replace ||
             !StringComparer.Ordinal.Equals(Experience.ExperienceId, ExperienceIds.CtrlRamReplace)))
        {
            throw new ArgumentException("CtrlRAM truncation requires the CtrlRAM Replace experience.");
        }
    }

    private void ValidateInputSelectionGroups(
        IReadOnlyDictionary<string, CompositionProfileInputSlot> slots)
    {
        var groupedSlotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CompositionProfileInputSelectionGroup group in _inputSelectionGroups)
        {
            foreach (string memberSlotId in group.MemberSlotIds)
            {
                CompositionProfileInputSlot slot = RequireReference(
                    slots,
                    memberSlotId,
                    "Input selection group references an unknown slot.");
                if (slot.Required || slot.Cardinality != CompositionProfileSlotCardinality.ZeroOrOne)
                {
                    throw new ArgumentException(
                        "Input selection groups may reference only optional zero-or-one slots.");
                }

                if (!groupedSlotIds.Add(memberSlotId))
                {
                    throw new ArgumentException(
                        "An input slot may belong to only one selection group.");
                }
            }
        }
    }

    private void ValidateSpaces(IReadOnlyDictionary<string, CompositionProfileInputSlot> slots)
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
                    CompositionProfileInputSlot sourceSlot = RequireReference(
                        slots,
                        clone.SourceSlotId,
                        "Clone initializer references an unknown slot.");
                    if (!sourceSlot.Required ||
                        sourceSlot.Cardinality != CompositionProfileSlotCardinality.ExactlyOne)
                    {
                        throw new ArgumentException("Clone initializer source slots must require exactly one artifact.");
                    }

                    if (space.Kind == CompositionProfileSpaceKind.OutputImage &&
                        CompositionKind == CompositionKind.Replace &&
                        sourceSlot.ArtifactClass != CompositionProfileArtifactClass.ReferenceImage)
                    {
                        throw new ArgumentException("Replace output must clone a reference-image slot.");
                    }

                    _ = referencedSlotIds.Add(clone.SourceSlotId);
                    break;
                case MutableCompositionProfileSpace:
                    break;
                default:
                    throw new InvalidOperationException("Unknown composition profile space shape.");
            }
        }

        if (_inputSlots.Any(slot => !referencedSlotIds.Contains(slot.SlotId)))
        {
            throw new ArgumentException("Every input slot must feed an input or clone space.");
        }
    }

    private void ValidateViews(IReadOnlyDictionary<string, CompositionProfileSpace> spaces)
    {
        foreach (CompositionProfileView view in _views)
        {
            _ = RequireReference(spaces, view.SpaceId, "View references an unknown space.");
            string? regionId = view.Selector switch
            {
                MapRegionViewSelector region => region.RegionId,
                MapRegionSliceViewSelector slice => slice.RegionId,
                _ => null,
            };
            if (regionId is not null &&
                !MapBinding.RequiredRegionIds.Contains(regionId, StringComparer.Ordinal) &&
                !MapBinding.OptionalRegionIds.Contains(regionId, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Map-region views must declare their region in mapBinding required or optional regions.");
            }
        }
    }

    private void ValidateMetadataBindings(IReadOnlyDictionary<string, CompositionProfileSpace> spaces)
    {
        foreach (CompositionProfileMetadataBinding binding in _metadataBindings)
        {
            _ = RequireReference(spaces, binding.SpaceId, "Metadata binding references an unknown space.");
            if (!MapBinding.RequiredMetadataStructureIds.Contains(binding.StructureId, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Metadata bindings must declare their structure in mapBinding requirements.");
            }
        }
    }

    private void ValidateRegionAccess()
    {
        if (_regionAccessRules.Any(rule =>
                !MapBinding.RequiredRegionIds.Contains(rule.RegionId, StringComparer.Ordinal) &&
                !MapBinding.OptionalRegionIds.Contains(rule.RegionId, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "Region access rules must declare their region in mapBinding required or optional regions.");
        }
    }

    private void ValidateOperations(
        IReadOnlyDictionary<string, CompositionProfileView> views,
        IReadOnlyDictionary<string, CompositionProfileSpace> spaces,
        IReadOnlyDictionary<string, CompositionProfileProcessorStage> processors)
    {
        foreach (CompositionProfileOperation operation in _operations)
        {
            switch (operation)
            {
                case CopyOrReplaceProfileOperation copy:
                    _ = RequireReference(views, copy.SourceViewId, "Operation references an unknown source view.");
                    RequireMutableTarget(copy.TargetViewId, views, spaces);
                    break;
                case FillRangeProfileOperation fill:
                    RequireMutableTarget(fill.TargetViewId, views, spaces);
                    break;
                case PatchScalarProfileOperation patch:
                    RequireMutableTarget(patch.TargetViewId, views, spaces);
                    break;
                case TransformScalarProfileOperation transform:
                    _ = RequireReference(
                        views,
                        transform.SourceViewId,
                        "Transform references an unknown source view.");
                    RequireMutableTarget(transform.TargetViewId, views, spaces);
                    break;
                case RunProcessorProfileOperation processor:
                    _ = RequireReference(
                        processors,
                        processor.ProcessorStageId,
                        "Operation references an unknown processor stage.");
                    break;
                default:
                    throw new InvalidOperationException("Unknown composition profile operation shape.");
            }
        }

        if (_processorStages.Any(stage => !_operations.OfType<RunProcessorProfileOperation>().Any(operation =>
                StringComparer.Ordinal.Equals(operation.ProcessorStageId, stage.ProcessorStageId))))
        {
            throw new ArgumentException("Every processor stage must be invoked by an operation.");
        }
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
            if (targetSpace.Kind == CompositionProfileSpaceKind.InputArtifact &&
                processor.Authority == CompositionProfileProcessorAuthority.Transform)
            {
                throw new ArgumentException("Transform processors cannot target immutable input spaces.");
            }

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
        foreach (CompositionProfileValidation validation in _validations)
        {
            switch (validation)
            {
                case MetadataValueProfileValidation metadataValue:
                    ValidateFieldReference(metadataValue.Field, metadataBindings);
                    break;
                case PidSanityProfileValidation pid:
                    ValidateFieldReference(pid.Field, metadataBindings);
                    break;
                case MetadataEqualityProfileValidation equality:
                    ValidateFieldReference(equality.Left, metadataBindings);
                    ValidateFieldReference(equality.Right, metadataBindings);
                    break;
                case RejectMetadataBytePatternProfileValidation rejected:
                    ValidateFieldReference(rejected.Field, metadataBindings);
                    break;
                case ViewByteAssertionProfileValidation assertion:
                    _ = RequireReference(views, assertion.ViewId, "Validation references an unknown view.");
                    break;
                case NonUniformRegionProfileValidation nonUniform:
                    _ = RequireReference(views, nonUniform.ViewId, "Validation references an unknown view.");
                    break;
                default:
                    throw new InvalidOperationException("Unknown composition profile validation shape.");
            }
        }
    }

    private static void ValidateFieldReference(
        CompositionProfileMetadataFieldReference field,
        IReadOnlyDictionary<string, CompositionProfileMetadataBinding> bindings)
    {
        CompositionProfileMetadataBinding binding = RequireReference(
            bindings,
            field.BindingId,
            "Validation references an unknown metadata binding.");
        if (!binding.FieldIds.Contains(field.FieldId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Validation references a field not selected by its metadata binding.");
        }
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
        if (targetSpace.Kind == CompositionProfileSpaceKind.InputArtifact)
        {
            throw new ArgumentException("Operations cannot target immutable input spaces.");
        }
    }

    private static void RequireViewInSpace(
        string viewId,
        string spaceId,
        IReadOnlyDictionary<string, CompositionProfileView> views,
        string subject)
    {
        CompositionProfileView view = RequireReference(views, viewId, $"{subject} is unknown.");
        if (!StringComparer.Ordinal.Equals(view.SpaceId, spaceId))
        {
            throw new ArgumentException($"{subject} must belong to processor target space '{spaceId}'.");
        }
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
