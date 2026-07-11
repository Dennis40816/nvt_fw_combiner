namespace NvtFwCombiner.Domain.Composition;

/// <summary>Execution eligibility established by a composition compiler authority.</summary>
public enum CompiledCompositionEligibility
{
    /// <summary>The existing typed profile compiler accepted the composition for the legacy runtime.</summary>
    LegacyRuntimeExecutable,
}
