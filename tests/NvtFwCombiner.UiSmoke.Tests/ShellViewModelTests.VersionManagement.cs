using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the Settings projection and consent flow for managed versions.</summary>
public sealed class VersionManagementSettingsTests
{
    private const string Hash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Retention opens Version and Keep all clears only the reminder.</summary>
    [Fact]
    public async Task RetentionReminderOpensVersionAndKeepsEveryInstalledVersion()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: true));
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsModalOpen);
        Assert.True(viewModel.Settings.IsVersionSelected);
        Assert.True(viewModel.Settings.HasRetentionReview);
        Assert.Equal(4, viewModel.Settings.VersionRows.Count(row => row.IsInstalled));

        await viewModel.Settings.KeepAllVersionsCommand.ExecuteAsync(null);

        Assert.False(viewModel.Settings.HasRetentionReview);
        Assert.Equal(1, experience.Acknowledgements);
        Assert.Equal(4, viewModel.Settings.VersionRows.Count(row => row.IsInstalled));
        Assert.Empty(experience.DeleteConfirmations);
    }

    /// <summary>The last-known-good trash action requires two confirmations and passes explicit consent.</summary>
    [Fact]
    public async Task LastKnownGoodTrashRequiresSecondRollbackWarning()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);
        SettingsVersionRowViewModel rollback = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.IsLastKnownGood);

        viewModel.Settings.RequestDeleteVersionCommand.Execute(rollback);
        Assert.True(viewModel.Settings.IsVersionConfirmationOpen);
        Assert.DoesNotContain("rollback", viewModel.Settings.VersionConfirmationTitle, StringComparison.OrdinalIgnoreCase);

        await viewModel.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);

        Assert.True(viewModel.Settings.IsVersionConfirmationOpen);
        Assert.True(viewModel.Settings.IsVersionConfirmationDestructive);
        Assert.Contains("rollback", viewModel.Settings.VersionConfirmationTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(experience.DeleteConfirmations);

        await viewModel.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);

        Assert.Equal([true], experience.DeleteConfirmations);
        Assert.DoesNotContain(viewModel.Settings.VersionRows, row => row.Version == rollback.Version);
    }

    /// <summary>Permission denial has distinct icon and text instead of collapsing into Offline.</summary>
    [Fact]
    public void PermissionDeniedSourceHasDistinctVisibleProjection()
    {
        VersionManagementSnapshot permissionDenied = Snapshot(retentionReviewDue: false) with
        {
            SourceStatus = VersionSourceStatus.PermissionDenied,
            CatalogIssue = UpdateCatalogLoadIssue.PermissionDenied,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", new RecordingVersionExperience(permissionDenied)),
            ShellPreferenceSnapshot.Default);

        viewModel.Settings.ApplyVersionSnapshot(permissionDenied);

        Assert.Equal("⊘", viewModel.Settings.SourceStatusGlyph);
        Assert.Equal("Permission denied", viewModel.Settings.SourceStatusText);
        Assert.NotEqual("Offline", viewModel.Settings.SourceStatusText);
    }

    /// <summary>Delete icon names include the target version and follow the selected language.</summary>
    [Fact]
    public void DeleteActionAccessibleLabelIsVersionSpecificAndLocalized()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);
        viewModel.OpenSettingsCommand.Execute(null);

        SettingsVersionRowViewModel english = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));
        Assert.Equal("Delete installed version 0.10.4", english.DeleteActionLabel);

        viewModel.SelectedLanguage = "Traditional Chinese";

        SettingsVersionRowViewModel chinese = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));
        Assert.Equal("刪除已安裝版本 0.10.4", chinese.DeleteActionLabel);
    }

    private static VersionManagementSnapshot Snapshot(bool retentionReviewDue)
    {
        ManagedVersionAdmission[] admissions =
        [
            Admission("0.10.5"),
            Admission("0.10.4"),
            Admission("0.10.3"),
            Admission("0.10.2"),
        ];
        ManagedAppVersion active = ManagedAppVersion.Parse("0.10.5");
        ManagedAppVersion lastKnownGood = ManagedAppVersion.Parse("0.10.4");
        VersionManagerState state = VersionManagerState.Create(
            updateSource: null,
            activeVersion: active,
            lastKnownGoodVersion: lastKnownGood,
            admissions: admissions,
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue);
        ManagedVersionInventory inventory = ManagedVersionInventory.Create(admissions.Select(admission =>
            new InstalledVersionSnapshot(
                admission.Version,
                admission.AdmissionIdentity,
                ManagedVersionIntegrity.Healthy,
                DamageReason: null,
                IsActive: admission.Version == active,
                IsLastKnownGood: admission.Version == lastKnownGood)));
        return new(
            state,
            inventory,
            Catalog: null,
            VerifiedCandidate: null,
            SourceStatus: VersionSourceStatus.NotConfigured,
            CatalogIssue: null,
            Generation: 0,
            ShouldPromptForUpdate: false,
            VersionManagerStateLoadIssue.None);
    }

    private static ManagedVersionAdmission Admission(string version)
    {
        return new(ManagedAppVersion.Parse(version), $"identity-{version}", Hash);
    }

    private sealed class RecordingVersionExperience(VersionManagementSnapshot initial)
        : IVersionManagementExperience
    {
        internal VersionManagementSnapshot Current { get; private set; } = initial;

        internal int Acknowledgements { get; private set; }

        internal List<bool> DeleteConfirmations { get; } = [];

        public ValueTask<VersionManagementSnapshot> InitializeAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask<VersionManagementSnapshot> CheckAsync(
            bool isAutomatic,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask<VersionManagementSnapshot> CommitUpdateSourceAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask<VersionInstallOperationResult> InstallAsync(
            ManagedAppVersion version,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionDeleteOperationResult> DeleteAsync(
            ManagedAppVersion version,
            bool rollbackLossConfirmed,
            CancellationToken cancellationToken)
        {
            DeleteConfirmations.Add(rollbackLossConfirmed);
            VersionManagerState state = Current.State!;
            ManagedVersionAdmission[] remaining = [.. state.Admissions.Where(item => item.Version != version)];
            state = VersionManagerState.Create(
                state.UpdateSource,
                state.ActiveVersion,
                state.LastKnownGoodVersion == version ? null : state.LastKnownGoodVersion,
                remaining,
                state.PendingActivation,
                state.FailedActivationVersion == version ? null : state.FailedActivationVersion,
                state.RetentionReviewDue);
            ManagedVersionInventory inventory = ManagedVersionInventory.Create(
                Current.Inventory.Versions.Where(row => row.Version != version));
            Current = Current with { State = state, Inventory = inventory };
            return ValueTask.FromResult(new VersionDeleteOperationResult(
                new(ManagedVersionDeleteBlock.None, RequiresRollbackLossWarning: true),
                VersionDeleteOperationIssue.None,
                ManagedVersionDeleteIssue.None,
                Current));
        }

        public ValueTask<VersionManagementSnapshot> AcknowledgeRetentionReviewAsync(
            CancellationToken cancellationToken)
        {
            Acknowledgements++;
            VersionManagerState state = Current.State!;
            Current = Current with
            {
                State = VersionManagerState.Create(
                    state.UpdateSource,
                    state.ActiveVersion,
                    state.LastKnownGoodVersion,
                    state.Admissions,
                    state.PendingActivation,
                    state.FailedActivationVersion,
                    retentionReviewDue: false),
            };
            return ValueTask.FromResult(Current);
        }

        public ValueTask<VersionManagerState> PrepareActivationAsync(
            ManagedAppVersion version,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
