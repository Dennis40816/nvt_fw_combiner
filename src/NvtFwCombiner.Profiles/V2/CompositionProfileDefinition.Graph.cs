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
        ValidateSpaces(slots);
        ValidateViews(spaces);
        ValidateMetadataBindings(spaces);
        ValidateRegionAccess();
        ValidateOperations(views, spaces, processors);
        ValidateProcessors(views, spaces);
        ValidateValidations(views, metadataBindings);
        ValidateOutputNaming();
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

        CompositionProfileInitializerKind requiredInitializer = CompositionKind == CompositionKind.Merge
            ? CompositionProfileInitializerKind.Blank
            : CompositionProfileInitializerKind.Clone;
        if (outputs[0].Initializer.Kind != requiredInitializer)
        {
            throw new ArgumentException("The output initializer must match merge versus replace semantics.");
        }
    }

    private void ValidateInputPolicy()
    {
        bool padsInput = _inputSlots.Any(static slot =>
            slot.Normalization.Kind == CompositionProfileInputNormalizationKind.PadShorter);
        if (padsInput &&
            (CompositionKind != CompositionKind.Replace ||
             !StringComparer.Ordinal.Equals(Experience.ExperienceId, ExperienceIds.DpReplace) ||
             _processorStages.Length != 0))
        {
            throw new ArgumentException("Short-input padding requires DP Replace without processor stages.");
        }

        bool truncatesCtrlRam = _inputSlots.Any(static slot =>
            slot.Normalization.Kind == CompositionProfileInputNormalizationKind.TruncateCtrlRam);
        if (truncatesCtrlRam &&
            (CompositionKind != CompositionKind.Replace ||
             !StringComparer.Ordinal.Equals(Experience.ExperienceId, ExperienceIds.CtrlRamReplace)))
        {
            throw new ArgumentException("CtrlRAM truncation requires the CtrlRAM Replace experience.");
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
                !MapBinding.RequiredRegionIds.Contains(regionId, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Map-region views must declare their region in mapBinding.requiredRegionIds.");
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
                !MapBinding.RequiredRegionIds.Contains(rule.RegionId, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "Region access rules must declare their region in mapBinding requirements.");
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
                default:
                    throw new InvalidOperationException("Unknown composition profile validation shape.");
            }
        }
    }

    private void ValidateOutputNaming()
    {
        foreach (string tokenId in Output.RequiredTokenIds)
        {
            if (!Output.FileNameTemplate.Contains($"{{{tokenId}}}", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Output template does not contain required token '{tokenId}'.");
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
