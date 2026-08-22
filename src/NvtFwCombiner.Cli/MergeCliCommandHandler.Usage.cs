namespace NvtFwCombiner.Cli;

internal static partial class MergeCliCommandHandler
{
    private static Task WriteUsageAsync(TextWriter output)
    {
        return output.WriteLineAsync(
            "usage: nvt_fw_combiner general-merge preview --profile <ic> --size <length> [--fill <0x00..0xFF>] --mapping <source-start+target-start+length=path> [--mapping ...] [--report <path>]\n" +
            "       nvt_fw_combiner general-merge preview --profile <ic> --rule <v2-rule.json> --slot <slot-id=path> [--slot ...] [--report <path>]\n" +
            "       nvt_fw_combiner general-merge build --profile <ic> --size <length> [--fill <0x00..0xFF>] --mapping <source-start+target-start+length=path> [--mapping ...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]\n" +
            "       nvt_fw_combiner general-merge build --profile <ic> --rule <v2-rule.json> --slot <slot-id=path> [--slot ...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]");
    }
}
