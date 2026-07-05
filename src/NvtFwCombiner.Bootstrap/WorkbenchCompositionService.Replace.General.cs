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
    // Conservative deny-by-default header window. Inspected postbuild references use at least
    // a 0x100-byte firmware header copy block, and General Replace has no owner-approved
    // header editing workflow yet.
    private const long GeneralReplaceProtectedHeaderLength = 0x100;

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

        CompositionProfileDefinition profile = CreateGeneralReplaceProfile(icId, number, capacity, fullBasePath);
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
                profile.DefaultOutputFileName,
                succeeded: false);
        }

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
        string[] inputRoots =
        [
            .. bindings
                .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        (string outputDirectory, string outputFileName) = ResolveOutputTarget(
            fullBasePath,
            build,
            outputPath,
            profile.DefaultOutputFileName);
        if (build)
        {
            ProtectedPathGuard.EnsureOutputDoesNotAliasInputs(
                ProtectedPathGuard.CombineFullPath(outputDirectory, outputFileName),
                bindings,
                nameof(outputPath));
        }

        FileArtifactReader reader = new(inputRoots);
        AtomicFileCompositionOutputWriter? writer = build
            ? new AtomicFileCompositionOutputWriter(outputDirectory, overwrite: true)
            : null;
        CompositionRunService service = new(reader, new SystemClock(), writer);
        CompositionRunRequest request = new(
            $"ui-replace-general-{(build ? "build" : "preview")}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}",
            ToRunProfile(profile),
            compile.Plan!,
            bindings,
            outputFileName,
            icNumberSelection: ToIcNumberSelection(number));

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

    private static CompositionProfileDefinition CreateGeneralReplaceProfile(
        string icId,
        string number,
        long capacity,
        string basePath)
    {
        string normalizedIc = icId.ToLowerInvariant();
        IcNumberSelection selection = ToIcNumberSelection(number);
        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            icId,
            basePath,
            out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        ProfileRegion[] regions = CreateGeneralReplaceRegions(
            icId,
            selection,
            capacity,
            postbuildProfile);
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
            [],
            regions,
            accessRules,
            selection.Mode);
    }

    private static ProfileRegion[] CreateGeneralReplaceRegions(
        string icId,
        IcNumberSelection selection,
        long capacity,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        List<ProfileRegion> regions = [];
        long protectedHeaderEnd = Math.Min(capacity, GeneralReplaceProtectedHeaderLength);
        if (protectedHeaderEnd > 0)
        {
            regions.Add(new ProfileRegion(
                "protected-header",
                "output-image",
                new ByteRange(0, protectedHeaderEnd),
                RegionAtomicity.Whole,
                RegionWritePolicy.Forbidden,
                classificationTags: ["header", "protected"]));
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
            regions.Add(new ProfileRegion(
                region.RegionId,
                "output-image",
                ByteRange.FromStartEndExclusive(start, end),
                explicitRange ? RegionAtomicity.ExplicitMapping : RegionAtomicity.Whole,
                explicitRange ? RegionWritePolicy.GeneralExplicit : RegionWritePolicy.Forbidden,
                classificationTags: CreateGeneralReplaceRegionTags(region)));
        }

        return [.. regions];
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
