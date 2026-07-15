using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static bool RequireOption(
        ParsedCliOptions options,
        string optionName,
        TextWriter error,
        [NotNullWhen(true)] out string? value)
    {
        if (options.Values.TryGetValue(optionName, out value))
        {
            return true;
        }

        error.WriteLine($"error: {optionName} is required");
        return false;
    }
}
