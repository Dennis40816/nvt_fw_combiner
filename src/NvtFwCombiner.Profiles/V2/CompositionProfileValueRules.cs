using System.Text.RegularExpressions;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileValueRules
{
    internal static string RequireToolBindingIdForSchemaVersion(
        string schemaVersion,
        string value,
        string parameterName)
    {
        return schemaVersion switch
        {
            "2.0" or "2.1" => CanonicalPolicyValueRules.RequireCanonicalId(value, parameterName),
            "2.2" or "2.3" or "2.4" or "2.5" or "2.6" or "2.7" or "2.8" or "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15" => CanonicalProfileValueRules.RequireExternalToolBindingId(value, parameterName),
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Unsupported profile schema version."),
        };
    }

    internal static string RequireLegacyCombinerInvocationProfileIdForSchemaVersion(
        string schemaVersion,
        string value,
        string parameterName)
    {
        return schemaVersion is "2.7" or "2.8" or "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15"
            ? CanonicalProfileValueRules.RequireInvocationProfileId(value, parameterName)
            : CanonicalPolicyValueRules.RequireCanonicalId(value, parameterName);
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

    [GeneratedRegex("^NT[0-9A-Z-]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex IcIdPattern();
}
