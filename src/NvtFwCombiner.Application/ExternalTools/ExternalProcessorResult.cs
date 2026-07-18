using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Structured result from a staged external processor transform.</summary>
public sealed class ExternalProcessorResult
{
    private readonly byte[] _outputBytes;
    private readonly CompositionIssue[] _issues;
    private readonly ByteRange[] _changedRanges;
    private readonly ExternalProcessInvocation[] _executedCommands;

    private ExternalProcessorResult(
        bool succeeded,
        byte[] ownedOutputBytes,
        IReadOnlyList<ByteRange> changedRanges,
        IReadOnlyList<CompositionIssue> issues,
        IReadOnlyList<ExternalProcessInvocation>? executedCommands)
    {
        Succeeded = succeeded;
        _outputBytes = ownedOutputBytes;
        _changedRanges = [.. changedRanges];
        _issues = [.. issues];
        _executedCommands = executedCommands is null ? [] : [.. executedCommands];
    }

    /// <summary>True when the transform completed and every changed byte was authorized.</summary>
    public bool Succeeded { get; }

    /// <summary>Transformed bytes when the request succeeded; empty on failure.</summary>
    public ReadOnlyMemory<byte> OutputBytes => _outputBytes;

    /// <summary>Changed ranges observed by host-side independent diffing.</summary>
    public IReadOnlyList<ByteRange> ChangedRanges => _changedRanges;

    /// <summary>Structured issues explaining a failed transform.</summary>
    public IReadOnlyList<CompositionIssue> Issues => _issues;

    /// <summary>Completed external process calls with the exact expanded argv passed to the runner.</summary>
    public IReadOnlyList<ExternalProcessInvocation> ExecutedCommands => _executedCommands;

    /// <summary>Creates a successful transform result.</summary>
    public static ExternalProcessorResult Success(
        ReadOnlyMemory<byte> outputBytes,
        IReadOnlyList<ByteRange> changedRanges)
    {
        return Success(outputBytes, changedRanges, []);
    }

    /// <summary>Creates a successful transform result with completed process audit evidence.</summary>
    public static ExternalProcessorResult Success(
        ReadOnlyMemory<byte> outputBytes,
        IReadOnlyList<ByteRange> changedRanges,
        IReadOnlyList<ExternalProcessInvocation> executedCommands)
    {
        return new ExternalProcessorResult(true, outputBytes.ToArray(), changedRanges, [], executedCommands);
    }

    /// <summary>Creates a failed transform result.</summary>
    public static ExternalProcessorResult Failed(IReadOnlyList<CompositionIssue> issues)
    {
        return Failed(issues, []);
    }

    /// <summary>Creates a failed transform result with completed process audit evidence.</summary>
    public static ExternalProcessorResult Failed(
        IReadOnlyList<CompositionIssue> issues,
        IReadOnlyList<ExternalProcessInvocation> executedCommands)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return new ExternalProcessorResult(false, [], [], issues, executedCommands);
    }
}
