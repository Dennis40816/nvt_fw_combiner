using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed result of lowering an admitted V2 declaration into one non-executable composition plan.</summary>
internal sealed class V2CompositionPlanCompileResult
{
    private readonly CompositionIssue[] _issues;

    private V2CompositionPlanCompileResult(CompiledComposition? compiledComposition, IEnumerable<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        _issues = [.. issues];
        if (_issues.Any(static issue => issue is null) || (compiledComposition is null) != (_issues.Length != 0))
        {
            throw new ArgumentException("V2 plan compilation requires either one artifact or one or more issues.", nameof(issues));
        }

        Array.Sort(_issues, static (left, right) =>
        {
            int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
            if (code != 0)
            {
                return code;
            }

            int message = StringComparer.Ordinal.Compare(left.Message, right.Message);
            return message != 0
                ? message
                : StringComparer.Ordinal.Compare(left.OperationId, right.OperationId);
        });
        CompiledComposition = compiledComposition;
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>One complete non-executable V2 plan artifact when all supported declarations compiled.</summary>
    internal CompiledComposition? CompiledComposition { get; }

    /// <summary>Deterministic unsupported or incomplete declaration diagnostics.</summary>
    internal IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>True only when lowering produced one atomic plan artifact.</summary>
    internal bool IsCompiled => CompiledComposition is not null;

    internal static V2CompositionPlanCompileResult Succeeded(CompiledComposition compiledComposition)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        return new V2CompositionPlanCompileResult(compiledComposition, []);
    }

    internal static V2CompositionPlanCompileResult Failed(IEnumerable<CompositionIssue> issues)
    {
        return new V2CompositionPlanCompileResult(compiledComposition: null, issues);
    }
}

/// <summary>Profiles-owned first lowering slice for admitted blank-output V2 Merge declarations.</summary>
internal static class V2CompositionPlanCompiler
{
    private const string PreparationNotAdmitted = "profile.v2.plan.preparation-not-admitted";
    private const string UnsupportedDeclaration = "profile.v2.plan.unsupported-declaration";
    private const string InvalidView = "profile.v2.plan.invalid-view";
    private const string CopyLengthMismatch = "profile.v2.plan.copy-length-mismatch";

    /// <summary>Lowers only the closed blank-copy V2 subset and never grants Application runtime eligibility.</summary>
    internal static V2CompositionPlanCompileResult Compile(V2CompositionPreparationResult preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (!preparation.IsAdmitted || preparation.Selection is null || preparation.Admission is null)
        {
            return V2CompositionPlanCompileResult.Failed([
                new CompositionIssue(PreparationNotAdmitted, "V2 plan lowering requires an admitted trusted preparation.")]);
        }

        CompositionProfileDefinition profile = preparation.Admission.Profile;
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = preparation.Admission.ResolvedMap;
        var issues = new List<CompositionIssue>();
        ValidateSupportedProfile(profile, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        Dictionary<string, AddressSpace> spaces = LowerAddressSpaces(profile, resolvedMap);
        Dictionary<string, ResolvedView> views = LowerViews(profile, resolvedMap, spaces, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        CompositionOperation[] operations = LowerCopyOperations(profile, views, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        var plan = new CompositionPlan(
            ImageInitialization.Blank(output.SpaceId, resolvedMap.CapacityBytes, ((BlankProfileInitializer)output.Initializer).FillByte),
            spaces.Values,
            operations);
        var promotion = new CompiledProfilePromotion(
            MapPromotionStage(profile.Promotion.Stage),
            profile.Promotion.Blockers.Select(MapPromotionBlocker));
        var provenance = new V2CompilationProvenance(
            preparation.Selection.BundleIdentity,
            preparation.Selection.ProfileEntryIdentity,
            resolvedMap,
            promotion,
            profile.EvidenceRefs,
            [],
            preparation.Admission.RequiredCapabilities.Select(static capability => new CompiledCapabilityAdmission(
                capability.RequiredCapabilityId,
                capability.Binding)));
        var inputContract = new CompiledInputContract(
            profile.InputSlots.Select(slot => MapInputSlot(slot, resolvedMap)),
            profile.Spaces.OfType<InputArtifactProfileSpace>().Select(MapInputSpaceBinding));
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
            new V2CompiledCompositionDetails(provenance, inputContract, outputNaming));
        return V2CompositionPlanCompileResult.Succeeded(CompiledComposition.CreateV2(
            plan,
            identity,
            CompiledIcNumberPolicy.NotApplicable));
    }

    private static void ValidateSupportedProfile(CompositionProfileDefinition profile, List<CompositionIssue> issues)
    {
        if (profile.CompositionKind != CompositionKind.Merge)
        {
            AddUnsupported(issues, "composition kind must be Merge");
        }

        if (profile.Promotion.Stage < CompositionProfilePromotionStage.Compilable)
        {
            AddUnsupported(issues, "promotion stage must be Compilable or later");
        }

        if (profile.MetadataBindings.Count != 0 ||
            profile.RegionAccessRules.Count != 0 ||
            profile.Validations.Count != 0 ||
            profile.ProcessorStages.Count != 0)
        {
            AddUnsupported(issues, "metadata bindings, region access rules, validations, and processor stages are not lowered in this slice");
        }

        MutableCompositionProfileSpace[] mutableSpaces = [.. profile.Spaces.OfType<MutableCompositionProfileSpace>()];
        if (mutableSpaces.Length != 1 || mutableSpaces[0].Kind != CompositionProfileSpaceKind.OutputImage)
        {
            AddUnsupported(issues, "exactly one output-image mutable space is required and work buffers are unsupported");
        }
        else if (mutableSpaces[0].Capacity is not ResolvedMapProfileCapacity ||
                 mutableSpaces[0].Initializer is not BlankProfileInitializer)
        {
            AddUnsupported(issues, "the output image must use resolved-map blank initialization");
        }

        foreach (InputArtifactProfileSpace inputSpace in profile.Spaces.OfType<InputArtifactProfileSpace>())
        {
            CompositionProfileInputSlot? slot = profile.InputSlots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, inputSpace.SlotId));
            if (inputSpace.InstancePolicy != CompositionProfileInstancePolicy.Singleton ||
                slot is null ||
                !slot.Required ||
                slot.Cardinality != CompositionProfileSlotCardinality.ExactlyOne ||
                slot.LengthRule is not ExactResolvedMapCapacityLengthRule ||
                slot.Normalization is not NoInputNormalization)
            {
                AddUnsupported(issues, $"input space '{inputSpace.SpaceId}' must bind one required singleton exact-map-capacity unnormalized slot");
            }
        }

        foreach (CompositionProfileOperation operation in profile.Operations)
        {
            if (operation is not CopyOrReplaceProfileOperation { Kind: CompositionProfileOperationKind.CopyRange } ||
                operation.OverlapPolicy != OverlapPolicy.Reject)
            {
                AddUnsupported(issues, $"operation '{operation.OperationId}' is outside the CopyRange/Reject lowering subset", operation.OperationId);
            }
        }
    }

