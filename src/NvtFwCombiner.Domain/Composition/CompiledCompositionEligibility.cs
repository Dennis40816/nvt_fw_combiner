namespace NvtFwCombiner.Domain.Composition;

/// <summary>Execution eligibility established by a composition compiler authority.</summary>
public enum CompiledCompositionEligibility
{
    /// <summary>The existing typed profile compiler accepted the composition for the legacy runtime.</summary>
    LegacyRuntimeExecutable,

    /// <summary>A profile-bundle-v2 compiler proved one complete plan, but the runtime is not yet authorized to execute it.</summary>
    V2PlanCompiled,

    /// <summary>A profile-bundle-v2 compiler proved the closed runtime subset and its supported promotion.</summary>
    V2RuntimeExecutable,
}
