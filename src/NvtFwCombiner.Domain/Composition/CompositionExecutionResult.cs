namespace NvtFwCombiner.Domain.Composition;

/// <summary>Structured result from executing a composition plan.</summary>
public sealed class CompositionExecutionResult
{
    private readonly byte[] _outputBytes;

    private CompositionExecutionResult(
        CompositionExecutionStatus status,
        byte[] outputBytes,
        IReadOnlyList<MutationRecord> mutations,
        IReadOnlyList<CompositionIssue> issues)
    {
        Status = status;
        _outputBytes = [.. outputBytes];
        Mutations = mutations;
        Issues = issues;
    }

    /// <summary>Overall execution status.</summary>
    public CompositionExecutionStatus Status { get; }

    /// <summary>Final bytes for the initialized output address space, or empty when execution failed before output exists.</summary>
    public ReadOnlyMemory<byte> OutputBytes => _outputBytes;

    /// <summary>Mutation records in operation execution order.</summary>
    public IReadOnlyList<MutationRecord> Mutations { get; }

    /// <summary>Structured issues collected during execution.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>Creates a successful result.</summary>
    public static CompositionExecutionResult Succeeded(byte[] outputBytes, IReadOnlyList<MutationRecord> mutations)
    {
        ArgumentNullException.ThrowIfNull(outputBytes);
        ArgumentNullException.ThrowIfNull(mutations);
        return new CompositionExecutionResult(CompositionExecutionStatus.Succeeded, outputBytes, mutations, []);
    }

    /// <summary>Creates a failed result with no committed output bytes.</summary>
    public static CompositionExecutionResult Failed(IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return new CompositionExecutionResult(CompositionExecutionStatus.Failed, [], [], issues);
    }
}
