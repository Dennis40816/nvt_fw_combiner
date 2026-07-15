using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Result of compiling a profile into one atomic composition artifact.</summary>
public sealed class ProfileCompileResult
{
    private ProfileCompileResult(
        CompiledComposition? compiledComposition,
        IReadOnlyList<CompositionIssue> issues)
    {
        CompiledComposition = compiledComposition;
        Issues = issues;
    }

    /// <summary>Atomic compiled artifact when compilation succeeded.</summary>
    public CompiledComposition? CompiledComposition { get; }

    /// <summary>Structured compilation issues.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>True when one compiled artifact is available.</summary>
    public bool IsSuccess => CompiledComposition is not null;

    /// <summary>Creates a successful compilation result.</summary>
    public static ProfileCompileResult Succeeded(CompiledComposition compiledComposition)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        return new ProfileCompileResult(compiledComposition, []);
    }

    /// <summary>Creates a failed compilation result.</summary>
    public static ProfileCompileResult Failed(IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return new ProfileCompileResult(null, issues);
    }
}
