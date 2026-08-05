using System.Globalization;
using NvtFwCombiner.Application.Capabilities;
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
        foreach (CapabilityProfileSummary profile in
            CanonicalCapabilityProjection.GetStandardMergeProfileSummaries())
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

        await output.WriteLineAsync("Built-in AB Merge profiles:").ConfigureAwait(false);
        foreach (CapabilityProfileSummary profile in
            CanonicalCapabilityProjection.GetAbMergeProfileSummaries())
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
        foreach (CapabilityProfileSummary profile in
            CanonicalCapabilityProjection.GetDpReplaceProfileSummaries())
        {
            string inputs = profile.CompileSucceeded
                ? string.Join(", ", profile.RequiredInputAddressSpaceIds)
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

    private static string FormatIcNumberPolicy(CapabilityProfileSummary profile)
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

    private static string FormatProfileIssues(CapabilityProfileSummary profile)
    {
        return profile.CompileSucceeded
            ? string.Empty
            : $"  issues={string.Join(',', profile.IssueCodes)}";
    }
}
