using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class GeneralWorkflowTestSupport
{
    internal static async ValueTask<CompositionRunResult> RunGeneralMergeAsync(
        CanonicalTestContext canonical,
        string icId,
        GeneralMergeDraftState draft,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return await RunGeneralMergeAsync(
            canonical,
            icId,
            draft,
            savedRulePolicy: null,
            build,
            cancellationToken,
            outputPath).ConfigureAwait(false);
    }

    internal static async ValueTask<CompositionRunResult> RunGeneralMergeAsync(
        CanonicalTestContext canonical,
        string icId,
        GeneralMergeDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        GeneralAuthoringSessionPreparation prepared =
            await PrepareGeneralMergeAsync(
                canonical,
                icId,
                draft,
                savedRulePolicy,
                cancellationToken).ConfigureAwait(false);
        GeneralAuthoringSessionPreparation accepted = prepared.Succeeded
            ? prepared
            : throw new InvalidOperationException(
                CompositionExecutionTestSupport.FormatIssues(prepared.Issues));

        return await CompositionExecutionTestSupport.Create(canonical)
            .ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    accepted.AcceptedSession!,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    build,
                    outputPath: outputPath),
                new CompositionRunProgressFeed(),
                cancellationToken).ConfigureAwait(false);
    }

    internal static ValueTask<GeneralAuthoringSessionPreparation> PrepareGeneralMergeAsync(
        CanonicalTestContext canonical,
        string icId,
        GeneralMergeDraftState draft,
        CancellationToken cancellationToken)
    {
        return PrepareGeneralMergeAsync(
            canonical,
            icId,
            draft,
            savedRulePolicy: null,
            cancellationToken);
    }

    internal static ValueTask<GeneralAuthoringSessionPreparation> PrepareGeneralMergeAsync(
        CanonicalTestContext canonical,
        string icId,
        GeneralMergeDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (savedRulePolicy is not null)
        {
            draft = new GeneralMergeDraftState(
                draft.OutputInitializer,
                draft.Mappings.WithSavedRuleResourcePolicy(savedRulePolicy));
        }

        return canonical.GeneralAuthoring
            .PrepareMergeSessionAsync(
            new AuthoringSessionState(ExperienceIds.GeneralMerge),
            icId,
            draft,
            cancellationToken);
    }

    internal static ValueTask<CompositionRunResult> PreviewGeneralReplaceAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState draft,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceAsync(
            canonical,
            icId,
            number,
            slotPaths,
            draft,
            savedRulePolicy: null,
            build: false,
            outputPath: null,
            progress: null,
            cancellationToken);
    }

    internal static ValueTask<CompositionRunResult> PreviewGeneralReplaceAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceAsync(
            canonical,
            icId,
            number,
            slotPaths,
            draft,
            savedRulePolicy,
            build: false,
            outputPath: null,
            progress: null,
            cancellationToken);
    }

    internal static ValueTask<CompositionRunResult> BuildGeneralReplaceAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState draft,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceAsync(
            canonical,
            icId,
            number,
            slotPaths,
            draft,
            savedRulePolicy: null,
            build: true,
            outputPath,
            progress: null,
            cancellationToken);
    }

    internal static ValueTask<CompositionRunResult> BuildGeneralReplaceAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState draft,
        string? outputPath,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceAsync(
            canonical,
            icId,
            number,
            slotPaths,
            draft,
            savedRulePolicy,
            build: true,
            outputPath,
            progress: null,
            cancellationToken);
    }

    internal static ValueTask<CompositionRunResult> RunGeneralReplaceWithProgressAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState draft,
        bool build,
        CompositionRunProgressFeed progress,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceAsync(
            canonical,
            icId,
            number,
            slotPaths,
            draft,
            savedRulePolicy: null,
            build,
            outputPath,
            progress,
            cancellationToken);
    }

    private static async ValueTask<CompositionRunResult> RunGeneralReplaceAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        bool build,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        GeneralAuthoringSessionPreparation prepared =
            await PrepareGeneralReplaceAsync(
                canonical,
                icId,
                number,
                slotPaths,
                draft,
                savedRulePolicy,
                cancellationToken).ConfigureAwait(false);
        if (!prepared.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                prepared.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        }

        ICompositionExecution execution = CompositionExecutionTestSupport.Create(canonical);
        progress ??= new CompositionRunProgressFeed();
        return await execution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(
                prepared.AcceptedSession!,
                 slotPaths,
                 build,
                 outputPath: outputPath,
                 actionReadiness: prepared.Readiness),
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    internal static ValueTask<GeneralAuthoringSessionPreparation> PrepareGeneralReplaceAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        CancellationToken cancellationToken)
    {
        if (savedRulePolicy is not null)
        {
            draft = draft.WithSavedRuleResourcePolicy(savedRulePolicy);
        }

        return canonical.GeneralAuthoring
            .PrepareReplaceSessionAsync(
            new AuthoringSessionState(ExperienceIds.GeneralReplace),
            icId,
            number,
            slotPaths[CompositionSlotIds.ReplaceBase],
            draft,
            cancellationToken);
    }
}
