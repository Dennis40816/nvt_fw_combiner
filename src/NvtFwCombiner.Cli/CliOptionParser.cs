namespace NvtFwCombiner.Cli;

internal static class CliOptionParser
{
    internal static bool TryParse(
        string[] args,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string> repeatableValueOptions,
        IReadOnlyCollection<string> flagOptions,
        TextWriter error,
        out ParsedCliOptions parsed)
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
                    return Fail($"error: duplicate option '{name}'", error, out parsed);
                }

                continue;
            }

            if (!valueOptions.Contains(name))
            {
                return Fail($"error: unknown option '{name}'", error, out parsed);
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return Fail($"error: option '{name}' requires a value", error, out parsed);
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
                return Fail($"error: duplicate option '{name}'", error, out parsed);
            }
        }

        parsed = new ParsedCliOptions(values, multiValues, flags);
        return true;
    }

    private static bool Fail(string message, TextWriter error, out ParsedCliOptions parsed)
    {
        error.WriteLine(message);
        parsed = default!;
        return false;
    }
}

internal sealed record ParsedCliOptions(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, List<string>> MultiValues,
    IReadOnlySet<string> Flags)
{
    internal List<string> GetValues(string optionName)
    {
        return MultiValues.TryGetValue(optionName, out List<string>? values)
            ? values
            : Values.TryGetValue(optionName, out string? value)
                ? [value]
                : [];
    }
}
