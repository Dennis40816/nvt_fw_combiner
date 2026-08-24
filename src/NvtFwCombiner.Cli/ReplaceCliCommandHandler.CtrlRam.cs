using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunCtrlRamReplaceAsync(
        CliCompositionServices services,
        string action,
        string icId,
        ParsedCliOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath))
        {
            return UsageError;
        }

        if (!TryCreateCtrlRamSlotPaths(
                services.CtrlRamAuthoring,
                icId,
                icNumber,
                basePath,
                options,
                error,
                out Dictionary<string, string>? slotPaths))
        {
            return UsageError;
        }

        Dictionary<string, byte[]> inputBytes = new(StringComparer.Ordinal);
        IReadOnlyList<CompositionIssue> inputReadIssues = [];
        try
        {
            inputBytes = slotPaths.ToDictionary(
                static pair => pair.Key,
                static pair => File.ReadAllBytes(pair.Value),
                StringComparer.Ordinal);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            string message = !File.Exists(basePath)
                ? "Base firmware BIN path does not exist."
                : exception.Message;
            inputReadIssues = [new CompositionIssue(
                CompositionPlanningIssueCodes.InputArtifactReadFailed,
                message,
                CompositionSlotIds.ReplaceBase)];
        }

        if (inputReadIssues.Count != 0)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, inputReadIssues)
                .ConfigureAwait(false);
            return CompositionFailed;
        }

        var session = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        CtrlRamAuthoringSessionPreparation prepared =
            services.CtrlRamAuthoring.PrepareSession(
                session,
                icId,
                icNumber,
                slotPaths,
                inputBytes);
        if (!prepared.Succeeded)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, prepared.Issues)
                .ConfigureAwait(false);
            return CompositionFailed;
        }

        ActiveSessionSnapshot acceptedSession = prepared.AcceptedSession!;
        CapabilityActionReadinessSnapshot? readiness =
            await services.CtrlRamAuthoring.GetActionReadinessAsync(
                    icId,
                    icNumber,
                    slotPaths,
                    acceptedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        bool build = action == "build";
        CapabilityActionAvailability? selectedAction = build
            ? readiness?.Build
            : readiness?.Preview;
        if (readiness is null || selectedAction?.IsAvailable != true)
        {
            CapabilityActionBlocker? blocker = selectedAction?.PrimaryBlocker;
            await CliCompositionRunSupport.PrintIssuesAsync(
                    error,
                    [new CompositionIssue(
                        blocker?.Code ?? CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
                        blocker?.Message ??
                            "CtrlRAM action readiness is unavailable for the accepted publication.",
                        blocker?.SubjectId ?? ExperienceIds.CtrlRamReplace)])
                .ConfigureAwait(false);
            return CompositionFailed;
        }

        string defaultOutputFileName = services.OutputNaming
            .ResolveAcceptedOutput(acceptedSession)
            .OutputName.FileName;
        if (!CliBundleOptions.TryCreateIntent(
                services.OutputNaming,
                acceptedSession,
                options.Values,
                error,
                out CompositionOutputBundleIntent? outputBundle))
        {
            return UsageError;
        }

        ValueTask<CompositionRunResult> RunAcceptedAsync(
            string? outputPath,
            string? automaticOutputDirectory,
            CompositionOutputBundleIntent? bundle,
            bool executeBuild,
            CancellationToken token)
        {
            return services.Execution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    acceptedSession,
                    slotPaths,
                    executeBuild,
                    outputPath: outputPath,
                    automaticOutputDirectory: automaticOutputDirectory,
                    actionReadiness: readiness,
                    outputBundle: bundle),
                new CompositionRunProgressFeed(),
                token);
        }

        return await CompleteReplaceRunAsync(
                action,
                icId,
                ExperienceIds.CtrlRamReplace,
                options,
                slotPaths,
                defaultOutputFileName,
                outputBundle,
                RunAcceptedAsync,
                output,
                error,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
