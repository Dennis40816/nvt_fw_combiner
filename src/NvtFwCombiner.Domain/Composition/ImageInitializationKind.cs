namespace NvtFwCombiner.Domain.Composition;

/// <summary>Source used to initialize the output image before composition operations.</summary>
public enum ImageInitializationKind
{
    /// <summary>Initializes the output image with blank bytes.</summary>
    Blank,

    /// <summary>Initializes the output image from a reference artifact.</summary>
    Reference,
}
