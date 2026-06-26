namespace NvtFwCombiner.Domain.Composition;

/// <summary>Operation kind compiled from an explicit profile mapping.</summary>
public enum ExplicitMappingOperationKind
{
    /// <summary>Copies bytes from the source binding to the target range.</summary>
    CopyRange,

    /// <summary>Replaces bytes in an initialized target image.</summary>
    ReplaceRange,
}
