using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunCtrlRamReplaceAsync(
        CliCompositionServices services,
        ILocalFileStore localFiles,
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

        if (!TryParseCtrlRamSlotArguments(
                options,
                error,
                out IReadOnlyList<CtrlRamSlotArgument>? ctrlRamArguments))
        {
            return UsageError;
        }

        string resolvedBasePath = Path.GetFullPath(basePath);
        byte[] acceptedBaseBytes;
        try
        {
            acceptedBaseBytes = await CliFixedWorkflowInputReader.ReadBytesAsync(
                    localFiles,
                    resolvedBasePath,
                    CompiledInputArtifactInspectionService.MaximumContentReadBytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LocalFileReadException exception)
        {
            string message = exception is LocalFileNotFoundException
                ? "Base firmware BIN path does not exist."
                : exception.Message;
            await CliCompositionRunSupport.PrintIssuesAsync(
                    error,
                    [new CompositionIssue(
                        CompositionPlanningIssueCodes.InputArtifactReadFailed,
                        message,
                        CompositionSlotIds.ReplaceBase)])
                .ConfigureAwait(false);
            return CompositionFailed;
        }

        if (!TryCreateCtrlRamSlotPaths(
                services.CtrlRamAuthoring,
                icId,
                icNumber,
                resolvedBasePath,
                acceptedBaseBytes,
                ctrlRamArguments,
                error,
                out Dictionary<string, string>? slotPaths))
        {
            return UsageError;
        }

        var inputBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = acceptedBaseBytes,
        };
        IReadOnlyList<CompositionIssue> inputReadIssues = [];
        foreach ((string slotId, string path) in slotPaths.Where(static pair =>
                     !StringComparer.Ordinal.Equals(
                         pair.Key,
                         CompositionSlotIds.ReplaceBase)))
        {
            try
            {
                inputBytes[slotId] = await CliFixedWorkflowInputReader.ReadBytesAsync(
                        localFiles,
                        path,
                        CompiledInputArtifactInspectionService.MaximumContentReadBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (LocalFileReadException exception)
            {
                inputReadIssues = [new CompositionIssue(
                    CompositionPlanningIssueCodes.InputArtifactReadFailed,
                    exception.Message,
                    slotId)];
                break;
            }
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
