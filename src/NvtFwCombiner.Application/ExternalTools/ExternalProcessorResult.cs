using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Structured result from a staged external processor transform.</summary>
public sealed class ExternalProcessorResult
{
    private readonly byte[] _outputBytes;
    private readonly CompositionIssue[] _issues;
    private readonly ByteRange[] _changedRanges;

    private ExternalProcessorResult(
        bool succeeded,
        byte[] outputBytes,
        IReadOnlyList<ByteRange> changedRanges,
        IReadOnlyList<CompositionIssue> issues)
    {
        Succeeded = succeeded;
        _outputBytes = [.. outputBytes];
        _changedRanges = [.. changedRanges];
        _issues = [.. issues];
    }

    /// <summary>True when the transform completed and every changed byte was authorized.</summary>
    public bool Succeeded { get; }

    /// <summary>Transformed bytes when the request succeeded; empty on failure.</summary>
    public ReadOnlyMemory<byte> OutputBytes => _outputBytes;

    /// <summary>Changed ranges observed by host-side independent diffing.</summary>
    public IReadOnlyList<ByteRange> ChangedRanges => _changedRanges;

    /// <summary>Structured issues explaining a failed transform.</summary>
    public IReadOnlyList<CompositionIssue> Issues => _issues;

    /// <summary>Creates a successful transform result.</summary>
    public static ExternalProcessorResult Success(
        ReadOnlyMemory<byte> outputBytes,
        IReadOnlyList<ByteRange> changedRanges)
    {
        return new ExternalProcessorResult(true, outputBytes.ToArray(), changedRanges, []);
    }

    /// <summary>Creates a failed transform result.</summary>
    public static ExternalProcessorResult Failed(IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return new ExternalProcessorResult(false, [], [], issues);
    }
}
