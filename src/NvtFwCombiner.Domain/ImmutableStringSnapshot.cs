namespace NvtFwCombiner.Domain;

internal static class CanonicalSha256
{
    internal static string Require(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Length == 64 && value.All(static character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            ? value
            : throw new ArgumentException("Expected a lowercase 64-character SHA-256 hash.", parameterName);
    }
}

internal static class ImmutableStringSnapshot
{
    internal static string[] Create(
        IEnumerable<string> values,
        string parameterName,
        string? requiredMessage,
        string invalidValueMessage,
        string duplicateMessage)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        string[] snapshot = [.. values];
        if (requiredMessage is not null && snapshot.Length == 0)
        {
            throw new ArgumentException(requiredMessage, parameterName);
        }

        if (snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(invalidValueMessage, parameterName);
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException(duplicateMessage, parameterName);
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}
