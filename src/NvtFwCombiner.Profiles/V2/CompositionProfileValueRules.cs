using System.Text.RegularExpressions;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileValueRules
{
    internal static string RequireExternalToolBindingId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return !ExternalToolBindingIdPattern().IsMatch(value)
            ? throw new ArgumentException("External tool binding identifier is not in the external-tool manifest form.", parameterName)
            : value;
    }

    internal static string RequireToolBindingIdForSchemaVersion(
        string schemaVersion,
        string value,
        string parameterName)
    {
        return schemaVersion switch
        {
            "2.0" or "2.1" => CanonicalPolicyValueRules.RequireCanonicalId(value, parameterName),
            "2.2" or "2.3" or "2.4" or "2.5" or "2.6" or "2.7" or "2.8" or "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15" => RequireExternalToolBindingId(value, parameterName),
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Unsupported profile schema version."),
        };
    }

    internal static string RequireLegacyCombinerInvocationProfileIdForSchemaVersion(
        string schemaVersion,
        string value,
        string parameterName)
    {
        if (schemaVersion is not "2.7" and not "2.8" and not "2.9" and not "2.10" and not "2.11" and not "2.12" and not "2.13" and not "2.14" and not "2.15")
        {
            return CanonicalPolicyValueRules.RequireCanonicalId(value, parameterName);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return CanonicalPolicyValueRules.IsCanonicalId(value) || LegacyCombinerInvocationProfileIdPattern().IsMatch(value)
            ? value
            : throw new ArgumentException(
                "Invocation profile identifier is not canonical or a published legacy Combiner catalog identifier.",
                parameterName);
    }

    internal static string RequireSemanticVersion(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return !SemanticVersionPattern().IsMatch(value)
            ? throw new ArgumentException("Value is not a canonical semantic version.", parameterName)
            : value;
    }

    private static string[] SnapshotIds(
        IEnumerable<string> values,
        string parameterName,
        bool requireValue,
        Func<string, string, string> requireId)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        string[] snapshot = [.. values];
        if (requireValue && snapshot.Length == 0)
        {
            throw new ArgumentException("At least one identifier is required.", parameterName);
        }

        for (int index = 0; index < snapshot.Length; index++)
        {
            _ = requireId(snapshot[index], parameterName);
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Identifiers must be ordinally unique.", parameterName);
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }

    internal static string[] SnapshotLogicalMemberIds(
        string schemaVersion,
        IEnumerable<string> values,
        string parameterName)
    {
        return schemaVersion switch
        {
            "2.4" => CanonicalPolicyValueRules.SnapshotCanonicalIds(values, parameterName, requireValue: true),
            "2.5" or "2.6" or "2.7" or "2.8" or "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15" => SnapshotIds(values, parameterName, requireValue: true, RequireIcId),
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Unsupported logical-output schema version."),
        };
    }

    private static string RequireIcId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return !IcIdPattern().IsMatch(value)
            ? throw new ArgumentException("Identifier is not a canonical IC ID.", parameterName)
            : value;
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

    [GeneratedRegex("^NT[0-9A-Z-]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex IcIdPattern();

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