    private static Dictionary<string, AddressSpace> LowerAddressSpaces(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        var spaces = new Dictionary<string, AddressSpace>(StringComparer.Ordinal);
        foreach (InputArtifactProfileSpace input in profile.Spaces.OfType<InputArtifactProfileSpace>())
        {
            spaces.Add(
                input.SpaceId,
                new AddressSpace(
                    input.SpaceId,
                    resolvedMap.CapacityBytes,
                    AddressSpaceMutability.Immutable,
                    allowedInputLengths: [resolvedMap.CapacityBytes]));
        }

        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        spaces.Add(
            output.SpaceId,
            new AddressSpace(output.SpaceId, resolvedMap.CapacityBytes, AddressSpaceMutability.Mutable));
        return spaces;
    }

    private static Dictionary<string, ResolvedView> LowerViews(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        Dictionary<string, AddressSpace> spaces,
        List<CompositionIssue> issues)
    {
        var views = new Dictionary<string, ResolvedView>(StringComparer.Ordinal);
        foreach (CompositionProfileView view in profile.Views)
        {
            if (!spaces.TryGetValue(view.SpaceId, out AddressSpace? space))
            {
                issues.Add(new CompositionIssue(InvalidView, $"View '{view.ViewId}' names an unsupported space '{view.SpaceId}'.", view.ViewId));
                continue;
            }

            if (!TryResolveViewRange(view, resolvedMap, space, out ByteRange range, out string? error))
            {
                issues.Add(new CompositionIssue(InvalidView, error!, view.ViewId));
                continue;
            }

            views.Add(view.ViewId, new ResolvedView(view.SpaceId, range));
        }

        return views;
    }

