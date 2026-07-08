using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

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
                    $"Select a DP BIN before final ownership is drawn. Supported DP lengths are {DpPerspectiveCatalog.FormatSupportedLengths()}.",
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

    /// <summary>Gets a compact, catalog-backed policy summary for the selected Standard Merge IC.</summary>
    public static string GetStandardMergePolicySummary(string icId)
    {
        return DpPerspectiveCatalog.IsSupportedIc(icId)
            ? $"TP paste range: {FormatDisplayRange(DpPerspectiveCatalog.TpOverlayRange)}; {FormatDisplayRange(DpPerspectiveCatalog.CustomerInfoPreserveRange)} is preserved customer information."
            : "Address ranges come from the built-in Standard Merge profile.";
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
                $"Selected DP BIN length 0x{dpInputLength.Value:X} is unsupported; expected {DpPerspectiveCatalog.FormatSupportedLengths()}.");
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
                ? $"Start with the initialized image after selecting a DP BIN. Supported DP lengths are {DpPerspectiveCatalog.FormatSupportedLengths()}."
                : "Start with the initialized image using the selected DP BIN length. Unlisted ranges keep this value until a later operation writes them.";
    }

    private static IReadOnlyList<string> GetRequiredAddressSpaces(CompositionProfileDefinition profile)
    {
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return compile.IsSuccess ? compile.Plan!.RequiredInputAddressSpaceIds : [];
    }
}
