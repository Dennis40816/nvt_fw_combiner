namespace NvtFwCombiner.Domain.Composition;

/// <summary>Profile-declared external processor binding and byte authority.</summary>
public sealed class ExternalProcessorInvocation
{
    private readonly ByteRange[] _allowedReadRanges;
    private readonly ByteRange[] _allowedWriteRanges;
    private readonly Dictionary<string, IReadOnlyList<string>> _parameters;

    /// <summary>Creates an external processor invocation declaration.</summary>
    public ExternalProcessorInvocation(
        string processorId,
        string toolBindingId,
        IEnumerable<ByteRange> allowedReadRanges,
        IEnumerable<ByteRange> allowedWriteRanges,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        ArgumentNullException.ThrowIfNull(allowedReadRanges);
        ArgumentNullException.ThrowIfNull(allowedWriteRanges);

        _allowedReadRanges = [.. allowedReadRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        _allowedWriteRanges = [.. allowedWriteRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        if (_allowedReadRanges.Length == 0)
        {
            throw new ArgumentException("External processor allowed read ranges must not be empty.", nameof(allowedReadRanges));
        }

        if (_allowedWriteRanges.Length == 0)
        {
            throw new ArgumentException("External processor allowed write ranges must not be empty.", nameof(allowedWriteRanges));
        }

        ProcessorId = processorId;
        ToolBindingId = toolBindingId;
        _parameters = CopyParameters(parameters);
    }

    /// <summary>Profile-selected processor id.</summary>
    public string ProcessorId { get; }

    /// <summary>Manifest binding id selected by the profile.</summary>
    public string ToolBindingId { get; }

    /// <summary>Byte ranges the processor may read from the staged target image.</summary>
    public IReadOnlyList<ByteRange> AllowedReadRanges => _allowedReadRanges;

    /// <summary>Byte ranges the processor may mutate in the staged target image.</summary>
    public IReadOnlyList<ByteRange> AllowedWriteRanges => _allowedWriteRanges;

    /// <summary>Profile-compiled processor parameters with no firmware semantics in the domain layer.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Parameters => _parameters;

    private static Dictionary<string, IReadOnlyList<string>> CopyParameters(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? parameters)
    {
        Dictionary<string, IReadOnlyList<string>> copy = new(StringComparer.Ordinal);
        if (parameters is null)
        {
            return copy;
        }

        foreach ((string key, IReadOnlyList<string> values) in parameters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(values);

            string[] valueCopy = [.. values];
            if (valueCopy.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("External processor parameter values must not be empty.", nameof(parameters));
            }

            copy.Add(key, valueCopy);
        }

        return copy;
    }
}
