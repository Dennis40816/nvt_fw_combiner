using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task WriteUsageAsync(string command, TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        switch (command)
        {
            case IcWorkflowIds.DpReplace:
                await output.WriteLineAsync("  nvt_fw_combiner dp-replace preview --profile <id|ic> --ic-num <value> --base <path> --dp <path> [--ld <path>] [--output <path>] [--report <path>]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner dp-replace build --profile <id|ic> --ic-num <value> --base <path> --dp <path> [--ld <path>] [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
                break;
            case IcWorkflowIds.CtrlRamReplace:
                await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace preview --profile <ic> --ic-num <value> --base <path> --ctrlram <slot-id=path> [--ctrlram <slot-id=path> ...] [--report <path>]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace build --profile <ic> --ic-num <value> --base <path> --ctrlram <slot-id=path> [--ctrlram <slot-id=path> ...] [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace preview --profile synthetic-ctrlram-replace --ic-family <value> --ic-num <value> --base <path> --ctrlram <path> [--report <path>]").ConfigureAwait(false);
                break;
            case IcWorkflowIds.GeneralReplace:
                await output.WriteLineAsync("  nvt_fw_combiner general-replace preview --profile <id|ic> --ic-num <value> --base <path> --input <path> --source-start <n> --target-start <n> --length <n> [--output <path>] [--report <path>]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner general-replace build --profile <id|ic> --ic-num <value> --base <path> --input <path> --source-start <n> --target-start <n> --length <n> [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner general-replace preview --profile <ic> --ic-num <value> --base <path> --mapping <target-start+length=path> [--mapping <target-start+length=path> ...] [--report <path>]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner general-replace build --profile <ic> --ic-num <value> --base <path> --mapping <target-start+length=path> [--mapping <target-start+length=path> ...] [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
                break;
            default:
                await output.WriteLineAsync("  nvt_fw_combiner <dp-replace|ctrlram-replace|general-replace> <preview|build> [options]").ConfigureAwait(false);
                break;
        }
    }
}
