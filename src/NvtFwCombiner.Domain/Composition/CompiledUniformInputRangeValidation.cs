namespace NvtFwCombiner.Domain.Composition;

internal static partial class CompiledValidationRequirements
{
    /// <summary>Rejects any declared input range whose bytes are all one repeated value.</summary>
    internal static CompiledUniformInputRangeValidation RejectUniformInputRanges(
        string ruleId,
        CompiledValidationSeverity severity,
        string issueCode,
        string addressSpaceId,
        IEnumerable<ByteRange> ranges)
    {
        return new CompiledUniformInputRangeValidation(
            ruleId,
            severity,
            issueCode,
            addressSpaceId,
            ranges);
    }
}

/// <summary>Input-load validation for profile-declared nonuniform artifact ranges.</summary>
public sealed record CompiledUniformInputRangeValidation : CompiledValidationRequirement
{
    private readonly ByteRange[] _ranges;

    internal CompiledUniformInputRangeValidation(
        string ruleId,
        CompiledValidationSeverity severity,
        string issueCode,
        string addressSpaceId,
        IEnumerable<ByteRange> ranges)
        : base(
            ruleId,
            CompiledValidationStage.InputLoad,
            severity,
            issueCode,
            CompiledValidationKind.RejectUniformInputRanges)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentNullException.ThrowIfNull(ranges);
        _ranges = [.. ranges];
        DomainInvariant.Reject(
            _ranges.Length == 0 ||
            _ranges.Distinct().Count() != _ranges.Length,
            "Uniform-input validation requires unique nonempty ranges.",
            nameof(ranges));

        Array.Sort(_ranges, static (left, right) =>
        {
            int start = left.Start.CompareTo(right.Start);
            return start != 0 ? start : left.Length.CompareTo(right.Length);
        });
        for (int index = 1; index < _ranges.Length; index++)
        {
            DomainInvariant.Reject(
                _ranges[index - 1].Overlaps(_ranges[index]),
                "Uniform-input validation ranges must not overlap.",
                nameof(ranges));
        }

        AddressSpaceId = addressSpaceId;
        Ranges = Array.AsReadOnly(_ranges);
    }

    /// <summary>Immutable input address space whose accepted snapshot is inspected.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Canonical non-overlapping ranges that must each contain more than one byte value.</summary>
    public IReadOnlyList<ByteRange> Ranges { get; }
}
