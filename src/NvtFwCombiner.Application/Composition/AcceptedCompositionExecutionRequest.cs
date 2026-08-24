using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>One accepted-session execution intent shared by every authoring workflow.</summary>
public sealed class AcceptedCompositionExecutionRequest(
    ActiveSessionSnapshot acceptedSession,
    IReadOnlyDictionary<string, string> slotPaths,
    bool build,
    string? outputPath = null,
    string? previewOutputFileName = null,
    string? additionalDeliveryOutputPath = null,
    bool outputPathUsesAutomaticName = false,
    bool additionalDeliveryOutputPathUsesAutomaticName = false,
    string? automaticOutputDirectory = null,
    string? reportPath = null,
    CapabilityActionReadinessSnapshot? actionReadiness = null,
    CompositionOutputBundleIntent? outputBundle = null)
{
    /// <summary>Exact immutable authoring session admitted for this run.</summary>
    public ActiveSessionSnapshot AcceptedSession { get; } = CompositionSummaryValue.NotNull(
        acceptedSession,
        nameof(acceptedSession));

    internal AcceptedCompositionExecutionRoute Route { get; } = AcceptedCompositionExecutionRoutes.Resolve(CompositionSummaryValue.NotNull(
            acceptedSession,
            nameof(acceptedSession)).WorkflowId);

    /// <summary>Current host path hints matched against the accepted immutable input identities.</summary>
    public IReadOnlyDictionary<string, string> SlotPaths { get; } = SnapshotSlotPaths(slotPaths);

    /// <summary>Whether the run may commit output through the configured host writer.</summary>
    public bool Build { get; } = build;

    /// <summary>Selected IC identifier retained by the accepted session.</summary>
    public string IcId => AcceptedSession.SelectedIc;

    /// <summary>Caller-selected primary output path for Build.</summary>
    public string? OutputPath { get; } = outputPath;

    /// <summary>Plain caller-selected Preview filename override.</summary>
    public string? PreviewOutputFileName { get; } = previewOutputFileName;

    /// <summary>Optional compiled additional-delivery output path.</summary>
    public string? AdditionalDeliveryOutputPath { get; } = additionalDeliveryOutputPath;

    /// <summary>Whether the primary Build path came from automatic compiled naming.</summary>
    public bool OutputPathUsesAutomaticName { get; } = outputPathUsesAutomaticName;

    /// <summary>Whether the additional delivery path came from automatic compiled naming.</summary>
    public bool AdditionalDeliveryOutputPathUsesAutomaticName { get; } =
        additionalDeliveryOutputPathUsesAutomaticName;

    /// <summary>Optional host-selected directory for an automatically rendered output filename.</summary>
    public string? AutomaticOutputDirectory { get; } = automaticOutputDirectory;

    /// <summary>Optional report path protected from output aliasing.</summary>
    public string? ReportPath { get; } = reportPath;

    /// <summary>Exact action readiness publication required by readiness-gated workflows.</summary>
    public CapabilityActionReadinessSnapshot? ActionReadiness { get; } = actionReadiness;

    /// <summary>Optional atomic output-and-accepted-sources folder intent.</summary>
    public CompositionOutputBundleIntent? OutputBundle { get; } = outputBundle;

    private static ReadOnlyDictionary<string, string> SnapshotSlotPaths(
        IReadOnlyDictionary<string, string> slotPaths)
    {
        ArgumentNullException.ThrowIfNull(slotPaths);
        return new ReadOnlyDictionary<string, string>(
            slotPaths.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal));
    }
}

internal delegate ValueTask<CompositionRunResult> AcceptedCompositionExecutionRoute(
    CompositionExecutionExperience owner,
    AcceptedCompositionExecutionRequest request,
    CompositionRunProgressFeed progress,
    CancellationToken cancellationToken);

internal static class AcceptedCompositionExecutionRoutes
{
    internal static AcceptedCompositionExecutionRoute Resolve(string workflowId)
    {
        return workflowId switch
        {
            ExperienceIds.StandardMerge => static (owner, request, progress, token) => owner.ExecuteStandardMergeAsync(request, progress, token),
            ExperienceIds.AbMerge => static (owner, request, progress, token) => owner.ExecuteAbMergeAsync(request, progress, token),
            ExperienceIds.GeneralMerge => static (owner, request, progress, token) => owner.ExecuteGeneralMergeAsync(request, progress, token),
            ExperienceIds.DpReplace => static (owner, request, progress, token) => owner.ExecuteDpReplaceAsync(request, progress, token),
            ExperienceIds.CtrlRamReplace => static (owner, request, progress, token) => owner.ExecuteCtrlRamReplaceAsync(request, progress, token),
            ExperienceIds.GeneralReplace => static (owner, request, progress, token) => owner.ExecuteGeneralReplaceAsync(request, progress, token),
            _ => throw new InvalidOperationException($"Accepted workflow '{workflowId}' has no execution path."),
        };
    }
}
