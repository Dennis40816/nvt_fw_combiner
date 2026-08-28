using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    /// <summary>
    /// Lowers one already admitted resolved-map declaration without reopening authority.
    /// The phase consumes only the accepted catalog entry and its resolved facts.
    /// </summary>
    private static class ResolvedMapCompilationPhase
    {
        internal static V2CompositionPlanCompileResult Compile(
            ProfileBundleIdentity bundleIdentity,
            TrustedCompositionProfileCatalogEntry profileEntry,
            FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
            IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityAdmissions,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            List<CompositionIssue> issues)
        {
            CompositionProfileDefinition profile = profileEntry.Profile;
            MutableCompositionProfileSpace output = AssertOutputSpace(profile);
            if (output.Capacity is RuntimeRequestProfileCapacity)
            {
                AddUnsupported(issues, "runtime-request output capacity requires logical-output V2 lowering");
            }
            else if (output.Capacity is not ResolvedMapProfileCapacity)
            {
                AddUnsupported(issues, "map-bound output images require resolved-map capacity");
            }

            if (issues.Count != 0) { return V2CompositionPlanCompileResult.Failed(issues); }

            ResolvedInputSelection inputSelection = ResolveInputSelection(
                profile, resolvedMap, selectedInputSlotIds, issues);
            if (issues.Count != 0) { return V2CompositionPlanCompileResult.Failed(issues); }

            Dictionary<string, AddressSpace> spaces = LowerAddressSpaces(
                profile,
                profileEntry.Family.Family,
                resolvedMap,
                issues,
                inputSelection.ActiveSlotIds);
            if (issues.Count != 0) { return V2CompositionPlanCompileResult.Failed(issues); }

            Dictionary<string, ResolvedView> views = LowerViews(
                profile,
                resolvedMap,
                spaces,
                issues,
                inputSelection.ActiveViewIds);
            if (issues.Count != 0) { return V2CompositionPlanCompileResult.Failed(issues); }

            LoweredRegionAccess regionAccess = LowerRegionAccess(
                profile,
                resolvedMap,
                views,
                issues,
                inputSelection.ActiveRegionIds);
            if (issues.Count != 0) { return V2CompositionPlanCompileResult.Failed(issues); }

            CompositionOperation[] operations = LowerOperations(
                profile,
                resolvedMap,
                spaces,
                views,
                regionAccess,
                issues,
                activeOperationIds: inputSelection.ActiveOperationIds);
            ValidateOperationOverlaps(operations, issues);
            if (issues.Count != 0) { return V2CompositionPlanCompileResult.Failed(issues); }

            ImageInitialization[] initializations = LowerInitializations(profile, spaces, issues);
            if (issues.Count != 0) { return V2CompositionPlanCompileResult.Failed(issues); }

            var plan = new CompositionPlan(
                initializations,
                output.SpaceId,
                spaces.Values,
                operations);
            return Succeed(
                profile,
                bundleIdentity,
                profileEntry.EntryIdentity,
                new ResolvedMapV2CompilationContext(resolvedMap),
                plan,
                profile.InputSlots
                    .Where(slot => inputSelection.ActiveSlotIds.Contains(slot.SlotId))
                    .Select(slot => MapInputSlot(
                        slot,
                        resolvedMap,
                        forceRequired: !slot.Required)),
                profile.Spaces
                    .OfType<InputArtifactProfileSpace>()
                    .Where(space => inputSelection.ActiveSlotIds.Contains(space.SlotId))
                    .Select(MapInputSpaceBinding),
                regionAccess.Contract,
                capabilityAdmissions,
                runtimeExecutable: profile.Promotion.Stage == CompiledProfilePromotionStage.Supported,
                additionalValidationRequirements: LowerInputValidations(profile, views),
                inputSelectionGroups: inputSelection.Groups);
        }
    }
}
