using System.Globalization;
using NvtFwCombiner.Profiles;

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
        foreach (CompositionProfileDefinition profile in BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
                     .OrderBy(profile => profile.IcId, StringComparer.Ordinal))
        {
            ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
            string inputs = compile.IsSuccess
                ? string.Join(", ", compile.CompiledComposition!.Plan.RequiredInputAddressSpaceIds)
                : "compile-error";
            await output.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{profile.ProfileId}  ic={profile.IcId}  inputs={inputs}  default-output={profile.DefaultOutputFileName}"))
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("Built-in replace profiles:").ConfigureAwait(false);
        foreach (CompositionProfileDefinition profile in BuiltInReplaceProfiles.All
                     .OrderBy(profile => profile.ProfileId, StringComparer.Ordinal))
        {
            ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
            string inputs = compile.IsSuccess
                ? string.Join(", ", compile.CompiledComposition!.Plan.RequiredInputAddressSpaceIds)
                : "compile-error";
            await output.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{profile.ProfileId}  ic={profile.IcId}  inputs={inputs}  ic-num={profile.IcNumberInputMode?.ToString() ?? "none"}  default-output={profile.DefaultOutputFileName}"))
                .ConfigureAwait(false);
        }

        return Success;
    }
}
