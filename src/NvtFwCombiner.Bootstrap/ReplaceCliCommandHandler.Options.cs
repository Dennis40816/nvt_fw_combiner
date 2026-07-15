using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryParseOptions(
        string[] args,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string> repeatableValueOptions,
        IReadOnlyCollection<string> flagOptions,
        TextWriter error,
        out ParsedOptions parsed)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        Dictionary<string, List<string>> multiValues = new(StringComparer.Ordinal);
        HashSet<string> flags = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string name = args[index];
            if (flagOptions.Contains(name))
            {
                if (!flags.Add(name))
                {
                    error.WriteLine($"error: duplicate option '{name}'");
                    parsed = ParsedOptions.Empty;
                    return false;
                }

                continue;
            }

            if (!valueOptions.Contains(name))
            {
                error.WriteLine($"error: unknown option '{name}'");
                parsed = ParsedOptions.Empty;
                return false;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error.WriteLine($"error: option '{name}' requires a value");
                parsed = ParsedOptions.Empty;
                return false;
            }

            string value = args[++index];
            if (repeatableValueOptions.Contains(name))
            {
                if (!multiValues.TryGetValue(name, out List<string>? items))
                {
                    items = [];
                    multiValues.Add(name, items);
                }

                items.Add(value);
                _ = values.TryAdd(name, value);
                continue;
            }

            if (!values.TryAdd(name, value))
            {
                error.WriteLine($"error: duplicate option '{name}'");
                parsed = ParsedOptions.Empty;
                return false;
            }
        }

        parsed = new ParsedOptions(values, multiValues, flags);
        return true;
    }

    private static bool RequireOption(
        ParsedOptions options,
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

    private sealed record ParsedOptions(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlyDictionary<string, List<string>> MultiValues,
        IReadOnlySet<string> Flags)
    {
        internal static ParsedOptions Empty { get; } = new(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, List<string>>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

        internal List<string> GetValues(string optionName)
        {
            return MultiValues.TryGetValue(optionName, out List<string>? values)
                ? values
                : Values.TryGetValue(optionName, out string? value)
                    ? [value]
                    : [];
        }
    }
}
