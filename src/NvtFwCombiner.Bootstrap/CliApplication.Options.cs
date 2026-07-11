using System.Globalization;

namespace NvtFwCombiner.Bootstrap;

public static partial class CliApplication
{
    private static bool TryParseOptions(
        string[] args,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string> flagOptions,
        TextWriter error,
        out ParsedOptions parsed)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
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
            if (!values.TryAdd(name, value))
            {
                error.WriteLine($"error: duplicate option '{name}'");
                parsed = ParsedOptions.Empty;
                return false;
            }
        }

        parsed = new ParsedOptions(values, flags);
        return true;
    }

    private static string CreateRunId(string action)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"cli-{action}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
    }

    private sealed record ParsedOptions(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlySet<string> Flags)
    {
        internal static ParsedOptions Empty { get; } = new(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
    }
}
