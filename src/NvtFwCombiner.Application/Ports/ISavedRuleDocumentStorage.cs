using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Host-resolved storage authority for one Saved Rule document.</summary>
public sealed record SavedRuleDocumentStorageLocation
{
    /// <summary>Creates one opaque adapter-owned document identity.</summary>
    public SavedRuleDocumentStorageLocation(
        string documentId,
        bool isTrustedCatalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        DocumentId = documentId;
        IsTrustedCatalog = isTrustedCatalog;
    }

    /// <summary>Opaque identity understood only by the configured adapter.</summary>
    public string DocumentId { get; }

    /// <summary>Whether the host resolved this location under immutable Catalog storage.</summary>
    public bool IsTrustedCatalog { get; }
}

/// <summary>
/// Resolves Saved Rule paths against configured authoring/Catalog roots and
/// performs the one controlled document write.
/// </summary>
public interface ISavedRuleDocumentStorage
{
    /// <summary>Resolves one existing source document and its Catalog authority.</summary>
    SavedRuleDocumentStorageLocation ResolveSource(string documentReference);

    /// <summary>Resolves one proposed write target and its Catalog authority.</summary>
    SavedRuleDocumentStorageLocation ResolveTarget(string documentReference);

    /// <summary>Compares two resolved locations using adapter-native path semantics.</summary>
    bool RepresentsSameDocument(
        SavedRuleDocumentStorageLocation left,
        SavedRuleDocumentStorageLocation right);

    /// <summary>Atomically writes exact document bytes to an already resolved editable target.</summary>
    ValueTask WriteAsync(
        SavedRuleDocumentStorageLocation target,
        ReadOnlyMemory<byte> documentBytes,
        CancellationToken cancellationToken);
}

/// <summary>
/// Strictly validates one complete Saved Rule v2 document and derives its
/// canonical execution identity from the same immutable byte snapshot.
/// </summary>
public interface ISavedRuleDocumentIdentityReader
{
    /// <summary>Returns null when the bytes are not one schema-valid Saved Rule v2 document.</summary>
    SavedRuleExecutionIdentity? TryReadIdentity(
        ReadOnlyMemory<byte> documentBytes);
}