    private static bool TryResolveViewRange(
        CompositionProfileView view,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        AddressSpace space,
        out ByteRange range,
        out string? error)
    {
        range = default;
        error = null;
        try
        {
            switch (view.Selector)
            {
                case MapRegionViewSelector regionSelector:
                    FirmwareRegion? region = resolvedMap.ImageMap.Regions.SingleOrDefault(candidate =>
                        StringComparer.Ordinal.Equals(candidate.RegionId, regionSelector.RegionId));
                    if (region is null)
                    {
                        error = $"View '{view.ViewId}' names unknown map region '{regionSelector.RegionId}'.";
                        return false;
                    }

                    range = region.Range;
                    break;
                case MapRegionSliceViewSelector sliceSelector:
                    FirmwareRegion? slicedRegion = resolvedMap.ImageMap.Regions.SingleOrDefault(candidate =>
                        StringComparer.Ordinal.Equals(candidate.RegionId, sliceSelector.RegionId));
                    if (slicedRegion is null)
                    {
                        error = $"View '{view.ViewId}' names unknown map region '{sliceSelector.RegionId}'.";
                        return false;
                    }

                    range = new ByteRange(checked(slicedRegion.Range.Start + sliceSelector.RelativeRange.Start), sliceSelector.RelativeRange.Length);
                    if (!slicedRegion.Range.Contains(range))
                    {
                        error = $"View '{view.ViewId}' escapes map region '{sliceSelector.RegionId}'.";
                        return false;
                    }

                    break;
                case SpaceRangeViewSelector spaceSelector:
                    range = spaceSelector.Range;
                    break;
                default:
                    error = $"View '{view.ViewId}' uses an unsupported selector.";
                    return false;
            }

            if (!space.Contains(range))
            {
                error = $"View '{view.ViewId}' escapes address space '{space.AddressSpaceId}'.";
                return false;
            }

            return true;
        }
        catch (OverflowException)
        {
            error = $"View '{view.ViewId}' range overflows its declared bounds.";
            return false;
        }
    }

    private static CompositionOperation[] LowerCopyOperations(
        CompositionProfileDefinition profile,
        IReadOnlyDictionary<string, ResolvedView> views,
        List<CompositionIssue> issues)
    {
        var operations = new List<CompositionOperation>();
        foreach (CopyOrReplaceProfileOperation operation in profile.Operations.OfType<CopyOrReplaceProfileOperation>())
        {
            if (!views.TryGetValue(operation.SourceViewId, out ResolvedView? source) || source is null ||
                !views.TryGetValue(operation.TargetViewId, out ResolvedView? target) || target is null)
            {
                issues.Add(new CompositionIssue(InvalidView, $"Operation '{operation.OperationId}' references an unresolved view.", operation.OperationId));
                continue;
            }

            if (source.Range.Length != target.Range.Length)
            {
                issues.Add(new CompositionIssue(CopyLengthMismatch, $"Operation '{operation.OperationId}' source and target views have different lengths.", operation.OperationId));
                continue;
            }

            if (operation.Sequence > int.MaxValue)
            {
                AddUnsupported(issues, $"operation '{operation.OperationId}' sequence exceeds Int32", operation.OperationId);
                continue;
            }

            operations.Add(CompositionOperation.CopyRange(
                operation.OperationId,
                (int)operation.Sequence,
                source.SpaceId,
                source.Range,
                target.SpaceId,
                target.Range,
                OverlapPolicy.Reject,
                operation.Reason));
        }

        return [.. operations];
    }

    private static MutableCompositionProfileSpace AssertOutputSpace(CompositionProfileDefinition profile)
    {
        return profile.Spaces.OfType<MutableCompositionProfileSpace>().Single();
    }

