using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static CtrlRamReplaceRunContext CreateCtrlRamReplaceRunContext(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        List<CompositionIssue> validationIssues = [];
        List<CompositionIssue> advisoryIssues = [];
        LegacyCombinerPostbuildProfile? postbuildProfile = null;
        LegacyCombinerPostbuildCommandPlan? commandPlan = null;
        FirmwareConfigVersionWritePlan? firmwareVersionWritePlan = null;
        FirmwareConfigMetadata? baseFirmwareConfig = null;
        IReadOnlyList<TpFlashMapRegion> regions = [];
        IReadOnlyList<TpCtrlRamPostbuildSource> sources = [];

        string? basePath = null;
        byte[]? baseBytes = null;
        long baseLength = 0;
        if (!slotPaths.TryGetValue(WorkbenchSlotIds.ReplaceBase, out string? suppliedBasePath) ||
            string.IsNullOrWhiteSpace(suppliedBasePath))
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.InputMissing,
                "Base firmware BIN is required before CtrlRAM Replace can run.",
                WorkbenchSlotIds.ReplaceBase));
        }
        else
        {
            basePath = Path.GetFullPath(suppliedBasePath);
            baseBytes = TryReadFirmwareImage(basePath);
            if (baseBytes is null)
            {
                validationIssues.Add(new CompositionIssue(
                    WorkbenchIssueCodes.InputArtifactReadFailed,
                    "Base firmware BIN path does not exist.",
                    WorkbenchSlotIds.ReplaceBase));
            }
            else
            {
                baseLength = baseBytes.LongLength;
                if (baseLength <= 0)
                {
                    validationIssues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        "Base firmware BIN must not be empty.",
                        WorkbenchSlotIds.ReplaceBase));
                }

                if (TryReadFirmwareConfigBackupMetadata(
                        icId,
                        baseBytes,
                        out FirmwareConfigMetadata parsedFirmwareConfig))
                {
                    baseFirmwareConfig = parsedFirmwareConfig;
                }
            }
        }

        if (basePath is not null && baseLength > 0)
        {
            if (!TryGetPostbuildProfile(icId, basePath, out postbuildProfile, out CompositionIssue? postbuildIssue, baseBytes))
            {
                validationIssues.Add(postbuildIssue!);
            }
            else
            {
                try
                {
                    int? reportedChipCount =
                        baseFirmwareConfig is { ChipNumber: > 0 } reportedFirmwareConfig
                            ? reportedFirmwareConfig.ChipNumber
                            : null;
                    commandPlan = LegacyCombinerPostbuildPlanner.CreatePlan(
                        postbuildProfile!,
                        selection,
                        reportedChipCount);
                }
                catch (ArgumentException exception)
                {
                    validationIssues.Add(new CompositionIssue(
                        WorkbenchIssueCodes.ReplaceCtrlRamIcNumberUnsupported,
                        exception.Message,
                        "number"));
                }
            }
        }
        else if (GetPostbuildProfiles(icId).Count == 0)
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildProfileMissing,
                $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild"));
        }

        if (commandPlan is not null &&
            baseFirmwareConfig is { } firmwareConfig &&
            firmwareConfig.ChipNumber != 0 &&
            commandPlan.TopologyCount != firmwareConfig.ChipNumber)
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch,
                $"Selected Number is {commandPlan.TopologyCount} IC, but the base firmware FWConfig reports {firmwareConfig.ChipNumber} IC. Switch Number to the matching plan and review the CtrlRAM inputs before Build.",
                "number"));
        }

        if (commandPlan is not null &&
            baseFirmwareConfig is { } chipCountMetadata &&
            FirmwareConfigChipCountDiagnostics.CreateZeroIssue(
                chipCountMetadata,
                requirement: commandPlan.ChipCountRequirement,
                operationId: WorkbenchSlotIds.ReplaceBase,
                dependencyReason:
                    "Masked DiffDLM uses IC Count to resolve active records and the profile-owned FWConfig Backup policy.")
                is { } chipCountIssue)
        {
            if (StringComparer.Ordinal.Equals(chipCountIssue.Severity, CompositionIssueSeverity.Error))
            {
                validationIssues.Add(chipCountIssue);
            }
            else
            {
                advisoryIssues.Add(chipCountIssue);
            }
        }

        if (commandPlan is not null || basePath is null)
        {
            sources = commandPlan is null
                ? BuiltInTpFlashMapCatalog.GetPostbuildCtrlRamSources(
                    postbuildProfile?.IcId ?? icId,
                    selection,
                    postbuildProfile)
                : BuiltInTpFlashMapCatalog.GetPostbuildCtrlRamSources(
                    postbuildProfile?.IcId ?? icId,
                    commandPlan);
            regions = [.. sources.SelectMany(source => source.Regions)
                .DistinctBy(region => region.RegionId, StringComparer.Ordinal)
                .OrderBy(region => region.Range.Start)];
        }

        if (regions.Count == 0)
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamNoMappedRegion,
                $"No postbuild-mapped CtrlRAM region is available for {icId} / {number}.",
                IcWorkflowIds.CtrlRamReplace));
        }

        HashSet<string> availableSourceSlots =
        [
            .. sources.Select(source => CtrlRamSlotId(source.SourceId)),
        ];
        foreach ((string slotId, string path) in slotPaths.Where(pair =>
                     pair.Key.StartsWith(
                         WorkbenchSlotIds.ReplaceCtrlRamPrefix,
                         StringComparison.Ordinal) &&
                     !string.IsNullOrWhiteSpace(pair.Value) &&
                     !availableSourceSlots.Contains(pair.Key)))
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamSourceUnavailable,
                $"CtrlRAM source '{slotId}' is unavailable for the resolved {icId} / {number} route.",
                slotId));
        }

        List<TpCtrlRamPostbuildSource> selectedSources =
        [
            .. sources
                .Where(source => slotPaths.TryGetValue(CtrlRamSlotId(source.SourceId), out string? path) &&
                    !string.IsNullOrWhiteSpace(path)),
        ];
        if (selectedSources.Count == 0)
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamNoRegionInput,
                "Select at least one CtrlRAM replacement BIN.",
                IcWorkflowIds.CtrlRamReplace));
        }

        Dictionary<string, long> selectedSourceLengths = new(StringComparer.Ordinal);
        foreach (TpCtrlRamPostbuildSource source in selectedSources)
        {
            string slotId = CtrlRamSlotId(source.SourceId);
            string path = Path.GetFullPath(slotPaths[slotId]);
            if (!File.Exists(path))
            {
                validationIssues.Add(new CompositionIssue(
                    WorkbenchIssueCodes.InputArtifactReadFailed,
                    $"CtrlRAM source '{source.SourceFileName}' does not exist.",
                    slotId));
                continue;
            }

            long length = new FileInfo(path).Length;
            LegacyCombinerBlockArgument? unsafeBlock = source.Blocks.FirstOrDefault(block =>
                block.SourceOffset > 0 && checked(block.SourceOffset + block.FirmwareRange.Length) > length);
            LegacyCombinerDiffDlmPolicy? diffDlmPolicy =
                commandPlan?.Profile.DiffDlmPolicy is { } candidate &&
                StringComparer.Ordinal.Equals(candidate.SourceFileName, source.SourceFileName)
                    ? candidate
                    : null;
            LegacyCombinerBlockArgument? missingActiveDlmBlock =
                diffDlmPolicy is null
                    ? null
                    : source.Blocks.FirstOrDefault(block =>
                        checked(block.SourceOffset + block.FirmwareRange.Length) > length);
            long? requiredActiveRecordPrefix = diffDlmPolicy?.GetRequiredSourceLength(
                commandPlan!.TopologyCount);
            bool missingCompleteActiveRecord =
                requiredActiveRecordPrefix is { } requiredPrefix &&
                length < requiredPrefix;
            if (length <= 0 ||
                unsafeBlock is not null ||
                missingActiveDlmBlock is not null ||
                missingCompleteActiveRecord)
            {
                validationIssues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    length <= 0
                        ? $"CtrlRAM source '{source.SourceFileName}' must not be empty."
                        : missingCompleteActiveRecord
                            ? $"DiffDLM source is too short for {commandPlan!.TopologyCount} IC: complete active records require 0x{requiredActiveRecordPrefix!.Value:X} bytes, but the selected file has 0x{length:X} bytes."
                        : missingActiveDlmBlock is not null
                            ? $"DiffDLM source is missing active record '{missingActiveDlmBlock.BlockId}' ending at 0x{checked(missingActiveDlmBlock.SourceOffset + missingActiveDlmBlock.FirmwareRange.Length):X}."
                        : $"CtrlRAM source '{source.SourceFileName}' is too short for nonzero source offset 0x{unsafeBlock!.SourceOffset:X} and section length {unsafeBlock.FirmwareRange.Length} bytes.",
                    slotId));
                continue;
            }

            selectedSourceLengths.Add(source.SourceId, Math.Min(length, source.RequiredLength));
        }

        if (commandPlan is not null && baseLength > 0)
        {
            long requiredCapacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(
                commandPlan,
                selectedSources.SelectMany(source => source.Regions).Select(region => region.Range));
            if (baseLength < requiredCapacity)
            {
                validationIssues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"Base firmware BIN is too short for {icId} / {number} CtrlRAM postbuild (actual {baseLength} bytes, required at least {requiredCapacity} bytes).",
                    WorkbenchSlotIds.ReplaceBase));
            }
        }

        if (firmwareVersionEdit is not null && baseBytes is not null && commandPlan is not null && TryReadFirmwareConfigBackupMetadata(icId, baseBytes, out FirmwareConfigMetadata backupMetadata) &&
            !TryCreateCtrlRamFirmwareVersionWritePlan(
                backupMetadata,
                postbuildProfile!,
                commandPlan,
                firmwareVersionEdit,
                baseBytes,
                out firmwareVersionWritePlan,
                out CompositionIssue? firmwareVersionIssue))
        {
            validationIssues.Add(firmwareVersionIssue!);
        }
        else if (firmwareVersionEdit is not null && basePath is not null && commandPlan is not null &&
                 firmwareVersionWritePlan is null)
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamFirmwareVersionSourceInvalid,
                "TP FW version editing requires readable metadata from the canonical NVT Backup.",
                WorkbenchSlotIds.ReplaceBase));
        }

        return new CtrlRamReplaceRunContext(
            selection,
            basePath,
            baseBytes,
            postbuildProfile,
            commandPlan,
            firmwareVersionWritePlan,
            regions,
            sources,
            selectedSources,
            selectedSourceLengths,
            validationIssues,
            advisoryIssues);
    }

    private static InputArtifactBinding[] CreateCtrlRamReplaceBindings(
        CompiledComposition compiledComposition,
        CtrlRamReplaceRunContext context,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot? acceptedSession = null)
    {
        return [
            acceptedSession is null
                ? CompiledCompositionInputBindingFactory.Create(
                    compiledComposition,
                    CompositionAddressSpaceIds.ReferenceBase,
                    context.BasePath!)
                : CreateAcceptedSessionBinding(
                    compiledComposition,
                    CompositionAddressSpaceIds.ReferenceBase,
                    context.BasePath!,
                    acceptedSession),
            .. context.SelectedSources
                .Select(source => CtrlRamSlotId(source.SourceId))
                .Select(sourceSpaceId => acceptedSession is null
                    ? CompiledCompositionInputBindingFactory.Create(
                        compiledComposition,
                        sourceSpaceId,
                        Path.GetFullPath(slotPaths[sourceSpaceId]))
                    : CreateAcceptedSessionBinding(
                        compiledComposition,
                        sourceSpaceId,
                        slotPaths[sourceSpaceId],
                        acceptedSession)),
        ];
    }

    private sealed record CtrlRamReplaceRunContext(
        IcNumberSelection Selection,
        string? BasePath,
        byte[]? BaseBytes,
        LegacyCombinerPostbuildProfile? PostbuildProfile,
        LegacyCombinerPostbuildCommandPlan? CommandPlan,
        FirmwareConfigVersionWritePlan? FirmwareVersionWritePlan,
        IReadOnlyList<TpFlashMapRegion> Regions,
        IReadOnlyList<TpCtrlRamPostbuildSource> Sources,
        IReadOnlyList<TpCtrlRamPostbuildSource> SelectedSources,
        IReadOnlyDictionary<string, long> SelectedSourceLengths,
        IReadOnlyList<CompositionIssue> ValidationIssues,
        IReadOnlyList<CompositionIssue> AdvisoryIssues);
}
