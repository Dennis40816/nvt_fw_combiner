namespace NvtFwCombiner.Domain.Composition;

/// <summary>Source used to initialize one mutable address space before composition operations.</summary>
public enum ImageInitializationKind
{
    /// <summary>Initializes the output image with blank bytes.</summary>
    Blank,

    /// <summary>Initializes the output image from a reference artifact.</summary>
    Reference,
}
