using System.Text.RegularExpressions;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileValueRules
{
    internal static string RequireId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return !CanonicalIdPattern().IsMatch(value)
            ? throw new ArgumentException("Identifier is not in canonical lowercase form.", parameterName)
            : value;
    }

    internal static string RequireSemanticVersion(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return !SemanticVersionPattern().IsMatch(value)
            ? throw new ArgumentException("Value is not a canonical semantic version.", parameterName)
            : value;
    }

    internal static string RequireIssueCode(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return !IssueCodePattern().IsMatch(value)
            ? throw new ArgumentException("Value is not a canonical issue code.", parameterName)
            : value;
    }

    internal static string[] SnapshotIds(
        IEnumerable<string> values,
        string parameterName,
        bool requireValue)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        string[] snapshot = [.. values];
        if (requireValue && snapshot.Length == 0)
        {
            throw new ArgumentException("At least one identifier is required.", parameterName);
        }

        for (int index = 0; index < snapshot.Length; index++)
        {
            _ = RequireId(snapshot[index], parameterName);
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Identifiers must be ordinally unique.", parameterName);
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }

    internal static bool IsLowercaseSha256(string value)
    {
        return value.Length == 64 && value.All(static character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    internal static ByteRange RequireRange(ByteRange value, string parameterName)
    {
        try
        {
            return new ByteRange(value.Start, value.Length);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, exception.Message);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, exception.Message);
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CanonicalIdPattern();

    [GeneratedRegex(
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SemanticVersionPattern();

    [GeneratedRegex("^[A-Z][A-Z0-9_]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex IssueCodePattern();
}
