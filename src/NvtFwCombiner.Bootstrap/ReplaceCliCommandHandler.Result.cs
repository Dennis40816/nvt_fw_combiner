namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> UnknownReplaceProfileAsync(
        string command,
        string profileSelector,
        TextWriter error)
    {
        await error.WriteLineAsync($"error: unknown {command} profile '{profileSelector}'").ConfigureAwait(false);
        return UsageError;
    }
}
