using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests owner-approved installed-version, prompt, and retention policy.</summary>
public sealed class VersionManagementPolicyTests
{
    /// <summary>The active version cannot be deleted; last-known-good requires an explicit warning.</summary>
    [Fact]
    public void DeleteDecisionProtectsActiveAndWarnsForLastKnownGood()
    {
        ManagedVersionInventory inventory = Inventory(
            Row("0.10.6", ManagedVersionIntegrity.Healthy, isActive: true),
            Row("0.10.5", ManagedVersionIntegrity.Healthy, isLastKnownGood: true),
            Row("0.10.4", ManagedVersionIntegrity.Damaged));

        ManagedVersionDeleteDecision active = VersionManagementPolicy.DecideDelete(
            inventory,
            ManagedAppVersion.Parse("0.10.6"));
        ManagedVersionDeleteDecision lastKnownGood = VersionManagementPolicy.DecideDelete(
            inventory,
            ManagedAppVersion.Parse("0.10.5"));
        ManagedVersionDeleteDecision damaged = VersionManagementPolicy.DecideDelete(
            inventory,
            ManagedAppVersion.Parse("0.10.4"));

        Assert.Equal(ManagedVersionDeleteBlock.ActiveVersion, active.Block);
        Assert.False(active.IsAllowed);
        Assert.True(lastKnownGood.IsAllowed);
        Assert.True(lastKnownGood.RequiresRollbackLossWarning);
        Assert.True(damaged.IsAllowed);
    }

    /// <summary>Only healthy installed versions count toward the soft threshold of three.</summary>
    [Fact]
    public void RetentionReviewCountsOnlyHealthyVersionsAndNeverSelectsDeletion()
    {
        ManagedVersionInventory atThreshold = Inventory(
            Row("1.0.0", ManagedVersionIntegrity.Healthy, isActive: true),
            Row("0.10.6", ManagedVersionIntegrity.Healthy),
            Row("0.10.5", ManagedVersionIntegrity.Healthy),
            Row("0.10.4", ManagedVersionIntegrity.Damaged));
        ManagedVersionInventory aboveThreshold = Inventory(
            Row("1.0.0", ManagedVersionIntegrity.Healthy, isActive: true),
            Row("0.10.6", ManagedVersionIntegrity.Healthy),
            Row("0.10.5", ManagedVersionIntegrity.Healthy),
            Row("0.10.4", ManagedVersionIntegrity.Healthy),
            Row("0.10.3", ManagedVersionIntegrity.Damaged));

        Assert.False(VersionManagementPolicy.ShouldOfferRetentionReview(atThreshold, updateSucceeded: true));
        Assert.False(VersionManagementPolicy.ShouldOfferRetentionReview(aboveThreshold, updateSucceeded: false));
        Assert.True(VersionManagementPolicy.ShouldOfferRetentionReview(aboveThreshold, updateSucceeded: true));
        Assert.Empty(VersionManagementPolicy.SuggestAutomaticDeletions(aboveThreshold));
    }

    /// <summary>A stale generation and a non-newer candidate cannot publish an automatic prompt.</summary>
    [Fact]
    public void DiscoverySessionPublishesOneVerifiedNewerPromptForCurrentGeneration()
    {
        var session = new VersionDiscoverySession();
        long staleGeneration = session.BeginCheck();
        long currentGeneration = session.BeginCheck();
        VerifiedUpdateCandidate newer = Candidate("0.10.6");

        Assert.False(session.TryPublishAutomaticPrompt(
            staleGeneration,
            ManagedAppVersion.Parse("0.10.5"),
            newer));
        Assert.False(session.TryPublishAutomaticPrompt(
            currentGeneration,
            ManagedAppVersion.Parse("0.10.6"),
            newer));
        Assert.True(session.TryPublishAutomaticPrompt(
            currentGeneration,
            ManagedAppVersion.Parse("0.10.5"),
            newer));
        Assert.False(session.TryPublishAutomaticPrompt(
            currentGeneration,
            ManagedAppVersion.Parse("0.10.5"),
            newer));
    }

    private static ManagedVersionInventory Inventory(params InstalledVersionSnapshot[] versions)
    {
        return ManagedVersionInventory.Create(versions);
    }

    private static InstalledVersionSnapshot Row(
        string version,
        ManagedVersionIntegrity integrity,
        bool isActive = false,
        bool isLastKnownGood = false)
    {
        return new(
            ManagedAppVersion.Parse(version),
            $"identity-{version}",
            integrity,
            integrity == ManagedVersionIntegrity.Healthy ? null : ManagedVersionDamageReason.ManifestMismatch,
            isActive,
            isLastKnownGood);
    }

    private static VerifiedUpdateCandidate Candidate(string version)
    {
        return new(
            ManagedAppVersion.Parse(version),
            $"identity-{version}",
            $"Release {version}");
    }
}
