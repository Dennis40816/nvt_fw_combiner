namespace NvtFwCombiner.Domain;

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
