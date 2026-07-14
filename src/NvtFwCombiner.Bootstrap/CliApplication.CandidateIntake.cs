using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Bootstrap;

public static partial class CliApplication
{
    private static async Task<int> RunCandidateIntakeAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args[0] is "--help" or "help")
        {
            await WriteCandidateIntakeUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        if (!StringComparer.Ordinal.Equals(args[0], "stage"))
        {
            await error.WriteLineAsync($"error: unknown candidate-intake command '{args[0]}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryParseOptions(
                args[1..],
                ["--request", "--source-root", "--output-dir"],
                [],
                error,
                out ParsedOptions options))
        {
            return UsageError;
        }

        if (!TryGetRequiredOption(options, "--request", error, out string requestPath) ||
            !TryGetRequiredOption(options, "--source-root", error, out string sourceRoot) ||
            !TryGetRequiredOption(options, "--output-dir", error, out string outputDirectory))
        {
            return UsageError;
        }

        CandidateEvidenceMaterializationResult result = CandidateEvidenceIntakeMaterializer.Materialize(
            new CandidateEvidenceMaterializationRequest(
                requestPath,
                sourceRoot,
                outputDirectory,
                DateTimeOffset.UtcNow));

        await output.WriteLineAsync("Candidate evidence staged.").ConfigureAwait(false);
        await output.WriteLineAsync($"Candidate root: {result.CandidateRootDirectory}").ConfigureAwait(false);
        await output.WriteLineAsync($"Validation report: {result.ValidationReportPath}").ConfigureAwait(false);
        await output.WriteLineAsync($"Root content hash: {result.RootContentHash}").ConfigureAwait(false);
        await output.WriteLineAsync("Runtime authority: none").ConfigureAwait(false);
        return Success;
    }

    private static bool TryGetRequiredOption(
        ParsedOptions options,
        string optionName,
        TextWriter error,
        out string value)
    {
        if (options.Values.TryGetValue(optionName, out value!))
        {
            return true;
        }

        error.WriteLine($"error: option '{optionName}' is required");
        value = string.Empty;
        return false;
    }
}
