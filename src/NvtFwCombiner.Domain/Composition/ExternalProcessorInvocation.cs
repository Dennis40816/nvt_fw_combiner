namespace NvtFwCombiner.Domain.Composition;

/// <summary>Profile-declared external processor binding and byte authority.</summary>
public sealed class ExternalProcessorInvocation
{
    private readonly ByteRange[] _allowedReadRanges;
    private readonly ByteRange[] _allowedWriteRanges;
    private readonly ExternalProcessorWriteRangeSection[] _allowedWriteRangeSections;
    private readonly ExternalProcessorStagedSourceBinding[] _stagedSourceBindings;

    /// <summary>Creates an external processor invocation declaration.</summary>
    public ExternalProcessorInvocation(
        string processorId,
        string toolBindingId,
        IEnumerable<ByteRange> allowedReadRanges,
        IEnumerable<ByteRange> allowedWriteRanges,
        IEnumerable<ExternalProcessorStagedSourceBinding>? stagedSourceBindings = null,
        IEnumerable<ExternalProcessorWriteRangeSection>? allowedWriteRangeSections = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        ArgumentNullException.ThrowIfNull(allowedReadRanges);
        ArgumentNullException.ThrowIfNull(allowedWriteRanges);

        _allowedReadRanges = [.. allowedReadRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        _allowedWriteRanges = [.. allowedWriteRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        _allowedWriteRangeSections = [
            .. (allowedWriteRangeSections ?? [])
                .OrderBy(section => section.Range.Start)
                .ThenBy(section => section.Range.Length)
                .ThenBy(section => section.SectionId, StringComparer.Ordinal),
        ];
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

        foreach (ExternalProcessorWriteRangeSection section in _allowedWriteRangeSections)
        {
            if (!_allowedWriteRanges.Any(range => range.Contains(section.Range)))
            {
                throw new ArgumentException(
                    $"External processor write section '{section.SectionId}' must stay inside an allowed write range.",
                    nameof(allowedWriteRangeSections));
            }
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

    /// <summary>Profile-owned section identifiers for report and diagnostics over allowed write ranges.</summary>
    public IReadOnlyList<ExternalProcessorWriteRangeSection> AllowedWriteRangeSections => _allowedWriteRangeSections;

    /// <summary>Additional source bytes the processor may stage without the host first writing them into the target image.</summary>
    public IReadOnlyList<ExternalProcessorStagedSourceBinding> StagedSourceBindings => _stagedSourceBindings;
}

/// <summary>Diagnostic section attached to a declared external-processor write range.</summary>
public sealed class ExternalProcessorWriteRangeSection
{
    /// <summary>Creates a write-range section annotation owned by the profile or adapter.</summary>
    public ExternalProcessorWriteRangeSection(string sectionId, ByteRange range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);

        SectionId = sectionId.Trim();
        Range = range;
    }

    /// <summary>Stable section identifier used by reports.</summary>
    public string SectionId { get; }

    /// <summary>Half-open processor write range covered by this section.</summary>
    public ByteRange Range { get; }
}
