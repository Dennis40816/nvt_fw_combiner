using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NvtFwCombiner.Domain;

internal static class ClosedEnum
{
    internal static bool IsDefined<T>(T value)
        where T : struct, Enum
    {
        return Enum.IsDefined(value);
    }

    internal static void ThrowIfUndefined<T>(
        T value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        where T : struct, Enum
    {
        if (!IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, message);
        }
    }

    internal static T Require<T>(
        T value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        where T : struct, Enum
    {
        ThrowIfUndefined(value, message, parameterName);
        return value;
    }
}

internal static class RequiredValue
{
    internal static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return value;
    }

    internal static string NotBlank(
        string value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    internal static long Positive(
        long value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, parameterName);
        return value;
    }
}

internal static class DomainInvariant
{
    internal static void Require(
        [DoesNotReturnIf(false)] bool condition,
        string message,
        string? parameterName = null)
    {
        _ = condition
            ? true
            : throw new ArgumentException(message, parameterName);
    }

    internal static void Reject(
        [DoesNotReturnIf(true)] bool condition,
        string message,
        string? parameterName = null)
    {
        Require(!condition, message, parameterName);
    }

    internal static void Reject(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))]
        ref DomainInvariantMessageHandler message,
        string? parameterName = null)
    {
        if (condition)
        {
            throw new ArgumentException(message.GetFormattedText(), parameterName);
        }
    }
}

[InterpolatedStringHandler]
internal ref struct DomainInvariantMessageHandler
{
    private DefaultInterpolatedStringHandler _builder;

    internal DomainInvariantMessageHandler(
        int literalLength,
        int formattedCount,
        bool condition,
        out bool shouldAppend)
    {
        shouldAppend = condition;
        _builder = condition
            ? new DefaultInterpolatedStringHandler(literalLength, formattedCount)
            : default;
    }

    public void AppendLiteral(string value)
    {
        _builder.AppendLiteral(value);
    }

    public void AppendFormatted<T>(T value)
    {
        _builder.AppendFormatted(value);
    }

    public void AppendFormatted<T>(T value, string? format)
    {
        _builder.AppendFormatted(value, format);
    }

    internal string GetFormattedText()
    {
        return _builder.ToStringAndClear();
    }
}

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
