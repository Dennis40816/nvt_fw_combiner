using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the Settings projection and consent flow for managed versions.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed partial class VersionManagementSettingsTests
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

    /// <summary>An unavailable source commit keeps the last visible source and always clears busy state.</summary>
    [Fact]
    public async Task UnavailableSourceCommitDoesNotAdoptDraftOrLeaveBusyState()
    {
        VersionManagementSnapshot initial = Snapshot(
            retentionReviewDue: false,
            updateSource: "source-root");
        VersionManagementSnapshot unavailable = initial with
        {
            StateIssue = VersionManagerStateLoadIssue.Unavailable,
        };
        var experience = new RecordingVersionExperience(initial)
        {
            UpdateSourceCommitResult = unavailable,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(initial);
        viewModel.Settings.BeginEditUpdateSourceCommand.Execute(null);
        viewModel.Settings.UpdateSourceDraft = "candidate-source-root";

        await viewModel.Settings.ConfirmUpdateSourceCommand.ExecuteAsync(null);

        Assert.Equal("candidate-source-root", experience.LastCommittedUpdateSource);
        Assert.Equal("source-root", viewModel.Settings.UpdateSourcePath);
        Assert.Equal("source-root", viewModel.Settings.UpdateSourceDraft);
        Assert.False(viewModel.Settings.IsUpdateSourceEditing);
        Assert.False(viewModel.Settings.IsSourceChecking);
        Assert.False(viewModel.Settings.IsVersionBusy);
        Assert.Equal("Recovery required", viewModel.Settings.CurrentStatusLabel);
    }

    /// <summary>An unavailable retention result hides stale actions and cannot report success.</summary>
    [Fact]
    public async Task UnavailableRetentionAcknowledgementHidesReviewAndDoesNotReportSuccess()
    {
        VersionManagementSnapshot initial = Snapshot(retentionReviewDue: true);
        VersionManagementSnapshot unavailable = initial with
        {
            StateIssue = VersionManagerStateLoadIssue.Unavailable,
        };
        var experience = new RecordingVersionExperience(initial)
        {
            RetentionAcknowledgementResult = unavailable,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(initial);

        await viewModel.Settings.KeepAllVersionsCommand.ExecuteAsync(null);

        Assert.False(viewModel.Settings.HasRetentionReview);
        Assert.Empty(viewModel.Settings.VersionRows);
        Assert.Contains("unavailable", viewModel.Settings.VersionOperationStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("were kept", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.False(viewModel.Settings.IsVersionBusy);
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

        Assert.True(viewModel.Settings.IsSourceDisconnected);
        Assert.Equal("Permission denied", viewModel.Settings.SourceStatusText);
        Assert.NotEqual("Offline", viewModel.Settings.SourceStatusText);
    }

    /// <summary>An unknown version directory is shown as recovery state, never as installed or deletable.</summary>
    [Fact]
    public void UnadmittedDirectoryCannotAppearAsOrdinaryInstalledVersion()
    {
        VersionManagementSnapshot initial = Snapshot(retentionReviewDue: false);
        ManagedAppVersion unknownVersion = ManagedAppVersion.Parse("0.10.7");
        ManagedVersionInventory inventory = ManagedVersionInventory.Create(
        [
            .. initial.Inventory.Versions,
            new InstalledVersionSnapshot(
                unknownVersion,
                "unadmitted-directory",
                ManagedVersionIntegrity.Damaged,
                ManagedVersionDamageReason.UnexpectedPath,
                IsActive: false,
                IsLastKnownGood: false,
                ManagedVersionAdmissionState.Unadmitted),
        ]);
        initial = initial with { Inventory = inventory };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", new RecordingVersionExperience(initial)),
            ShellPreferenceSnapshot.Default);

        viewModel.Settings.ApplyVersionSnapshot(initial);

        SettingsVersionRowViewModel row = Assert.Single(
            viewModel.Settings.VersionRows,
            candidate => candidate.Version == unknownVersion);
        Assert.False(row.IsInstalled);
        Assert.False(row.IsAvailable);
        Assert.False(row.CanDelete);
        Assert.False(row.HasPrimaryAction);
        Assert.Contains("Recovery required", row.StatusLabel, StringComparison.Ordinal);
        Assert.Contains("1 need recovery", viewModel.Settings.InventorySummary, StringComparison.Ordinal);
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

    /// <summary>A background update prompt never replaces an existing destructive confirmation.</summary>
    [Fact]
    public void AutomaticUpdatePromptDoesNotStackOverDeleteConfirmation()
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
        string title = viewModel.Settings.VersionConfirmationTitle;
        UpdateCatalogVersionSnapshot available = CatalogVersion("0.10.6");
        VersionManagementSnapshot update = experience.Current with
        {
            Catalog = new([available]),
            VerifiedCandidate = new(available.Version, available.Identity, available.ReleaseNotes),
            ShouldPromptForUpdate = true,
            SourceStatus = VersionSourceStatus.Connected,
        };

        viewModel.Settings.ApplyVersionSnapshot(update);

        Assert.True(viewModel.Settings.IsVersionConfirmationOpen);
        Assert.True(viewModel.Settings.IsVersionConfirmationDestructive);
        Assert.Equal(title, viewModel.Settings.VersionConfirmationTitle);
    }

    /// <summary>A verified update performs no install or activation until the user confirms the named version.</summary>
    [Fact]
    public async Task VerifiedUpdateRequiresExplicitInstallConsentBeforeActivation()
    {
        VersionManagementSnapshot initial = Snapshot(retentionReviewDue: false);
        UpdateCatalogVersionSnapshot available = CatalogVersion("0.10.6");
        initial = initial with
        {
            Catalog = new([available]),
            VerifiedCandidate = new(available.Version, available.Identity, available.ReleaseNotes),
            SourceStatus = VersionSourceStatus.Connected,
        };
        var experience = new RecordingVersionExperience(initial);
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(initial);
        bool activationRequested = false;
        viewModel.Settings.ActivationRequested += (_, _) => activationRequested = true;
        SettingsVersionRowViewModel update = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.Version == available.Version);

        viewModel.Settings.ShowVerifiedReleaseNotesCommand.Execute(null);

        Assert.True(viewModel.Settings.IsVerifiedReleaseNotesVisible);
        Assert.False(viewModel.Settings.IsVersionConfirmationOpen);
        Assert.Empty(experience.Installations);

        viewModel.Settings.RequestVersionPrimaryActionCommand.Execute(update);

        Assert.True(viewModel.Settings.IsVersionConfirmationOpen);
        Assert.Empty(experience.Installations);
        Assert.Empty(experience.Activations);

        await viewModel.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);

        Assert.Equal([available.Version], experience.Installations);
        Assert.Equal([available.Version], experience.Activations);
        Assert.True(activationRequested);
    }

    /// <summary>Incomplete exact cleanup is shown as recovery, not generic verification failure.</summary>
    [Fact]
    public async Task CleanupIncompleteInstallShowsRecoveryRequired()
    {
        VersionManagementSnapshot initial = Snapshot(retentionReviewDue: false);
        UpdateCatalogVersionSnapshot available = CatalogVersion("0.10.6");
        initial = initial with
        {
            Catalog = new([available]),
            VerifiedCandidate = new(available.Version, available.Identity, available.ReleaseNotes),
            SourceStatus = VersionSourceStatus.Connected,
        };
        var experience = new RecordingVersionExperience(initial)
        {
            InstallIssue = ManagedVersionInstallIssue.CleanupIncomplete,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(initial);
        SettingsVersionRowViewModel update = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.Version == available.Version);

        viewModel.Settings.RequestVersionPrimaryActionCommand.Execute(update);
        await viewModel.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);

        Assert.Contains("Recovery required", viewModel.Settings.VersionOperationStatus);
        Assert.Empty(experience.Activations);
    }

    /// <summary>An active damaged installation is never presented as verified or unmanaged.</summary>
    [Fact]
    public void ActiveDamagedVersionIsReportedAsDamaged()
    {
        VersionManagementSnapshot initial = Snapshot(retentionReviewDue: false);
        ManagedVersionInventory damagedInventory = ManagedVersionInventory.Create(
            initial.Inventory.Versions.Select(version => version.IsActive
                ? version with
                {
                    Integrity = ManagedVersionIntegrity.Damaged,
                    DamageReason = ManagedVersionDamageReason.ContentMismatch,
                }
                : version));
        initial = initial with { Inventory = damagedInventory };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", new RecordingVersionExperience(initial)),
            ShellPreferenceSnapshot.Default);

        viewModel.Settings.ApplyVersionSnapshot(initial);

        Assert.False(viewModel.Settings.HasManagedCurrentVersion);
        Assert.Equal("Active · Damaged", viewModel.Settings.CurrentStatusLabel);
        SettingsVersionRowViewModel active = Assert.Single(viewModel.Settings.VersionRows, row => row.IsActive);
        Assert.Equal("Active · Damaged", active.StatusLabel);
        Assert.True(active.IsDamaged);
    }

    /// <summary>Unavailable launcher state suppresses stale healthy Active/Verified badges.</summary>
    [Fact]
    public void UnavailableStateShowsRecoveryInsteadOfStaleVerifiedStatus()
    {
        VersionManagementSnapshot unavailable = Snapshot(retentionReviewDue: false) with
        {
            StateIssue = VersionManagerStateLoadIssue.Unavailable,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", new RecordingVersionExperience(unavailable)),
            ShellPreferenceSnapshot.Default);

        viewModel.Settings.ApplyVersionSnapshot(unavailable);

        Assert.False(viewModel.Settings.HasManagedCurrentVersion);
        Assert.Equal("Recovery required", viewModel.Settings.CurrentStatusLabel);
    }

    /// <summary>A valid managed 0.0.0 identity is not mistaken for missing launcher state.</summary>
    [Fact]
    public void ZeroVersionRemainsAValidManagedActiveVersion()
    {
        ManagedVersionAdmission admission = Admission("0.0.0");
        VersionManagerState state = VersionManagerState.Create(
            updateSource: null,
            activeVersion: admission.Version,
            lastKnownGoodVersion: null,
            admissions: [admission],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false);
        var installed = new InstalledVersionSnapshot(
            admission.Version,
            admission.AdmissionIdentity,
            ManagedVersionIntegrity.Healthy,
            DamageReason: null,
            IsActive: true,
            IsLastKnownGood: false);
        var snapshot = new VersionManagementSnapshot(
            state,
            ManagedVersionInventory.Create([installed]),
            Catalog: null,
            VerifiedCandidate: null,
            SourceStatus: VersionSourceStatus.NotConfigured,
            CatalogIssue: null,
            Generation: 0,
            ShouldPromptForUpdate: false,
            VersionManagerStateLoadIssue.None);
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", new RecordingVersionExperience(snapshot)),
            ShellPreferenceSnapshot.Default);

        viewModel.Settings.ApplyVersionSnapshot(snapshot);

        Assert.Equal("NVT FW Combiner 0.0.0", viewModel.Settings.CurrentVersionLabel);
        Assert.True(viewModel.Settings.HasManagedCurrentVersion);
        Assert.Equal("Active · Verified", viewModel.Settings.CurrentStatusLabel);
    }

    private static VersionManagementSnapshot Snapshot(
        bool retentionReviewDue,
        string? updateSource = null)
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
            updateSource,
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

    private static UpdateCatalogVersionSnapshot CatalogVersion(string version)
    {
        return new(
            ManagedAppVersion.Parse(version),
            DateTimeOffset.Parse("2026-08-21T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            new($"packages/NvtFwCombiner-v{version}-win-x64.zip"),
            42,
            Hash,
            Hash,
            $"Release {version}");
    }

    private sealed class RecordingVersionExperience(VersionManagementSnapshot initial)
        : IVersionManagementExperience
    {
        internal VersionManagementSnapshot Current { get; private set; } = initial;

        internal int Acknowledgements { get; private set; }

        internal List<bool> DeleteConfirmations { get; } = [];

        internal List<ManagedAppVersion> Installations { get; } = [];

        internal List<ManagedAppVersion> Activations { get; } = [];

        internal bool FailActivationPreparation { get; init; }

        internal bool FailPendingActivationCancellation { get; init; }

        internal ManagedVersionInstallIssue InstallIssue { get; init; }

        internal string? LastCommittedUpdateSource { get; private set; }

        internal VersionManagementSnapshot? RetentionAcknowledgementResult { get; init; }

        internal VersionManagementSnapshot? UpdateSourceCommitResult { get; init; }

        internal VersionEnvironmentSelfTestResult SelfTestResult { get; init; } =
            new(UpdateSourceRegistryLoadIssue.NotConfigured, []);

        internal TaskCompletionSource? SelfTestGate { get; init; }

        internal TaskCompletionSource? SelfTestStarted { get; init; }

        internal SynchronizationContext? SelfTestSynchronizationContext { get; private set; }

        internal int SelfTests { get; private set; }

        public ValueTask<VersionManagementSnapshot> InitializeAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask<VersionManagementSnapshot> InitializeAfterManagedReadyAsync(
            CancellationToken cancellationToken)
        {
            return InitializeAsync(cancellationToken);
        }

        public ValueTask<VersionManagementSnapshot> CheckAsync(
            bool isAutomatic,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask<VersionManagementSnapshot> ResumeRegistryAsync(
            CancellationToken cancellationToken)
        {
            return CheckAsync(isAutomatic: false, cancellationToken);
        }

        public async ValueTask<VersionEnvironmentSelfTestResult> RunEnvironmentSelfTestAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SelfTests++;
            SelfTestSynchronizationContext = SynchronizationContext.Current;
            _ = SelfTestStarted?.TrySetResult();
            if (SelfTestGate is not null)
            {
                await SelfTestGate.Task.WaitAsync(cancellationToken);
            }
            return SelfTestResult;
        }

        public ValueTask<VersionManagementSnapshot> CommitUpdateSourceAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            LastCommittedUpdateSource = sourceRoot;
            Current = UpdateSourceCommitResult ?? Current;
            return ValueTask.FromResult(Current);
        }

        public ValueTask<VersionInstallOperationResult> InstallAsync(
            ManagedAppVersion version,
            CancellationToken cancellationToken)
        {
            Installations.Add(version);
            if (InstallIssue != ManagedVersionInstallIssue.None)
            {
                return ValueTask.FromResult(new VersionInstallOperationResult(
                    new(null, InstallIssue, WasAlreadyInstalled: false),
                    Current));
            }
            VersionManagerState state = Current.State!;
            ManagedVersionAdmission admission = Admission(version.ToString());
            state = VersionManagerState.Create(
                state.UpdateSource,
                state.ActiveVersion,
                state.LastKnownGoodVersion,
                [.. state.Admissions, admission],
                state.PendingActivation,
                failedActivationVersion: null,
                state.RetentionReviewDue);
            ManagedVersionInventory inventory = ManagedVersionInventory.Create(
                [.. Current.Inventory.Versions, new(
                    admission.Version,
                    admission.AdmissionIdentity,
                    ManagedVersionIntegrity.Healthy,
                    DamageReason: null,
                    IsActive: false,
                    IsLastKnownGood: false)]);
            Current = Current with { State = state, Inventory = inventory };
            return ValueTask.FromResult(new VersionInstallOperationResult(
                new(admission, ManagedVersionInstallIssue.None, WasAlreadyInstalled: false),
                Current));
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
            if (RetentionAcknowledgementResult is { } result)
            {
                Current = result;
                return ValueTask.FromResult(Current);
            }
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
            if (FailActivationPreparation)
            {
                throw new InvalidOperationException("Injected state failure.");
            }
            Activations.Add(version);
            VersionManagerState state = VersionActivationPolicy.BeginActivation(Current.State!, version);
            Current = Current with { State = state };
            return ValueTask.FromResult(state);
        }

        public ValueTask<VersionManagementSnapshot> CancelPendingActivationAsync(
            CancellationToken cancellationToken)
        {
            if (FailPendingActivationCancellation)
            {
                throw new InvalidOperationException("Injected pending-activation clear failure.");
            }
            VersionManagerState state = VersionActivationPolicy.CancelRequestedActivation(Current.State!);
            Current = Current with { State = state };
            return ValueTask.FromResult(Current);
        }
    }

    private sealed class RecordingStableLauncherHandoff(bool started) : IStableLauncherHandoff
    {
        internal int Attempts { get; private set; }

        public ValueTask<bool> TryStartLauncherAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            return ValueTask.FromResult(started);
        }
    }
}
