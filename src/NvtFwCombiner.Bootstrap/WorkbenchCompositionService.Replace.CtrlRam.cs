using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
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
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection);
        if (regions.Count == 0)
        {
            return CreatePlanningRunResult(
                icId,
                number,
                "CtrlRAM",
                slotPaths,
                build,
                "replace.ctrlram.no-mapped-region",
                $"No postbuild-mapped CtrlRAM region is available for {icId} / {number}.");
        }

        List<CompositionIssue> validationIssues = [];
        if (!TryGetPostbuildProfile(icId, out LegacyCombinerPostbuildProfile? postbuildProfile))
        {
            validationIssues.Add(new CompositionIssue(
                "replace.ctrlram.postbuild-profile-missing",
                $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild"));
        }

        LegacyCombinerPostbuildCommandPlan? commandPlan = null;
        if (postbuildProfile is not null)
        {
            try
            {
                commandPlan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection);
            }
            catch (ArgumentException exception)
            {
                validationIssues.Add(new CompositionIssue(
                    "replace.ctrlram.ic-number-unsupported",
                    exception.Message,
                    "number"));
            }
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

        if (commandPlan is not null && baseLength > 0)
        {
            long requiredCapacity = CalculatePostbuildRequiredCapacity(commandPlan, selectedRegions);
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
                CreateCtrlRamPlanningOperations(icId, selection, regions, slotPaths, runnablePreview: false),
                validationIssues,
                GetReplaceDefaultOutputFileName(icId, "CtrlRAM"),
                succeeded: false);
        }

        List<ByteRange> postbuildWriteRanges = CreatePostbuildAllowedWriteRanges(
            commandPlan,
            baseLength,
            regions);
        if (postbuildWriteRanges.Count == 0)
        {
            return CreateReplaceReportRunResult(
                icId,
                "CtrlRAM",
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(icId, selection, regions, slotPaths, runnablePreview: false),
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
                CreateCtrlRamPlanningOperations(icId, selection, regions, slotPaths, runnablePreview: false),
                compile.Issues,
                profile.DefaultOutputFileName,
                succeeded: false);
        }

        InputArtifactBinding[] bindings = CreateCtrlRamReplaceBindings(selectedRegions, slotPaths, basePath);
        string[] inputRoots =
        [
            .. bindings
                .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        (string outputDirectory, string outputFileName) = ResolveOutputTarget(
            basePath,
            build,
            outputPath,
            profile.DefaultOutputFileName);
        FileArtifactReader reader = new(inputRoots);
        AtomicFileCompositionOutputWriter? writer = build
            ? new AtomicFileCompositionOutputWriter(outputDirectory, overwrite: true)
            : null;
        CompositionRunService service = new(reader, new SystemClock(), writer, ExternalProcessorFactory.CreateOrNull());
        CompositionRunRequest request = new(
            $"ui-replace-ctrlram-{(build ? "build" : "preview")}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}",
            ToRunProfile(profile),
            compile.Plan!,
            bindings,
            outputFileName,
            icNumberSelection: selection);

        CompositionRunResult result;
        if (!build)
        {
            result = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            CompositionRunResult preview = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
            result = preview.Status == CompositionExecutionStatus.Succeeded
                ? await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), cancellationToken)
                    .ConfigureAwait(false)
                : preview;
        }

        return ToWorkbenchRunResult(result);
    }

    private static CompositionProfileDefinition CreateCtrlRamReplaceProfile(
        string icId,
        IcNumberSelection selection,
        long capacity,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions,
        IReadOnlyList<TpFlashMapRegion> selectedRegions,
        LegacyCombinerPostbuildProfile postbuildProfile,
        LegacyCombinerPostbuildCommandPlan commandPlan,
        List<ByteRange> postbuildWriteRanges)
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
        foreach (TpFlashMapRegion region in selectedRegions.OrderBy(region => region.Range.Start))
        {
            string slotId = CtrlRamSlotId(region.RegionId);
            addressSpaces.Add(new AddressSpace(
                slotId,
                region.Range.Length,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.TruncateWithWarning));
            operations.Add(CompositionOperation.ReplaceRange(
                $"replace-{region.RegionId}",
                sequence,
                slotId,
                new ByteRange(0, region.Range.Length),
                "output-image",
                region.Range,
                OverlapPolicy.Reject,
                $"Replace {region.DisplayName} at {FormatDisplayRange(region.Range)} with the selected BIN; oversized inputs are truncated by CtrlRAM profile policy."));
            sequence += 10;
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
                postbuildWriteRanges),
            OverlapPolicy.ReplaceExisting,
            $"Run {commandPlan.Branch} legacy Combiner postbuild after CtrlRAM replacement. Combiner command: {FormatPostbuildCommandBlock(commandPlan)}."));

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
        bool runnablePreview)
    {
        OperationRunStatus status = runnablePreview ? OperationRunStatus.Succeeded : OperationRunStatus.Skipped;
        List<OperationRunSummary> operations = [];
        long capacity = Math.Max(1, TpFlashMapCatalog.GetRegions(icId, selection).Max(region => region.Range.EndExclusive));
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
                    $"Replace {region.DisplayName} at {FormatDisplayRange(region.Range)} with the selected BIN; oversized inputs are expected to truncate only by profile policy."));
                sequence += 10;
            }
        }

        if (LegacyCombinerPostbuildCatalog.All.FirstOrDefault(profile =>
                string.Equals(profile.IcId, icId, StringComparison.Ordinal)) is not { } postbuildProfile)
        {
            return operations;
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection);
        string firmwarePath = Path.Combine("output", postbuildProfile.FirmwareFileName);
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
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = LegacyCombinerPostbuildCatalog.All.FirstOrDefault(profile =>
            string.Equals(profile.IcId, icId, StringComparison.Ordinal));
        return postbuildProfile is not null;
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

    private static long CalculatePostbuildRequiredCapacity(
        LegacyCombinerPostbuildCommandPlan commandPlan,
        List<TpFlashMapRegion> selectedRegions)
    {
        long requiredCapacity = selectedRegions.Count == 0
            ? 1
            : selectedRegions.Max(region => region.Range.EndExclusive);
        foreach (LegacyCombinerPostbuildCommand command in commandPlan.Commands)
        {
            foreach (LegacyCombinerBlockArgument block in command.Blocks)
            {
                requiredCapacity = Math.Max(requiredCapacity, block.FirmwareRange.EndExclusive);
                if (block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage)
                {
                    requiredCapacity = Math.Max(
                        requiredCapacity,
                        checked(block.SourceOffset + block.FirmwareRange.Length));
                }
            }
        }

        return requiredCapacity;
    }

    private static List<ByteRange> CreatePostbuildAllowedWriteRanges(
        LegacyCombinerPostbuildCommandPlan commandPlan,
        long capacity,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions)
    {
        List<ByteRange> candidateRanges = [];
        foreach (LegacyCombinerPostbuildCommand command in commandPlan.Commands)
        {
            foreach (LegacyCombinerBlockArgument block in command.Blocks)
            {
                bool writesFirmware = block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile ||
                    block.SourceOffset != block.FirmwareRange.Start;
                if (!writesFirmware ||
                    block.FirmwareRange.EndExclusive > capacity)
                {
                    continue;
                }

                candidateRanges.Add(block.FirmwareRange);
            }
        }

        return NormalizeCandidateWriteRanges(candidateRanges, ctrlRamRegions);
    }

    private static List<ByteRange> NormalizeCandidateWriteRanges(
        List<ByteRange> candidateRanges,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions)
    {
        if (candidateRanges.Count == 0)
        {
            return [];
        }

        SortedSet<long> splitPoints = [];
        foreach (ByteRange range in candidateRanges)
        {
            _ = splitPoints.Add(range.Start);
            _ = splitPoints.Add(range.EndExclusive);
            foreach (TpFlashMapRegion region in ctrlRamRegions)
            {
                ByteRange? overlap = range.Intersect(region.Range);
                if (overlap is not null)
                {
                    _ = splitPoints.Add(overlap.Value.Start);
                    _ = splitPoints.Add(overlap.Value.EndExclusive);
                }
            }
        }

        long[] points = [.. splitPoints];
        List<ByteRange> ranges = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var segment = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            if (candidateRanges.Any(range => range.Contains(segment)))
            {
                ranges.Add(segment);
            }
        }

        return [
            .. ranges
                .Distinct()
                .OrderBy(range => range.Start)
                .ThenBy(range => range.Length),
        ];
    }

    private static string FormatPostbuildCommandBlock(LegacyCombinerPostbuildCommandPlan commandPlan)
    {
        string firmwarePath = Path.Combine("output", commandPlan.Profile.FirmwareFileName);
        const string binDirectory = "BIN";
        return string.Join(
            Environment.NewLine,
            commandPlan.Commands.Select(command =>
                $"Combiner.exe {string.Join(' ', LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(command, firmwarePath, binDirectory))}"));
    }
}
