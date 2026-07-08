using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
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
}
