using System.Runtime.CompilerServices;

namespace NvtFwCombiner.Domain.Composition;

internal static class ImmutableReferenceSnapshot
{
    internal static T[] Create<T>(
        IEnumerable<T> values,
        string invalidMessage,
        bool requireValue = false,
        [CallerArgumentExpression(nameof(values))] string? parameterName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        T[] snapshot = [.. values];
        return (requireValue && snapshot.Length == 0) || snapshot.Any(static value => value is null)
            ? throw new ArgumentException(invalidMessage, parameterName)
            : snapshot;
    }

    internal static T[] CreateUnique<T, TKey>(
        IEnumerable<T> values,
        Func<T, TKey> keySelector,
        string invalidMessage,
        string duplicateMessage,
        IEqualityComparer<TKey>? comparer = null,
        bool requireValue = false,
        [CallerArgumentExpression(nameof(values))] string? parameterName = null)
        where T : class
    {
        T[] snapshot = Create(values, invalidMessage, requireValue, parameterName);
        return snapshot.Select(keySelector).Distinct(comparer).Count() != snapshot.Length
            ? throw new ArgumentException(duplicateMessage, parameterName)
            : snapshot;
    }
}
