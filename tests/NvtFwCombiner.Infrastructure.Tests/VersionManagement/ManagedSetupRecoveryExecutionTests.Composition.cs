using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Connects the canonical marker, Application policy, and real recovery adapter.</summary>
public sealed partial class ManagedSetupRecoveryExecutionTests
{
    /// <summary>The real policy and filesystem adapter complete an explicitly confirmed rollback.</summary>
    [Fact]
    public async Task CanonicalCoordinatorAndFilesystemAdapterCompleteRollback()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using RecoveryEvidenceFixture fixture = await RecoveryEvidenceFixture.CreateAsync();
        await WriteCanonicalMarkerAsync(fixture.Transaction);
        var directStateStore = new JsonVersionManagerStateStore(
            fixture.Transaction.StatePathIdentity);
        VersionManagerStateLoadResult directState = await directStateStore.LoadAsync(
            TestContext.Current.CancellationToken);
        ManagedInstallationRootObservation directRoot = await new FileSystemManagedInstallationRootProbe()
            .ObserveAsync(fixture.ManagedRoot, TestContext.Current.CancellationToken);
        ManagedSetupRecoveryFact directMarker = await new FileSystemManagedSetupRecoveryProbe()
            .ObserveAsync(
                fixture.ManagedRoot,
                fixture.Transaction.StatePathIdentity,
                TestContext.Current.CancellationToken);
        Assert.True(directState.IsSuccess, directState.Issue.ToString());
        Assert.Equal(ManagedInstallationRootStatus.Residue, directRoot.Status);
        Assert.Equal(ManagedSetupRecoveryFactKind.Exact, directMarker.Kind);
        ManagedSetupRecoveryEvidenceObservation directEvidence = await fixture.Executor.ObserveAsync(
            directMarker.Transaction!,
            TestContext.Current.CancellationToken);
        Assert.Equal(ManagedSetupRecoveryEvidenceIssue.None, directEvidence.Issue);
        Assert.NotNull(directEvidence.Admission);
        Assert.NotNull(directEvidence.InstalledLauncher);
        Assert.True(directEvidence.InstalledLauncher.MatchesOwner(directEvidence.Admission));
        Assert.Equal(LauncherBootstrapStateLoadIssue.Missing, directEvidence.LauncherState?.Issue);
        Assert.Equal(
            ManagedSetupRecoveryAction.RemoveIncompleteInstallation,
            ManagedSetupRecoveryPolicy.SelectAction(
                directState,
                directMarker.Transaction!,
                directEvidence,
                fixture.ManagedRoot));
        (ManagedInstallationRecoveryExperience diagnosis, ManagedSetupRecoveryExecutionCoordinator execution) =
            CreateRealComposition(fixture);

