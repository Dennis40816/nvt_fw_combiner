namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Stable result category for configured-folder catalog discovery.</summary>
public enum UpdateCatalogLoadIssue
{
    /// <summary>No issue occurred.</summary>
    None,
    /// <summary>The configured folder or root catalog is absent.</summary>
    SourceMissing,
    /// <summary>The configured source could not be read.</summary>
    SourceUnavailable,
    /// <summary>The configured source exists but the current user cannot read it.</summary>
    PermissionDenied,
    /// <summary>The source or catalog crosses a link/reparse boundary.</summary>
    UnsafeSource,
    /// <summary>The catalog exceeds its raw byte ceiling.</summary>
    CatalogTooLarge,
    /// <summary>The catalog JSON or admitted values are invalid.</summary>
    InvalidManifest,
    /// <summary>The file changed during its stable read.</summary>
    UnstableRead,
}

/// <summary>Fail-closed result from one configured-folder catalog read.</summary>
public sealed record UpdateCatalogLoadResult(
    UpdateCatalogSnapshot? Snapshot,
    UpdateCatalogLoadIssue Issue)
{
    /// <summary>Gets whether a complete immutable catalog was published.</summary>
    public bool IsSuccess => Snapshot is not null && Issue == UpdateCatalogLoadIssue.None;
}

/// <summary>Reads one immutable update-catalog snapshot from a configured root.</summary>
public interface IUpdateCatalogSource
{
    /// <summary>Loads and validates the configured source without recursive discovery.</summary>
    /// <param name="sourceRoot">Configured local or UNC folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fail-closed catalog result.</returns>
    ValueTask<UpdateCatalogLoadResult> LoadAsync(
        string sourceRoot,
        CancellationToken cancellationToken);
}
