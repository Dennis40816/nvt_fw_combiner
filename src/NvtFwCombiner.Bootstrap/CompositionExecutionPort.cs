#pragma warning disable IDE0022 // Focused ports intentionally stay as concise forwarding adapters.
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal sealed class CompositionExecutionPort : ICompositionExecution
{
    public ValueTask<WorkbenchRunResult> RunStandardMergeAcceptedSessionWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null) =>
        CompositionExecutionAdapter.RunStandardMergeAcceptedSessionWithProgressAsync(
            icId,
            slotPaths,
            acceptedSession,
            build,
            progress,
            cancellationToken,
            outputPath);

    public ValueTask<WorkbenchRunResult> RunGeneralMergeAcceptedSessionWithProgressAsync(
        string icId,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null) =>
        CompositionExecutionAdapter.RunGeneralMergeAcceptedSessionWithProgressAsync(
            icId,
            acceptedSession,
            build,
            progress,
            cancellationToken,
            outputPath);

    public ValueTask<WorkbenchRunResult> RunAbMergeAcceptedSessionWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        string? abMergeTopologyToken = null,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false) =>
        CompositionExecutionAdapter.RunAbMergeAcceptedSessionWithProgressAsync(
            icId,
            slotPaths,
            acceptedSession,
            build,
            progress,
            cancellationToken,
            outputPath,
            abMergeTopologyToken,
            aFlashCodeOutputPath,
            outputPathUsesAutomaticName,
            aFlashCodeOutputPathUsesAutomaticName);

    public ValueTask<WorkbenchRunResult> PreviewGeneralReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken) =>
        CompositionExecutionAdapter.PreviewGeneralReplaceAcceptedSessionWithProgressAsync(
            icId,
            number,
            slotPaths,
            acceptedSession,
            progress,
            cancellationToken);

    public ValueTask<WorkbenchRunResult> BuildGeneralReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CompositionRunProgressFeed progress,
        string? outputPath,
        CancellationToken cancellationToken) =>
        CompositionExecutionAdapter.BuildGeneralReplaceAcceptedSessionWithProgressAsync(
            icId,
            number,
            slotPaths,
            acceptedSession,
            progress,
            outputPath,
            cancellationToken);

    public ValueTask<WorkbenchRunResult> RunReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null) =>
        CompositionExecutionAdapter.RunReplaceAcceptedSessionWithProgressAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            acceptedSession,
            build,
            progress,
            cancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit);

    public WorkbenchRunResult CreateRejectedReplaceAttemptResult(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<CompositionIssue> authoringIssues,
        bool build) =>
        CompositionExecutionAdapter.CreateRejectedReplaceAttemptResult(
            icId,
            number,
            replaceMode,
            slotPaths,
            authoringIssues,
            build);
}



