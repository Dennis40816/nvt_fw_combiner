namespace NvtFwCombiner.Domain.Composition;

/// <summary>Supported core operation primitives.</summary>
public enum CompositionOperationKind
{
    /// <summary>Copies bytes from a source address space into a target address space.</summary>
    CopyRange = 0,

    /// <summary>Replaces bytes in an initialized target address space from a source address space.</summary>
    ReplaceRange = 1,

    /// <summary>Fills a target range with one byte value.</summary>
    FillRange = 2,

    /// <summary>Writes exact profile-supplied scalar bytes into a target range.</summary>
    PatchScalar = 3,

    /// <summary>Runs an approved external processor against a host-created staging copy.</summary>
    RunExternalProcessor = 4,

    /// <summary>Reads one unsigned scalar, applies a checked addend, and writes the result to a target range.</summary>
    TransformScalar = 5,
}
