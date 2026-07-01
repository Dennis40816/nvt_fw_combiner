using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Typed facade used by the desktop shell to query catalogs and run application services.</summary>
public static class WorkbenchCompositionService
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Dictionary<string, CompositionProfileDefinition> StandardMergeProfilesByIc =
        BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles.ToDictionary(
            profile => profile.IcId,
            StringComparer.Ordinal);

    /// <summary>Returns true when the selected IC has a built-in standard merge profile.</summary>
    public static bool IsStandardMergeSupported(string icId)
    {
        return StandardMergeProfilesByIc.ContainsKey(icId);
    }

    /// <summary>Gets the built-in standard merge profile id for the selected IC, if any.</summary>
    public static string? GetStandardMergeProfileId(string icId)
    {
        return StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? profile.ProfileId
            : null;
    }

    /// <summary>Gets required standard merge input address spaces for the selected IC.</summary>
    public static IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId)
    {
        return StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? GetRequiredAddressSpaces(profile)
            : [];
    }

    /// <summary>Gets the profile-owned default Standard Merge output file name for the selected IC.</summary>
    public static string GetStandardMergeDefaultOutputFileName(string icId)
    {
        return StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? profile.DefaultOutputFileName
            : "nvt-fw-combiner-output.bin";
    }

    /// <summary>Gets selectable IC ids from the TP flash-map catalog.</summary>
    public static IReadOnlyList<string> GetSupportedIcIds()
    {
        return TpFlashMapCatalog.IcIds;
    }

    /// <summary>Gets supported IC-number choices from the TP flash-map/postbuild catalog.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return TpFlashMapCatalog.GetNumberChoices(icId);
    }

    /// <summary>Gets TP Overview CtrlRAM regions visible for a selected IC and IC-number context.</summary>
    public static IReadOnlyList<WorkbenchCtrlRamRegion> GetCtrlRamRegions(string icId, string number)
    {
        return
        [
            .. TpFlashMapCatalog.GetCtrlRamRegions(icId, ToIcNumberSelection(number))
                .Select(region => new WorkbenchCtrlRamRegion(
                    region.DisplayName,
                    region.Range.Start,
                    region.Range.Length,
                    region.Tags.Any(tag =>
                        string.Equals(tag, "diff", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "dlm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "slave", StringComparison.OrdinalIgnoreCase)))),
        ];
    }

    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetStandardMergeMemoryMapRows(string icId)
    {
        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Profile",
                    "No profile",
                    "Blocked",
                    "No output",
                    $"Standard Merge is not available for {icId}."),
            ];
        }

        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Profile",
                    "Profile",
                    "Blocked",
                    "No output",
                    FormatIssues(compile.Issues)),
            ];
        }

        string initializedState = profile.Initialization.Kind == ImageInitializationKind.Blank
            ? $"Blank output 0x{profile.Initialization.FillByte:X2}"
            : $"Reference {profile.Initialization.ReferenceSpaceId}";
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatFullRange(profile.Initialization.Capacity),
                "No output",
                "Initialize",
                initializedState,
                "Start with the initialized image. Unlisted ranges keep this value until a later operation writes them."),
        ];

        foreach (CompositionOperation operation in compile.Plan!.OrderedOperations)
        {
            string afterSource = operation.SourceSpaceId is null
                ? operation.Kind.ToString()
                : AddressSpaceLabel(operation.SourceSpaceId);
            string sourceRange = operation.SourceRange is null
                ? "no source range"
                : FormatDisplayRange(operation.SourceRange.Value);
            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(operation.TargetRange),
                initializedState,
                ActionLabel(operation.Kind),
                afterSource,
                $"Sequence {operation.Sequence}: {operation.Kind} {sourceRange} -> output-image {FormatDisplayRange(operation.TargetRange)}. Reason: {operation.Reason}"));
        }

        return rows;
    }

    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetStandardMergeCoverageSegments(string icId)
    {
        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile))
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "No range",
                    "No profile",
                    "Standard Merge is unavailable.",
                    "#CBD5E1",
                    280),
            ];
        }

        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Profile",
                    "Invalid profile",
                    FormatIssues(compile.Issues),
                    "#F97316",
                    280),
            ];
        }

        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, profile.Initialization.Capacity),
                $"Blank 0x{profile.Initialization.FillByte:X2}",
                "No source input writes this output range.",
                "#E2E8F0"),
        ];

        foreach (CompositionOperation operation in compile.Plan!.OrderedOperations)
        {
            string label = operation.SourceSpaceId is null
                ? ActionLabel(operation.Kind)
                : AddressSpaceLabel(operation.SourceSpaceId);
            string detail = operation.SourceRange is null
                ? $"Operation {operation.OperationId}, sequence {operation.Sequence}."
                : $"Copies source {FormatDisplayRange(operation.SourceRange.Value)} into this output range.";
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    operation.TargetRange,
                    label,
                    detail,
                    CoverageFill(label)));
        }

        return
        [
            .. segments.Select(segment => new WorkbenchMemoryCoverageSegment(
                FormatDisplayRange(segment.Range),
                segment.SourceLabel,
                segment.Detail,
                segment.Fill,
                WidthForRange(segment.Range, profile.Initialization.Capacity))),
        ];
    }

    /// <summary>Gets output address coverage text for the selected Standard Merge profile.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId)
    {
        return StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? FormatFullRange(profile.Initialization.Capacity)
            : "No Standard Merge profile";
    }

    /// <summary>Gets readable memory-map rows for the selected Replace mode.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetReplaceMemoryMapRows(
        string icId,
        string number,
        string replaceMode)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        return regions.Count == 0
            ?
            [
                new WorkbenchMemoryMapRow(
                    "Catalog",
                    "No flash-map row",
                    "Blocked",
                    "No target",
                    $"No TP Overview flash-map profile is available for {icId}."),
            ]
            : replaceMode switch
            {
                "DP" => CreateDpReplaceRows(regions),
                "CtrlRAM" => CreateCtrlRamReplaceRows(regions),
                "General" =>
                [
                    .. CreatePreserveRows(regions),
                    new WorkbenchMemoryMapRow(
                        "Runtime range",
                        "Base flash",
                        "Replace",
                        "General BIN",
                        "The selected explicit range must be approved by the compiled General Replace profile."),
                ],
                _ =>
                [
                    new WorkbenchMemoryMapRow(
                        "Mode",
                        "Unknown",
                        "Select",
                        "No target",
                        "Select DP, CtrlRAM, or General Replace."),
                ],
            };
    }

    /// <summary>Gets TP Overview address coverage text for the selected Replace context.</summary>
    public static string GetReplaceMemoryRangeLabel(string icId, string number)
    {
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, ToIcNumberSelection(number));
        return regions.Count == 0
            ? "No flash-map profile"
            : FormatFullRange(regions.Max(region => region.Range.EndExclusive));
    }

    /// <summary>Gets final visual coverage segments for the selected Replace view.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetReplaceCoverageSegments(
        string icId,
        string number,
        string replaceMode)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        if (regions.Count == 0)
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "No range",
                    "No profile",
                    $"No TP Overview flash-map profile is available for {icId}.",
                    "#CBD5E1",
                    280),
            ];
        }

        long capacity = regions.Max(region => region.Range.EndExclusive);
        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                "Base flash",
                "Reference image is cloned before replacement.",
                "#E2E8F0"),
        ];

        foreach (TpFlashMapRegion region in regions
            .Where(IsPreservedRegion)
            .OrderBy(region => region.Range.Start))
        {
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    region.Range,
                    "Preserve",
                    $"{region.DisplayName} stays from the base image.",
                    "#94A3B8"));
        }

        IEnumerable<TpFlashMapRegion> replacementRegions = replaceMode switch
        {
            "DP" => regions.Where(region => region.Kind == TpFlashMapRegionKind.Dp),
            "CtrlRAM" => regions.Where(region => region.Kind == TpFlashMapRegionKind.CtrlRam),
            _ => [],
        };

        foreach (TpFlashMapRegion region in replacementRegions.OrderBy(region => region.Range.Start))
        {
            string label = replaceMode switch
            {
                "DP" => IsLdRegion(region) ? "LD BIN" : "DP BIN",
                "CtrlRAM" => "CtrlRAM BIN",
                _ => "Replacement BIN",
            };
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    region.Range,
                    label,
                    $"{region.DisplayName}; {ActionSummaryForReplaceMode(replaceMode)}",
                    CoverageFill(label)));
        }

        return
        [
            .. segments.Select(segment => new WorkbenchMemoryCoverageSegment(
                FormatDisplayRange(segment.Range),
                segment.SourceLabel,
                segment.Detail,
                segment.Fill,
                WidthForRange(segment.Range, capacity))),
        ];
    }

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static WorkbenchSettingsSnapshot GetSettingsSnapshot()
    {
        IReadOnlyList<string> toolBindingIds =
        [
            .. LegacyCombinerPostbuildCatalog.All
                .Select(profile => profile.ToolBindingId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        return new WorkbenchSettingsSnapshot(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles.Count,
            BuiltInReplaceProfiles.All.Count,
            TpFlashMapCatalog.IcIds.Count,
            LegacyCombinerPostbuildCatalog.All.Count,
            string.Join(", ", toolBindingIds),
            "external-tools/legacy-combiner/1.13.0/manifest.json");
    }

    /// <summary>Runs Standard Merge preview or build through the application core.</summary>
    public static async ValueTask<WorkbenchRunResult> RunStandardMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);

        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile))
        {
            throw new InvalidOperationException($"Standard Merge is not available for '{icId}'.");
        }

        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            throw new InvalidOperationException(FormatIssues(compile.Issues));
        }

        CompositionPlan plan = compile.Plan!;
        InputArtifactBinding[] bindings = [
            .. plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => CreateBinding(addressSpaceId, slotPaths)),
        ];
        string[] inputRoots = [
            .. bindings
                .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        (string outputDirectory, string outputFileName) = ResolveOutputTarget(
            bindings[0].ArtifactId,
            build,
            outputPath,
            profile.DefaultOutputFileName);
        FileArtifactReader reader = new(inputRoots);
        AtomicFileCompositionOutputWriter? writer = build
            ? new AtomicFileCompositionOutputWriter(outputDirectory, overwrite: true)
            : null;
        CompositionRunService service = new(reader, new SystemClock(), writer);
        CompositionRunRequest request = new(
            $"ui-{(build ? "build" : "preview")}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}",
            ToRunProfile(profile),
            plan,
            bindings,
            outputFileName);

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

    private static (string Directory, string FileName) ResolveOutputTarget(
        string firstInputPath,
        bool build,
        string? outputPath,
        string defaultOutputFileName)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return (Path.GetDirectoryName(firstInputPath)!, defaultOutputFileName);
        }

        if (!build)
        {
            throw new ArgumentException("Preview does not accept an output file path.", nameof(outputPath));
        }

        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        string fileName = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("Output path must include a directory and file name.", nameof(outputPath))
            : (directory, fileName);
    }

    private static IReadOnlyList<string> GetRequiredAddressSpaces(CompositionProfileDefinition profile)
    {
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return compile.IsSuccess ? compile.Plan!.RequiredInputAddressSpaceIds : [];
    }

    private static IcNumberSelection ToIcNumberSelection(string number)
    {
        IcNumberInputMode mode = string.Equals(number, "single", StringComparison.OrdinalIgnoreCase)
            ? IcNumberInputMode.SingleSelector
            : int.TryParse(number, out _)
                ? IcNumberInputMode.NumericSelector
                : IcNumberInputMode.CascadeSelector;
        return new IcNumberSelection(mode, [number]);
    }

    private static IReadOnlyList<WorkbenchMemoryMapRow> CreateDpReplaceRows(
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
        [
            .. CreatePreserveRows(regions),
            .. regions
                .Where(region => region.Kind == TpFlashMapRegionKind.Dp)
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base flash",
                    "Replace",
                    IsLdRegion(region) ? "LD BIN" : "DP BIN",
                    $"{region.DisplayName}; short DP/LD inputs can be padded by policy.")),
        ];
    }

    private static IReadOnlyList<WorkbenchMemoryMapRow> CreateCtrlRamReplaceRows(
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
        [
            .. CreatePreserveRows(regions),
            .. regions
                .Where(region => region.Kind == TpFlashMapRegionKind.CtrlRam)
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base flash",
                    "Replace + CRC",
                    region.PostbuildFileName ?? "CtrlRAM BIN",
                    $"{region.DisplayName}; combiner.exe postbuild refreshes CRC/header after staging.")),
        ];
    }

    private static IReadOnlyList<WorkbenchMemoryMapRow> CreatePreserveRows(
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
        [
            .. regions
                .Where(IsPreservedRegion)
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base flash",
                    "Preserve",
                    "Base flash",
                    $"{region.DisplayName} is intentionally not written by this workflow.")),
        ];
    }

    private static bool IsLdRegion(TpFlashMapRegion region)
    {
        return region.RegionId.Contains("ld", StringComparison.OrdinalIgnoreCase) ||
            region.DisplayName.Contains("LDC", StringComparison.OrdinalIgnoreCase);
    }

    private static InputArtifactBinding CreateBinding(
        string addressSpaceId,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return slotPaths.TryGetValue(addressSpaceId, out string? path) && !string.IsNullOrWhiteSpace(path)
            ? new InputArtifactBinding(addressSpaceId, addressSpaceId, Path.GetFullPath(path))
            : throw new InvalidOperationException($"Input slot '{addressSpaceId}' is required.");
    }

    private static CompositionRunProfile ToRunProfile(CompositionProfileDefinition profile)
    {
        return new CompositionRunProfile(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.IcId,
            profile.ModeId,
            profile.ExperienceId,
            profile.CompositionKind,
            profile.IcNumberInputMode);
    }

    private static WorkbenchRunResult ToWorkbenchRunResult(CompositionRunResult result)
    {
        CompositionRunReport report = result.Report;
        string reportJson = JsonSerializer.Serialize(report, ReportJsonOptions);
        return new WorkbenchRunResult(
            result.Status == CompositionExecutionStatus.Succeeded,
            result.Status.ToString(),
            report.ProfileId,
            report.Output.Size,
            report.Output.Sha256,
            report.Output.FileName,
            result.CommittedOutputId,
            reportJson);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }

    private static string ActionLabel(CompositionOperationKind kind)
    {
        return kind switch
        {
            CompositionOperationKind.CopyRange => "Copy",
            CompositionOperationKind.ReplaceRange => "Replace",
            CompositionOperationKind.FillRange => "Fill",
            CompositionOperationKind.PatchScalar => "Patch",
            CompositionOperationKind.RunExternalProcessor => "Postbuild",
            _ => kind.ToString(),
        };
    }

    private static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => "DP BIN",
            "tp-input" => "TP BIN",
            "ld-input" => "LD BIN",
            "output-image" => "Output",
            _ => addressSpaceId,
        };
    }

    private static bool IsPreservedRegion(TpFlashMapRegion region)
    {
        return region.Kind == TpFlashMapRegionKind.CustomerInfo ||
            region.Tags.Contains("preserve", StringComparer.OrdinalIgnoreCase);
    }

    private static string ActionSummaryForReplaceMode(string replaceMode)
    {
        return replaceMode switch
        {
            "DP" => "profile policy controls padding",
            "CtrlRAM" => "postbuild refreshes CRC/header",
            _ => "profile validation controls write access",
        };
    }

    private static CoverageSegment[] ApplyCoverageWrite(
        IReadOnlyList<CoverageSegment> current,
        CoverageSegment write)
    {
        List<CoverageSegment> next = [];
        foreach (CoverageSegment segment in current)
        {
            if (!segment.Range.Overlaps(write.Range))
            {
                next.Add(segment);
                continue;
            }

            if (segment.Range.Start < write.Range.Start)
            {
                next.Add(segment with
                {
                    Range = ByteRange.FromStartEndExclusive(segment.Range.Start, write.Range.Start),
                });
            }

            long overlapStart = Math.Max(segment.Range.Start, write.Range.Start);
            long overlapEnd = Math.Min(segment.Range.EndExclusive, write.Range.EndExclusive);
            next.Add(write with
            {
                Range = ByteRange.FromStartEndExclusive(overlapStart, overlapEnd),
            });

            if (write.Range.EndExclusive < segment.Range.EndExclusive)
            {
                next.Add(segment with
                {
                    Range = ByteRange.FromStartEndExclusive(write.Range.EndExclusive, segment.Range.EndExclusive),
                });
            }
        }

        return [.. MergeAdjacentCoverage(next.OrderBy(segment => segment.Range.Start))];
    }

    private static IEnumerable<CoverageSegment> MergeAdjacentCoverage(IEnumerable<CoverageSegment> ordered)
    {
        CoverageSegment? pending = null;
        foreach (CoverageSegment segment in ordered)
        {
            if (pending is null)
            {
                pending = segment;
                continue;
            }

            if (pending.Range.EndExclusive == segment.Range.Start &&
                string.Equals(pending.SourceLabel, segment.SourceLabel, StringComparison.Ordinal) &&
                string.Equals(pending.Fill, segment.Fill, StringComparison.Ordinal))
            {
                pending = pending with
                {
                    Range = ByteRange.FromStartEndExclusive(pending.Range.Start, segment.Range.EndExclusive),
                };
                continue;
            }

            yield return pending;
            pending = segment;
        }

        if (pending is not null)
        {
            yield return pending;
        }
    }

    private static string CoverageFill(string sourceLabel)
    {
        return sourceLabel switch
        {
            "DP BIN" => "#2563EB",
            "TP BIN" => "#16A34A",
            "LD BIN" => "#F97316",
            "CtrlRAM BIN" => "#16A34A",
            "Preserve" => "#64748B",
            _ => "#CBD5E1",
        };
    }

    private static double WidthForRange(ByteRange range, long capacity)
    {
        const double maxWidth = 300;
        return Math.Max(8, Math.Round(maxWidth * range.Length / capacity, 1));
    }

    private static string FormatFullRange(long capacity)
    {
        return capacity <= 0 ? "No range" : $"0x00000..0x{capacity:X5}";
    }

    private static string FormatDisplayRange(ByteRange range)
    {
        return $"0x{range.Start:X5}..0x{range.EndExclusive:X5}";
    }
}

