using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests explicit first-run seeding without filesystem or launcher policy duplication.</summary>
public sealed class ManagedVersionSeedBootstrapperTests
{
    /// <summary>A missing destination accepts exactly one healthy canonical seed.</summary>
    [Fact]
    public async Task MissingStateImportsOneHealthyCanonicalSeed()
    {
        VersionManagerState seed = SeedState();
        var destination = new MemoryStateStore(null, VersionManagerStateLoadIssue.Missing);
        var bootstrapper = new ManagedVersionSeedBootstrapper(
            "managed-root",
            destination,
            new MemoryStateStore(seed, VersionManagerStateLoadIssue.None),
            new SeedRepository(ManagedVersionIntegrity.Healthy));

        ManagedVersionSeedOutcome outcome = await bootstrapper.EnsureInitializedAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionSeedOutcome.Seeded, outcome);
        Assert.Equal(seed, destination.Saved);
        Assert.Equal(1, destination.SaveCount);
    }

    /// <summary>An invalid existing user state is never replaced by a packaged seed.</summary>
    [Fact]
    public async Task InvalidExistingStateIsNeverOverwritten()
    {
        var destination = new MemoryStateStore(null, VersionManagerStateLoadIssue.Invalid);
        var bootstrapper = new ManagedVersionSeedBootstrapper(
            "managed-root",
            destination,
            new MemoryStateStore(SeedState(), VersionManagerStateLoadIssue.None),
            new SeedRepository(ManagedVersionIntegrity.Healthy));

        ManagedVersionSeedOutcome outcome = await bootstrapper.EnsureInitializedAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionSeedOutcome.InvalidExistingState, outcome);
        Assert.Null(destination.Saved);
        Assert.Equal(0, destination.SaveCount);
    }

    /// <summary>A valid existing state is authoritative and never rereads the package seed.</summary>
    [Fact]
    public async Task ExistingStateSkipsSeedImport()
    {
        VersionManagerState existing = SeedState();
        var destination = new MemoryStateStore(existing, VersionManagerStateLoadIssue.None);
        var bootstrapper = new ManagedVersionSeedBootstrapper(
            "managed-root",
            destination,
            new MemoryStateStore(null, VersionManagerStateLoadIssue.Invalid),
            new SeedRepository(ManagedVersionIntegrity.Healthy));

        ManagedVersionSeedOutcome outcome = await bootstrapper.EnsureInitializedAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionSeedOutcome.ExistingState, outcome);
        Assert.Equal(0, destination.SaveCount);
    }

    /// <summary>Missing and malformed packaged seed files remain distinct fail-closed outcomes.</summary>
    [Theory]
    [InlineData(VersionManagerStateLoadIssue.Missing, ManagedVersionSeedOutcome.MissingSeed)]
    [InlineData(VersionManagerStateLoadIssue.Invalid, ManagedVersionSeedOutcome.InvalidSeed)]
    [InlineData(VersionManagerStateLoadIssue.Unavailable, ManagedVersionSeedOutcome.InvalidSeed)]
    public async Task UnavailableSeedNeverCreatesUserState(
        VersionManagerStateLoadIssue seedIssue,
        ManagedVersionSeedOutcome expected)
    {
        var destination = new MemoryStateStore(null, VersionManagerStateLoadIssue.Missing);
        var bootstrapper = new ManagedVersionSeedBootstrapper(
            "managed-root",
            destination,
            new MemoryStateStore(null, seedIssue),
            new SeedRepository(ManagedVersionIntegrity.Healthy));

        ManagedVersionSeedOutcome outcome = await bootstrapper.EnsureInitializedAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, outcome);
        Assert.Equal(0, destination.SaveCount);
    }

    /// <summary>Every mutable or ambiguous first-run seed shape is rejected before inventory.</summary>
    [Fact]
    public async Task NonCanonicalSeedShapesFailClosed()
    {
        foreach (VersionManagerState seed in NonCanonicalSeedStates())
        {
            var destination = new MemoryStateStore(null, VersionManagerStateLoadIssue.Missing);
            var bootstrapper = new ManagedVersionSeedBootstrapper(
                "managed-root",
                destination,
                new MemoryStateStore(seed, VersionManagerStateLoadIssue.None),
                new SeedRepository(ManagedVersionIntegrity.Healthy));

            ManagedVersionSeedOutcome outcome = await bootstrapper.EnsureInitializedAsync(
                TestContext.Current.CancellationToken);

            Assert.Equal(ManagedVersionSeedOutcome.InvalidSeed, outcome);
            Assert.Equal(0, destination.SaveCount);
        }
    }

    /// <summary>A damaged seeded payload cannot create launchable user state.</summary>
    [Fact]
    public async Task DamagedSeedPayloadFailsBeforeStatePersistence()
    {
        var destination = new MemoryStateStore(null, VersionManagerStateLoadIssue.Missing);
        var bootstrapper = new ManagedVersionSeedBootstrapper(
            "managed-root",
            destination,
            new MemoryStateStore(SeedState(), VersionManagerStateLoadIssue.None),
            new SeedRepository(ManagedVersionIntegrity.Damaged));

        ManagedVersionSeedOutcome outcome = await bootstrapper.EnsureInitializedAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionSeedOutcome.DamagedSeedPayload, outcome);
        Assert.Null(destination.Saved);
    }

    private static VersionManagerState SeedState()
    {
        ManagedAppVersion version = ManagedAppVersion.Parse("1.0.0");
        return VersionManagerState.Create(
            updateSource: null,
            activeVersion: version,
            lastKnownGoodVersion: version,
            admissions: [new(version, "seed|1.0.0", new string('a', 64))],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false);
    }

    private static IEnumerable<VersionManagerState> NonCanonicalSeedStates()
    {
        ManagedAppVersion version = ManagedAppVersion.Parse("1.0.0");
        ManagedAppVersion second = ManagedAppVersion.Parse("1.0.1");
        ManagedVersionAdmission admission = new(version, "seed|1.0.0", new string('a', 64));
        ManagedVersionAdmission secondAdmission = new(second, "seed|1.0.1", new string('b', 64));
        yield return VersionManagerState.Create(
            "source", version, version, [admission], null, null, false);
        yield return VersionManagerState.Create(
            null, null, version, [admission], null, null, false);
        yield return VersionManagerState.Create(
            null, version, null, [admission], null, null, false);
        yield return VersionManagerState.Create(
            null, version, version, [admission, secondAdmission], null, null, false);
        yield return VersionActivationPolicy.BeginActivation(
            VersionManagerState.Create(
                null, version, version, [admission, secondAdmission], null, null, false),
            second);
        yield return VersionManagerState.Create(
            null, version, version, [admission], null, version, false);
        yield return VersionManagerState.Create(
            null, version, version, [admission], null, null, true);
    }

    private sealed class MemoryStateStore(
        VersionManagerState? state,
        VersionManagerStateLoadIssue issue) : IVersionManagerStateStore
    {
        internal int SaveCount { get; private set; }

        internal VersionManagerState? Saved { get; private set; }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(state, issue));
        }

        public ValueTask SaveAsync(VersionManagerState stateToSave, CancellationToken cancellationToken)
        {
            Saved = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SeedRepository(ManagedVersionIntegrity integrity) : IManagedVersionRepository
    {
        public ValueTask<ManagedVersionInventory> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            ManagedVersionAdmission admission = Assert.Single(admissions);
            return ValueTask.FromResult(ManagedVersionInventory.Create(
            [
                new(
                    admission.Version,
                    admission.AdmissionIdentity,
                    integrity,
                    integrity == ManagedVersionIntegrity.Healthy
                        ? null
                        : ManagedVersionDamageReason.ContentMismatch,
                    IsActive: true,
                    IsLastKnownGood: true),
            ]));
        }

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
