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
        IReadOnlyDictionary<string, string> slotPaths)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        List<CompositionIssue> validationIssues = [];
        LegacyCombinerPostbuildProfile? postbuildProfile = null;
        LegacyCombinerPostbuildCommandPlan? commandPlan = null;
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
                        "replace.ctrlram.ic-number-unsupported",
                        exception.Message,
                        "number"));
                }
            }
        }
        else if (LegacyCombinerPostbuildCatalog.GetProfiles(icId).Count == 0)
        {
            validationIssues.Add(new CompositionIssue(
                "replace.ctrlram.postbuild-profile-missing",
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
                "replace.ctrlram.no-mapped-region",
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
                "replace.ctrlram.no-region-input",
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
                    "input.address-space.length-mismatch",
                    $"Base flash BIN is too short for {icId} / {number} CtrlRAM postbuild (actual {baseLength} bytes, required at least {requiredCapacity} bytes).",
                    "replace-base"));
            }
        }

        return new CtrlRamReplaceRunContext(
            selection,
            basePath,
            baseLength,
            postbuildProfile,
            commandPlan,
            regions,
            selectedRegions,
            validationIssues);
    }

    private static (string? Path, long Length) ResolveCtrlRamBaseInput(
        IReadOnlyDictionary<string, string> slotPaths,
        List<CompositionIssue> validationIssues)
    {
        if (!slotPaths.TryGetValue("replace-base", out string? suppliedBasePath) ||
            string.IsNullOrWhiteSpace(suppliedBasePath))
        {
            validationIssues.Add(new CompositionIssue(
                "ui.input.missing",
                "Base flash BIN is required before CtrlRAM Replace can run.",
                "replace-base"));
            return (null, 0);
        }

        string basePath = Path.GetFullPath(suppliedBasePath);
        if (!File.Exists(basePath))
        {
            validationIssues.Add(new CompositionIssue(
                "input.artifact.read-failed",
                "Base flash BIN path does not exist.",
                "replace-base"));
            return (basePath, 0);
        }

        long baseLength = new FileInfo(basePath).Length;
        if (baseLength <= 0)
        {
            validationIssues.Add(new CompositionIssue(
                "input.address-space.length-mismatch",
                "Base flash BIN must not be empty.",
                "replace-base"));
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
            new("reference-base", "replace-base", context.BasePath!),
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
