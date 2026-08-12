namespace NvtFwCombiner.Application.Composition;

internal static class CompositionSummaryValue
{
    internal static string NotBlank(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    internal static long NonNegative(long value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
        return value;
    }

    internal static T NotNull<T>(T? value, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return value;
    }

    internal static IReadOnlyList<T> Snapshot<T>(
        IReadOnlyList<T>? values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return [.. values];
    }

    internal static IReadOnlyList<T> ReadOnlySnapshot<T>(
        IReadOnlyList<T>? values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly([.. values]);
    }
}
