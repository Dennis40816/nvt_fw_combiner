using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunGeneralReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> reportSlotPaths = CreateGeneralReplaceReportSlotPaths(slotPaths, mappingInputs);
        if (!slotPaths.TryGetValue("replace-base", out string? basePath) ||
            string.IsNullOrWhiteSpace(basePath))
        {
            return CreatePlanningRunResult(
                icId,
                number,
                "General",
                reportSlotPaths,
                build,
                "ui.input.missing",
                "Base flash BIN is required before General Replace can compile explicit mappings.");
        }

        string fullBasePath = Path.GetFullPath(basePath);
        if (!File.Exists(fullBasePath))
        {
            return CreatePlanningRunResult(
                icId,
                number,
                "General",
                reportSlotPaths,
                build,
                "input.artifact.read-failed",
                "Base flash BIN path does not exist.");
        }

        WorkbenchGeneralReplaceMappingInput[] selectedMappings =
        [
            .. mappingInputs.Where(mapping => !string.IsNullOrWhiteSpace(mapping.FilePath)),
        ];
        if (selectedMappings.Length == 0)
        {
            return CreatePlanningRunResult(
                icId,
                number,
                "General",
                reportSlotPaths,
                build,
                "ui.input.missing",
                "At least one General Replace mapping row must select a replacement BIN.");
        }

        long capacity = new FileInfo(fullBasePath).Length;
        if (capacity <= 0)
        {
            return CreatePlanningRunResult(
                icId,
                number,
                "General",
                reportSlotPaths,
                build,
                "input.address-space.length-mismatch",
                "Base flash BIN must not be empty.");
        }

        if (!TryCreateGeneralReplaceMappings(
                selectedMappings,
                out IReadOnlyList<ExplicitMapping> explicitMappings,
                out IReadOnlyList<AddressSpace> requestAddressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<CompositionIssue> mappingIssues))
        {
            return CreateReplaceReportRunResult(
                icId,
                "General",
                reportSlotPaths,
                build,
                [],
                mappingIssues,
                GetReplaceDefaultOutputFileName(icId, "General"),
                succeeded: false);
        }

        IcNumberSelection selection = ToIcNumberSelection(number);
        bool postbuildProfileResolved = TryGetPostbuildProfile(
            icId,
            fullBasePath,
            out LegacyCombinerPostbuildProfile? postbuildProfile,
            out CompositionIssue? postbuildIssue);
        IReadOnlyList<TpFlashMapRegion> regionsForMappingPolicy = TpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfileResolved ? postbuildProfile : null);
        bool touchesTpRegion = GeneralReplaceTouchesTpRegion(regionsForMappingPolicy, explicitMappings);
        LegacyCombinerPostbuildCommandPlan? commandPlan = null;
        List<ByteRange> postbuildWriteRanges = [];
        if (touchesTpRegion)
        {
            if (!postbuildProfileResolved)
            {
                return CreateReplaceReportRunResult(
                    icId,
                    "General",
                    reportSlotPaths,
                    build,
                    CreateGeneralReplacePlanningOperations(explicitMappings),
                    [postbuildIssue!],
                    GetReplaceDefaultOutputFileName(icId, "General"),
                    succeeded: false);
            }

            try
            {
                commandPlan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile!, selection);
            }
            catch (ArgumentException exception)
            {
                return CreateReplaceReportRunResult(
                    icId,
                    "General",
                    reportSlotPaths,
                    build,
                    CreateGeneralReplacePlanningOperations(explicitMappings),
                    [
                        new CompositionIssue(
                            "replace.general.ic-number-unsupported",
                            exception.Message,
                            "number"),
                    ],
                    GetReplaceDefaultOutputFileName(icId, "General"),
                    succeeded: false);
            }

            long requiredCapacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(commandPlan, []);
            if (capacity < requiredCapacity)
            {
                return CreateReplaceReportRunResult(
                    icId,
                    "General",
                    reportSlotPaths,
                    build,
                    CreateGeneralReplacePlanningOperations(explicitMappings),
                    [
                        new CompositionIssue(
                            "input.address-space.length-mismatch",
                            $"Base flash BIN is too short for {icId} / {number} General Replace postbuild (actual {capacity} bytes, required at least {requiredCapacity} bytes).",
                            "replace-base"),
                    ],
                    GetReplaceDefaultOutputFileName(icId, "General"),
                    succeeded: false);
            }

            postbuildWriteRanges =
            [
                .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangesForInPlaceRefresh(commandPlan, capacity),
            ];
            if (postbuildWriteRanges.Count == 0)
            {
                return CreateReplaceReportRunResult(
                    icId,
                    "General",
                    reportSlotPaths,
                    build,
                    CreateGeneralReplacePlanningOperations(explicitMappings),
                    [
                        new CompositionIssue(
                            "replace.general.postbuild-write-range-missing",
                            "No approved postbuild write range could be derived for TP-touching General Replace.",
                            "postbuild"),
                    ],
                    GetReplaceDefaultOutputFileName(icId, "General"),
                    succeeded: false);
            }
        }

        CompositionProfileDefinition profile = CreateGeneralReplaceProfile(
            icId,
            selection,
            capacity,
            postbuildProfileResolved ? postbuildProfile : null,
            commandPlan,
            postbuildWriteRanges);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(
            profile,
            explicitMappings,
            requestAddressSpaces);
        if (!compile.IsSuccess)
        {
            return CreateReplaceReportRunResult(
                icId,
                "General",
                reportSlotPaths,
                build,
                CreateGeneralReplacePlanningOperations(explicitMappings),
                compile.Issues,
                profile.DefaultOutputFileName,
                succeeded: false);
        }

        InputArtifactBinding[] bindings =
        [
            new("reference-base", "replace-base", fullBasePath),
            .. mappingBindings,
        ];

        return await RunCompiledCompositionAsync(
            "ui-replace-general",
            profile,
            compile.Plan!,
            bindings,
            fullBasePath,
            build,
            outputPath,
            externalProcessor: commandPlan is null ? null : ExternalProcessorFactory.CreateOrNull(),
            icNumberSelection: ToIcNumberSelection(number),
            cancellationToken).ConfigureAwait(false);
    }

}
