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
internal static partial class V2CompositionPlanCompiler
{
    private const string PreparationNotAdmitted = "profile.v2.plan.preparation-not-admitted";
    private const string UnsupportedDeclaration = "profile.v2.plan.unsupported-declaration";
    private const string InvalidView = "profile.v2.plan.invalid-view";
    private const string CopyLengthMismatch = "profile.v2.plan.copy-length-mismatch";
    private const string OperationLengthMismatch = "profile.v2.plan.operation-length-mismatch";
    private const string OperationOverlap = "profile.v2.plan.operation-overlap";
    private const string ScalarWidthMismatch = "profile.v2.plan.scalar-width-mismatch";
    private const string InvalidScalarTransform = "profile.v2.plan.invalid-scalar-transform";
    private const string InvalidRegionAccess = "profile.v2.plan.invalid-region-access";
    private const string RegionAccessDenied = "profile.v2.plan.region-access-denied";

    /// <summary>Lowers the closed blank-output V2 operation subset and never grants Application runtime eligibility.</summary>
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

        LoweredRegionAccess regionAccess = LowerRegionAccess(profile, resolvedMap, views, issues);
        if (issues.Count != 0)
        {
            return V2CompositionPlanCompileResult.Failed(issues);
        }

        CompositionOperation[] operations = LowerOperations(profile, views, regionAccess, issues);
        ValidateRejectOperationOverlaps(operations, issues);
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
            new V2CompiledCompositionDetails(provenance, inputContract, regionAccess.Contract, outputNaming));
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
            profile.Validations.Count != 0 ||
            profile.ProcessorStages.Count != 0)
        {
            AddUnsupported(issues, "metadata bindings, validations, and processor stages are not lowered in this slice");
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
            if (operation.OverlapPolicy != OverlapPolicy.Reject ||
                operation is not CopyOrReplaceProfileOperation { Kind: CompositionProfileOperationKind.CopyRange } and
                    not FillRangeProfileOperation and
                    not PatchScalarProfileOperation and
                    not TransformScalarProfileOperation)
            {
                AddUnsupported(
                    issues,
                    $"operation '{operation.OperationId}' is outside the CopyRange/FillRange/PatchScalar/TransformScalar Reject lowering subset",
                    operation.OperationId);
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
        var regionsById = resolvedMap.ImageMap.Regions.ToDictionary(
            static region => region.RegionId,
            StringComparer.Ordinal);
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

            if (!TryResolveGoverningRegionChain(range, regionsById, out FirmwareRegion[] governingRegionChain))
            {
                issues.Add(new CompositionIssue(
                    InvalidView,
                    $"View '{view.ViewId}' does not resolve to one canonical physical region chain.",
                    view.ViewId));
                continue;
            }

            views.Add(view.ViewId, new ResolvedView(view.SpaceId, range, governingRegionChain));
        }

        return views;
    }

    private static LoweredRegionAccess LowerRegionAccess(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        IReadOnlyDictionary<string, ResolvedView> views,
        List<CompositionIssue> issues)
    {
        var regionsById = resolvedMap.ImageMap.Regions.ToDictionary(
            static region => region.RegionId,
            StringComparer.Ordinal);
        var resolvedRules = new Dictionary<string, ResolvedRegionAccessRule>(StringComparer.Ordinal);
        var requirements = new List<CompiledRegionAccessRequirement>();
        foreach (CompositionProfileRegionAccess rule in profile.RegionAccessRules)
        {
            if (!regionsById.TryGetValue(rule.RegionId, out FirmwareRegion? region))
            {
                issues.Add(new CompositionIssue(
                    InvalidRegionAccess,
                    $"Region access rule names unknown map region '{rule.RegionId}'.",
                    rule.RegionId));
                continue;
            }

            if (rule.Access == RegionAccessKind.Parts)
            {
                foreach (string allowedSubregionId in rule.AllowedSubregionIds)
                {
                    if (!regionsById.TryGetValue(allowedSubregionId, out FirmwareRegion? subregion) ||
                        !StringComparer.Ordinal.Equals(subregion.ParentRegionId, rule.RegionId))
                    {
                        issues.Add(new CompositionIssue(
                            InvalidRegionAccess,
                            $"Parts rule for '{rule.RegionId}' must name only direct canonical child regions.",
                            rule.RegionId));
                    }
                }
            }

            if (!TryResolveGoverningRegionChain(region.Range, regionsById, out FirmwareRegion[] governingRegionChain))
            {
                issues.Add(new CompositionIssue(
                    InvalidRegionAccess,
                    $"Region access rule '{rule.RegionId}' does not resolve to one canonical physical region chain.",
                    rule.RegionId));
                continue;
            }

            var requirement = new CompiledRegionAccessRequirement(
                rule.RegionId,
                MapRegionAccessKind(rule.Access),
                rule.Reason,
                rule.AllowedSubregionIds,
                ToCompiledRegionChain(governingRegionChain));
            requirements.Add(requirement);
            resolvedRules.Add(rule.RegionId, new ResolvedRegionAccessRule(region, requirement));
        }

        CompiledResolvedPhysicalView[] resolvedViews = [.. views
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => new CompiledResolvedPhysicalView(
                entry.Key,
                entry.Value.SpaceId,
                entry.Value.Range,
                ToCompiledRegionChain(entry.Value.GoverningRegionChain)))];
        return new LoweredRegionAccess(
            new CompiledRegionAccessContract(requirements, resolvedViews),
            resolvedRules,
            regionsById);
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

    private static bool TryResolveGoverningRegionChain(
        ByteRange range,
        IReadOnlyDictionary<string, FirmwareRegion> regionsById,
        out FirmwareRegion[] governingRegionChain)
    {
        FirmwareRegion? deepestContainingRegion = regionsById.Values
            .Where(region => region.Range.Contains(range))
            .OrderBy(static region => region.Range.Length)
            .ThenBy(static region => region.RegionId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (deepestContainingRegion is null)
        {
            governingRegionChain = [];
            return false;
        }

        var reverseChain = new List<FirmwareRegion>();
        for (FirmwareRegion? current = deepestContainingRegion; current is not null;)
        {
            reverseChain.Add(current);
            current = current.ParentRegionId is { } parentRegionId
                ? regionsById[parentRegionId]
                : null;
        }

        reverseChain.Reverse();
        governingRegionChain = [.. reverseChain];
        return true;
    }

    private static bool TryAuthorizeTargetWrite(
        string operationId,
        string targetViewId,
        ResolvedView target,
        LoweredRegionAccess regionAccess,
        List<CompositionIssue> issues)
    {
        ResolvedRegionAccessRule[] applicableRules = [.. regionAccess.Rules.Values
            .Where(rule => rule.Region.Range.Contains(target.Range))
            .OrderBy(rule => rule.Region.Range.Length)
            .ThenBy(rule => rule.Region.RegionId, StringComparer.Ordinal)];
        if (applicableRules.Length == 0)
        {
            AddAccessDenied(
                issues,
                operationId,
                $"target view '{targetViewId}' has no declared profile access rule");
            return false;
        }

        foreach (ResolvedRegionAccessRule rule in applicableRules)
        {
            if (!AllowsProfileTarget(rule, target.Range, regionAccess.RegionsById))
            {
                AddAccessDenied(
                    issues,
                    operationId,
                    $"profile access '{rule.Requirement.Access}' for region '{rule.Region.RegionId}' does not authorize its target range");
                return false;
            }
        }

        foreach (FirmwareRegion region in target.GoverningRegionChain)
        {
            if (!AllowsPhysicalTarget(region, target.Range, regionAccess.RegionsById))
            {
                AddAccessDenied(
                    issues,
                    operationId,
                    $"physical write constraint '{region.WriteConstraint}' for region '{region.RegionId}' does not authorize its target range");
                return false;
            }
        }

        return true;
    }

    private static bool AllowsProfileTarget(
        ResolvedRegionAccessRule rule,
        ByteRange targetRange,
        IReadOnlyDictionary<string, FirmwareRegion> regionsById)
    {
        return rule.Requirement.Access switch
        {
            CompiledRegionAccessKind.Hidden or CompiledRegionAccessKind.ReadOnly => false,
            CompiledRegionAccessKind.Whole => rule.Region.Range == targetRange,
            CompiledRegionAccessKind.Parts => rule.Requirement.AllowedSubregionIds.Any(subregionId =>
                regionsById[subregionId].Range == targetRange &&
                StringComparer.Ordinal.Equals(regionsById[subregionId].ParentRegionId, rule.Region.RegionId)),
            CompiledRegionAccessKind.ExplicitRange =>
                rule.Region.Range.Contains(targetRange) && IsAligned(targetRange, rule.Region.Alignment),
            _ => throw new InvalidOperationException("Validated V2 lowering encountered an unknown region access kind."),
        };
    }

    private static bool AllowsPhysicalTarget(
        FirmwareRegion region,
        ByteRange targetRange,
        IReadOnlyDictionary<string, FirmwareRegion> regionsById)
    {
        return region.WriteConstraint switch
        {
            FirmwareWriteConstraint.Forbidden => false,
            FirmwareWriteConstraint.WholeRegion => region.Range == targetRange,
            FirmwareWriteConstraint.DeclaredSubregions => regionsById.Values.Any(child =>
                StringComparer.Ordinal.Equals(child.ParentRegionId, region.RegionId) && child.Range == targetRange),
            FirmwareWriteConstraint.ExplicitRange =>
                region.Range.Contains(targetRange) && IsAligned(targetRange, region.Alignment),
            _ => throw new InvalidOperationException("Validated V2 lowering encountered an unknown firmware write constraint."),
        };
    }

    private static bool IsAligned(ByteRange range, int alignment)
    {
        return range.Start % alignment == 0 && range.Length % alignment == 0;
    }

    private static void AddAccessDenied(
        List<CompositionIssue> issues,
        string operationId,
        string detail)
    {
        issues.Add(new CompositionIssue(
            RegionAccessDenied,
            $"Operation '{operationId}' {detail}.",
            operationId));
    }

    private static CompiledPhysicalRegionConstraint[] ToCompiledRegionChain(
        IEnumerable<FirmwareRegion> governingRegionChain)
    {
        return [.. governingRegionChain.Select(static region => new CompiledPhysicalRegionConstraint(
            region.RegionId,
            region.WriteConstraint,
            region.Alignment))];
    }

    private static CompiledRegionAccessKind MapRegionAccessKind(RegionAccessKind access)
    {
        return access switch
        {
            RegionAccessKind.Hidden => CompiledRegionAccessKind.Hidden,
            RegionAccessKind.ReadOnly => CompiledRegionAccessKind.ReadOnly,
            RegionAccessKind.Whole => CompiledRegionAccessKind.Whole,
            RegionAccessKind.Parts => CompiledRegionAccessKind.Parts,
            RegionAccessKind.ExplicitRange => CompiledRegionAccessKind.ExplicitRange,
            _ => throw new InvalidOperationException("Validated V2 lowering encountered an unknown region access kind."),
        };
    }

    private sealed record ResolvedView(
        string SpaceId,
        ByteRange Range,
        IReadOnlyList<FirmwareRegion> GoverningRegionChain);

    private sealed record ResolvedRegionAccessRule(
        FirmwareRegion Region,
        CompiledRegionAccessRequirement Requirement);

    private sealed record LoweredRegionAccess(
        CompiledRegionAccessContract Contract,
        IReadOnlyDictionary<string, ResolvedRegionAccessRule> Rules,
        IReadOnlyDictionary<string, FirmwareRegion> RegionsById);
}
