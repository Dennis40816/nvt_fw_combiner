using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static CompositionProfileDefinition CreateCtrlRamReplaceProfile(
        string icId,
        IcNumberSelection selection,
        long capacity,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions,
        IReadOnlyList<TpFlashMapRegion> selectedRegions,
        LegacyCombinerPostbuildProfile postbuildProfile,
        LegacyCombinerPostbuildCommandPlan commandPlan,
        IReadOnlyList<LegacyCombinerPostbuildWriteRange> postbuildWriteRangeSections,
        FirmwareConfigVersionWritePlan? firmwareVersionWritePlan)
    {
        string normalizedIc = icId.ToLowerInvariant();
        ByteRange[] postbuildWriteRanges = [.. postbuildWriteRangeSections.Select(section => section.Range)];
        List<AddressSpace> addressSpaces =
        [
            new(CompositionAddressSpaceIds.ReferenceBase, capacity, AddressSpaceMutability.Immutable),
            new(CompositionAddressSpaceIds.OutputImage, capacity, AddressSpaceMutability.Mutable),
        ];
        List<CompositionOperation> operations = [];
        List<ProfileRegion> profileRegions = [];
        List<RegionAccessRule> accessRules = [];
        List<CompiledValidationRequirement> validationRequirements = [];

        foreach (TpFlashMapRegion region in ctrlRamRegions.OrderBy(region => region.Range.Start))
        {
            profileRegions.Add(new ProfileRegion(
                region.RegionId,
                CompositionAddressSpaceIds.OutputImage,
                region.Range,
                RegionAtomicity.Whole,
                RegionWritePolicy.WholeOnly,
                processorDependencyIds: [postbuildProfile.ProcessorId],
                classificationTags: ["tp-ctrlram"]));
            accessRules.Add(new RegionAccessRule(
                region.RegionId,
                RegionAccessKind.Whole,
                "CtrlRAM Replace allows whole-region replacement before the postbuild processor refreshes integrity data."));
        }

        int sequence = 100;
        List<ExternalProcessorStagedSourceBinding> stagedSourceBindings = [];
        foreach (TpFlashMapRegion region in selectedRegions.OrderBy(region => region.Range.Start))
        {
            string slotId = CtrlRamSlotId(region.RegionId);
            addressSpaces.Add(new AddressSpace(
                slotId,
                region.Range.Length,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.TruncateWithWarning));
            stagedSourceBindings.Add(new ExternalProcessorStagedSourceBinding(
                slotId,
                new ByteRange(0, region.Range.Length),
                region.Range));
        }

        ByteRange[] ctrlRamRanges = [.. ctrlRamRegions.Select(region => region.Range)];
        LegacyCombinerPostbuildWriteRange[] processorOnlyRanges =
        [
            .. postbuildWriteRangeSections
                .Where(section => !ctrlRamRanges.Any(ctrlRamRange => ctrlRamRange.Contains(section.Range)))
                .GroupBy(section => (section.Range, section.SectionId))
                .Select(group => group.First())
                .OrderBy(section => section.Range.Start)
                .ThenBy(section => section.Range.Length)
                .ThenBy(section => section.SectionId, StringComparer.Ordinal),
        ];
        foreach ((LegacyCombinerPostbuildWriteRange section, int index) in processorOnlyRanges.Select((section, index) => (section, index)))
        {
            profileRegions.Add(new ProfileRegion(
                FormattableString.Invariant($"postbuild-{section.SectionId}-{index:D2}"),
                CompositionAddressSpaceIds.OutputImage,
                section.Range,
                RegionAtomicity.ExplicitMapping,
                RegionWritePolicy.GeneralExplicit,
                processorDependencyIds: [postbuildProfile.ProcessorId],
                classificationTags: ["postbuild", section.SectionId]));
        }

        if (firmwareVersionWritePlan is not null)
        {
            AddFirmwareVersionWriteRegion(
                profileRegions,
                accessRules,
                "firmware-version-source",
                firmwareVersionWritePlan.FirmwareVersionAndBarRange,
                "TP FW version and complement source for the legacy Combiner Backup propagation.");
            AddFirmwareVersionWriteRegion(
                profileRegions,
                accessRules,
                "firmware-sub-version-source",
                firmwareVersionWritePlan.FirmwareSubVersionRange,
                "TP FW sub-version source for the legacy Combiner Backup propagation.");
            operations.Add(CompositionOperation.PatchScalar(
                "patch-fw-version-and-bar",
                10,
                CompositionAddressSpaceIds.OutputImage,
                firmwareVersionWritePlan.FirmwareVersionAndBarRange,
                firmwareVersionWritePlan.FirmwareVersionAndBarBytes.ToArray(),
                OverlapPolicy.ReplaceExisting,
                "Apply the user-confirmed TP FW version before the approved legacy Combiner postbuild sequence."));
            operations.Add(CompositionOperation.PatchScalar(
                "patch-fw-sub-version",
                20,
                CompositionAddressSpaceIds.OutputImage,
                firmwareVersionWritePlan.FirmwareSubVersionRange,
                firmwareVersionWritePlan.FirmwareSubVersionBytes.ToArray(),
                OverlapPolicy.ReplaceExisting,
                "Apply the user-confirmed TP FW sub-version before the approved legacy Combiner postbuild sequence."));
            validationRequirements.Add(LegacyProfileValidationRequirements.FirmwareConfigBackupVersion(
                firmwareVersionWritePlan.FirmwareVersion,
                firmwareVersionWritePlan.FirmwareSubVersion));
        }

        operations.Add(CompositionOperation.RunExternalProcessor(
            $"postbuild-{commandPlan.Branch.ToString().ToLowerInvariant()}",
            sequence,
            CompositionAddressSpaceIds.OutputImage,
            new ByteRange(0, capacity),
            new ExternalProcessorInvocation(
                postbuildProfile.ProcessorId,
                postbuildProfile.ToolBindingId,
                [new ByteRange(0, capacity)],
                postbuildWriteRanges,
                stagedSourceBindings,
                allowedWriteRangeSections: postbuildWriteRangeSections.Select(section =>
                    new ExternalProcessorWriteRangeSection(section.SectionId, section.Range, section.SourceRange))),
            OverlapPolicy.ReplaceExisting,
            $"Run {commandPlan.Branch} legacy Combiner postbuild and stage selected CtrlRAM BINs for Combiner pasteback. Combiner command: {FormatPostbuildCommandBlock(commandPlan)}."));

        return new CompositionProfileDefinition(
            $"{normalizedIc}-ctrlram-replace-workbench",
            "0.5.0",
            icId,
            IcWorkflowIds.CtrlRamReplace,
            CompositionKind.Replace,
            IcWorkflowIds.CtrlRamReplace,
            $"{normalizedIc}-ctrlram-replace.bin",
            ImageInitialization.Reference(CompositionAddressSpaceIds.OutputImage, CompositionAddressSpaceIds.ReferenceBase, capacity),
            addressSpaces,
            operations,
            profileRegions,
            accessRules,
            selection.Mode,
            validationRequirements);
    }

    private static void AddFirmwareVersionWriteRegion(
        List<ProfileRegion> profileRegions,
        List<RegionAccessRule> accessRules,
        string regionId,
        ByteRange range,
        string reason)
    {
        profileRegions.Add(new ProfileRegion(
            regionId,
            CompositionAddressSpaceIds.OutputImage,
            range,
            RegionAtomicity.Whole,
            RegionWritePolicy.WholeOnly,
            classificationTags: ["firmware-config", "firmware-version"]));
        accessRules.Add(new RegionAccessRule(regionId, RegionAccessKind.Whole, reason));
    }
}
