using System.Globalization;
using System.Reflection;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const byte GeneralMergeFillByte = 0x00;

    private static string GeneralMergeProfileVersion =>
        typeof(WorkbenchCompositionService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

    /// <summary>Gets the default General Merge output length text for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputLength(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        long capacity = StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? profile.Initialization.Capacity
            : 0x100000;
        return FormatWorkbenchHex(capacity);
    }

    /// <summary>Gets the profile-owned default General Merge output file name for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputFileName(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return $"{icId.ToLowerInvariant()}-general-merge.bin";
    }

    /// <summary>Gets the profile id used by the General Merge workbench profile for the selected IC.</summary>
    public static string GetGeneralMergeWorkbenchProfileId(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return $"{icId.ToLowerInvariant()}-general-merge-workbench";
    }

    /// <summary>Gets output address coverage text for a General Merge output length.</summary>
    public static string GetGeneralMergeMemoryRangeLabel(string outputLength)
    {
        return TryParseGeneralMergeCapacity(outputLength, out long capacity, out _)
            ? FormatFullRange(capacity)
            : "Enter a valid output length";
    }

    /// <summary>Gets readable memory-map rows for General Merge authoring state.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetGeneralMergeMemoryMapRows(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);

        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out CompositionIssue? issue))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Output length",
                    "No output",
                    "Blocked",
                    "No output",
                    issue!.Message),
            ];
        }

        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatFullRange(capacity),
                "No output",
                "Initialize",
                $"Blank output 0x{GeneralMergeFillByte:X2}",
                "Start with a blank output image. Unmapped ranges remain reserved until an explicit mapping writes them."),
        ];

        foreach (WorkbenchGeneralMergeMappingInput mapping in mappingInputs)
        {
            if (!TryParseGeneralMergeMapping(
                    mapping,
                    out ByteRange sourceRange,
                    out ByteRange targetRange,
                    out CompositionIssue parseIssue))
            {
                rows.Add(new WorkbenchMemoryMapRow(
                    $"Mapping {mapping.MappingId}",
                    "Pending",
                    "Blocked",
                    "No output",
                    parseIssue.Message));
                continue;
            }

            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(targetRange),
                "Reserved",
                "Copy",
                "Source BIN",
                $"Copy source {FormatDisplayRange(sourceRange)} from {GeneralMergeSourceLabel(mapping)} into output {FormatDisplayRange(targetRange)}."));
        }

        return rows;
    }

    /// <summary>Gets visual coverage segments for General Merge authoring state.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetGeneralMergeCoverageSegments(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);

        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out _))
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Output length",
                    "Pending",
                    "Enter a valid General Merge output length.",
                    "#CBD5E1",
                    280,
                    false),
            ];
        }

        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                $"Blank 0x{GeneralMergeFillByte:X2}",
                "No source mapping writes this output range.",
                "#E2E8F0",
                false),
        ];

        ByteRange outputRange = new(0, capacity);
        foreach (WorkbenchGeneralMergeMappingInput mapping in mappingInputs)
        {
            if (!TryParseGeneralMergeMapping(mapping, out _, out ByteRange targetRange, out _) ||
                !outputRange.Contains(targetRange))
            {
                continue;
            }

            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    targetRange,
                    "Source BIN",
                    $"Written by {mapping.MappingId} from {GeneralMergeSourceLabel(mapping)}.",
                    CoverageFill("Source BIN"),
                    true));
        }

        return ToWorkbenchCoverageSegments(segments, capacity);
    }

    /// <summary>Runs General Merge preview or build through the application core.</summary>
    public static async ValueTask<WorkbenchRunResult> RunGeneralMergeAsync(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(mappingInputs);

        Dictionary<string, string> reportSlotPaths = CreateGeneralMergeReportSlotPaths(mappingInputs);
        string defaultOutputFileName = GetGeneralMergeDefaultOutputFileName(icId);
        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out CompositionIssue? capacityIssue))
        {
            return CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                [],
                [capacityIssue!],
                defaultOutputFileName,
                succeeded: false);
        }

        if (mappingInputs.Count == 0)
        {
            return CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                [],
                [
                    new CompositionIssue(
                        "ui.general-merge.mapping-required",
                        "General Merge requires at least one explicit source-to-target mapping.",
                        "general-merge"),
                ],
                defaultOutputFileName,
                succeeded: false);
        }

        if (!TryCreateGeneralMergeMappings(
                mappingInputs,
                out IReadOnlyList<ExplicitMapping> explicitMappings,
                out IReadOnlyList<AddressSpace> requestAddressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<CompositionIssue> mappingIssues))
        {
            return CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                mappingIssues,
                defaultOutputFileName,
                succeeded: false);
        }

        CompositionProfileDefinition profile = CreateGeneralMergeProfile(icId, capacity);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(
            profile,
            explicitMappings,
            requestAddressSpaces);
        WorkbenchRunResult? compileFailure = !compile.IsSuccess
            ? CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                compile.Issues,
                profile.DefaultOutputFileName,
                succeeded: false)
            : null;
        return compileFailure ?? await RunCompiledCompositionAsync(
            "ui-merge-general",
            profile,
            compile.Plan!,
            mappingBindings,
            mappingBindings[0].ArtifactId,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: null,
            overwrite: overwrite,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool TryParseGeneralMergeCapacity(
        string outputLength,
        out long capacity,
        out CompositionIssue? issue)
    {
        if (!TryParseNonNegativeLong(outputLength, out capacity) || capacity <= 0)
        {
            issue = new CompositionIssue(
                "ui.general-merge.capacity-invalid",
                "General Merge output length must be a positive byte count.",
                "output-length");
            return false;
        }

        if (capacity > int.MaxValue)
        {
            issue = new CompositionIssue(
                "ui.general-merge.capacity-unsupported",
                "General Merge output length exceeds the supported in-memory composition size.",
                "output-length");
            return false;
        }

        issue = null;
        return true;
    }

    private static bool TryCreateGeneralMergeMappings(
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        out IReadOnlyList<ExplicitMapping> explicitMappings,
        out IReadOnlyList<AddressSpace> requestAddressSpaces,
        out IReadOnlyList<InputArtifactBinding> mappingBindings,
        out IReadOnlyList<CompositionIssue> issues)
    {
        List<ExplicitMapping> mappings = [];
        List<AddressSpace> spaces = [];
        List<InputArtifactBinding> bindings = [];
        List<CompositionIssue> issueList = [];
        for (int index = 0; index < mappingInputs.Count; index++)
        {
            WorkbenchGeneralMergeMappingInput input = mappingInputs[index];
            if (!TryParseGeneralMergeMapping(input, out ByteRange sourceRange, out ByteRange targetRange, out CompositionIssue? issue))
            {
                issueList.Add(issue);
                continue;
            }

            string addressSpaceId = $"{input.MappingId}-input";
            string fullPath = Path.GetFullPath(input.FilePath);
            long declaredLength = File.Exists(fullPath)
                ? new FileInfo(fullPath).Length
                : sourceRange.EndExclusive;
            if (declaredLength < sourceRange.EndExclusive)
            {
                issueList.Add(new CompositionIssue(
                    "ui.general-merge.source-out-of-bounds",
                    $"General Merge mapping '{input.MappingId}' source range exceeds the selected input file length.",
                    input.MappingId));
                continue;
            }

            spaces.Add(new AddressSpace(addressSpaceId, declaredLength, AddressSpaceMutability.Immutable));
            bindings.Add(new InputArtifactBinding(addressSpaceId, input.MappingId, fullPath));
            mappings.Add(new ExplicitMapping(
                input.MappingId,
                100 + (index * 10),
                ExplicitMappingOperationKind.CopyRange,
                addressSpaceId,
                sourceRange,
                "output-image",
                targetRange,
                OverlapPolicy.Reject,
                input.Alignment,
                input.Reason ?? "Copy explicit General Merge mapping.",
                targetRegionId: "general-output",
                provenance: input.Provenance));
        }

        explicitMappings = mappings;
        requestAddressSpaces = spaces;
        mappingBindings = bindings;
        issues = issueList;
        return issueList.Count == 0;
    }

    private static bool TryParseGeneralMergeMapping(
        WorkbenchGeneralMergeMappingInput input,
        out ByteRange sourceRange,
        out ByteRange targetRange,
        out CompositionIssue issue)
    {
        sourceRange = default;
        targetRange = default;
        if (!TryParseNonNegativeLong(input.SourceStart, out long sourceStart) ||
            !TryParseNonNegativeLong(input.TargetStart, out long targetStart) ||
            !TryParseNonNegativeLong(input.Length, out long length) ||
            length <= 0)
        {
            issue = new CompositionIssue(
                "ui.general-merge.range-invalid",
                $"General Merge mapping '{input.MappingId}' must use valid source start, target start, and positive length values.",
                input.MappingId);
            return false;
        }

        try
        {
            sourceRange = new ByteRange(sourceStart, length);
            targetRange = new ByteRange(targetStart, length);
            issue = default!;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        {
            issue = new CompositionIssue(
                "ui.general-merge.range-invalid",
                $"General Merge mapping '{input.MappingId}' range exceeds the supported address size.",
                input.MappingId);
            return false;
        }
    }

    private static CompositionProfileDefinition CreateGeneralMergeProfile(string icId, long capacity)
    {
        return new CompositionProfileDefinition(
            GetGeneralMergeWorkbenchProfileId(icId),
            GeneralMergeProfileVersion,
            icId,
            "general-merge",
            CompositionKind.Merge,
            "general-merge",
            GetGeneralMergeDefaultOutputFileName(icId),
            ImageInitialization.Blank("output-image", capacity, GeneralMergeFillByte),
            [
                new AddressSpace("output-image", capacity, AddressSpaceMutability.Mutable),
            ],
            [],
            [
                new ProfileRegion(
                    "general-output",
                    "output-image",
                    new ByteRange(0, capacity),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    classificationTags: ["general-merge"]),
            ],
            [
                new RegionAccessRule("general-output", RegionAccessKind.ExplicitRange, "General Merge explicit mapping output."),
            ],
            IcNumberInputMode.SingleSelector);
    }

    private static WorkbenchRunResult CreateGeneralMergeReportRunResult(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        bool succeeded)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        string profileId = GetGeneralMergeWorkbenchProfileId(icId);
        var report = new CompositionRunReport(
            $"ui-merge-general-{(build ? "build" : "preview")}-{timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}",
            profileId,
            GeneralMergeProfileVersion,
            icId,
            "general-merge",
            "general-merge",
            CompositionKind.Merge,
            timestamp,
            timestamp,
            CreateInputSummaries(slotPaths),
            operations,
            [],
            issues,
            new OutputArtifactSummary(outputFileName, 0, EmptySha256, committed: false));
        string reportJson = JsonSerializer.Serialize(report, ReportJsonOptions);
        return new WorkbenchRunResult(
            succeeded,
            succeeded ? "Succeeded" : "Blocked",
            profileId,
            0,
            EmptySha256,
            outputFileName,
            null,
            reportJson);
    }

    private static IReadOnlyList<OperationRunSummary> CreateGeneralMergePlanningOperations(
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        return
        [
            .. explicitMappings.Select(mapping => new OperationRunSummary(
                mapping.MappingId,
                mapping.Sequence,
                CompositionOperationKind.CopyRange,
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
                mapping.Reason,
                mapping.Provenance)),
        ];
    }

    private static Dictionary<string, string> CreateGeneralMergeReportSlotPaths(
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        foreach (WorkbenchGeneralMergeMappingInput mapping in mappingInputs)
        {
            if (!string.IsNullOrWhiteSpace(mapping.FilePath))
            {
                paths[mapping.MappingId] = mapping.FilePath;
            }
        }

        return paths;
    }

    private static string GeneralMergeSourceLabel(WorkbenchGeneralMergeMappingInput mapping)
    {
        return string.IsNullOrWhiteSpace(mapping.FilePath)
            ? "Source BIN"
            : Path.GetFileName(mapping.FilePath);
    }

    private static string FormatWorkbenchHex(long value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{value:X}");
    }
}

/// <summary>One user-authored General Merge mapping row from the workbench surface.</summary>
public sealed record WorkbenchGeneralMergeMappingInput(
    string MappingId,
    string FilePath,
    string SourceStart,
    string TargetStart,
    string Length,
    int Alignment = 1,
    string? Reason = null,
    OperationProvenance? Provenance = null);
