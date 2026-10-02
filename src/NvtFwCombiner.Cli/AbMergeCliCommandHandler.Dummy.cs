using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Cli;

internal static partial class AbMergeCliCommandHandler
{
    private const string DpModeOption = "--dp-mode";
    private const string DummyAcknowledgementOption = "--acknowledge-non-tp-ff";

    private static async Task<(int ExitCode, IReadOnlyDictionary<string, string> SlotPaths)> CreateDummySlotPathsAsync(
        IAbMergeAuthoring authoring, string icId, ParsedCliOptions options, TextWriter error)
    {
        CompiledAuthoringSelectionSnapshot selection = authoring.GetAuthoringSnapshot(
            icId, options.Values.GetValueOrDefault("--ab-topology"), [],
            new Dictionary<string, FileStamp>(StringComparer.Ordinal), new AuthoringRevision(1),
            dpMode: AbMergeDpMode.Dummy);
        if (selection.Issues.Count != 0)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, selection.Issues).ConfigureAwait(false);
            return (SoftwareError, new Dictionary<string, string>());
        }

        bool succeeded = TryCreateSlotPaths([.. selection.InputBindings.Select(static binding => binding.AddressSpaceId)],
            options, error, out IReadOnlyDictionary<string, string> slotPaths);
        return (succeeded ? Success : UsageError, slotPaths);
    }

    private static int ParseDpMode(ParsedCliOptions options, TextWriter error, out AbMergeDpMode mode)
    {
        string token = options.Values.GetValueOrDefault(DpModeOption, "normal").Trim();
        mode = AbMergeDpMode.Normal;
        if (StringComparer.OrdinalIgnoreCase.Equals(token, "dummy"))
        {
            mode = AbMergeDpMode.Dummy;
        }
        else if (!StringComparer.OrdinalIgnoreCase.Equals(token, "normal"))
        {
            error.WriteLine($"error: {DpModeOption} must be normal or dummy");
            return UsageError;
        }

        if (mode == AbMergeDpMode.Dummy && options.Values.ContainsKey("--dp-ab"))
        {
            error.WriteLine("error: --dp-ab is not used with --dp-mode dummy");
            return UsageError;
        }

        bool acknowledged = options.Flags.Contains(DummyAcknowledgementOption);
        if (mode == AbMergeDpMode.Normal && acknowledged)
        {
            error.WriteLine($"error: {DummyAcknowledgementOption} requires {DpModeOption} dummy");
            return UsageError;
        }

        if (mode == AbMergeDpMode.Dummy && !acknowledged)
        {
            error.WriteLine($"error: {DummyAcknowledgementOption} is required to confirm that 0xFF replaces every non-TP output");
            return UsageError;
        }

        return Success;
    }
}
