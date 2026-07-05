using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    // Conservative deny-by-default header window. Inspected postbuild references use at least
    // a 0x100-byte firmware header copy block, and General Replace has no owner-approved
    // header editing workflow yet.
    private const long GeneralReplaceProtectedHeaderLength = 0x100;
    private const int GeneralReplacePostbuildSequence = 900;

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

    private static CompositionProfileDefinition CreateGeneralReplaceProfile(
        string icId,
        IcNumberSelection selection,
        long capacity,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        LegacyCombinerPostbuildCommandPlan? commandPlan,
        IReadOnlyList<ByteRange> postbuildWriteRanges)
    {
        string normalizedIc = icId.ToLowerInvariant();
        ProfileRegion[] regions = CreateGeneralReplaceRegions(
            icId,
            selection,
            capacity,
            postbuildProfile,
            postbuildWriteRanges);
        RegionAccessRule[] accessRules =
        [
            .. regions.Select(region => new RegionAccessRule(
                region.RegionId,
                region.WritePolicy == RegionWritePolicy.GeneralExplicit
                    ? RegionAccessKind.ExplicitRange
                    : RegionAccessKind.Hidden,
                region.WritePolicy == RegionWritePolicy.GeneralExplicit
                    ? "General Replace explicit mapping range."
                    : "Protected from General Replace.")),
        ];
        CompositionOperation[] operations = commandPlan is null || postbuildProfile is null
            ? []
            :
            [
                CompositionOperation.RunExternalProcessor(
                    $"postbuild-{commandPlan.Branch.ToString().ToLowerInvariant()}",
                    GeneralReplacePostbuildSequence,
                    "output-image",
                    new ByteRange(0, capacity),
                    new ExternalProcessorInvocation(
                        postbuildProfile.ProcessorId,
                        postbuildProfile.ToolBindingId,
                        [new ByteRange(0, capacity)],
                        postbuildWriteRanges),
                    OverlapPolicy.ReplaceExisting,
                    $"Run {commandPlan.Branch} legacy Combiner postbuild after TP-touching General Replace mappings. Combiner command: {FormatPostbuildCommandBlock(commandPlan)}."),
            ];

        return new CompositionProfileDefinition(
            $"{normalizedIc}-general-replace-workbench",
            "0.7.0",
            icId,
            "general-replace",
            CompositionKind.Replace,
            "general-replace",
            $"{normalizedIc}-general-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            [
                new AddressSpace("reference-base", capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", capacity, AddressSpaceMutability.Mutable),
            ],
            operations,
            regions,
            accessRules,
            selection.Mode);
    }

    private static ProfileRegion[] CreateGeneralReplaceRegions(
        string icId,
        IcNumberSelection selection,
        long capacity,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        IReadOnlyList<ByteRange> postbuildWriteRanges)
    {
        List<ProfileRegion> regions = [];
        IReadOnlyList<ByteRange> normalizedPostbuildWriteRanges = NormalizeGeneralPostbuildWriteRanges(
            postbuildWriteRanges,
            capacity);
        long protectedHeaderEnd = Math.Min(capacity, GeneralReplaceProtectedHeaderLength);
        if (protectedHeaderEnd > 0)
        {
            AddGeneralReplaceSplitRegion(
                regions,
                "protected-header",
                "output-image",
                new ByteRange(0, protectedHeaderEnd),
                RegionAtomicity.Whole,
                RegionWritePolicy.Forbidden,
                ["header", "protected"],
                normalizedPostbuildWriteRanges);
        }

        foreach (TpFlashMapRegion region in TpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile))
        {
            long start = Math.Max(region.Range.Start, protectedHeaderEnd);
            long end = Math.Min(region.Range.EndExclusive, capacity);
            if (end <= start)
            {
                continue;
            }

            bool explicitRange = region.Kind is TpFlashMapRegionKind.Dp or TpFlashMapRegionKind.CtrlRam;
            AddGeneralReplaceSplitRegion(
                regions,
                region.RegionId,
                "output-image",
                ByteRange.FromStartEndExclusive(start, end),
                explicitRange ? RegionAtomicity.ExplicitMapping : RegionAtomicity.Whole,
                explicitRange ? RegionWritePolicy.GeneralExplicit : RegionWritePolicy.Forbidden,
                CreateGeneralReplaceRegionTags(region),
                normalizedPostbuildWriteRanges);
        }

        if (postbuildProfile is not null)
        {
            foreach ((ByteRange range, int index) in normalizedPostbuildWriteRanges.Select((range, index) => (range, index)))
            {
                regions.Add(new ProfileRegion(
                    FormattableString.Invariant($"postbuild-write-{index:D2}"),
                    "output-image",
                    range,
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    processorDependencyIds: [postbuildProfile.ProcessorId],
                    classificationTags: ["postbuild", "protected"]));
            }
        }

        return [.. regions];
    }

    private static void AddGeneralReplaceSplitRegion(
        List<ProfileRegion> regions,
        string regionId,
        string addressSpaceId,
        ByteRange range,
        RegionAtomicity atomicity,
        RegionWritePolicy writePolicy,
        IReadOnlyList<string> classificationTags,
        IReadOnlyList<ByteRange> postbuildWriteRanges)
    {
        List<ByteRange> remainingSegments = SubtractRanges(range, postbuildWriteRanges);
        bool split = remainingSegments.Count != 1 || remainingSegments[0] != range;
        foreach ((ByteRange segment, int index) in remainingSegments.Select((segment, index) => (segment, index)))
        {
            regions.Add(new ProfileRegion(
                split ? FormattableString.Invariant($"{regionId}-{index:D2}") : regionId,
                addressSpaceId,
                segment,
                atomicity,
                writePolicy,
                classificationTags: classificationTags));
        }
    }

    private static List<ByteRange> SubtractRanges(
        ByteRange source,
        IReadOnlyList<ByteRange> removedRanges)
    {
        ByteRange[] overlaps =
        [
            .. removedRanges
                .Select(source.Intersect)
                .Where(overlap => overlap is not null)
                .Select(overlap => overlap!.Value),
        ];
        if (overlaps.Length == 0)
        {
            return [source];
        }

        SortedSet<long> splitPoints = [source.Start, source.EndExclusive];
        foreach (ByteRange overlap in overlaps)
        {
            _ = splitPoints.Add(overlap.Start);
            _ = splitPoints.Add(overlap.EndExclusive);
        }

        long[] points = [.. splitPoints];
        List<ByteRange> ranges = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var segment = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            if (!overlaps.Any(overlap => overlap.Overlaps(segment)))
            {
                ranges.Add(segment);
            }
        }

        return ranges;
    }

    private static IReadOnlyList<ByteRange> NormalizeGeneralPostbuildWriteRanges(
        IReadOnlyList<ByteRange> postbuildWriteRanges,
        long capacity)
    {
        return
        [
            .. postbuildWriteRanges
                .Where(range => range.Start >= 0 && range.EndExclusive <= capacity)
                .Distinct()
                .OrderBy(range => range.Start)
                .ThenBy(range => range.Length),
        ];
    }

    private static bool GeneralReplaceTouchesTpRegion(
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        return explicitMappings.Any(mapping => regions.Any(region =>
            IsGeneralReplaceTpRegion(region) &&
            region.Range.Overlaps(mapping.TargetRange)));
    }

    private static bool IsGeneralReplaceTpRegion(TpFlashMapRegion region)
    {
        return region.Kind == TpFlashMapRegionKind.CtrlRam ||
            region.Tags.Any(tag =>
                string.Equals(tag, "tp", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("tp-", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> CreateGeneralReplaceRegionTags(TpFlashMapRegion region)
    {
        List<string> tags = region.Kind switch
        {
            TpFlashMapRegionKind.Dp => ["dp"],
            TpFlashMapRegionKind.CtrlRam => ["tp", "tp-ctrlram"],
            TpFlashMapRegionKind.CustomerInfo => ["customer-info", "protected"],
            TpFlashMapRegionKind.ProjectId => ["project-id", "protected"],
            TpFlashMapRegionKind.Other => ["other", "protected"],
            _ => ["unknown", "protected"],
        };
        tags.AddRange(region.Tags);
        return [.. tags.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static bool TryCreateGeneralReplaceMappings(
        WorkbenchGeneralReplaceMappingInput[] mappingInputs,
        out IReadOnlyList<ExplicitMapping> explicitMappings,
        out IReadOnlyList<AddressSpace> requestAddressSpaces,
        out IReadOnlyList<InputArtifactBinding> mappingBindings,
        out IReadOnlyList<CompositionIssue> issues)
    {
        List<ExplicitMapping> mappings = [];
        List<AddressSpace> spaces = [];
        List<InputArtifactBinding> bindings = [];
        List<CompositionIssue> issueList = [];
        for (int index = 0; index < mappingInputs.Length; index++)
        {
            WorkbenchGeneralReplaceMappingInput input = mappingInputs[index];
            if (!TryParseGeneralReplaceRange(input, out ByteRange targetRange, out CompositionIssue? issue))
            {
                issueList.Add(issue);
                continue;
            }

            string addressSpaceId = $"{input.MappingId}-input";
            string fullPath = Path.GetFullPath(input.FilePath);
            long declaredLength = File.Exists(fullPath)
                ? new FileInfo(fullPath).Length
                : targetRange.Length;
            spaces.Add(new AddressSpace(addressSpaceId, declaredLength, AddressSpaceMutability.Immutable));
            bindings.Add(new InputArtifactBinding(addressSpaceId, input.MappingId, fullPath));
            mappings.Add(new ExplicitMapping(
                input.MappingId,
                100 + (index * 10),
                ExplicitMappingOperationKind.ReplaceRange,
                addressSpaceId,
                new ByteRange(0, targetRange.Length),
                "output-image",
                targetRange,
                OverlapPolicy.Reject,
                alignment: 1,
                "Replace explicit General range.",
                targetRegionId: null));
        }

        explicitMappings = mappings;
        requestAddressSpaces = spaces;
        mappingBindings = bindings;
        issues = issueList;
        return issueList.Count == 0;
    }

    private static bool TryParseGeneralReplaceRange(
        WorkbenchGeneralReplaceMappingInput input,
        out ByteRange targetRange,
        out CompositionIssue issue)
    {
        targetRange = default;
        if (!TryParseNonNegativeLong(input.TargetStart, out long start) ||
            !TryParseNonNegativeLong(input.TargetEndInclusive, out long endInclusive) ||
            endInclusive < start)
        {
            issue = new CompositionIssue(
                "ui.general-replace.range-invalid",
                $"General Replace mapping '{input.MappingId}' must use a valid inclusive start/end range.",
                input.MappingId);
            return false;
        }

        try
        {
            targetRange = ByteRange.FromStartEndExclusive(start, checked(endInclusive + 1));
            issue = default!;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            issue = new CompositionIssue(
                "ui.general-replace.range-invalid",
                $"General Replace mapping '{input.MappingId}' must use a valid inclusive start/end range.",
                input.MappingId);
            return false;
        }
        catch (OverflowException)
        {
            issue = new CompositionIssue(
                "ui.general-replace.range-invalid",
                $"General Replace mapping '{input.MappingId}' range exceeds the supported address size.",
                input.MappingId);
            return false;
        }
    }

    private static bool TryParseNonNegativeLong(string text, out long value)
    {
        value = 0;
        string trimmed = text.Trim();
        bool parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        return parsed && value >= 0;
    }

    private static IReadOnlyList<OperationRunSummary> CreateGeneralReplacePlanningOperations(
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        return
        [
            .. explicitMappings.Select(mapping => new OperationRunSummary(
                mapping.MappingId,
                mapping.Sequence,
                CompositionOperationKind.ReplaceRange,
                OperationRunStatus.Skipped,
                mapping.SourceBindingId,
                mapping.SourceRange,
                mapping.TargetSpaceId,
                mapping.TargetRange,
                mapping.OverlapPolicy,
                null,
                null,
                [],
                [],
                mapping.Reason)),
        ];
    }

    private static Dictionary<string, string> CreateGeneralReplaceReportSlotPaths(
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs)
    {
        Dictionary<string, string> paths = new(slotPaths, StringComparer.Ordinal);
        foreach (WorkbenchGeneralReplaceMappingInput mapping in mappingInputs)
        {
            if (!string.IsNullOrWhiteSpace(mapping.FilePath))
            {
                paths[mapping.MappingId] = mapping.FilePath;
            }
        }

        return paths;
    }
}

/// <summary>One user-authored General Replace mapping row from the workbench surface.</summary>
public sealed record WorkbenchGeneralReplaceMappingInput(
    string MappingId,
    string FilePath,
    string TargetStart,
    string TargetEndInclusive);
