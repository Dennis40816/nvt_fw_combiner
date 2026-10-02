namespace NvtFwCombiner.Cli;

internal static partial class AbMergeCliCommandHandler
{
    private static async Task WriteUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge preview --profile <id|ic> (--dp-ab <path> [--dp-mode normal] | --dp-mode dummy --acknowledge-non-tp-ff) --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge build --profile <id|ic> (--dp-ab <path> [--dp-mode normal] | --dp-mode dummy --acknowledge-non-tp-ff) --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>] [--include-a-flashcode]] [--report <path>]").ConfigureAwait(false);
        await CliApplication.WriteAbDummyOptionsUsageAsync(output).ConfigureAwait(false);
    }
}
