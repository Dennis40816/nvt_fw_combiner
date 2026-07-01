using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Thin UI adapter for invoking application composition services.</summary>
public static class UiCompositionRunner
{
    private static readonly Dictionary<string, CompositionProfileDefinition> StandardMergeProfilesByIc =
        BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles.ToDictionary(
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

    /// <summary>Gets production-supported IC ids from the TP flash-map catalog.</summary>
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
    public static IReadOnlyList<TpFlashMapRegion> GetCtrlRamRegions(string icId, string number)
    {
        return TpFlashMapCatalog.GetCtrlRamRegions(icId, ToIcNumberSelection(number));
    }

    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<MemoryMapRowViewModel> GetStandardMergeMemoryMapRows(string icId)
    {
        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile))
        {
            return
            [
                new MemoryMapRowViewModel(
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
                new MemoryMapRowViewModel(
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
        List<MemoryMapRowViewModel> rows =
        [
            new(
                FormatFullRange(profile.Initialization.Capacity),
                "No output",
                "Initialize",
                initializedState,
                "Unlisted ranges keep this initialized value until a copy step writes them."),
        ];

        foreach (CompositionOperation operation in compile.Plan!.OrderedOperations)
        {
            string afterSource = operation.SourceSpaceId is null
                ? operation.Kind.ToString()
                : AddressSpaceLabel(operation.SourceSpaceId);
            string sourceRange = operation.SourceRange is null
                ? "no source range"
                : FormatRange(operation.SourceRange.Value);
            rows.Add(new MemoryMapRowViewModel(
                FormatRange(operation.TargetRange),
                initializedState,
                ActionLabel(operation.Kind),
                afterSource,
                $"{sourceRange} -> output, sequence {operation.Sequence}."));
        }

        return rows;
    }

    /// <summary>Gets output address coverage text for the selected Standard Merge profile.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId)
    {
        return StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? FormatFullRange(profile.Initialization.Capacity)
            : "No Standard Merge profile";
    }

    /// <summary>Gets readable memory-map rows for the selected Replace mode.</summary>
    public static IReadOnlyList<MemoryMapRowViewModel> GetReplaceMemoryMapRows(
        string icId,
        string number,
        string replaceMode)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        return regions.Count == 0
            ?
            [
                new MemoryMapRowViewModel(
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
                new MemoryMapRowViewModel(
                    "Runtime range",
                    "Base flash",
                    "Replace",
                    "General BIN",
                    "The selected explicit range must be approved by the compiled General Replace profile."),
            ],
                _ =>
                [
                    new MemoryMapRowViewModel(
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

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static UiSettingsSnapshot GetSettingsSnapshot()
    {
        IReadOnlyList<string> toolBindingIds =
        [
            .. LegacyCombinerPostbuildCatalog.All
                .Select(profile => profile.ToolBindingId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        return new UiSettingsSnapshot(
            BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles.Count,
            BuiltInReplaceProfiles.All.Count,
            TpFlashMapCatalog.IcIds.Count,
            LegacyCombinerPostbuildCatalog.All.Count,
            string.Join(", ", toolBindingIds),
            "external-tools/legacy-combiner/1.13.0/manifest.json");
    }

    /// <summary>Runs Standard Merge preview or build through the application core.</summary>
    public static async ValueTask<CompositionRunResult> RunStandardMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken)
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
        string outputDirectory = Path.GetDirectoryName(bindings[0].ArtifactId)!;
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
            profile.DefaultOutputFileName);

        if (!build)
        {
            return await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        }

        CompositionRunResult preview = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        return preview.Status == CompositionExecutionStatus.Succeeded
            ? await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), cancellationToken)
                .ConfigureAwait(false)
            : preview;
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

    private static IReadOnlyList<MemoryMapRowViewModel> CreateDpReplaceRows(
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
        [
            .. CreatePreserveRows(regions),
            .. regions
                .Where(region => region.Kind == TpFlashMapRegionKind.Dp)
                .OrderBy(region => region.Range.Start)
                .Select(region => new MemoryMapRowViewModel(
                    FormatRange(region.Range),
                    "Base flash",
                    "Replace",
                    IsLdRegion(region) ? "LD BIN" : "DP BIN",
                    $"{region.DisplayName}; short DP/LD inputs can be padded by policy.")),
        ];
    }

    private static IReadOnlyList<MemoryMapRowViewModel> CreateCtrlRamReplaceRows(
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
        [
            .. CreatePreserveRows(regions),
            .. regions
                .Where(region => region.Kind == TpFlashMapRegionKind.CtrlRam)
                .OrderBy(region => region.Range.Start)
                .Select(region => new MemoryMapRowViewModel(
                    FormatRange(region.Range),
                    "Base flash",
                    "Replace + CRC",
                    region.PostbuildFileName ?? "CtrlRAM BIN",
                    $"{region.DisplayName}; combiner.exe postbuild refreshes CRC/header after staging.")),
        ];
    }

    private static IReadOnlyList<MemoryMapRowViewModel> CreatePreserveRows(
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
        [
            .. regions
                .Where(region => region.Kind == TpFlashMapRegionKind.CustomerInfo ||
                    region.Tags.Contains("preserve", StringComparer.OrdinalIgnoreCase))
                .OrderBy(region => region.Range.Start)
                .Select(region => new MemoryMapRowViewModel(
                    FormatRange(region.Range),
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

    private static string FormatFullRange(long capacity)
    {
        return capacity <= 0 ? "No range" : $"0x00000 - 0x{capacity - 1:X5}";
    }

    private static string FormatRange(ByteRange range)
    {
        return $"0x{range.Start:X5} - 0x{range.EndExclusive - 1:X5}";
    }
}

/// <summary>Catalog and tool status used by the Settings page.</summary>
public sealed record UiSettingsSnapshot(
    int StandardMergeProfileCount,
    int ReplaceProfileCount,
    int FlashMapIcCount,
    int PostbuildProfileCount,
    string ToolBindingIds,
    string ToolManifestPath);
