namespace NvtFwCombiner.Application.Composition;

/// <summary>Stable Application-owned run issue codes.</summary>
public static class CompositionRunIssueCodes
{
    /// <summary>A reopened input no longer matches its accepted content stamp.</summary>
    public const string InputArtifactContentSnapshotMismatch =
        "input.artifact.content-snapshot-mismatch";
}
