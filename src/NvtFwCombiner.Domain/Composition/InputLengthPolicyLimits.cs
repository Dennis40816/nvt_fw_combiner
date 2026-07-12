namespace NvtFwCombiner.Domain.Composition;

/// <summary>Bounds profile-declared expected immutable input-length sets.</summary>
public static class InputLengthPolicyLimits
{
    /// <summary>Maximum number of non-blocking expected input lengths carried by one contract.</summary>
    public const int MaximumExpectedInputLengths = 8;
}
