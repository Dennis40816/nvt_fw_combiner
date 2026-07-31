using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private static bool TryParseOptions(
        string[] args,
        bool build,
        TextWriter error,
        out ParsedOptions options)
    {
        options = new ParsedOptions(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, List<string>>(StringComparer.Ordinal));
        string[] valueOptions = build
            ? ["--profile", "--size", "--fill", "--mapping", "--rule", "--slot", "--output", "--report"]
            : ["--profile", "--size", "--fill", "--mapping", "--rule", "--slot", "--report"];
        string[] repeatableOptions = ["--mapping", "--slot"];
        for (int index = 0; index < args.Length; index++)
        {
            string token = args[index];
            if (!valueOptions.Contains(token, StringComparer.Ordinal))
            {
                error.WriteLine($"error: unknown option '{token}'");
                return false;
            }

            if (index + 1 >= args.Length)
            {
                error.WriteLine($"error: option '{token}' expects a value");
                return false;
            }

            string value = args[++index];
            if (repeatableOptions.Contains(token, StringComparer.Ordinal))
            {
                if (!options.RepeatedValues.TryGetValue(token, out List<string>? values))
                {
                    values = [];
                    options.RepeatedValues[token] = values;
                }

                values.Add(value);
                continue;
            }

            options.Values[token] = value;
        }

        return true;
    }

    private static bool RequireOption(
        ParsedOptions options,
        string option,
        TextWriter error,
        [NotNullWhen(true)] out string? value)
    {
        if (options.Values.TryGetValue(option, out value) && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        error.WriteLine($"error: {option} is required");
        value = null;
        return false;
    }

    private sealed record ParsedOptions(
        Dictionary<string, string> Values,
        Dictionary<string, List<string>> RepeatedValues)
    {
        public List<string> GetValues(string option)
        {
            return RepeatedValues.TryGetValue(option, out List<string>? values)
                ? values
                : Values.TryGetValue(option, out string? value) ? [value] : [];
        }
    }
}
