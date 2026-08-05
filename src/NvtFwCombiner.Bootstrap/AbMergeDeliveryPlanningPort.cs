using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap;

internal sealed class AbMergeDeliveryPlanningPort : IAbMergeDeliveryPlanning
{
    public async ValueTask<WorkbenchAbAFlashCodeDeliveryPlan?> TryCreateAFlashCodeDeliveryPlanAsync(
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
        CompositionOutputNamePreview preview = await CompositionOutputNaming.ResolveAutomaticOutputNameAsync(
            "ui-merge-ab",
            composition,
            bindings,
            topology,
            cancellationToken).ConfigureAwait(false);
        return !preview.CanUseAutomaticName
            ? throw new InvalidOperationException(CompositionOutputNaming.FormatIssues(preview.Issues))
            : await AbMergeAFlashCodeExportService.TryCreatePlanAsync(
                composition,
                slotPaths,
                preview,
                cancellationToken).ConfigureAwait(false);
    }
}
