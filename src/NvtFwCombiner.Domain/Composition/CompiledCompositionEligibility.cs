namespace NvtFwCombiner.Domain.Composition;

/// <summary>Execution eligibility established by a composition compiler authority.</summary>
public enum CompiledCompositionEligibility
{
    /// <summary>A profile-bundle-v2 compiler proved one complete plan, but the runtime is not yet authorized to execute it.</summary>
    V2PlanCompiled = 1,

    /// <summary>A profile-bundle-v2 compiler proved the closed runtime subset and its supported promotion.</summary>
    V2RuntimeExecutable = 2,
}
