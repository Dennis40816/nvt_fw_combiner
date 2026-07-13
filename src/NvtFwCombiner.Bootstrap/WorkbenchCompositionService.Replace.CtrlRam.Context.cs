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

        if (postbuildProfile is not null)
        {
            regions = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile);
        }
        else if (basePath is null)
        {
            regions = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection);
        }

        if (regions.Count == 0)
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamNoMappedRegion,
                $"No postbuild-mapped CtrlRAM region is available for {icId} / {number}.",
                IcWorkflowIds.CtrlRamReplace));
        }

        List<TpFlashMapRegion> selectedRegions =
        [
            .. regions
                .Where(region => IsSlotSupplied(slotPaths, CtrlRamSlotId(region.RegionId)))
                .OrderBy(region => region.Range.Start),
        ];
        if (selectedRegions.Count == 0)
        {
            validationIssues.Add(new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamNoRegionInput,
                "Select at least one CtrlRAM replacement BIN.",
                IcWorkflowIds.CtrlRamReplace));
        }

        if (commandPlan is not null && baseLength > 0)
        {
            long requiredCapacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(
                commandPlan,
                selectedRegions.Select(region => region.Range));
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
            selectedRegions,
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
        List<InputArtifactBinding> bindings =
        [
            new(CompositionAddressSpaceIds.ReferenceBase, WorkbenchSlotIds.ReplaceBase, context.BasePath!),
        ];
        foreach (TpFlashMapRegion region in context.SelectedRegions.OrderBy(region => region.Range.Start))
        {
            string slotId = CtrlRamSlotId(region.RegionId);
            bindings.Add(CreateBinding(slotId, slotId, slotPaths));
        }

        return [.. bindings];
    }

    private sealed record CtrlRamReplaceRunContext(
        IcNumberSelection Selection,
        string? BasePath,
        long BaseLength,
        LegacyCombinerPostbuildProfile? PostbuildProfile,
        LegacyCombinerPostbuildCommandPlan? CommandPlan,
        FirmwareConfigVersionWritePlan? FirmwareVersionWritePlan,
        IReadOnlyList<TpFlashMapRegion> Regions,
        IReadOnlyList<TpFlashMapRegion> SelectedRegions,
        IReadOnlyList<CompositionIssue> ValidationIssues)
    {
        public bool CanRun =>
            ValidationIssues.Count == 0 &&
            BasePath is not null &&
            PostbuildProfile is not null &&
            CommandPlan is not null;
    }
}