    private static CompiledInputSlotRequirement MapInputSlot(
        CompositionProfileInputSlot slot,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
        return new CompiledInputSlotRequirement(
            slot.SlotId,
            slot.Role,
            slot.ArtifactClass switch
            {
                CompositionProfileArtifactClass.TpFirmware => CompiledInputArtifactClass.TpFirmware,
                CompositionProfileArtifactClass.DpFirmware => CompiledInputArtifactClass.DpFirmware,
                CompositionProfileArtifactClass.ReferenceImage => CompiledInputArtifactClass.ReferenceImage,
                CompositionProfileArtifactClass.CtrlRamReplacement => CompiledInputArtifactClass.CtrlRamReplacement,
                CompositionProfileArtifactClass.Auxiliary => CompiledInputArtifactClass.Auxiliary,
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot.ArtifactClass, "Unknown profile input artifact class."),
            },
            slot.Required,
            slot.Cardinality switch
            {
                CompositionProfileSlotCardinality.ExactlyOne => CompiledInputSlotCardinality.ExactlyOne,
                CompositionProfileSlotCardinality.ZeroOrOne => CompiledInputSlotCardinality.ZeroOrOne,
                CompositionProfileSlotCardinality.OneOrMore => CompiledInputSlotCardinality.OneOrMore,
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot.Cardinality, "Unknown profile input slot cardinality."),
            },
            slot.AcceptedExtensions,
            MapInputLengthRequirement(slot.LengthRule, resolvedMap.CapacityBytes),
            MapInputNormalization(slot.Normalization));
    }

    private static CompiledInputSpaceBinding MapInputSpaceBinding(InputArtifactProfileSpace space)
    {
        return new CompiledInputSpaceBinding(
            space.SpaceId,
            space.SlotId,
            space.InstancePolicy switch
            {
                CompositionProfileInstancePolicy.Singleton => CompiledInputInstancePolicy.Singleton,
                CompositionProfileInstancePolicy.PerBinding => CompiledInputInstancePolicy.PerBinding,
                _ => throw new ArgumentOutOfRangeException(nameof(space), space.InstancePolicy, "Unknown profile input instance policy."),
            });
    }

    private static CompiledInputLengthRequirement MapInputLengthRequirement(
        CompositionProfileLengthRule lengthRule,
        long resolvedMapCapacity)
    {
        return lengthRule switch
        {
            ExactBytesLengthRule exact => new CompiledExactBytesInputLengthRequirement(exact.Bytes),
            ExactResolvedMapCapacityLengthRule => new CompiledExactResolvedMapCapacityInputLengthRequirement(
                resolvedMapCapacity),
            BoundedLengthRule bounded => new CompiledBoundedInputLengthRequirement(
                bounded.MinimumBytes,
                bounded.MaximumBytes),
            NormalDpExtractWithWarningLengthRule normalDp =>
                new CompiledNormalDpExtractWithWarningInputLengthRequirement(normalDp.IssueCode),
            TpMaximum256KLengthRule => new CompiledTpMaximum256KInputLengthRequirement(),
            _ => throw new ArgumentOutOfRangeException(nameof(lengthRule), "Unknown profile input length rule."),
        };
    }

    private static CompiledInputNormalization MapInputNormalization(
        CompositionProfileInputNormalization normalization)
    {
        return normalization switch
        {
            NoInputNormalization => new CompiledNoInputNormalization(),
            PadShorterInputNormalization padded => new CompiledPadShorterInputNormalization(
                padded.FillByte,
                padded.EvidenceRef),
            TruncateCtrlRamInputNormalization truncated => new CompiledTruncateCtrlRamInputNormalization(
                truncated.WarningIssueCode,
                truncated.EvidenceRef),
            _ => throw new ArgumentOutOfRangeException(nameof(normalization), "Unknown profile input normalization."),
        };
    }

    private static CompiledProfilePromotionStage MapPromotionStage(CompositionProfilePromotionStage stage)
    {
        return stage switch
        {
            CompositionProfilePromotionStage.Known => CompiledProfilePromotionStage.Known,
            CompositionProfilePromotionStage.MapResolvable => CompiledProfilePromotionStage.MapResolvable,
            CompositionProfilePromotionStage.Inspectable => CompiledProfilePromotionStage.Inspectable,
            CompositionProfilePromotionStage.Authorable => CompiledProfilePromotionStage.Authorable,
            CompositionProfilePromotionStage.Compilable => CompiledProfilePromotionStage.Compilable,
            CompositionProfilePromotionStage.ExecutableCandidate => CompiledProfilePromotionStage.ExecutableCandidate,
            CompositionProfilePromotionStage.Supported => CompiledProfilePromotionStage.Supported,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown profile promotion stage."),
        };
    }

    private static CompiledProfilePromotionBlocker MapPromotionBlocker(CompositionProfilePromotionBlocker blocker)
    {
        return new CompiledProfilePromotionBlocker(
            blocker.BlockerId,
            blocker.Kind switch
            {
                CompositionProfileBlockerKind.Map => CompiledProfilePromotionBlockerKind.Map,
                CompositionProfileBlockerKind.Metadata => CompiledProfilePromotionBlockerKind.Metadata,
                CompositionProfileBlockerKind.Operation => CompiledProfilePromotionBlockerKind.Operation,
                CompositionProfileBlockerKind.Processor => CompiledProfilePromotionBlockerKind.Processor,
                CompositionProfileBlockerKind.Integrity => CompiledProfilePromotionBlockerKind.Integrity,
                CompositionProfileBlockerKind.Golden => CompiledProfilePromotionBlockerKind.Golden,
                CompositionProfileBlockerKind.HumanReview => CompiledProfilePromotionBlockerKind.HumanReview,
                CompositionProfileBlockerKind.Ui => CompiledProfilePromotionBlockerKind.Ui,
                CompositionProfileBlockerKind.Release => CompiledProfilePromotionBlockerKind.Release,
                _ => throw new ArgumentOutOfRangeException(nameof(blocker), blocker.Kind, "Unknown profile blocker kind."),
            },
            blocker.Reason,
            blocker.EvidenceRefs);
    }

    private static CompiledOutputInvalidCharacterPolicy MapOutputPolicy(CompositionProfileInvalidCharacterPolicy policy)
    {
        return policy switch
        {
            CompositionProfileInvalidCharacterPolicy.Reject => CompiledOutputInvalidCharacterPolicy.Reject,
            CompositionProfileInvalidCharacterPolicy.ReplaceUnderscore => CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown output invalid-character policy."),
        };
    }

    private static void AddUnsupported(List<CompositionIssue> issues, string message, string? operationId = null)
    {
        issues.Add(new CompositionIssue(UnsupportedDeclaration, message, operationId));
    }

    private sealed record ResolvedView(string SpaceId, ByteRange Range);
}
