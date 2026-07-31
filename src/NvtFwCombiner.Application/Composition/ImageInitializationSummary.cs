using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report provenance for the exact engine-owned output initialization.</summary>
public sealed record ImageInitializationSummary(
    ImageInitializationKind Kind,
    long Capacity,
    byte? FillByte,
    string? ReferenceSpaceId)
{
    /// <summary>Projects one compiled initialization without redefining its semantics.</summary>
    public static ImageInitializationSummary FromCompiled(
        ImageInitialization initialization)
    {
        ArgumentNullException.ThrowIfNull(initialization);
        return new ImageInitializationSummary(
            initialization.Kind,
            initialization.Capacity,
            initialization.Kind == ImageInitializationKind.Blank
                ? initialization.FillByte
                : null,
            initialization.ReferenceSpaceId);
    }
}
