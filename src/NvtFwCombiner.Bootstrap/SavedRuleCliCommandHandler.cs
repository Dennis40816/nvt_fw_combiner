using System.Globalization;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedRuleCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("validate" or "mappings"))
        {
            await error.WriteLineAsync($"error: unknown saved-rule command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        if (args.Length != 2)
        {
            await error.WriteLineAsync($"error: saved-rule {action} expects exactly one JSON file path").ConfigureAwait(false);
            return UsageError;
        }

        if (HasV2SchemaVersion(args[1]))
        {
            return await RunV2Async(
                action,
                args[1],
                output,
                error).ConfigureAwait(false);
        }

        SavedCompositionRuleLoadResult result = SavedCompositionRuleLoader.Load(args[1]);
        if (!result.IsValid)
        {
            await PrintIssuesAsync(result.Issues, error).ConfigureAwait(false);
            return CompositionFailed;
        }

        SavedCompositionRule rule = result.Rule!;
        await PrintSummaryAsync(rule, output).ConfigureAwait(false);
        if (action == "mappings")
        {
            if (!SavedRuleGeneralMappingDraftAdapter.TryCreate(
                    rule,
                    static row => row.SourceReference,
                    out GeneralMappingDraftState? draft,
                    out IReadOnlyList<SavedRuleValidationIssue> projectionIssues))
            {
                await PrintIssuesAsync(projectionIssues, error).ConfigureAwait(false);
                return CompositionFailed;
            }

            await PrintMappingsAsync(draft, output).ConfigureAwait(false);
        }

        return Success;
    }

    private static async Task PrintSummaryAsync(SavedCompositionRule rule, TextWriter output)
    {
        await output.WriteLineAsync($"Rule: {rule.RuleId} {rule.RuleVersion}").ConfigureAwait(false);
        await output.WriteLineAsync($"Name: {rule.DisplayName}").ConfigureAwait(false);
        await output.WriteLineAsync($"Status: {rule.SupportStatus}").ConfigureAwait(false);
        await output.WriteLineAsync($"Composition: {rule.CompositionKind} / {rule.SourceExperience}").ConfigureAwait(false);
        await output.WriteLineAsync(
                $"Compatibility: IC={JoinOrAll(rule.Compatibility.IcIds)} mode={JoinOrAll(rule.Compatibility.ModeIds)} profile={JoinOrAll(rule.Compatibility.ProfileIds)}")
            .ConfigureAwait(false);
        await output.WriteLineAsync(
                string.Create(CultureInfo.InvariantCulture, $"Mappings: {rule.MappingRows.Count}"))
            .ConfigureAwait(false);
    }

    private static async Task PrintMappingsAsync(
        GeneralMappingDraftState draft,
        TextWriter output)
    {
        await output.WriteLineAsync("Mapping rows:").ConfigureAwait(false);
        foreach (GeneralMappingDraftRow row in draft.Rows)
        {
            await output.WriteLineAsync(
                    $"  {row.MappingId}: {row.Source.Reference} {FormatRange(row.SourceRange)} -> {row.TargetAddressSpaceId} {FormatRange(row.TargetRange)}")
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("CLI mapping fragments:").ConfigureAwait(false);
        foreach (GeneralMappingDraftRow row in draft.Rows)
        {
            string fragment = row.OperationKind switch
            {
                Domain.Composition.ExplicitMappingOperationKind.CopyRange =>
                    FormatGeneralMergeMapping(row),
                Domain.Composition.ExplicitMappingOperationKind.ReplaceRange =>
                    FormatGeneralReplaceMapping(row),
                _ => throw new InvalidOperationException(
                    $"Unsupported General mapping operation kind '{row.OperationKind}'."),
            };
            await output.WriteLineAsync($"  {fragment}").ConfigureAwait(false);
        }
    }

    private static string FormatGeneralMergeMapping(GeneralMappingDraftRow row)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"--mapping 0x{row.SourceRange.Start:X}+0x{row.TargetRange.Start:X}+0x{row.TargetRange.Length:X}=<{row.Source.Reference}>");
    }

    private static string FormatGeneralReplaceMapping(GeneralMappingDraftRow row)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"--mapping 0x{row.TargetRange.Start:X}+0x{row.TargetRange.Length:X}=<{row.Source.Reference}>");
    }

    private static async Task PrintIssuesAsync(IReadOnlyList<SavedRuleValidationIssue> issues, TextWriter error)
    {
        await error.WriteLineAsync("Saved rule validation failed:").ConfigureAwait(false);
        foreach (SavedRuleValidationIssue issue in issues)
        {
            await error.WriteLineAsync($"  {issue.Code} at {issue.Path}: {issue.Message}").ConfigureAwait(false);
        }
    }

    private static string FormatRange(Domain.Composition.ByteRange range)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"0x{range.Start:X}-0x{range.EndExclusive - 1:X} (len 0x{range.Length:X})");
    }

    private static string JoinOrAll(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "(any)" : string.Join(",", values);
    }

    private static Task WriteUsageAsync(TextWriter output)
    {
        return output.WriteLineAsync(
            "usage: nvt_fw_combiner saved-rule validate <rule.json>\n" +
            "       nvt_fw_combiner saved-rule mappings <rule.json>");
    }
}
