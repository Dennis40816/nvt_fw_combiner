using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>
/// Atomic Saved Rule document storage constrained to configured editable and
/// immutable Catalog roots.
/// </summary>
public sealed class SavedRuleDocumentStorage : ISavedRuleDocumentStorage
{
    private static readonly StringComparer DocumentIdComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    private readonly string[] _authoringRoots;
    private readonly string[] _catalogRoots;
    private readonly string[] _allRoots;

    /// <summary>Creates one host-owned storage authority over disjoint roots.</summary>
    public SavedRuleDocumentStorage(
        IEnumerable<string> authoringRoots,
        IEnumerable<string> catalogRoots)
    {
        ArgumentNullException.ThrowIfNull(authoringRoots);
        ArgumentNullException.ThrowIfNull(catalogRoots);
        _authoringRoots =
        [
            .. authoringRoots
                .Select(FileSystemPathGuard.ResolveRoot)
                .Distinct(DocumentIdComparer),
        ];
        _catalogRoots =
        [
            .. catalogRoots
                .Select(FileSystemPathGuard.ResolveExistingRoot)
                .Distinct(DocumentIdComparer),
        ];
        if (_authoringRoots.Length == 0 ||
            _authoringRoots.Any(authoring =>
                _catalogRoots.Any(catalog =>
                    FileSystemPathGuard.IsUnderRoot(authoring, catalog) ||
                    FileSystemPathGuard.IsUnderRoot(catalog, authoring))))
        {
            throw new ArgumentException(
                "Saved Rule storage requires at least one authoring root disjoint from every Catalog root.");
        }

        _allRoots = [.. _authoringRoots, .. _catalogRoots];
    }

    /// <inheritdoc />
    public SavedRuleDocumentStorageLocation ResolveSource(
        string documentReference)
    {
        string path = FileSystemPathGuard.ResolveFileUnderRoots(
            documentReference,
            _allRoots,
            mustExist: true);
        return Location(path);
    }

    /// <inheritdoc />
    public SavedRuleDocumentStorageLocation ResolveTarget(
        string documentReference)
    {
        string path = FileSystemPathGuard.ResolveFileUnderRoots(
            documentReference,
            _allRoots,
            mustExist: false);
        return Location(path);
    }

    /// <inheritdoc />
    public bool RepresentsSameDocument(
        SavedRuleDocumentStorageLocation left,
        SavedRuleDocumentStorageLocation right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return DocumentIdComparer.Equals(left.DocumentId, right.DocumentId);
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(
        SavedRuleDocumentStorageLocation target,
        ReadOnlyMemory<byte> documentBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.IsTrustedCatalog)
        {
            throw new UnauthorizedAccessException(
                "Trusted Catalog Saved Rule documents are immutable.");
        }

        string destinationPath = FileSystemPathGuard.ResolveFileUnderRoots(
            target.DocumentId,
            _authoringRoots,
            mustExist: false);
        if (!DocumentIdComparer.Equals(destinationPath, target.DocumentId))
        {
            throw new UnauthorizedAccessException(
                "Saved Rule target no longer matches its resolved location.");
        }

        using IAtomicFileWriteScope writeScope =
            AtomicFileWriteScope.Open(destinationPath);
        destinationPath = FileSystemPathGuard.ResolveFileUnderRoots(
            target.DocumentId,
            _authoringRoots,
            mustExist: false);
        if (!DocumentIdComparer.Equals(destinationPath, target.DocumentId))
        {
            throw new UnauthorizedAccessException(
                "Saved Rule target changed while acquiring its directory lease.");
        }

        await writeScope.WriteAsync(documentBytes, cancellationToken)
            .ConfigureAwait(false);
    }

    private SavedRuleDocumentStorageLocation Location(string path)
    {
        return new SavedRuleDocumentStorageLocation(
            path,
            _catalogRoots.Any(root =>
                FileSystemPathGuard.IsUnderRoot(path, root)));
    }
}
