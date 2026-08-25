using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class ManagedActivationCoordinatorTests
{
    /// <summary>A durably quarantined active version cannot be launched again.</summary>
    [Fact]
    public async Task FailedActiveVersionDoesNotLaunch()
    {
        ManagedAppVersion failed = ManagedAppVersion.Parse("0.10.5");
        VersionManagerState state = VersionManagerState.Create(
            updateSource: null,
            activeVersion: failed,
            lastKnownGoodVersion: failed,
            admissions: [Admission("0.10.5"), Admission("0.10.6")],
            pendingActivation: null,
            failedActivationVersion: failed,
            retentionReviewDue: false,
            managedRootIdentity: "managed");
        var process = new FakeProcess(ManagedProcessStartOutcome.Ready);
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new FakeStateStore(state),
            new HealthyRepository(),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.DamagedVersion, result.Outcome);
        Assert.Equal(failed, result.FailedVersion);
        Assert.Empty(process.Starts);
    }

    /// <summary>A healthy self-admitted directory is not launcher authority.</summary>
    [Fact]
    public async Task HealthyUnadmittedInventoryDoesNotLaunch()
    {
        ManagedVersionAdmission admission = Admission("0.10.5");
        var process = new FakeProcess(ManagedProcessStartOutcome.Ready);
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new FakeStateStore(State()),
            new FixedInventoryRepository(new(
                admission.Version,
                admission.AdmissionIdentity,
                ManagedVersionIntegrity.Healthy,
                DamageReason: null,
                IsActive: true,
                IsLastKnownGood: true,
                ManagedVersionAdmissionState.Unadmitted,
                admission)),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.DamagedVersion, result.Outcome);
        Assert.Empty(process.Starts);
    }

    /// <summary>A healthy row cannot launch under another admission identity.</summary>
    [Fact]
    public async Task MismatchedAdmissionIdentityDoesNotLaunch()
    {
        ManagedVersionAdmission admission = Admission("0.10.5");
        var process = new FakeProcess(ManagedProcessStartOutcome.Ready);
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new FakeStateStore(State()),
            new FixedInventoryRepository(new(
                admission.Version,
                "different-admission",
                ManagedVersionIntegrity.Healthy,
                DamageReason: null,
                IsActive: true,
                IsLastKnownGood: true)),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.DamagedVersion, result.Outcome);
        Assert.Empty(process.Starts);
    }

    private sealed class FixedInventoryRepository(InstalledVersionSnapshot row)
        : IManagedVersionRepository
    {
        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionInventory> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ManagedVersionInventory.Create([row]));
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
