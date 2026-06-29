using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Result of compiling a profile into a composition plan.</summary>
public sealed class ProfileCompileResult
{
    private ProfileCompileResult(CompositionPlan? plan, IReadOnlyList<CompositionIssue> issues)
    {
        Plan = plan;
        Issues = issues;
    }

    /// <summary>Compiled plan when compilation succeeded.</summary>
    public CompositionPlan? Plan { get; }

    /// <summary>Structured compilation issues.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>True when a validated plan is available.</summary>
    public bool IsSuccess => Plan is not null;

    /// <summary>Creates a successful compilation result.</summary>
    public static ProfileCompileResult Succeeded(CompositionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new ProfileCompileResult(plan, []);
    }

    /// <summary>Creates a failed compilation result.</summary>
    public static ProfileCompileResult Failed(IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return new ProfileCompileResult(null, issues);
    }
}
