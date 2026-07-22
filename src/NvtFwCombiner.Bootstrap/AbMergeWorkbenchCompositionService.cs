using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Focused workbench adapter for the owner-approved AB Merge pilot.</summary>
public static class AbMergeWorkbenchCompositionService
{
    private const string RunIdPrefix = "ui-merge-ab";

    /// <summary>Returns whether the selected IC owns an executable AB Merge profile.</summary>
    public static bool IsAbMergeSupported(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return BuiltInV2RegistrationRegistry.AbMergeByIc.TryGetValue(
            IcSupportCatalog.NormalizeIcId(icId),
            out BuiltInV2Registration? registration) &&
            registration.CreateProfileSummary().CompileSucceeded;
    }

    internal static bool TryCompileAbMerge(
        string icId,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        issues = [];
        if (!BuiltInV2RegistrationRegistry.AbMergeByIc.TryGetValue(
                IcSupportCatalog.NormalizeIcId(icId),
                out BuiltInV2Registration? registration))
        {
            return false;
        }

        registration.TryCompile(inputLength: null, out composition, out issues);
        return composition is not null;
    }

    /// <summary>Runs AB Merge preview or build through the shared Application composition service.</summary>
    public static ValueTask<WorkbenchRunResult> RunAbMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return RunAbMergeCoreAsync(
            icId,
            slotPaths,
            build,
            outputPath,
            previewOutputFileName: null,
            overwrite: true,
            progress: null,
            cancellationToken);
    }

    /// <summary>Runs AB Merge for the focused CLI adapter while preserving explicit output policy.</summary>
    internal static ValueTask<WorkbenchRunResult> RunAbMergeForCliAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        string previewOutputFileName,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previewOutputFileName);
        return RunAbMergeCoreAsync(
            icId,
            slotPaths,
            build,
            outputPath,
            previewOutputFileName,
            overwrite,
            progress: null,
            cancellationToken);
    }

    /// <summary>Runs AB Merge and publishes bounded Application-owned lifecycle phases.</summary>
    public static ValueTask<WorkbenchRunResult> RunAbMergeWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return RunAbMergeCoreAsync(
            icId,
            slotPaths,
            build,
            outputPath,
            previewOutputFileName: null,
            overwrite: true,
            progress,
            cancellationToken);
    }

    private static async ValueTask<WorkbenchRunResult> RunAbMergeCoreAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        string? previewOutputFileName,
        bool overwrite,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);
        string normalizedIcId = IcSupportCatalog.NormalizeIcId(icId);
        if (!WorkbenchCompositionService.GetAbMergeProfileSummaries().Any(
                profile => StringComparer.Ordinal.Equals(profile.IcId, normalizedIcId)))
        {
            throw new InvalidOperationException($"AB Merge is not available for '{icId}'.");
        }

        if (!TryCompileAbMerge(normalizedIcId, out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues))
        {
            throw new InvalidOperationException(WorkbenchCompositionService.FormatIssues(issues));
        }

        InputArtifactBinding[] bindings =
        [
            .. composition.Plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => slotPaths.TryGetValue(addressSpaceId, out string? path) &&
                    !string.IsNullOrWhiteSpace(path)
                        ? CompiledCompositionInputBindingFactory.Create(
                            composition,
                            addressSpaceId,
                            Path.GetFullPath(path))
                        : throw new InvalidOperationException($"Input slot '{addressSpaceId}' is required.")),
        ];
        string firstInputPath = bindings.Single(static binding =>
            binding.AddressSpaceId == CompositionAddressSpaceIds.DpAbInput).ArtifactId;

        return await WorkbenchCompositionService.RunCompiledCompositionAsync(
            RunIdPrefix,
            composition,
            bindings,
            firstInputPath,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: null,
            overwrite,
            cancellationToken,
            progress: progress,
            previewOutputFileName: previewOutputFileName).ConfigureAwait(false);
    }
}
