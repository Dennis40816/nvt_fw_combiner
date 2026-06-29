namespace NvtFwCombiner.Domain.Composition;

/// <summary>Supported core operation primitives.</summary>
public enum CompositionOperationKind
{
    /// <summary>Copies bytes from a source address space into a target address space.</summary>
    CopyRange,

    /// <summary>Replaces bytes in an initialized target address space from a source address space.</summary>
    ReplaceRange,

    /// <summary>Fills a target range with one byte value.</summary>
    FillRange,

    /// <summary>Writes exact profile-supplied scalar bytes into a target range.</summary>
    PatchScalar,
}
