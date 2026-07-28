using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CliApplication
{
    private static async Task<int> RunProfilesAsync(
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Length > 1 || args is ["--help"])
        {
            await WriteProfilesUsageAsync(output).ConfigureAwait(false);
            return args is ["--help"] ? Success : UsageError;
        }

        if (args.Length == 1 && args[0] != "list")
        {
            await error.WriteLineAsync($"error: unknown profiles command '{args[0]}'").ConfigureAwait(false);
            return UsageError;
        }

        await output.WriteLineAsync("Built-in standard merge profiles:").ConfigureAwait(false);
        foreach (WorkbenchProfileSummary profile in WorkbenchCompositionService.GetStandardMergeProfileSummaries())
        {
            string inputs = profile.CompileSucceeded
                ? string.Join(
                    ", ",
                    WorkbenchCompositionService.GetStandardMergeInputSlots(profile.IcId)
                        .Select(static input => input.Required
                            ? input.AddressSpaceId
                            : $"{input.AddressSpaceId} (optional)"))
                : "compile-error";
            string issues = FormatProfileIssues(profile);
            await output.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{profile.ProfileId}  ic={profile.IcId}  inputs={inputs}  default-output={profile.DefaultOutputFileName}{issues}"))
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("Built-in AB Merge profiles:").ConfigureAwait(false);
        foreach (WorkbenchProfileSummary profile in WorkbenchCompositionService.GetAbMergeProfileSummaries())
        {
            string inputs = profile.CompileSucceeded
                ? string.Join(", ", profile.RequiredInputAddressSpaceIds)
                : "compile-error";
            string issues = FormatProfileIssues(profile);
            await output.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{profile.ProfileId}  ic={profile.IcId}  inputs={inputs}  default-output={profile.DefaultOutputFileName}{issues}"))
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("Built-in replace profiles:").ConfigureAwait(false);
        foreach (WorkbenchProfileSummary profile in WorkbenchCompositionService.GetReplaceProfileSummaries())
        {
            string inputs = profile.CompileSucceeded
                ? FormatReplaceProfileInputs(profile)
                : "compile-error";
            string icNumberPolicy = FormatIcNumberPolicy(profile);
            string issues = FormatProfileIssues(profile);
            await output.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{profile.ProfileId}  ic={profile.IcId}  inputs={inputs}  ic-num={icNumberPolicy}  default-output={profile.DefaultOutputFileName}{issues}"))
                .ConfigureAwait(false);
        }

        return Success;
    }

    private static string FormatReplaceProfileInputs(WorkbenchProfileSummary profile)
    {
        var authoringSlots =
            WorkbenchCompositionService.GetReplaceInputSlots(
                    profile.IcId,
                    WorkbenchIcNumberTokens.SingleChip,
                    WorkbenchReplaceModes.Dp)
                .ToDictionary(static slot => slot.AddressSpaceId, StringComparer.Ordinal);
        return string.Join(
            ", ",
            profile.RequiredInputAddressSpaceIds.Select(addressSpaceId =>
                authoringSlots.TryGetValue(addressSpaceId, out WorkbenchReplaceInputSlot? slot) &&
                slot.IsOptional
                    ? $"{addressSpaceId} (optional)"
                    : addressSpaceId));
    }

    private static string FormatIcNumberPolicy(WorkbenchProfileSummary profile)
    {
        return profile.IcNumberPolicy switch
        {
            CompiledIcNumberPolicy.NotApplicable => "none",
            CompiledIcNumberPolicy.SingleSelector => nameof(CompiledIcNumberPolicy.SingleSelector),
            CompiledIcNumberPolicy.CascadeSelector => nameof(CompiledIcNumberPolicy.CascadeSelector),
            CompiledIcNumberPolicy.NumericSelector => nameof(CompiledIcNumberPolicy.NumericSelector),
            null => "compile-error",
            _ => throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile.IcNumberPolicy,
                "Unknown compiled IC-number policy."),
        };
    }

    private static string FormatProfileIssues(WorkbenchProfileSummary profile)
    {
        return profile.CompileSucceeded
            ? string.Empty
            : $"  issues={string.Join(',', profile.IssueCodes)}";
    }
}
