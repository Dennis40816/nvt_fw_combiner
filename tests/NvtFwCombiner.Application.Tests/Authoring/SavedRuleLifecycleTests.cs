using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Locks the Application-owned Saved Rule edit/publication lifecycle.</summary>
public sealed class SavedRuleLifecycleTests
{
    /// <summary>Import transfers bytes but never trust or publication state.</summary>
    [Fact]
    public void ImportedRuleStartsAsUntrustedDraft()
    {
        SavedRuleLifecycleSnapshot snapshot = SavedRuleLifecycle.Import(Identity("a"));

        Assert.Equal(SavedRuleStorageKind.Imported, snapshot.StorageKind);
        Assert.Equal(SavedRuleLifecycleState.Draft, snapshot.State);
        Assert.False(snapshot.HasApproval);
        Assert.False(snapshot.HasEvidence);
        Assert.False(snapshot.IsTrusted);
    }

    /// <summary>A semantic local edit can overwrite its file while invalidating prior gates.</summary>
    [Fact]
    public void LocalSemanticEditMaySaveInPlaceAndInvalidatesPublicationClaims()
    {
        var original = new SavedRuleLifecycleSnapshot(
            Identity("a"),
            SavedRuleStorageKind.UserOwned,
            SavedRuleLifecycleState.Published,
            hasApproval: true,
            hasEvidence: true,
            isTrusted: true);

        SavedRuleSaveDecision decision = SavedRuleLifecycle.PrepareSave(
            original,
            Identity("b"),
            SavedRuleStorageKind.UserOwned,
            savesToOriginalLocation: true);

        Assert.True(decision.IsAllowed);
        Assert.True(decision.SemanticContentChanged);
        Assert.True(decision.RequiresNewRuleVersionForPublication);
        Assert.Equal(SavedRuleLifecycleState.Draft, decision.Snapshot!.State);
        Assert.False(decision.Snapshot.HasApproval);
        Assert.False(decision.Snapshot.HasEvidence);
        Assert.False(decision.Snapshot.IsTrusted);
    }

    /// <summary>Display-only changes do not alter the externally computed semantic identity.</summary>
    [Fact]
    public void DisplayOnlyEditRetainsSemanticIdentity()
    {
        var original = new SavedRuleLifecycleSnapshot(
            Identity("a"),
            SavedRuleStorageKind.UserOwned,
            SavedRuleLifecycleState.Draft,
            hasApproval: false,
            hasEvidence: false,
            isTrusted: false);

        SavedRuleSaveDecision decision = SavedRuleLifecycle.PrepareSave(
            original,
            Identity("a"),
            SavedRuleStorageKind.UserOwned,
            savesToOriginalLocation: true);

        Assert.True(decision.IsAllowed);
        Assert.False(decision.SemanticContentChanged);
        Assert.False(decision.RequiresNewRuleVersionForPublication);
    }

    /// <summary>Catalog snapshots are immutable and editing projects an untrusted working copy.</summary>
    [Fact]
    public void CatalogRuleIsReadOnlyAndEditCreatesUntrustedWorkingCopy()
    {
        var catalog = new SavedRuleLifecycleSnapshot(
            Identity("a"),
            SavedRuleStorageKind.TrustedCatalog,
            SavedRuleLifecycleState.Published,
            hasApproval: true,
            hasEvidence: true,
            isTrusted: true);

        SavedRuleSaveDecision blocked = SavedRuleLifecycle.PrepareSave(
            catalog,
            Identity("a"),
            SavedRuleStorageKind.TrustedCatalog,
            savesToOriginalLocation: true);
        SavedRuleSaveDecision workingCopy = SavedRuleLifecycle.PrepareSave(
            catalog,
            Identity("a"),
            SavedRuleStorageKind.UserOwned,
            savesToOriginalLocation: false);

        Assert.False(blocked.IsAllowed);
        Assert.Equal("saved-rule.lifecycle.catalog-read-only", Assert.Single(blocked.Issues).Code);
        Assert.True(workingCopy.IsAllowed);
        Assert.Equal(SavedRuleLifecycleState.Draft, workingCopy.Snapshot!.State);
        Assert.False(workingCopy.Snapshot.IsTrusted);
    }

    private static SavedRuleExecutionIdentity Identity(string hashSeed)
    {
        string hash = new(hashSeed[0], 64);
        return new SavedRuleExecutionIdentity(
            "copy-display-window",
            "1.0.0",
            hash,
            new SavedRuleParentIdentity(
                "bundle",
                "1.0.0",
                new string('b', 64),
                "profile",
                "1.0.0",
                new string('c', 64),
                "family",
                "1.0.0",
                new string('d', 64),
                "map"));
    }
}
