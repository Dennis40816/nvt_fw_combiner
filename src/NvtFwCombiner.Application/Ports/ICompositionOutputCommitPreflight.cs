using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Ports;

/// <summary>
/// Optionally rejects a resolved output filename before composition or external processing starts.
/// </summary>
/// <remarks>
/// This port is intentionally separate from <see cref="ICompositionOutputWriter"/> so in-memory
/// and test writers remain publication-only. File-backed adapters can protect a concrete filename
/// only after Application has resolved its execution-derived identity.
/// </remarks>
public interface ICompositionOutputCommitPreflight
{
    /// <summary>Verifies that the execution-derived output identity may be safely published later in this run.</summary>
    /// <remarks>
    /// <paramref name="outputNaming"/> is report-safe execution provenance. File-backed adapters may use it
    /// to validate a dependent delivery destination, but must not parse a presentation filename to rederive it.
    /// </remarks>
    void EnsureCanCommit(string fileName, OutputNamingSummary? outputNaming);
}
