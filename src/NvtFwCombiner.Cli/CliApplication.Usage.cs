namespace NvtFwCombiner.Cli;

public static partial class CliApplication
{
    private static async Task WriteUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner [--version|version|doctor]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner profiles list").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge preview --profile <id|ic> --dp <path> --tp <path> [--ldc <path>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge build --profile <id|ic> --dp <path> --tp <path> [--ldc <path>] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge preview --profile <id|ic> --dp-ab <path> --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge build --profile <id|ic> --dp-ab <path> --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>] [--include-a-flashcode]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-merge preview --profile <ic> --size <length> [--fill <0x00..0xFF>] --mapping <source-start+target-start+length=path> [--mapping ...] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-merge build --profile <ic> --size <length> [--fill <0x00..0xFF>] --mapping <source-start+target-start+length=path> [--mapping ...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-merge preview --profile <ic> --rule <v2-rule.json> --slot <slot-id=path> [--slot ...] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-merge build --profile <ic> --rule <v2-rule.json> --slot <slot-id=path> [--slot ...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner saved-rule validate <rule.json>").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner saved-rule mappings <rule.json>").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace preview --profile <ic> --ic-num <value> --base <path> --ctrlram <slot-id=path> [--ctrlram <slot-id=path> ...] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace build --profile <ic> --ic-num <value> --base <path> --ctrlram <slot-id=path> [--ctrlram <slot-id=path> ...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-replace preview --profile <ic> --ic-num <value> --base <path> (--mapping <target-start+length=path> | --patch <target-start+length=hex> | --fill <target-start+length=byte>) [...] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-replace build --profile <ic> --ic-num <value> --base <path> (--mapping <target-start+length=path> | --patch <target-start+length=hex> | --fill <target-start+length=byte>) [...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
    }

    private static async Task WriteProfilesUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage: nvt_fw_combiner profiles list").ConfigureAwait(false);
    }

    private static async Task WriteStandardMergeUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge preview --profile <id|ic> --dp <path> --tp <path> [--ldc <path>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge build --profile <id|ic> --dp <path> --tp <path> [--ldc <path>] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-replace preview --profile <ic> --ic-num <value> --base <path> (--mapping <target-start+length=path> | --patch <target-start+length=hex> | --fill <target-start+length=byte>) [...] [--report <path>]").ConfigureAwait(false);
    }

}
