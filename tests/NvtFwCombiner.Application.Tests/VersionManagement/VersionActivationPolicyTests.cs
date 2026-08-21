using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests pending activation, ready commit, and single rollback decisions.</summary>
public sealed class VersionActivationPolicyTests
{
    /// <summary>Ready commits the candidate as active and last-known-good.</summary>
    [Fact]
    public void ReadyCommitsPendingCandidate()
    {
        VersionManagerState initial = State("0.10.5", "0.10.5", "0.10.5", "0.10.6");
        VersionManagerState pending = VersionActivationPolicy.BeginActivation(
            initial,
            ManagedAppVersion.Parse("0.10.6"));

        VersionManagerState committed = VersionActivationPolicy.CommitReady(
            pending,
            ManagedAppVersion.Parse("0.10.6"));

        Assert.Equal("0.10.6", committed.ActiveVersion?.ToString());
        Assert.Equal("0.10.6", committed.LastKnownGoodVersion?.ToString());
        Assert.Null(committed.PendingActivation);
        Assert.Null(committed.FailedActivationVersion);
    }

    /// <summary>Failure restores exactly the prior LKG once and marks the candidate damaged.</summary>
    [Fact]
    public void FailureRestoresPriorVersionAndCannotOscillate()
    {
        VersionManagerState pending = VersionActivationPolicy.BeginActivation(
            State("0.10.5", "0.10.5", "0.10.5", "0.10.6"),
            ManagedAppVersion.Parse("0.10.6"));

        ActivationRecoveryDecision first = VersionActivationPolicy.FailActivation(
            pending,
            ManagedAppVersion.Parse("0.10.6"));
        ActivationRecoveryDecision repeated = VersionActivationPolicy.FailActivation(
            first.State,
            ManagedAppVersion.Parse("0.10.6"));

        Assert.Equal("0.10.5", first.RollbackVersion?.ToString());
        Assert.Equal("0.10.5", first.State.ActiveVersion?.ToString());
        Assert.Equal("0.10.6", first.State.FailedActivationVersion?.ToString());
        Assert.Null(first.State.PendingActivation);
        Assert.Null(repeated.RollbackVersion);
    }

    /// <summary>Persisted admission digests must retain canonical lowercase SHA-256 form.</summary>
    [Fact]
    public void NonCanonicalAdmissionHashIsRejected()
    {
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");

        _ = Assert.Throws<ArgumentException>(() => VersionManagerState.Create(
            updateSource: null,
            version,
            version,
            [new(version, "identity-0.10.6", new string('A', 64))],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false));
    }

    private static VersionManagerState State(
        string active,
        string lastKnownGood,
        params string[] versions)
    {
        return VersionManagerState.Create(
            updateSource: null,
            ManagedAppVersion.Parse(active),
            ManagedAppVersion.Parse(lastKnownGood),
            [.. versions.Select(version => new ManagedVersionAdmission(
                ManagedAppVersion.Parse(version),
                $"identity-{version}",
                new string('a', 64)))],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false);
    }
}