        ManagedSetupRecoveryDiagnosis plan = await diagnosis.DiagnoseAsync(
            fixture.ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryOutcome.ActionAvailable, plan.Outcome);
        Assert.Equal(ManagedSetupRecoveryAction.RemoveIncompleteInstallation, plan.Plan?.Action);
        ManagedSetupRecoveryExecutionResult result = await execution.ExecuteAsync(
            plan.Plan!,
            ManagedSetupRecoveryAction.RemoveIncompleteInstallation,
            ManagedFirstInstallationExperience.WriterLeaseTimeout,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryExecutionOutcome.Completed, result.Outcome);
        Assert.False(File.Exists(fixture.Transaction.StatePathIdentity));
        Assert.False(Directory.Exists(fixture.ManagedRoot));
        Assert.False(File.Exists(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(fixture.ManagedRoot)));
        ManagedSetupRecoveryDiagnosis terminal = await diagnosis.DiagnoseAsync(
            fixture.ManagedRoot,
            TestContext.Current.CancellationToken);
        Assert.Equal(ManagedSetupRecoveryOutcome.NoRecoveryNeeded, terminal.Outcome);
    }

    /// <summary>The real policy preserves a READY installation and removes only exact Setup residue.</summary>
    [Fact]
    public async Task CanonicalCoordinatorAndFilesystemAdapterConvergeReady()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using RecoveryEvidenceFixture fixture = await RecoveryEvidenceFixture.CreateAsync(
            readyLauncherState: true);
        await WriteCanonicalMarkerAsync(fixture.Transaction);
        (ManagedInstallationRecoveryExperience diagnosis, ManagedSetupRecoveryExecutionCoordinator execution) =
            CreateRealComposition(fixture);

        ManagedSetupRecoveryDiagnosis plan = await diagnosis.DiagnoseAsync(
            fixture.ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryOutcome.ActionAvailable, plan.Outcome);
        Assert.Equal(ManagedSetupRecoveryAction.ConvergeReady, plan.Plan?.Action);
        ManagedSetupRecoveryExecutionResult result = await execution.ExecuteAsync(
            plan.Plan!,
            ManagedSetupRecoveryAction.ConvergeReady,
            ManagedFirstInstallationExperience.WriterLeaseTimeout,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryExecutionOutcome.Completed, result.Outcome);
        Assert.True(File.Exists(fixture.Transaction.StatePathIdentity));
        Assert.True(Directory.Exists(fixture.ManagedRoot));
        Assert.False(File.Exists(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(fixture.ManagedRoot)));
        ManagedSetupRecoveryDiagnosis terminal = await diagnosis.DiagnoseAsync(
            fixture.ManagedRoot,
            TestContext.Current.CancellationToken);
        Assert.Equal(ManagedSetupRecoveryOutcome.NoRecoveryNeeded, terminal.Outcome);
    }

    /// <summary>A valid marker changed after planning is rejected before real mutation.</summary>
    [Fact]
    public async Task CanonicalCoordinatorRejectsMarkerDriftBeforeFilesystemMutation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using RecoveryEvidenceFixture fixture = await RecoveryEvidenceFixture.CreateAsync();
        ManagedSetupTransactionDocument marker = CanonicalMarker(fixture.Transaction);
        await WriteMarkerAsync(fixture.ManagedRoot, marker);
        (ManagedInstallationRecoveryExperience diagnosis, ManagedSetupRecoveryExecutionCoordinator execution) =
            CreateRealComposition(fixture);
        ManagedSetupRecoveryDiagnosis plan = await diagnosis.DiagnoseAsync(
            fixture.ManagedRoot,
            TestContext.Current.CancellationToken);
        Assert.Equal(ManagedSetupRecoveryOutcome.ActionAvailable, plan.Outcome);

        await WriteMarkerAsync(
            fixture.ManagedRoot,
            marker with
            {
                Candidate = marker.Candidate with
                {
                    RegistryRevision = marker.Candidate.RegistryRevision + 1,
                },
            });
        ManagedSetupRecoveryExecutionResult result = await execution.ExecuteAsync(
            plan.Plan!,
            plan.Plan!.Action,
            ManagedFirstInstallationExperience.WriterLeaseTimeout,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryExecutionOutcome.RecoveryRequired, result.Outcome);
        Assert.True(File.Exists(fixture.Transaction.StatePathIdentity));
        Assert.True(Directory.Exists(fixture.ManagedRoot));
        Assert.True(File.Exists(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(fixture.ManagedRoot)));
    }

    /// <summary>Application-state drift after planning fails closed before real mutation.</summary>
    [Fact]
    public async Task CanonicalCoordinatorRejectsStateDriftBeforeFilesystemMutation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using RecoveryEvidenceFixture fixture = await RecoveryEvidenceFixture.CreateAsync();
        await WriteCanonicalMarkerAsync(fixture.Transaction);
        (ManagedInstallationRecoveryExperience diagnosis, ManagedSetupRecoveryExecutionCoordinator execution) =
            CreateRealComposition(fixture);
        ManagedSetupRecoveryDiagnosis plan = await diagnosis.DiagnoseAsync(
            fixture.ManagedRoot,
            TestContext.Current.CancellationToken);
        Assert.Equal(ManagedSetupRecoveryOutcome.ActionAvailable, plan.Outcome);

        await File.WriteAllTextAsync(
            fixture.Transaction.StatePathIdentity,
            "foreign-state",
            TestContext.Current.CancellationToken);
        ManagedSetupRecoveryExecutionResult result = await execution.ExecuteAsync(
            plan.Plan!,
            plan.Plan!.Action,
            ManagedFirstInstallationExperience.WriterLeaseTimeout,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryExecutionOutcome.ManualInterventionRequired, result.Outcome);
        Assert.True(Directory.Exists(fixture.ManagedRoot));
        Assert.True(File.Exists(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(fixture.ManagedRoot)));
    }

    private static (
        ManagedInstallationRecoveryExperience Diagnosis,
        ManagedSetupRecoveryExecutionCoordinator Execution) CreateRealComposition(
            RecoveryEvidenceFixture fixture)
    {
        var stateStore = new JsonVersionManagerStateStore(fixture.Transaction.StatePathIdentity);
        var rootProbe = new FileSystemManagedInstallationRootProbe();
        var markerProbe = new FileSystemManagedSetupRecoveryProbe();
        var lifetimeProbe = new FileSystemManagedProcessLifetimeProbe();
        return (
            new(stateStore, rootProbe, markerProbe, lifetimeProbe, fixture.Executor),
            new(
                stateStore,
                rootProbe,
                markerProbe,
                lifetimeProbe,
                fixture.Executor,
                fixture.Executor));
    }

    private static Task WriteCanonicalMarkerAsync(ManagedSetupRecoveryTransaction transaction)
    {
        return WriteMarkerAsync(transaction.ManagedRootIdentity, CanonicalMarker(transaction));
    }

    private static Task WriteMarkerAsync(
        string managedRoot,
        ManagedSetupTransactionDocument marker)
    {
        return File.WriteAllBytesAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(managedRoot),
            ManagedSetupTransactionCodec.Serialize(marker),
            TestContext.Current.CancellationToken);
    }

    private static ManagedSetupTransactionDocument CanonicalMarker(
        ManagedSetupRecoveryTransaction transaction)
    {
        ManagedSetupRecoveryPayloadIdentity payload = transaction.Payload;
        ManagedSetupRecoveryCandidateIdentity candidate = transaction.Candidate;
        return new(
            "1.0",
            "NVT FW Combiner",
            1,
            transaction.TransactionId,
            transaction.ManagedRootIdentity,
            transaction.StatePathIdentity,
            new(payload.LauncherSize, payload.LauncherSha256),
            new(
                payload.DescriptorSize,
                payload.DescriptorSha256,
                payload.BootstrapFileName,
                payload.BootstrapSize,
                payload.BootstrapSha256),
            new(
                candidate.RegistryRevision,
                candidate.RegistryDigest,
                candidate.CatalogSchemaVersion,
                candidate.CatalogLatestVersion,
                candidate.CatalogDigest,
                candidate.CatalogPath,
                candidate.RegistryId,
                candidate.SourceRoot,
                candidate.SourceStatus,
                candidate.Version,
                candidate.PackagePath,
                candidate.PackageSize,
                candidate.PackageSha256,
                candidate.ReleaseManifestSha256,
                candidate.EntryIdentity),
            [
                Path.GetFileName(transaction.ManagedRootIdentity),
                Path.GetFileName(
                    FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(
                        transaction.ManagedRootIdentity)),
                string.Join(
                    '/',
                    Path.GetFileName(
                        FileSystemManagedInstallationRootProbe.GetStagingContainerPath(
                            transaction.ManagedRootIdentity)),
                    transaction.TransactionId),
            ],
            transaction.Phase switch
            {
                ManagedSetupRecoveryPhase.Staging => ManagedSetupTransactionCodec.StagingPhase,
                ManagedSetupRecoveryPhase.RootPromoted =>
                    ManagedSetupTransactionCodec.RootPromotedPhase,
                ManagedSetupRecoveryPhase.BootstrapLaunchRecorded =>
                    ManagedSetupTransactionCodec.BootstrapLaunchRecordedPhase,
                _ => throw new InvalidOperationException("Recovery phase is undefined."),
            });
    }
}
