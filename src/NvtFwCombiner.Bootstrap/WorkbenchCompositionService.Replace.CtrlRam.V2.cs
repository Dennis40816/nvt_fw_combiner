using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    internal static MetadataPlanDefinition CreateCtrlRamReportMetadataPlan(
        string icId,
        long referenceCapacity)
    {
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc.GetValueOrDefault(icId) ??
            throw new InvalidDataException(
                $"CtrlRAM report metadata requires the reviewed {icId} Standard Merge definition.");
        CompiledComposition[] candidates =
        [
            .. CreateStandardMergeMetadataSelectionCandidates(registration)
                .Select(selectedSlots =>
                {
                    registration.TryCompile(
                        referenceCapacity,
                        selectedSlots,
                        out CompiledComposition? composition,
                        out IReadOnlyList<CompositionIssue> issues);
                    return (Composition: composition, Issues: issues);
                })
                .Where(static candidate =>
                    candidate.Composition is not null &&
                    candidate.Issues.Count == 0)
                .Select(static candidate => candidate.Composition!)
                .DistinctBy(static composition =>
                    composition.V2Details!.Provenance.ResolvedMap.ImageMap.MapId),
        ];
        CompiledComposition[] matches =
        [
            .. candidates.Where(composition =>
                composition.Plan.OutputInitialization.Capacity == referenceCapacity),
        ];
        if (matches.Length == 0)
        {
            matches =
            [
                .. candidates.Where(composition =>
                    composition.Plan.AddressSpaces.Single(space =>
                        StringComparer.Ordinal.Equals(
                            space.AddressSpaceId,
                            CompositionAddressSpaceIds.TpInput)).Length <=
                    referenceCapacity),
            ];
        }

        CompiledComposition metadataComposition = matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"CtrlRAM report metadata resolved {matches.Length} reviewed {icId} Standard Merge maps for capacity 0x{referenceCapacity:X}.");
        MetadataPlanDefinition sourceMetadataPlan =
            registration.CreateMetadataPlan(metadataComposition);
        MetadataPlanEntry[] entries =
        [
            .. sourceMetadataPlan.Entries
                .Where(static entry => entry.Purposes.Contains(
                    MetadataReferencePurpose.ReportClassification))
                .Select(static entry => new MetadataPlanEntry(
                    entry.BindingId,
                    entry.SpaceId,
                    CompositionAddressSpaceIds.ReferenceBase,
                    entry.FamilyDefinition,
                    entry.ResolvedMap,
                    entry.MetadataSetBinding,
                    entry.StructureDefinition,
                    entry.TargetReferences,
                    entry.Purposes,
                    entry.EvidenceRefs)),
        ];
        return new MetadataPlanDefinition(
            entries,
            sourceMetadataPlan.SourceIdentity);
    }

    private static IEnumerable<IReadOnlyCollection<string>>
        CreateStandardMergeMetadataSelectionCandidates(
            BuiltInV2Registration registration)
    {
        yield return [];
        if (registration.InputSelectionGroupMemberSlotIds.Count != 0)
        {
            yield return registration.InputSelectionGroupMemberSlotIds;
        }
    }

    private static V2CompositionPlanCompileResult CompileCtrlRamV2(
        CtrlRamReplaceRunContext context,
        CtrlRamV2Route route,
        TopologySelection topology,
        FirmwareArtifactPayload referencePayload)
    {
        V2RuntimeReferenceReplaceInputBinding[] bindings =
        [
            new(
                CompositionAddressSpaceIds.ReferenceBase,
                CompositionAddressSpaceIds.ReferenceBase,
                referencePayload.LengthBytes),
            .. context.SelectedSources.Select(source => new V2RuntimeReferenceReplaceInputBinding(
                CtrlRamSlotId(source.SourceId),
                "ctrlram-source",
                context.SelectedSourceLengths[source.SourceId])),
        ];
        ExplicitMapping[] mappings =
        [
            .. context.SelectedSources
                .SelectMany(source => source.Blocks
                    .OrderBy(static block => block.FirmwareRange.Start)
                    .ThenBy(static block => block.SourceOffset)
                    .Select(block => (Source: source, Block: block)))
                .Select((entry, index) =>
                {
                    long sourceLength = context.SelectedSourceLengths[entry.Source.SourceId];
                    long effectiveLength = Math.Min(
                        entry.Block.FirmwareRange.Length,
                        sourceLength - entry.Block.SourceOffset);
                    return new ExplicitMapping(
                    FormattableString.Invariant($"replace-{entry.Source.SourceId}-{index:D2}"),
                    100 + index,
                    ExplicitMappingOperationKind.ReplaceRange,
                    CtrlRamSlotId(entry.Source.SourceId),
                    new ByteRange(entry.Block.SourceOffset, effectiveLength),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(entry.Block.FirmwareRange.Start, effectiveLength),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "Copy the selected CtrlRAM source prefix into the exact physical range selected from the reference firmware.");
                }),
        ];
        V2RuntimeReferenceReplaceFirmwareVersionEdit? firmwareVersionEdit =
            CtrlRamV2FirmwareVersionAdapter.Create(context.FirmwareVersionWritePlan);
        LegacyCombinerDiffDlmPolicy? diffDlmPolicy =
            context.CommandPlan?.Branch == LegacyCombinerPostbuildBranch.Cascade
                ? context.PostbuildProfile?.DiffDlmPolicy
                : null;
        TpCtrlRamPostbuildSource? diffDlmSource = diffDlmPolicy is null
            ? null
            : context.SelectedSources.SingleOrDefault(source =>
                StringComparer.Ordinal.Equals(
                    source.SourceFileName,
                    diffDlmPolicy.SourceFileName));
        V2RuntimeReferenceReplacePostbuildPolicy? postbuildPolicy =
            diffDlmPolicy is null
                ? null
                : new V2RuntimeReferenceReplacePostbuildPolicy(
                    diffDlmPolicy.PolicyId,
                    diffDlmSource is null ? null : CtrlRamSlotId(diffDlmSource.SourceId),
                    diffDlmSource?.Blocks.Select(block =>
                        new ByteRange(
                            block.SourceOffset,
                            block.FirmwareRange.Length)),
                    diffDlmSource is null
                        ? null
                        : WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmPlaceholder,
                    diffDlmPolicy.FirmwareConfigBackupAuthority,
                    diffDlmPolicy.GetResolvedFirmwareConfigBackupAuthority(topology.ChipCount),
                    diffDlmPolicy.GetExpectedFirmwareConfigBackupStart(topology.ChipCount),
                    diffDlmPolicy.FirmwareConfigBackupLength,
                    WorkbenchIssueCodes.ReplaceCtrlRamFirmwareConfigBackupPlacementInvalid,
                    WorkbenchIssueCodes.ReplaceCtrlRamDynamicDiffDlmInactiveMutation,
                    WorkbenchIssueCodes.ReplaceCtrlRamFirmwareConfigBackupPlacementUnexpected);
        LegacyCombinerPostbuildCommandPlan commandPlan = context.CommandPlan ??
            throw new InvalidOperationException("CtrlRAM V2 compilation requires one resolved postbuild command plan.");
        ByteRange[] stagedTargetRanges =
        [
            .. LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(commandPlan)
                .Select(static block => block.FirmwareRange),
        ];
        ExternalProcessorWriteRangeSection[] postbuildWriteRangeSections =
        [
            .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
                    commandPlan,
                    referencePayload.LengthBytes,
                    stagedTargetRanges,
                    stagedTargetRanges),
        ];

        return BuiltInV2BundleRegistry.All[route.BundleId].CompileRuntimeReferenceReplace(
            route.ProfileId,
            route.ProfileVersion,
            route.Key.IcId,
            ExperienceIds.CtrlRamReplace,
            topology,
            [referencePayload],
            new V2RuntimeReferenceReplaceCompileRequest(
                bindings,
                mappings,
                firmwareVersionEdit,
                postbuildPolicy,
                postbuildWriteRangeSections));
    }
}
