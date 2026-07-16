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
        LegacyCombinerPostbuildProfile? postbuildProfile = null;
        LegacyCombinerPostbuildCommandPlan? commandPlan = null;
        FirmwareConfigVersionWritePlan? firmwareVersionWritePlan = null;
        IReadOnlyList<TpFlashMapRegion> regions = [];
        IReadOnlyList<TpCtrlRamPostbuildSource> sources = [];

        (string? basePath, long baseLength) = ResolveCtrlRamBaseInput(slotPaths, validationIssues);

        if (basePath is not null && baseLength > 0)
        {
            if (!TryGetPostbuildProfile(icId, basePath, out postbuildProfile, out CompositionIssue? postbuildIssue))
            {
                validationIssues.Add(postbuildIssue!);
            }
            else
            {
                try
                {
                    commandPlan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile!, selection);
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
        else if (IcMetadataFacade.GetPostbuildProfiles(icId).Count == 0)
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildProfileMissing,
                $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild"));
        }

        if (postbuildProfile is not null || basePath is null)
        {
            sources = TpFlashMapCatalog.GetPostbuildCtrlRamSources(icId, selection, postbuildProfile);
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

        List<TpCtrlRamPostbuildSource> selectedSources =
        [
            .. sources
                .Where(source => IsSlotSupplied(slotPaths, CtrlRamSlotId(source.SourceId))),
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
            if (length <= 0 || unsafeBlock is not null)
            {
                validationIssues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    length <= 0
                        ? $"CtrlRAM source '{source.SourceFileName}' must not be empty."
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
                    $"Base flash BIN is too short for {icId} / {number} CtrlRAM postbuild (actual {baseLength} bytes, required at least {requiredCapacity} bytes).",
                    WorkbenchSlotIds.ReplaceBase));
            }
        }

        if (firmwareVersionEdit is not null && basePath is not null && commandPlan is not null &&
            TryReadFirmwareConfigBackupMetadata(icId, basePath, out FirmwareConfigMetadata backupMetadata) &&
            !TryCreateCtrlRamFirmwareVersionWritePlan(
                backupMetadata,
                commandPlan,
                firmwareVersionEdit,
                baseLength,
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
            baseLength,
            postbuildProfile,
            commandPlan,
            firmwareVersionWritePlan,
            regions,
            sources,
            selectedSources,
            selectedSourceLengths,
            validationIssues);
    }

    private static (string? Path, long Length) ResolveCtrlRamBaseInput(
        IReadOnlyDictionary<string, string> slotPaths,
        List<CompositionIssue> validationIssues)
    {
        if (!slotPaths.TryGetValue(WorkbenchSlotIds.ReplaceBase, out string? suppliedBasePath) ||
            string.IsNullOrWhiteSpace(suppliedBasePath))
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.InputMissing,
                "Base flash BIN is required before CtrlRAM Replace can run.",
                WorkbenchSlotIds.ReplaceBase));
            return (null, 0);
        }

        string basePath = Path.GetFullPath(suppliedBasePath);
        if (!File.Exists(basePath))
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.InputArtifactReadFailed,
                "Base flash BIN path does not exist.",
                WorkbenchSlotIds.ReplaceBase));
            return (basePath, 0);
        }

        long baseLength = new FileInfo(basePath).Length;
        if (baseLength <= 0)
        {
            validationIssues.Add(new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                "Base flash BIN must not be empty.",
                WorkbenchSlotIds.ReplaceBase));
        }

        return (basePath, baseLength);
    }

    private static bool IsSlotSupplied(
        IReadOnlyDictionary<string, string> slotPaths,
        string slotId)
    {
        return slotPaths.TryGetValue(slotId, out string? path) &&
            !string.IsNullOrWhiteSpace(path);
    }

    private static InputArtifactBinding[] CreateCtrlRamReplaceBindings(
        CtrlRamReplaceRunContext context,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return [
            new(CompositionAddressSpaceIds.ReferenceBase, WorkbenchSlotIds.ReplaceBase, context.BasePath!),
            .. context.SelectedSources.Select(source =>
            {
                string slotId = CtrlRamSlotId(source.SourceId);
                return new InputArtifactBinding(slotId, slotId, Path.GetFullPath(slotPaths[slotId]));
            }),
        ];
    }

    private sealed record CtrlRamReplaceRunContext(
        IcNumberSelection Selection,
        string? BasePath,
        long BaseLength,
        LegacyCombinerPostbuildProfile? PostbuildProfile,
        LegacyCombinerPostbuildCommandPlan? CommandPlan,
        FirmwareConfigVersionWritePlan? FirmwareVersionWritePlan,
        IReadOnlyList<TpFlashMapRegion> Regions,
        IReadOnlyList<TpCtrlRamPostbuildSource> Sources,
        IReadOnlyList<TpCtrlRamPostbuildSource> SelectedSources,
        IReadOnlyDictionary<string, long> SelectedSourceLengths,
        IReadOnlyList<CompositionIssue> ValidationIssues)
    {
        public bool CanRun =>
            ValidationIssues.Count == 0 &&
            BasePath is not null &&
            PostbuildProfile is not null &&
            CommandPlan is not null;
    }
}
