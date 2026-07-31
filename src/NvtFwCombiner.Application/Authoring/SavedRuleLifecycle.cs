namespace NvtFwCombiner.Application.Authoring;

/// <summary>Exact separately installed Trusted Parent referenced by one Saved Rule.</summary>
public sealed record SavedRuleParentIdentity
{
    /// <summary>Creates one exact bundle/profile/family/map identity.</summary>
    public SavedRuleParentIdentity(
        string bundleId,
        string bundleVersion,
        string bundleContentHash,
        string profileId,
        string profileVersion,
        string profileContentHash,
        string familyId,
        string familyVersion,
        string familyContentHash,
        string mapId)
    {
        BundleId = Require(bundleId, nameof(bundleId));
        BundleVersion = Require(bundleVersion, nameof(bundleVersion));
        BundleContentHash = RequireSha256(bundleContentHash, nameof(bundleContentHash));
        ProfileId = Require(profileId, nameof(profileId));
        ProfileVersion = Require(profileVersion, nameof(profileVersion));
        ProfileContentHash = RequireSha256(profileContentHash, nameof(profileContentHash));
        FamilyId = Require(familyId, nameof(familyId));
        FamilyVersion = Require(familyVersion, nameof(familyVersion));
        FamilyContentHash = RequireSha256(familyContentHash, nameof(familyContentHash));
        MapId = Require(mapId, nameof(mapId));
    }

    /// <summary>Exact bundle id.</summary>
    public string BundleId { get; }

    /// <summary>Exact bundle version.</summary>
    public string BundleVersion { get; }

    /// <summary>Exact bundle content hash.</summary>
    public string BundleContentHash { get; }

    /// <summary>Exact profile id.</summary>
    public string ProfileId { get; }

    /// <summary>Exact profile version.</summary>
    public string ProfileVersion { get; }

    /// <summary>Exact profile content hash.</summary>
    public string ProfileContentHash { get; }

    /// <summary>Exact firmware-family id.</summary>
    public string FamilyId { get; }

    /// <summary>Exact firmware-family version.</summary>
    public string FamilyVersion { get; }

    /// <summary>Exact firmware-family content hash.</summary>
    public string FamilyContentHash { get; }

    /// <summary>Canonical map id inside the exact family snapshot.</summary>
    public string MapId { get; }

    private static string Require(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    private static string RequireSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Length == 64 && value.All(static character =>
                character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'))
            ? value
            : throw new ArgumentException(
                "Content hash must be 64 lowercase hexadecimal characters.",
                parameterName);
    }
}

/// <summary>Path-free canonical Saved Rule identity retained by compilation and reports.</summary>
public sealed record SavedRuleExecutionIdentity
{
    /// <summary>Creates one exact rule revision over one exact Parent.</summary>
    public SavedRuleExecutionIdentity(
        string ruleId,
        string ruleVersion,
        string contentHash,
        SavedRuleParentIdentity parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentNullException.ThrowIfNull(parent);
        ContentHash = contentHash.Length == 64 &&
            contentHash.All(static character =>
                character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'))
            ? contentHash
            : throw new ArgumentException(
                "Saved Rule content hash must be 64 lowercase hexadecimal characters.",
                nameof(contentHash));

        RuleId = ruleId;
        RuleVersion = ruleVersion;
        Parent = parent;
    }

    /// <summary>Logical rule id.</summary>
    public string RuleId { get; }

    /// <summary>Published rule version declared by the document.</summary>
    public string RuleVersion { get; }

    /// <summary>Canonical semantic content hash, independent of path and display name.</summary>
    public string ContentHash { get; }

    /// <summary>Exact independently resolved Trusted Parent.</summary>
    public SavedRuleParentIdentity Parent { get; }
}

/// <summary>Storage authority for one Saved Rule document.</summary>
public enum SavedRuleStorageKind
{
    /// <summary>Ordinary user-owned authoring path.</summary>
    UserOwned,

    /// <summary>Imported local document with no trust transfer.</summary>
    Imported,

    /// <summary>Immutable installed Trusted Catalog snapshot.</summary>
    TrustedCatalog,
}

/// <summary>Lifecycle state independent from untrusted fields inside imported JSON.</summary>
public enum SavedRuleLifecycleState
{
    /// <summary>Editable and not publication-authoritative.</summary>
    Draft,

