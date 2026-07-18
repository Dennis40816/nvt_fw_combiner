namespace NvtFwCombiner.Domain.Composition;

/// <summary>Domain-level result imported from an application-owned external processor hook.</summary>
public sealed class CompositionExternalProcessorResult
{
    private readonly byte[] _outputBytes;
    private readonly CompositionIssue[] _issues;

    private CompositionExternalProcessorResult(
        bool succeeded,
        byte[] ownedOutputBytes,
        IReadOnlyList<CompositionIssue> issues)
    {
        Succeeded = succeeded;
        _outputBytes = ownedOutputBytes;
        _issues = [.. issues];
    }

    /// <summary>True when the processor completed and returned validated output bytes.</summary>
    public bool Succeeded { get; }

    /// <summary>Transformed bytes for the operation target range.</summary>
    public ReadOnlyMemory<byte> OutputBytes => _outputBytes;

    /// <summary>Structured issues explaining a failed transform.</summary>
    public IReadOnlyList<CompositionIssue> Issues => _issues;

    /// <summary>Creates a successful external processor result.</summary>
    public static CompositionExternalProcessorResult Success(ReadOnlyMemory<byte> outputBytes)
    {
        return new CompositionExternalProcessorResult(true, outputBytes.ToArray(), []);
    }

    /// <summary>Creates a failed external processor result.</summary>
    public static CompositionExternalProcessorResult Failed(IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return new CompositionExternalProcessorResult(false, [], issues);
    }
}
