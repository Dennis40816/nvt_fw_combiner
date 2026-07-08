using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>UI-facing projection of stable composition engine issue codes.</summary>
public static class WorkbenchCompositionIssueCodes
{
    /// <summary>Input bytes do not satisfy the declared address-space length policy.</summary>
    public const string InputAddressSpaceLengthMismatch = CompositionIssueCodes.InputAddressSpaceLengthMismatch;

    /// <summary>Input bytes were truncated by an explicitly declared oversize policy.</summary>
    public const string InputAddressSpaceTruncated = CompositionIssueCodes.InputAddressSpaceTruncated;
}