    /// <summary>Immutable catalog publication resolved through its trust path.</summary>
    Published,
}

/// <summary>One Application-owned Saved Rule lifecycle snapshot.</summary>
public sealed record SavedRuleLifecycleSnapshot
{
    /// <summary>Creates one immutable lifecycle snapshot.</summary>
    public SavedRuleLifecycleSnapshot(
        SavedRuleExecutionIdentity identity,
        SavedRuleStorageKind storageKind,
        SavedRuleLifecycleState state,
        bool hasApproval,
        bool hasEvidence,
        bool isTrusted)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
        StorageKind = storageKind;
        State = state;
        HasApproval = hasApproval;
        HasEvidence = hasEvidence;
        IsTrusted = isTrusted;
    }

    /// <summary>Exact path-free rule identity.</summary>
    public SavedRuleExecutionIdentity Identity { get; }

    /// <summary>Storage authority, not inferred from JSON claims.</summary>
    public SavedRuleStorageKind StorageKind { get; }

    /// <summary>Current externally established lifecycle state.</summary>
    public SavedRuleLifecycleState State { get; }

    /// <summary>Whether approval is externally established for these exact bytes.</summary>
    public bool HasApproval { get; }

    /// <summary>Whether evidence is externally established for these exact bytes.</summary>
    public bool HasEvidence { get; }

    /// <summary>Whether the exact revision resolved through the Trusted Catalog path.</summary>
    public bool IsTrusted { get; }
}

/// <summary>Stable lifecycle issue returned for an unsafe save request.</summary>
public sealed record SavedRuleLifecycleIssue(string Code, string Message);

/// <summary>Decision returned before an adapter writes a Saved Rule document.</summary>
public sealed record SavedRuleSaveDecision(
    SavedRuleLifecycleSnapshot? Snapshot,
    bool SemanticContentChanged,
    bool RequiresNewRuleVersionForPublication,
    IReadOnlyList<SavedRuleLifecycleIssue> Issues)
{
    /// <summary>Whether the adapter may perform the requested write.</summary>
    public bool IsAllowed => Snapshot is not null && Issues.Count == 0;
}

/// <summary>Pure lifecycle policy; adapters remain responsible for actual filesystem writes.</summary>
public static class SavedRuleLifecycle
{
    /// <summary>Imports exact bytes as an untrusted Draft regardless of serialized claims.</summary>
    public static SavedRuleLifecycleSnapshot Import(SavedRuleExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return Draft(identity, SavedRuleStorageKind.Imported);
    }

    /// <summary>
    /// Determines whether a local save or Catalog-to-working-copy operation is allowed.
    /// Publication into Catalog storage remains a separate gated use case.
    /// </summary>
    public static SavedRuleSaveDecision PrepareSave(
        SavedRuleLifecycleSnapshot original,
        SavedRuleExecutionIdentity editedIdentity,
        SavedRuleStorageKind targetStorageKind,
        bool savesToOriginalLocation)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(editedIdentity);
        if (targetStorageKind == SavedRuleStorageKind.TrustedCatalog ||
            (original.StorageKind == SavedRuleStorageKind.TrustedCatalog &&
             savesToOriginalLocation))
        {
            return new SavedRuleSaveDecision(
                null,
                SemanticContentChanged: false,
                RequiresNewRuleVersionForPublication: false,
                [
                    new SavedRuleLifecycleIssue(
                        "saved-rule.lifecycle.catalog-read-only",
                        "Installed Trusted Catalog rules are read-only; create a working copy outside Catalog storage."),
                ]);
        }

        bool semanticContentChanged = !StringComparer.Ordinal.Equals(
            original.Identity.ContentHash,
            editedIdentity.ContentHash);
        bool createsWorkingCopy =
            original.StorageKind == SavedRuleStorageKind.TrustedCatalog;
        SavedRuleLifecycleSnapshot snapshot =
            semanticContentChanged || createsWorkingCopy
                ? Draft(editedIdentity, targetStorageKind)
                : new SavedRuleLifecycleSnapshot(
                    editedIdentity,
                    targetStorageKind,
                    original.State,
                    original.HasApproval,
                    original.HasEvidence,
                    original.IsTrusted &&
                    targetStorageKind == SavedRuleStorageKind.TrustedCatalog);
        return new SavedRuleSaveDecision(
            snapshot,
            semanticContentChanged,
            semanticContentChanged &&
            StringComparer.Ordinal.Equals(
                original.Identity.RuleVersion,
                editedIdentity.RuleVersion),
            []);
    }

    private static SavedRuleLifecycleSnapshot Draft(
        SavedRuleExecutionIdentity identity,
        SavedRuleStorageKind storageKind)
    {
        return new SavedRuleLifecycleSnapshot(
            identity,
            storageKind,
            SavedRuleLifecycleState.Draft,
            hasApproval: false,
            hasEvidence: false,
            isTrusted: false);
    }
}
