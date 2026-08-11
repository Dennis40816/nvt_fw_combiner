using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Cli;

internal static partial class SavedRuleCliCommandHandler
{
    private static async Task<int> RunV2Async(
        ISavedRuleAuthoring authoring,
        string action,
        string path,
        TextWriter output,
        TextWriter error)
    {
        SavedRuleV2InspectionResult inspection = authoring.InspectSavedRuleV2(path);
        if (!inspection.IsValid)
        {
            await PrintIssuesAsync(inspection.Issues, error).ConfigureAwait(false);
            return CompositionFailed;
        }

        SavedRuleExecutionIdentity identity = inspection.Identity!;
        SavedRuleLifecycleSnapshot lifecycle = SavedRuleLifecycle.Import(identity);
        await output.WriteLineAsync(
            $"Rule: {identity.RuleId} {identity.RuleVersion}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Content SHA256: {identity.ContentHash}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Lifecycle: {lifecycle.State}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Trusted: {lifecycle.IsTrusted}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Parent: {identity.Parent.BundleId} / {identity.Parent.ProfileId} / {identity.Parent.FamilyId} / {identity.Parent.MapId}")
            .ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Mappings: {inspection.Mappings!.Rows.Count}").ConfigureAwait(false);
        if (action == "mappings")
        {
            await PrintMappingsAsync(inspection.Mappings, output).ConfigureAwait(false);
        }

        return Success;
    }
}
