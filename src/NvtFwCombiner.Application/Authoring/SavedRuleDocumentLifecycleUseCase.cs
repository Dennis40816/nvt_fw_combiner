using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>One host-resolved local save or Catalog-to-working-copy request.</summary>
public sealed record SavedRuleDocumentSaveRequest
{
    private readonly byte[] _documentBytes;

    /// <summary>Creates one request without accepting caller-supplied storage authority.</summary>
    public SavedRuleDocumentSaveRequest(
        SavedRuleLifecycleSnapshot original,
        string sourceDocumentReference,
        string targetDocumentReference,
        ReadOnlyMemory<byte> documentBytes)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDocumentReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDocumentReference);
        if (documentBytes.IsEmpty)
        {
            throw new ArgumentException(
                "Saved Rule document bytes cannot be empty.",
                nameof(documentBytes));
        }

        Original = original;
        SourceDocumentReference = sourceDocumentReference;
        TargetDocumentReference = targetDocumentReference;
        _documentBytes = documentBytes.ToArray();
    }

    /// <summary>Lifecycle state established when the source was opened.</summary>
    public SavedRuleLifecycleSnapshot Original { get; }

    /// <summary>Source reference resolved by the configured storage adapter.</summary>
    public string SourceDocumentReference { get; }

    /// <summary>Target reference resolved independently by the configured storage adapter.</summary>
    public string TargetDocumentReference { get; }

    /// <summary>Use-case-owned immutable snapshot written only after lifecycle admission.</summary>
    internal ReadOnlyMemory<byte> DocumentBytes => _documentBytes;
}

/// <summary>Result of one controlled Saved Rule document lifecycle operation.</summary>
public sealed record SavedRuleDocumentSaveResult(
    SavedRuleSaveDecision Decision,
    bool WasWritten)
{
    /// <summary>True only when policy admitted the request and the adapter completed the write.</summary>
    public bool Succeeded => Decision.IsAllowed && WasWritten;
}

/// <summary>
/// Application-owned Saved Rule write use case. Storage authority always comes
/// from the configured host adapter, never from request fields or JSON claims.
/// </summary>
public sealed class SavedRuleDocumentLifecycleUseCase
{
    private readonly ISavedRuleDocumentStorage _storage;
    private readonly ISavedRuleDocumentIdentityReader _identityReader;

    /// <summary>Creates one lifecycle use case over a controlled storage port.</summary>
    public SavedRuleDocumentLifecycleUseCase(
        ISavedRuleDocumentStorage storage,
        ISavedRuleDocumentIdentityReader identityReader)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(identityReader);
        _storage = storage;
        _identityReader = identityReader;
    }

    /// <summary>Performs Save-in-place or creates an editable Catalog working copy.</summary>
    public async ValueTask<SavedRuleDocumentSaveResult> SaveAsync(
        SavedRuleDocumentSaveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        SavedRuleExecutionIdentity? editedIdentity =
            _identityReader.TryReadIdentity(request.DocumentBytes);
        if (editedIdentity is null)
        {
            return new SavedRuleDocumentSaveResult(
                Blocked(
                    "saved-rule.lifecycle.document-invalid",
                    "Saved Rule document must satisfy the complete v2 schema before it can be saved."),
                WasWritten: false);
        }

        SavedRuleDocumentStorageLocation source =
            _storage.ResolveSource(request.SourceDocumentReference);
        SavedRuleDocumentStorageLocation target =
            _storage.ResolveTarget(request.TargetDocumentReference);
        bool originalIsCatalog =
            request.Original.StorageKind == SavedRuleStorageKind.TrustedCatalog;
        if (source.IsTrustedCatalog != originalIsCatalog)
        {
            return new SavedRuleDocumentSaveResult(
                Blocked(
                    "saved-rule.lifecycle.storage-authority-mismatch",
                    "Saved Rule lifecycle state does not match host-resolved Catalog authority."),
                WasWritten: false);
        }

        bool savesToOriginalLocation =
            _storage.RepresentsSameDocument(source, target);
        SavedRuleStorageKind targetStorageKind = target.IsTrustedCatalog
            ? SavedRuleStorageKind.TrustedCatalog
            : source.IsTrustedCatalog
                ? SavedRuleStorageKind.UserOwned
                : request.Original.StorageKind;
        SavedRuleSaveDecision decision = SavedRuleLifecycle.PrepareSave(
            request.Original,
            editedIdentity,
            targetStorageKind,
            savesToOriginalLocation);
        if (!decision.IsAllowed)
        {
            return new SavedRuleDocumentSaveResult(decision, WasWritten: false);
        }

        await _storage.WriteAsync(
                target,
                request.DocumentBytes,
                cancellationToken)
            .ConfigureAwait(false);
        return new SavedRuleDocumentSaveResult(decision, WasWritten: true);
    }

    private static SavedRuleSaveDecision Blocked(string code, string message)
    {
        return new SavedRuleSaveDecision(
            Snapshot: null,
            SemanticContentChanged: false,
            RequiresNewRuleVersionForPublication: false,
            [new SavedRuleLifecycleIssue(code, message)]);
    }
}
