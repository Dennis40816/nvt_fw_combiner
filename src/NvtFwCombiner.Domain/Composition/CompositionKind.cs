namespace NvtFwCombiner.Domain.Composition;

/// <summary>Defines how the output image is initialized before operations run.</summary>
public enum CompositionKind
{
    /// <summary>Builds an output image from a blank initialized buffer.</summary>
    Merge,

    /// <summary>Builds an output image from a required reference image.</summary>
    Replace,
}
