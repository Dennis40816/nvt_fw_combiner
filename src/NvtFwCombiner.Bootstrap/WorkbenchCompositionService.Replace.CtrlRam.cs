using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        List<CompositionIssue> validationIssues = [];
        LegacyCombinerPostbuildProfile? postbuildProfile = null;
        LegacyCombinerPostbuildCommandPlan? commandPlan = null;
        IReadOnlyList<TpFlashMapRegion> regions = [];

        string? basePath = null;
        long baseLength = 0;
        if (!slotPaths.TryGetValue("replace-base", out string? suppliedBasePath) ||
            string.IsNullOrWhiteSpace(suppliedBasePath))
        {
            validationIssues.Add(new CompositionIssue(
                "ui.input.missing",
                "Base flash BIN is required before CtrlRAM Replace preview/build can run.",
                "replace-base"));
        }
        else
        {
            basePath = Path.GetFullPath(suppliedBasePath);
            if (!File.Exists(basePath))
            {
                validationIssues.Add(new CompositionIssue(
                    "input.artifact.read-failed",
                    "Base flash BIN path does not exist.",
                    "replace-base"));
            }
            else
            {
                baseLength = new FileInfo(basePath).Length;
                if (baseLength <= 0)
                {
                    validationIssues.Add(new CompositionIssue(
                        "input.address-space.length-mismatch",
                        "Base flash BIN must not be empty.",
                        "replace-base"));
                }
            }
        }

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
                "ctrlram-replace"));
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
                "ctrlram-replace"));
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

        if (validationIssues.Count > 0 ||
            basePath is null ||
            postbuildProfile is null ||
            commandPlan is null)
        {
            return CreateReplaceReportRunResult(
                icId,
                "CtrlRAM",
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    selection,
                    regions,
                    slotPaths,
                    runnablePreview: false,
                    postbuildProfile),
                validationIssues,
                GetReplaceDefaultOutputFileName(icId, "CtrlRAM"),
                succeeded: false);
        }

        IReadOnlyList<ByteRange> postbuildWriteRanges = LegacyCombinerPostbuildPlanner.GetAllowedWriteRangesForStagedSources(
            commandPlan,
            baseLength,
            regions.Select(region => region.Range),
            regions.Select(region => region.Range));
        if (postbuildWriteRanges.Count == 0)
        {
            return CreateReplaceReportRunResult(
                icId,
                "CtrlRAM",
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    selection,
                    regions,
                    slotPaths,
                    runnablePreview: false,
                    postbuildProfile),
                [
                    new CompositionIssue(
                        "replace.ctrlram.postbuild-write-range-missing",
                        "No approved postbuild write range could be derived from the legacy Combiner command plan.",
                        "postbuild"),
                ],
                GetReplaceDefaultOutputFileName(icId, "CtrlRAM"),
                succeeded: false);
        }

        CompositionProfileDefinition profile = CreateCtrlRamReplaceProfile(
            icId,
            selection,
            baseLength,
            regions,
            selectedRegions,
            postbuildProfile,
            commandPlan,
            postbuildWriteRanges);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return CreateReplaceReportRunResult(
                icId,
                "CtrlRAM",
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    selection,
                    regions,
                    slotPaths,
                    runnablePreview: false,
                    postbuildProfile),
                compile.Issues,
                profile.DefaultOutputFileName,
                succeeded: false);
        }

        InputArtifactBinding[] bindings = CreateCtrlRamReplaceBindings(selectedRegions, slotPaths, basePath);

        return await RunCompiledCompositionAsync(
            "ui-replace-ctrlram",
            profile,
            compile.Plan!,
            bindings,
            basePath,
            build,
            outputPath,
            externalProcessor: ExternalProcessorFactory.CreateOrNull(),
            icNumberSelection: selection,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsSlotSupplied(
        IReadOnlyDictionary<string, string> slotPaths,
        string slotId)
    {
        return slotPaths.TryGetValue(slotId, out string? path) &&
            !string.IsNullOrWhiteSpace(path);
    }

    private static InputArtifactBinding[] CreateCtrlRamReplaceBindings(
        IReadOnlyList<TpFlashMapRegion> selectedRegions,
        IReadOnlyDictionary<string, string> slotPaths,
        string basePath)
    {
        List<InputArtifactBinding> bindings =
        [
            new("reference-base", "replace-base", basePath),
        ];
        foreach (TpFlashMapRegion region in selectedRegions.OrderBy(region => region.Range.Start))
        {
            string slotId = CtrlRamSlotId(region.RegionId);
            bindings.Add(CreateBinding(slotId, slotId, slotPaths));
        }

        return [.. bindings];
    }

}
