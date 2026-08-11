using System.Text.RegularExpressions;

namespace NvtFwCombiner.Domain.Composition;

internal static partial class CanonicalProfileValueRules
{
    internal static string RequireSemanticVersion(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return SemanticVersionPattern().IsMatch(value)
            ? value
            : throw new ArgumentException("Value is not a canonical semantic version.", parameterName);
    }

    internal static string RequireExternalToolBindingId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return ExternalToolBindingIdPattern().IsMatch(value)
            ? value
            : throw new ArgumentException(
                "External tool binding identifier is not in the external-tool manifest form.",
                parameterName);
    }

    internal static string RequireInvocationProfileId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return CanonicalPolicyValueRules.IsCanonicalId(value) || LegacyCombinerInvocationProfileIdPattern().IsMatch(value)
            ? value
            : throw new ArgumentException(
                "Invocation profile identifier is not canonical or a published legacy Combiner catalog identifier.",
                parameterName);
    }

    internal static ByteRange RequireRange(ByteRange value, string parameterName)
    {
        try
        {
            return new ByteRange(value.Start, value.Length);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, exception.Message);
        }
    }

    [GeneratedRegex(
        "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:-[0-9]+(?:\\.[0-9]+)*(?:[-+][0-9A-Za-z.-]+)?)?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExternalToolBindingIdPattern();

    [GeneratedRegex(
        "^nfc\\.[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex LegacyCombinerInvocationProfileIdPattern();

    [GeneratedRegex(
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SemanticVersionPattern();
}
