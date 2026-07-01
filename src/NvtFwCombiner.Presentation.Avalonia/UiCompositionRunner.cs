using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
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
}