/// <summary>Catalog and tool status used by the Settings page.</summary>
public sealed record WorkbenchSettingsSnapshot(
    int StandardMergeProfileCount,
    int ReplaceProfileCount,
    int FlashMapIcCount,
    int PostbuildProfileCount,
    string ToolBindingIds,
    string ToolManifestPath);

/// <summary>One readable before/after memory-map row for shell display.</summary>
public sealed record WorkbenchMemoryMapRow(
    string RangeLabel,
    string BeforeSource,
    string ActionLabel,
    string AfterSource,
    string Detail);

/// <summary>One visual Standard Merge coverage segment for shell display.</summary>
public sealed record WorkbenchMemoryCoverageSegment(
    string RangeLabel,
    string SourceLabel,
    string Detail,
    string Fill,
    double BarWidth);

internal sealed record CoverageSegment(
    ByteRange Range,
    string SourceLabel,
    string Detail,
    string Fill);

/// <summary>One CtrlRAM region row for shell display.</summary>
public sealed record WorkbenchCtrlRamRegion(
    string DisplayName,
    long Start,
    long Length,
    bool IsMultiChipOnly);

/// <summary>Composition result returned to the desktop shell.</summary>
public sealed record WorkbenchRunResult(
    bool Succeeded,
    string Status,
    string ProfileId,
    long OutputSize,
    string OutputSha256,
    string OutputFileName,
    string? CommittedOutputId,
    string ReportJson);
