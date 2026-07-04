namespace NvtFwCombiner.Domain.Composition;

/// <summary>Profile-declared external processor binding and byte authority.</summary>
public sealed class ExternalProcessorInvocation
{
    private readonly ByteRange[] _allowedReadRanges;
    private readonly ByteRange[] _allowedWriteRanges;
    private readonly ExternalProcessorStagedSourceBinding[] _stagedSourceBindings;

    /// <summary>Creates an external processor invocation declaration.</summary>
    public ExternalProcessorInvocation(
        string processorId,
        string toolBindingId,
        IEnumerable<ByteRange> allowedReadRanges,
        IEnumerable<ByteRange> allowedWriteRanges,
        IEnumerable<ExternalProcessorStagedSourceBinding>? stagedSourceBindings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        ArgumentNullException.ThrowIfNull(allowedReadRanges);
        ArgumentNullException.ThrowIfNull(allowedWriteRanges);

        _allowedReadRanges = [.. allowedReadRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        _allowedWriteRanges = [.. allowedWriteRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        _stagedSourceBindings = [
            .. (stagedSourceBindings ?? [])
                .OrderBy(binding => binding.FirmwareRange.Start)
                .ThenBy(binding => binding.FirmwareRange.Length)
                .ThenBy(binding => binding.SourceSpaceId, StringComparer.Ordinal),
        ];
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
    }

    /// <summary>Profile-selected processor id.</summary>
    public string ProcessorId { get; }

    /// <summary>Manifest binding id selected by the profile.</summary>
    public string ToolBindingId { get; }

    /// <summary>Byte ranges the processor may read from the staged target image.</summary>
    public IReadOnlyList<ByteRange> AllowedReadRanges => _allowedReadRanges;

    /// <summary>Byte ranges the processor may mutate in the staged target image.</summary>
    public IReadOnlyList<ByteRange> AllowedWriteRanges => _allowedWriteRanges;

    /// <summary>Additional source bytes the processor may stage without the host first writing them into the target image.</summary>
    public IReadOnlyList<ExternalProcessorStagedSourceBinding> StagedSourceBindings => _stagedSourceBindings;
}
