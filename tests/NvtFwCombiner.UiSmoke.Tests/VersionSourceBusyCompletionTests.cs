using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Characterizes source-operation completion under the existing window publication lease.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class VersionSourceBusyCompletionTests
{
    /// <summary>Success and failure both clear source checking before version busy without replacing the outcome.</summary>
    [Theory]
    [InlineData("Refresh", false)]
    [InlineData("Confirm", false)]
    [InlineData("Check", false)]
    [InlineData("Refresh", true)]
    [InlineData("Confirm", true)]
    [InlineData("Check", true)]
    public async Task PublishableCompletionClearsFlagsInOrderAndPreservesOutcome(string action, bool fail)
    {
        var experience = new HeldSourceExperience();
        SettingsViewModel settings = CreateSettings(experience);
        Task operation = ExecuteAsync(settings, action);
        Assert.True(settings.IsSourceChecking);
        Assert.True(settings.IsVersionBusy);
        List<(string? Property, bool Checking, bool Busy)> changes = Observe(settings);

        if (fail)
        {
            var failure = new InvalidOperationException("Injected source failure.");
            experience.Result.SetException(failure);
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => operation));
            Assert.Equal("saved-source", settings.UpdateSourcePath);
            Assert.Equal("Offline", settings.SourceStatusText);
        }
        else
        {
            experience.Result.SetResult(Snapshot("returned-source", VersionSourceStatus.Connected));
            await operation.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal("returned-source", settings.UpdateSourcePath);
            Assert.Equal("Connected", settings.SourceStatusText);
        }

        Assert.Equal(
            [(nameof(SettingsViewModel.IsSourceChecking), false, true),
             (nameof(SettingsViewModel.IsVersionBusy), false, false)],
            changes.Where(change => change.Property is
                nameof(SettingsViewModel.IsSourceChecking) or nameof(SettingsViewModel.IsVersionBusy)));
        Assert.False(settings.IsSourceChecking);
        Assert.False(settings.IsVersionBusy);
        Assert.Equal(action != "Confirm", settings.IsUpdateSourceEditing);
        Assert.Equal(action == "Confirm" && !fail ? "returned-source" : "candidate-source",
            settings.UpdateSourceDraft);
        Assert.All(experience.Tokens, token => Assert.Equal(CancellationToken.None, token));
        Assert.True(settings.WhenOperationsIdleAsync().IsCompletedSuccessfully);
    }

    /// <summary>Revocation drops the late result and busy notifications while a confirmed source still commits.</summary>
    [Theory]
    [InlineData("Refresh")]
    [InlineData("Confirm")]
    [InlineData("Check")]
    public async Task RevokedCompletionReturnsWithoutPublicationOrCancellingCommit(string action)
    {
        var experience = new HeldSourceExperience();
        SettingsViewModel settings = CreateSettings(experience);
        var lease = new WindowPublicationLease();
        settings.WindowPublication = lease;
        Task operation = ExecuteAsync(settings, action);
        Assert.True(settings.IsSourceChecking);
        Assert.True(settings.IsVersionBusy);
        Assert.Null(experience.CommittedSource);
        List<(string? Property, bool Checking, bool Busy)> changes = Observe(settings);

        lease.Revoke();
        experience.Result.SetResult(Snapshot("returned-source", VersionSourceStatus.Connected));
        await operation.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Empty(changes);
        Assert.True(settings.IsSourceChecking);
        Assert.True(settings.IsVersionBusy);
        Assert.Equal("saved-source", settings.UpdateSourcePath);
        Assert.Equal("candidate-source", settings.UpdateSourceDraft);
        Assert.Equal("Offline", settings.SourceStatusText);
        Assert.Equal(action == "Confirm" ? "candidate-source" : null, experience.CommittedSource);
        Assert.All(experience.Tokens, token => Assert.Equal(CancellationToken.None, token));
        Assert.True(settings.WhenOperationsIdleAsync().IsCompletedSuccessfully);
    }

    /// <summary>A fault while suspended or revoked remains the same fault and publishes no completion state.</summary>
    [Theory]
    [InlineData("Refresh", false)]
    [InlineData("Confirm", false)]
    [InlineData("Check", false)]
    [InlineData("Refresh", true)]
    [InlineData("Confirm", true)]
    [InlineData("Check", true)]
    public async Task ClosedPublicationPreservesFailureWithoutClearingFlags(string action, bool revoke)
    {
        var experience = new HeldSourceExperience();
        SettingsViewModel settings = CreateSettings(experience);
        var lease = new WindowPublicationLease();
        settings.WindowPublication = lease;
        Task operation = ExecuteAsync(settings, action);
        Assert.True(settings.IsSourceChecking);
        Assert.True(settings.IsVersionBusy);
        List<(string? Property, bool Checking, bool Busy)> changes = Observe(settings);
        if (revoke)
        {
            lease.Revoke();
        }
        else
        {
            lease.Suspend();
        }

        var failure = new InvalidOperationException("Injected source failure.");
        experience.Result.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => operation));

        Assert.Empty(changes);
        Assert.True(settings.IsSourceChecking);
        Assert.True(settings.IsVersionBusy);
        Assert.Equal("saved-source", settings.UpdateSourcePath);
        Assert.Equal("candidate-source", settings.UpdateSourceDraft);
        Assert.Equal("Offline", settings.SourceStatusText);
        Assert.Null(experience.CommittedSource);
        Assert.True(settings.WhenOperationsIdleAsync().IsCompletedSuccessfully);
    }

    /// <summary>An unresolved recovery read returns before checking but still completes the visible busy state.</summary>
    [Theory]
    [InlineData("Refresh")]
    [InlineData("Check")]
    public async Task UnknownRecoveryEarlyReturnClearsFlagsWithoutCheckingSource(string action)
    {
        var experience = new HeldSourceExperience
        {
            Initial = Snapshot("saved-source", VersionSourceStatus.Offline) with
            {
                StateIssue = VersionManagerStateLoadIssue.Unavailable,
            },
        };
        SettingsViewModel settings = CreateSettings(experience);
        settings.MarkPendingRecoveryUnknown();

        await ExecuteAsync(settings, action).WaitAsync(TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        Assert.False(settings.IsSourceChecking);
        Assert.False(settings.IsVersionBusy);
        Assert.Equal(0, experience.Checks);
        Assert.Equal(PendingActivationRecoveryStatus.Unknown, settings.PendingRecoveryStatus);
        Assert.Equal("saved-source", settings.UpdateSourcePath);
        Assert.Equal("candidate-source", settings.UpdateSourceDraft);
        Assert.Equal("Recovery required", settings.CurrentStatusLabel);
        Assert.True(settings.WhenOperationsIdleAsync().IsCompletedSuccessfully);
    }

    private static SettingsViewModel CreateSettings(HeldSourceExperience experience)
    {
        SettingsViewModel settings = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("1.2.7", experience), ShellPreferenceSnapshot.Default).Settings;
        settings.ApplyVersionSnapshot(experience.Initial);
        settings.BeginEditUpdateSourceCommand.Execute(null);
        settings.UpdateSourceDraft = "candidate-source";
        return settings;
    }

    private static Task ExecuteAsync(SettingsViewModel settings, string action)
    {
        return action switch
        {
            "Refresh" => settings.RefreshVersionAsync(isAutomatic: false),
            "Confirm" => settings.ConfirmUpdateSourceCommand.ExecuteAsync(null),
            "Check" => settings.CheckNowCommand.ExecuteAsync(null),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static List<(string? Property, bool Checking, bool Busy)> Observe(SettingsViewModel settings)
    {
        List<(string? Property, bool Checking, bool Busy)> changes = [];
        settings.PropertyChanged += (_, args) =>
            changes.Add((args.PropertyName, settings.IsSourceChecking, settings.IsVersionBusy));
        return changes;
    }

    private static VersionManagementSnapshot Snapshot(string source, VersionSourceStatus status)
    {
        return new(
            VersionManagerState.Create(source, null, null, [], null, null, retentionReviewDue: false),
            ManagedVersionInventory.Create([]), null, null, status, null, 0, false,
            VersionManagerStateLoadIssue.None);
    }

    private sealed class HeldSourceExperience : IVersionManagementExperience
    {
        internal VersionManagementSnapshot Initial { get; init; } = Snapshot("saved-source", VersionSourceStatus.Offline);
        internal TaskCompletionSource<VersionManagementSnapshot> Result { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal List<CancellationToken> Tokens { get; } = [];
        internal int Checks { get; private set; }
        internal string? CommittedSource { get; private set; }

        public ValueTask<VersionManagementSnapshot> InitializeAsync(CancellationToken cancellationToken, bool isReadOnly = false)
        {
            Tokens.Add(cancellationToken);
            return ValueTask.FromResult(Initial);
        }

        public ValueTask<VersionManagementSnapshot> CheckAsync(bool isAutomatic, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            Checks++;
            return new(Result.Task);
        }

        public async ValueTask<VersionManagementSnapshot> CommitUpdateSourceAsync(string sourceRoot, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            VersionManagementSnapshot result = await Result.Task;
            CommittedSource = sourceRoot;
            return result;
        }

        public ValueTask<VersionManagementSnapshot> InitializeAfterManagedReadyAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionManagementSnapshot> ResumeRegistryAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionEnvironmentSelfTestResult> RunEnvironmentSelfTestAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionInstallOperationResult> InstallAsync(ManagedAppVersion version, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionDeleteOperationResult> DeleteAsync(ManagedAppVersion version, bool rollbackLossConfirmed,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionManagementSnapshot> AcknowledgeRetentionReviewAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionManagerState> PrepareActivationAsync(ManagedAppVersion version, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionManagementSnapshot> CancelPendingActivationAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
