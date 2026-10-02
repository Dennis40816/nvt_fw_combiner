namespace NvtFwCombiner.Cli;

public static partial class CliApplication
{
    private static async Task WriteUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner [--version|version|doctor]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner version-self-test [--registry <https-uri-or-absolute-path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner profiles list").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner workflows list [--workflow <id> [--profile <ic> [--ic-num <token>]]]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge preview --profile <id|ic> --dp <path> --tp <path> [--ldc <path>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge build --profile <id|ic> --dp <path> --tp <path> [--ldc <path>] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge preview --profile <id|ic> (--dp-ab <path> [--dp-mode normal] | --dp-mode dummy --acknowledge-non-tp-ff) --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge build --profile <id|ic> (--dp-ab <path> [--dp-mode normal] | --dp-mode dummy --acknowledge-non-tp-ff) --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>] [--include-a-flashcode]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-merge preview --profile <ic> --size <length> [--fill <0x00..0xFF>] --mapping <source-start+target-start+length=path> [--mapping ...] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-merge build --profile <ic> --size <length> [--fill <0x00..0xFF>] --mapping <source-start+target-start+length=path> [--mapping ...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-merge preview --profile <ic> --rule <v2-rule.json> --slot <slot-id=path> [--slot ...] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-merge build --profile <ic> --rule <v2-rule.json> --slot <slot-id=path> [--slot ...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner saved-rule validate <rule.json>").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner saved-rule mappings <rule.json>").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace preview --profile <ic> --ic-num <value> --base <path> --ctrlram <slot-id=path> [--ctrlram <slot-id=path> ...] [--bank <a|b|both>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace build --profile <ic> --ic-num <value> --base <path> --ctrlram <slot-id=path> [--ctrlram <slot-id=path> ...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--bank <a|b|both>] [firmware-version options] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-replace preview --profile <ic> --ic-num <value> --base <path> (--mapping <target-start+length=path> | --patch <target-start+length=hex> | --fill <target-start+length=byte>) [...] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-replace build --profile <ic> --ic-num <value> --base <path> (--mapping <target-start+length=path> | --patch <target-start+length=hex> | --fill <target-start+length=byte>) [...] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>]] [--report <path>]").ConfigureAwait(false);
        await WriteAbDummyOptionsUsageAsync(output).ConfigureAwait(false);
        await WriteCtrlRamChoiceOptionsUsageAsync(output).ConfigureAwait(false);
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

    internal static async Task WriteAbDummyOptionsUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("  AB --dp-mode defaults to normal. Dummy omits DP and requires --acknowledge-non-tp-ff: 0xFF replaces every non-TP output; both TP inputs remain required.").ConfigureAwait(false);
    }

    internal static async Task WriteCtrlRamChoiceOptionsUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("  CtrlRAM --bank a|b|both selects AB Reference banks; omitted preserves the Application default.").ConfigureAwait(false);
        await output.WriteLineAsync("  Standard Base version: --firmware-version <HH> --firmware-sub-version <HH>").ConfigureAwait(false);
        await output.WriteLineAsync("  AB A bank version: --a-firmware-version <HH> --a-firmware-sub-version <HH>").ConfigureAwait(false);
        await output.WriteLineAsync("  AB B bank version: --b-firmware-version <HH> --b-firmware-sub-version <HH>").ConfigureAwait(false);
        await output.WriteLineAsync("  Version options are available only for ctrlram-replace build. Supply each version/sub-version pair together, using two hexadecimal digits (00..FF); omitted pairs preserve versions. Only selected AB banks may be edited.").ConfigureAwait(false);
        await output.WriteLineAsync("  workflows list prints catalog-declared CLI workflows, profile=<ic>, ab-topology=<token> for AB Merge, ic-num=<token> for CtrlRAM Replace and CtrlRAM slot ids in stable order. Base inspection during a run remains authoritative.").ConfigureAwait(false);
    }

}
