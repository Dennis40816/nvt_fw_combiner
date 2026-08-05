using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionOutputNaming
{
    /// <summary>Resolves the compiled AB automatic filename without executing or publishing firmware output.</summary>
    public static async ValueTask<string> ResolveAutomaticOutputFileNameAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        CancellationToken cancellationToken,
        string? abMergeTopologyToken = null,
        ActiveSessionSnapshot? acceptedSession = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);
        string normalizedIcId = Profiles.IcIdentifier.Normalize(icId);
        TopologySelection? topology = CanonicalCapabilityResolution.ResolveAbMergeTopologySelection(
            normalizedIcId,
            abMergeTopologyToken);
        CompiledComposition composition = WorkbenchAbMergeInputProjection.ResolveComposition(
            normalizedIcId,
            topology,
            acceptedSession);
        InputArtifactBinding[] bindings = WorkbenchAbMergeInputProjection.CreateInputBindings(
            composition,
            slotPaths,
            acceptedSession);
        CompositionOutputNamePreview preview = await ResolveAutomaticOutputNameAsync(
            "ui-merge-ab",
            composition,
            bindings,
            topology,
            cancellationToken).ConfigureAwait(false);
        return !preview.CanUseAutomaticName
            ? throw new InvalidOperationException(FormatIssues(preview.Issues))
            : preview.FileName;
    }

    /// <summary>Resolves output naming through Application input admission without executing a composition.</summary>
    internal static async ValueTask<CompositionOutputNamePreview> ResolveAutomaticOutputNameAsync(
        string runIdPrefix,
        CompiledComposition compiledComposition,
        IReadOnlyList<InputArtifactBinding> bindings,
        TopologySelection? abMergeTopologySelection,
        CancellationToken cancellationToken)
    {
        string[] inputRoots =
        [
            .. bindings
                .Where(binding => !VirtualArtifactLocator.IsVirtual(binding.ArtifactId))
                .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!),
        ];
        IArtifactReader reader = inputRoots.Length > 0
            ? new FileArtifactReader(inputRoots)
            : throw new InvalidOperationException(
                "Automatic output naming requires at least one physical input artifact.");
        var service = new CompositionRunService(reader, new SystemClock());
        var request = new CompositionRunRequest(
            CreateNamingRunId(runIdPrefix),
            compiledComposition,
            bindings,
            compiledComposition.DefaultOutputFileName,
            abMergeTopologySelection: abMergeTopologySelection,
            resolvedCapability: CanonicalCapabilityResolution.ResolveCanonicalCapabilityForRun(
                compiledComposition));
        return await service.ResolveOutputNameAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(
            Environment.NewLine,
            issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }

    private static string CreateNamingRunId(string prefix)
    {
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            .ToString(CultureInfo.InvariantCulture);
        string suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        return $"{prefix}-preview-{timestamp}-{suffix}";
    }
}
