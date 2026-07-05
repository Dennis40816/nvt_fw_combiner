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

    private static CompositionProfileDefinition CreateCtrlRamReplaceProfile(
        string icId,
        IcNumberSelection selection,
        long capacity,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions,
        IReadOnlyList<TpFlashMapRegion> selectedRegions,
        LegacyCombinerPostbuildProfile postbuildProfile,
        LegacyCombinerPostbuildCommandPlan commandPlan,
        IReadOnlyList<ByteRange> postbuildWriteRanges)
    {
        string normalizedIc = icId.ToLowerInvariant();
        List<AddressSpace> addressSpaces =
        [
            new("reference-base", capacity, AddressSpaceMutability.Immutable),
            new("output-image", capacity, AddressSpaceMutability.Mutable),
        ];
        List<CompositionOperation> operations = [];
        List<ProfileRegion> profileRegions = [];
        List<RegionAccessRule> accessRules = [];

        foreach (TpFlashMapRegion region in ctrlRamRegions.OrderBy(region => region.Range.Start))
        {
            profileRegions.Add(new ProfileRegion(
                region.RegionId,
                "output-image",
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
        ByteRange[] processorOnlyRanges =
        [
            .. postbuildWriteRanges
                .Where(range => !ctrlRamRanges.Any(ctrlRamRange => ctrlRamRange.Contains(range)))
                .Distinct()
                .OrderBy(range => range.Start)
                .ThenBy(range => range.Length),
        ];
        foreach ((ByteRange range, int index) in processorOnlyRanges.Select((range, index) => (range, index)))
        {
            profileRegions.Add(new ProfileRegion(
                FormattableString.Invariant($"postbuild-write-{index:D2}"),
                "output-image",
                range,
                RegionAtomicity.ExplicitMapping,
                RegionWritePolicy.GeneralExplicit,
                processorDependencyIds: [postbuildProfile.ProcessorId],
                classificationTags: ["postbuild"]));
        }

        operations.Add(CompositionOperation.RunExternalProcessor(
            $"postbuild-{commandPlan.Branch.ToString().ToLowerInvariant()}",
            sequence,
            "output-image",
            new ByteRange(0, capacity),
            new ExternalProcessorInvocation(
                postbuildProfile.ProcessorId,
                postbuildProfile.ToolBindingId,
                [new ByteRange(0, capacity)],
                postbuildWriteRanges,
                stagedSourceBindings),
            OverlapPolicy.ReplaceExisting,
            $"Run {commandPlan.Branch} legacy Combiner postbuild and stage selected CtrlRAM BINs for Combiner pasteback. Combiner command: {FormatPostbuildCommandBlock(commandPlan)}."));

        return new CompositionProfileDefinition(
            $"{normalizedIc}-ctrlram-replace-workbench",
            "0.5.0",
            icId,
            "ctrlram-replace",
            CompositionKind.Replace,
            "ctrlram-replace",
            $"{normalizedIc}-ctrlram-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            addressSpaces,
            operations,
            profileRegions,
            accessRules,
            selection.Mode);
    }

    private static List<OperationRunSummary> CreateCtrlRamPlanningOperations(
        string icId,
        IcNumberSelection selection,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyDictionary<string, string> slotPaths,
        bool runnablePreview,
        LegacyCombinerPostbuildProfile? postbuildProfile = null)
    {
        OperationRunStatus status = runnablePreview ? OperationRunStatus.Succeeded : OperationRunStatus.Skipped;
        List<OperationRunSummary> operations = [];
        long capacity = Math.Max(
            1,
            TpFlashMapCatalog.GetRegions(icId, selection, postbuildProfile).Max(region => region.Range.EndExclusive));
        int sequence = 100;
        foreach (TpFlashMapRegion region in regions.OrderBy(region => region.Range.Start))
        {
            string slotId = CtrlRamSlotId(region.RegionId);
            operations.Add(new OperationRunSummary(
                $"split-base-{region.RegionId}",
                sequence,
                CompositionOperationKind.CopyRange,
                status,
                "reference-base",
                region.Range,
                region.PostbuildFileName ?? $"staged-{region.RegionId}",
                new ByteRange(0, region.Range.Length),
                OverlapPolicy.ReplaceExisting,
                null,
                null,
                [],
                [],
                $"Split original {region.DisplayName} from base flash for postbuild staging."));
            sequence += 10;

            if (slotPaths.ContainsKey(slotId))
            {
                operations.Add(new OperationRunSummary(
                    $"replace-{region.RegionId}",
                    sequence,
                    CompositionOperationKind.ReplaceRange,
                    status,
                    slotId,
                    new ByteRange(0, region.Range.Length),
                    "output-image",
                    region.Range,
                    OverlapPolicy.ReplaceExisting,
                    null,
                    null,
                    [],
                    [],
                    $"Stage selected {region.DisplayName} for Combiner pasteback at {FormatDisplayRange(region.Range)}; oversized inputs are expected to truncate only by profile policy."));
                sequence += 10;
            }
        }

        if (postbuildProfile is null &&
            !LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(icId, out postbuildProfile))
        {
            return operations;
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile!, selection);
        string firmwarePath = Path.Combine("output", postbuildProfile!.FirmwareFileName);
        string binDirectory = "BIN";
        foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
        {
            IReadOnlyList<string> args = LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(
                command,
                firmwarePath,
                binDirectory);
            operations.Add(new OperationRunSummary(
                $"postbuild-{command.CommandId}",
                sequence,
                CompositionOperationKind.RunExternalProcessor,
                status,
                null,
                null,
                "output-image",
                new ByteRange(0, capacity),
                OverlapPolicy.ReplaceExisting,
                postbuildProfile.ProcessorId,
                postbuildProfile.ToolBindingId,
                [new ByteRange(0, capacity)],
                [new ByteRange(0, capacity)],
                $"Generated {plan.Branch} Combiner command: Combiner.exe {string.Join(' ', args)}."));
            sequence += 10;
        }

        return operations;
    }

    private static bool TryGetPostbuildProfile(
        string icId,
        string basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out CompositionIssue? issue)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
        if (profiles.Count == 0)
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                "replace.ctrlram.postbuild-profile-missing",
                $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        string? commonFwVersion = null;
        if (profiles.Count > 1 &&
            !TryReadBaseCommonFwVersion(icId, basePath, out commonFwVersion))
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                "replace.ctrlram.postbuild-category-unknown",
                $"{icId} has multiple legacy Combiner postbuild categories, but the base BIN FWConfig Common FW version could not be read or failed FW/bar validation.",
                "replace-base");
            return false;
        }

        if (!LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
                icId,
                commonFwVersion,
                out postbuildProfile,
                out string? profileIssue))
        {
            issue = new CompositionIssue(
                "replace.ctrlram.postbuild-category-unsupported",
                profileIssue ?? $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        issue = null;
        return true;
    }

    private static bool TryReadBaseCommonFwVersion(
        string icId,
        string basePath,
        out string? commonFwVersion)
    {
        commonFwVersion = null;
        if (!TpFlashMapCatalog.TryGetFirmwareConfigStart(icId, out long firmwareConfigStart))
        {
            return false;
        }

        try
        {
            byte[] image = File.ReadAllBytes(basePath);
            if (!FirmwareConfigMetadataReader.TryRead(image, firmwareConfigStart, out FirmwareConfigMetadata metadata))
            {
                return false;
            }

            if (!metadata.IsFirmwareVersionBarValid)
            {
                return false;
            }

            commonFwVersion = metadata.CommonFwVersion;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
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
