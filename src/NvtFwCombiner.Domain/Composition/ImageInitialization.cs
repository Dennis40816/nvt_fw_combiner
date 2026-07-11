namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares how one engine-owned mutable address space is initialized before operations execute.</summary>
public sealed class ImageInitialization
{
    private ImageInitialization(
        ImageInitializationKind kind,
        string targetSpaceId,
        long capacity,
        byte fillByte,
        string? referenceSpaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSpaceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Kind = kind;
        TargetSpaceId = targetSpaceId;
        Capacity = capacity;
        FillByte = fillByte;
        ReferenceSpaceId = referenceSpaceId;
    }

    /// <summary>Initialization kind: blank fill or immutable reference clone.</summary>
    public ImageInitializationKind Kind { get; }

    /// <summary>Mutable address space initialized by this declaration.</summary>
    public string TargetSpaceId { get; }

    /// <summary>Required mutable-buffer capacity in bytes.</summary>
    public long Capacity { get; }

    /// <summary>Fill byte used for blank initialization.</summary>
    public byte FillByte { get; }

    /// <summary>Immutable reference address space cloned for reference initialization.</summary>
    public string? ReferenceSpaceId { get; }

    /// <summary>Creates a blank initializer for a mutable address space.</summary>
    public static ImageInitialization Blank(string targetSpaceId, long capacity, byte fillByte)
    {
        return new ImageInitialization(ImageInitializationKind.Blank, targetSpaceId, capacity, fillByte, null);
    }

    /// <summary>Creates an initializer that clones an immutable address space.</summary>
    public static ImageInitialization Reference(string targetSpaceId, string referenceSpaceId, long capacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceSpaceId);
        return new ImageInitialization(ImageInitializationKind.Reference, targetSpaceId, capacity, 0, referenceSpaceId);
    }
}
