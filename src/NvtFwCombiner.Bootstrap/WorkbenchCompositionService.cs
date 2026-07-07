using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Typed facade used by the desktop shell to query catalogs and run application services.</summary>
public static partial class WorkbenchCompositionService
{
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

    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetStandardMergeMemoryMapRows(string icId)
    {
        return GetStandardMergeMemoryMapRows(icId, dpInputLength: null);
    }

    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile and DP input length.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetStandardMergeMemoryMapRows(string icId, long? dpInputLength)
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

        if (IsDpPerspectiveLengthPending(profile, dpInputLength))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    FormatStandardMergeInitializationRangeLabel(profile, dpInputLength),
                    "No output",
                    "Initialize",
                    $"Blank output 0x{profile.Initialization.FillByte:X2}",
                    FormatStandardMergeInitializationDetail(profile, dpInputLength)),
            ];
        }

        if (!TryResolveStandardMergeProfileForDisplay(profile, dpInputLength, out profile, out string profileIssue))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Profile",
                    "Profile",
                    "Blocked",
                    "No output",
                    profileIssue),
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
                FormatStandardMergeInitializationRangeLabel(profile, dpInputLength),
                "No output",
                "Initialize",
                initializedState,
                FormatStandardMergeInitializationDetail(profile, dpInputLength)),
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
                $"Sequence {operation.Sequence}: {operation.Kind} {sourceRange} -> output image {FormatDisplayRange(operation.TargetRange)}. Reason: {operation.Reason}"));
        }

        return rows;
    }

    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetStandardMergeCoverageSegments(string icId)
    {
        return GetStandardMergeCoverageSegments(icId, dpInputLength: null);
    }

    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile and DP input length.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetStandardMergeCoverageSegments(
        string icId,
        long? dpInputLength)
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
                    280,
                    false),
            ];
        }

        if (IsDpPerspectiveLengthPending(profile, dpInputLength))
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Selected DP BIN length pending",
                    "DP length pending",
                    "Select a DP BIN before final ownership is drawn. Supported DP lengths are 0x40000, 0x80000, and 0x100000.",
                    "#CBD5E1",
                    280,
                    false),
            ];
        }

        if (!TryResolveStandardMergeProfileForDisplay(profile, dpInputLength, out profile, out string profileIssue))
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Profile",
                    "Invalid DP length",
                    profileIssue,
                    "#F97316",
                    280,
                    false),
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
                    280,
                    false),
            ];
        }

        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, profile.Initialization.Capacity),
                $"Blank 0x{profile.Initialization.FillByte:X2}",
                "No source input writes this output range.",
                "#CBD5E1",
                false),
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
                    CoverageFill(label),
                    false));
        }

        return
        [
            .. segments.Select(segment => new WorkbenchMemoryCoverageSegment(
                FormatDisplayRange(segment.Range),
                segment.SourceLabel,
                segment.Detail,
                segment.Fill,
                WidthForRange(segment.Range, profile.Initialization.Capacity),
                false)),
        ];
    }

    /// <summary>Gets output address coverage text for the selected Standard Merge profile.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId)
    {
        return GetStandardMergeMemoryRangeLabel(icId, dpInputLength: null);
    }

    /// <summary>Gets output address coverage text for the selected Standard Merge profile and DP input length.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId, long? dpInputLength)
    {
        return !StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? "No Standard Merge profile"
            : IsDpPerspectiveLengthPending(profile, dpInputLength)
                ? "Selected DP BIN length pending"
                : TryResolveStandardMergeProfileForDisplay(profile, dpInputLength, out profile, out string profileIssue)
                    ? FormatFullRange(profile.Initialization.Capacity)
                    : profileIssue;
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
            LegacyCombinerPostbuildCatalog.All.Select(profile => profile.IcId).Distinct(StringComparer.Ordinal).Count(),
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

        profile = ResolveStandardMergeProfileForInputs(profile, slotPaths);
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

        return await RunCompiledCompositionAsync(
            "ui",
            profile,
            plan,
            bindings,
            bindings[0].ArtifactId,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: null,
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
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

    private static CompositionProfileDefinition ResolveStandardMergeProfileForInputs(
        CompositionProfileDefinition profile,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return !BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile) ||
            !slotPaths.TryGetValue("dp-input", out string? dpPath) ||
            string.IsNullOrWhiteSpace(dpPath) ||
            !File.Exists(dpPath)
                ? profile
                : BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(
                    profile.IcId,
                    new FileInfo(dpPath).Length);
    }

    private static bool TryResolveStandardMergeProfileForDisplay(
        CompositionProfileDefinition profile,
        long? dpInputLength,
        out CompositionProfileDefinition resolvedProfile,
        out string profileIssue)
    {
        resolvedProfile = profile;
        profileIssue = string.Empty;
        if (dpInputLength is null ||
            !BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile))
        {
            return true;
        }

        try
        {
            resolvedProfile = BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(
                profile.IcId,
                dpInputLength.Value);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            profileIssue = FormattableString.Invariant(
                $"Selected DP BIN length 0x{dpInputLength.Value:X} is unsupported; expected 0x40000, 0x80000, or 0x100000.");
            return false;
        }
    }

    private static bool IsDpPerspectiveLengthPending(
        CompositionProfileDefinition profile,
        long? dpInputLength)
    {
        return BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile) &&
            dpInputLength is null;
    }

    private static string FormatStandardMergeInitializationRangeLabel(
        CompositionProfileDefinition profile,
        long? dpInputLength)
    {
        return IsDpPerspectiveLengthPending(profile, dpInputLength)
            ? "Selected DP BIN length pending"
            : FormatFullRange(profile.Initialization.Capacity);
    }

    private static string FormatStandardMergeInitializationDetail(
        CompositionProfileDefinition profile,
        long? dpInputLength)
    {
        return !BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile)
            ? "Start with the initialized image. Unlisted ranges keep this value until a later operation writes them."
            : dpInputLength is null
                ? "Start with the initialized image after selecting a DP BIN. Supported DP lengths are 0x40000, 0x80000, and 0x100000."
                : "Start with the initialized image using the selected DP BIN length. Unlisted ranges keep this value until a later operation writes them.";
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
            "reference-base" => "Base flash",
            "dp-replacement" => "DP replacement",
            "ldc-replacement" => "LDC replacement",
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
                string.Equals(pending.Detail, segment.Detail, StringComparison.Ordinal) &&
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
            "Changed DP BIN" => "#2563EB",
            "TP BIN" => "#16A34A",
            "LD BIN" => "#F97316",
            "Changed LDC BIN" => "#F97316",
            "CtrlRAM BIN" => "#16A34A",
            "Changed CtrlRAM BIN" => "#16A34A",
            "Source BIN" => "#0D9488",
            "Restored TP" => "#64748B",
            "Preserved customer info" => "#64748B",
            "Preserve" => "#64748B",
            string label when label.Contains("NF CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#DC2626",
            string label when label.Contains("Normal CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#0891B2",
            string label when label.Contains("MP CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#7C3AED",
            string label when label.Contains("VN CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#DB2777",
            string label when label.Contains("DIFF", StringComparison.OrdinalIgnoreCase) ||
                              label.Contains("DLM", StringComparison.OrdinalIgnoreCase) => "#D97706",
            string label when label.Contains("Vector", StringComparison.OrdinalIgnoreCase) => "#0D9488",
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
        return capacity <= 0 ? "No range" : FormatDisplayRange(new ByteRange(0, capacity));
    }

    private static string FormatDisplayRange(ByteRange range)
    {
        return FormattableString.Invariant($"0x{range.Start:X5}-0x{range.EndExclusive - 1:X5} (len 0x{range.Length:X})");
    }

    private static IReadOnlyList<WorkbenchMemoryCoverageSegment> ToWorkbenchCoverageSegments(
        IEnumerable<CoverageSegment> segments,
        long capacity)
    {
        return
        [
            .. segments.Select(segment => new WorkbenchMemoryCoverageSegment(
                FormatDisplayRange(segment.Range),
                segment.SourceLabel,
                segment.Detail,
                segment.Fill,
                WidthForRange(segment.Range, capacity),
                segment.IsChanged)),
        ];
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

/// <summary>Catalog and tool status used by the Settings page.</summary>
public sealed record WorkbenchSettingsSnapshot(int StandardMergeProfileCount, int ReplaceProfileCount, int FlashMapIcCount, int PostbuildProfileCount, string ToolBindingIds, string ToolManifestPath);

/// <summary>Firmware facts read from a flash image FWConfig block.</summary>
public sealed record WorkbenchFirmwareConfigMetadata(
    long FirmwareConfigStart,
    string CommonFwVersion,
    byte FirmwareVersion,
    byte FirmwareVersionBar,
    bool IsFirmwareVersionBarValid,
    byte FirmwareSubVersion,
    ushort ProjectId,
    string? PostbuildCategory);

/// <summary>DP version facts read using gen_flash standard-merge version-byte rules.</summary>
public sealed record WorkbenchDpVersionMetadata(
    string IcId,
    string Prefix,
    string VersionToken,
    string DisplayVersion,
    long InputReadOffset,
    long OutputAbsoluteAddress,
    string EvidenceSource);

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
    double BarWidth,
    bool IsChanged);

/// <summary>One file slot declared by the selected Replace workflow.</summary>
public sealed record WorkbenchReplaceInputSlot(
    string SlotId,
    string Title,
    string Description,
    bool IsOptional,
    string AddressSpaceId,
    string? RegionId);

internal sealed record CoverageSegment(
    ByteRange Range,
    string SourceLabel,
    string Detail,
    string Fill,
    bool IsChanged);

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
