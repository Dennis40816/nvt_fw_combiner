namespace NvtFwCombiner.Domain.Composition;

/// <summary>Profile-declared external processor binding and byte authority.</summary>
public sealed class ExternalProcessorInvocation
{
    private readonly ByteRange[] _allowedReadRanges;
    private readonly ByteRange[] _allowedWriteRanges;
    private readonly ExternalProcessorWriteRangeSection[] _allowedWriteRangeSections;
    private readonly ExternalProcessorStagedSourceBinding[] _stagedSourceBindings;
    private readonly ExternalProcessorStagedArtifactBinding[] _stagedArtifactBindings;

    /// <summary>Creates an external processor invocation declaration.</summary>
    public ExternalProcessorInvocation(
        string processorId,
        string toolBindingId,
        IEnumerable<ByteRange> allowedReadRanges,
        IEnumerable<ByteRange> allowedWriteRanges,
        IEnumerable<ExternalProcessorStagedSourceBinding>? stagedSourceBindings = null,
        IEnumerable<ExternalProcessorWriteRangeSection>? allowedWriteRangeSections = null,
        IEnumerable<ExternalProcessorStagedArtifactBinding>? stagedArtifactBindings = null,
        IEnumerable<ExternalProcessorOutputAssertion>? outputAssertions = null)
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
        ExternalProcessorStagedArtifactBinding[] artifactBindings = [.. stagedArtifactBindings ?? []];
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

        if (artifactBindings.Any(static binding => binding is null) ||
            artifactBindings.Select(static binding => binding.ArtifactId).Distinct(StringComparer.Ordinal).Count() !=
            artifactBindings.Length)
        {
            throw new ArgumentException(
                "External processor staged artifact bindings must be non-null with unique artifact ids.",
                nameof(stagedArtifactBindings));
        }

        _stagedArtifactBindings = [.. artifactBindings.OrderBy(binding => binding.ArtifactId, StringComparer.Ordinal)];
        ExternalProcessorOutputAssertion[] assertions = [.. outputAssertions ?? []];
        if (assertions.Any(static assertion => assertion is null))
        {
            throw new ArgumentException(
                "External processor output assertions must not contain null entries.",
                nameof(outputAssertions));
        }

        Array.Sort(assertions, static (left, right) =>
        {
            int startComparison = left.Range.Start.CompareTo(right.Range.Start);
            return startComparison != 0 ? startComparison : left.Range.Length.CompareTo(right.Range.Length);
        });
        for (int index = 1; index < assertions.Length; index++)
        {
            if (assertions[index - 1].Range.Overlaps(assertions[index].Range))
            {
                throw new ArgumentException(
                    "External processor output assertions must not overlap.",
                    nameof(outputAssertions));
            }
        }

        OutputAssertions = Array.AsReadOnly(assertions);

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

    /// <summary>Named source snapshots the host materializes as immutable tool input artifacts.</summary>
    public IReadOnlyList<ExternalProcessorStagedArtifactBinding> StagedArtifactBindings => _stagedArtifactBindings;

    /// <summary>Exact post-transform bytes the host verifies before importing the staged output.</summary>
    public IReadOnlyList<ExternalProcessorOutputAssertion> OutputAssertions { get; }
}

/// <summary>Diagnostic section attached to a declared external-processor write range.</summary>
public sealed class ExternalProcessorWriteRangeSection
{
    /// <summary>Creates a write-range section annotation owned by the profile or adapter.</summary>
    public ExternalProcessorWriteRangeSection(
        string sectionId,
        ByteRange range,
        ByteRange? sourceRange = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        if (sourceRange is { } source && source.Length != range.Length)
        {
            throw new ArgumentException(
                "An external processor copy source range must have the same length as its destination range.",
                nameof(sourceRange));
        }

        SectionId = sectionId.Trim();
        Range = range;
        SourceRange = sourceRange;
    }

    /// <summary>Stable section identifier used by reports.</summary>
    public string SectionId { get; }

    /// <summary>Half-open processor write range covered by this section.</summary>
    public ByteRange Range { get; }

    /// <summary>
    /// Firmware source range copied into <see cref="Range"/>. This is report provenance only and grants
    /// no additional processor read or write authority.
    /// </summary>
    public ByteRange? SourceRange { get; }

    /// <summary>Maps a destination subrange back to its copied firmware source range.</summary>
    public bool TryMapRangeToSourceRange(ByteRange range, out ByteRange sourceRange)
    {
        if (SourceRange is not { } source || !Range.Contains(range))
        {
            sourceRange = default;
            return false;
        }

        long offset = checked(range.Start - Range.Start);
        sourceRange = new ByteRange(checked(source.Start + offset), range.Length);
        return true;
    }
}
