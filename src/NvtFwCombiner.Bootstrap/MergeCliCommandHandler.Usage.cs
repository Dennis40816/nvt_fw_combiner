namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private static Task WriteUsageAsync(TextWriter output)
    {
        return output.WriteLineAsync(
            "usage: nvt_fw_combiner general-merge preview --profile <ic> --size <length> --mapping <source-start+target-start+length=path> [--mapping ...] [--report <path>]\n" +
            "       nvt_fw_combiner general-merge preview --profile <ic> --size <length> --rule <rule.json> --slot <slot-id=path> [--slot ...] [--report <path>]\n" +
            "       nvt_fw_combiner general-merge build --profile <ic> --size <length> --mapping <source-start+target-start+length=path> [--mapping ...] [--output <path>] [--report <path>]\n" +
            "       nvt_fw_combiner general-merge build --profile <ic> --size <length> --rule <rule.json> --slot <slot-id=path> [--slot ...] [--output <path>] [--report <path>]");
    }
}
