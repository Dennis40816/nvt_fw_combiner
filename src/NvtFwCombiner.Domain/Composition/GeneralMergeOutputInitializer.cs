namespace NvtFwCombiner.Domain.Composition;

/// <summary>
/// Exact blank-output initialization selected for one General Merge authoring
/// revision and compiled unchanged into the shared composition plan.
/// </summary>
public sealed record GeneralMergeOutputInitializer
{
    /// <summary>Reviewed compatibility default when no fill byte is authored.</summary>
    public const byte DefaultFillByte = 0x00;

    /// <summary>Largest exact capacity supported by the in-memory composition engine.</summary>
    public const long MaximumCapacity = int.MaxValue;

    /// <summary>Creates one positive in-memory output initializer.</summary>
    public GeneralMergeOutputInitializer(
        long capacity,
        byte fillByte = DefaultFillByte)
    {
        if (capacity is <= 0 or > MaximumCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                $"General Merge capacity must be in 1..{MaximumCapacity}.");
        }

        Capacity = capacity;
        FillByte = fillByte;
    }

    /// <summary>Exact final output capacity in bytes.</summary>
    public long Capacity { get; }

    /// <summary>Byte used to initialize the complete output before mappings.</summary>
    public byte FillByte { get; }

    /// <summary>Projects this authoring value into the shared engine initialization primitive.</summary>
    public ImageInitialization ToImageInitialization(string targetSpaceId)
    {
        return ImageInitialization.Blank(
            targetSpaceId,
            Capacity,
            FillByte);
    }
}
